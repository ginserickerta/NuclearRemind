using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ผู้ประสานผลกระทบวิกฤต (Story Guide §4) — subscribe OnDilemmaResolved แบบเดียวกับ DecreeManager
    /// เก็บ state ที่มาจากทางเลือกวิกฤตไว้ที่เดียว แล้ว manager อื่น "pull" ไปใช้ (null-safe):
    ///   • ResourceManager  → FoodYieldMultiplier / WorkerEfficiencyMultiplier / FoodSpoilRatePerDay / BusyWorkers
    ///   • CoreTowerManager → ReduceHeat/ReduceCore (สั่งตรง — ล้อ ForceIdle ใน DilemmaManager)
    ///   • RadiationManager → AddExposure (สั่งตรง)
    ///   • PopulationManager → sick/deaths/hope ผ่าน event เดิม
    /// ผลแบบมีเวลา (efficiency/busy/hopePerDay) นับถอยหลังทุกสิ้นวัน (ล้อ DecreeManager.HandleDayEnded)
    /// resolve แบบ deterministic (ตรง afterText ที่เล่าไว้) ผ่าน CrisisEffectMath (pure ให้เทสต์ตรวจ)
    /// DilemmaManager ไม่ต้องแก้ — ตัวนี้ subscribe OnDilemmaResolved เองแบบเดียวกับ DecreeManager
    /// </summary>
    public class CrisisEffectManager : MonoBehaviour
    {
        public static CrisisEffectManager Instance { get; private set; }

        [Header("Riot / Patient tuning (Story Guide §4 — deterministic · จูนเฟส 8)")]
        public float riotHopeThreshold = 40f; // Hope หลังเลือกต่ำกว่านี้ = จลาจล (Food C)
        public float riotHopePenalty = 10f;   // จลาจล → Hope −X
        public int riotDeaths = 1;            // จลาจล → เสียชีวิต X คน
        public int minVulnerable = 5;         // กลุ่มเสี่ยงขั้นต่ำของ patientDeathRisk เมื่อไม่มีคนป่วยระบุ

        // ── state ถาวร (ResourceManager pull ไปใช้) ──
        public float FoodYieldMultiplier { get; private set; } = 1f;
        public float FoodSpoilRatePerDay { get; private set; } = 0f;

        // ── Food spoilage (CONFIG.md ECONOMY · CARDS.md การ์ด 3) ──
        /// <summary>True once the "เสบียงเน่า" crisis has happened — before that, food never rots.</summary>
        public bool SpoilageActive { get; private set; }
        /// <summary>Permanent multiplier on the spoil rate (Co-60 = ×0.3, Granary = ×0.6). Stacks.</summary>
        public float SpoilMultiplier { get; private set; } = 1f;
        /// <summary>Player chose the Co-60 option — drives Dorn's D04 "ก็เอาเถอะ".</summary>
        public bool Co60Active { get; private set; }
        public float WorkerEfficiencyMultiplier { get; private set; } = 1f;
        private int _efficiencyDaysRemaining;

        // ── ผลแบบมีเวลา (คู่ index — ล้อ SaveData.deferredCrisisKeys/Days) ──
        private readonly List<int> _busyCounts = new List<int>();
        private readonly List<int> _busyDays = new List<int>();
        private readonly List<float> _hopeDrainPerDay = new List<float>();
        private readonly List<int> _hopeDrainDays = new List<int>();

        /// <summary>คนงานที่ถูกดึงชั่วคราวรวม (ResourceManager หักจากกำลังผลิต) — read-only</summary>
        public int BusyWorkers
        {
            get { int s = 0; for (int i = 0; i < _busyCounts.Count; i++) s += _busyCounts[i]; return s; }
        }

        // read-only accessors สำหรับ SaveManager (ล้อ StoryDirector.DeferredCrisisKeys)
        public int WorkerEfficiencyDaysRemaining => _efficiencyDaysRemaining;
        public IReadOnlyList<int> BusyWorkerCounts => _busyCounts;
        public IReadOnlyList<int> BusyWorkerDays => _busyDays;
        public IReadOnlyList<float> HopeDrainPerDay => _hopeDrainPerDay;
        public IReadOnlyList<int> HopeDrainDays => _hopeDrainDays;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─────────────────────────────────────────
        //  Food spoilage (CARDS.md การ์ด 3 · CONFIG.md spoil formula)
        // ─────────────────────────────────────────

        /// <summary>The spoil crisis fired — switch spoilage on from this day forward.</summary>
        public void ActivateSpoilage()
        {
            SpoilageActive = true;
            RecomputeSpoilRate();
        }

        /// <summary>
        /// Permanent mitigation from a card option (Co-60 ×0.3 / Granary ×0.6). Multiplicative so two
        /// mitigations stack the way CONFIG.md writes them. Ignores 0/negative — an unauthored
        /// spoilMult field must never silently stop spoilage (rule #5: no free option).
        /// </summary>
        public void ApplySpoilMultiplier(float mult, bool isCo60)
        {
            if (mult <= 0f) return;
            SpoilMultiplier *= mult;
            if (isCo60) Co60Active = true;
            RecomputeSpoilRate();
        }

        /// <summary>spoil = (spoilBase + avgRad/100 × spoilRadCoeff) × SpoilMultiplier — recomputed daily
        /// because avgRad climbs through the run.</summary>
        private void RecomputeSpoilRate()
        {
            if (!SpoilageActive) { FoodSpoilRatePerDay = 0f; return; }

            var cfg = GameConfigSO.Instance;
            if (cfg == null) return;
            float avgRad = WorkerManager.Instance != null ? WorkerManager.Instance.AvgRadiation : 0f;
            FoodSpoilRatePerDay = (cfg.spoilBase + avgRad / 100f * cfg.spoilRadCoeff) * SpoilMultiplier;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnDilemmaResolved += HandleDilemmaResolved;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnItemUsed += HandleItemUsed;
            EventManager.Instance.OnResearchCompleted += HandleResearchCompleted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDilemmaResolved -= HandleDilemmaResolved;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnItemUsed -= HandleItemUsed;
            EventManager.Instance.OnResearchCompleted -= HandleResearchCompleted;
        }

        /// <summary>
        /// โครงการวิจัย "เมล็ดพันธุ์ฉายรังสี" (ResearchLab_Spec §3.1 — ทางยั่งยืนของวิกฤต 3·A):
        /// ผลผลิตฟาร์ม +100% ถาวร ผ่าน FoodYieldMultiplier ตัวเดียวกับวิกฤต/ไอเทม (persist ใน SaveData เดิม)
        /// ขนาดโบนัสอ่านจาก ResearchManager (read-only query — ตัวเลขอยู่ที่ SerializeField ไม่ hardcode)
        /// </summary>
        private void HandleResearchCompleted(string projectId)
        {
            if (projectId != ResearchManager.ProjectSeeds || ResearchManager.Instance == null) return;
            FoodYieldMultiplier += ResearchManager.Instance.seedsFoodYieldBonus;
        }

        /// <summary>
        /// ไอเทม §13 ที่กระทบเศรษฐกิจอาหาร (ค่าอยู่ใน ItemSO asset — มาทาง event ไม่เรียกตรง):
        /// เครื่องฉายโคบอลต์-60 (วิกฤต 3·B): stopsFoodSpoilage → อาหารหยุดเน่า (rate = 0)
        /// เมล็ดพันธุ์ฉายรังสี (วิกฤต 3·A): foodYieldBonus → ผลผลิตอาหารเพิ่มถาวร
        /// state ทั้งคู่ persist ผ่าน SaveData field เดิม (foodSpoilRatePerDay/foodYieldMultiplier)
        /// </summary>
        private void HandleItemUsed(ItemSO item)
        {
            if (item == null) return;
            if (item.stopsFoodSpoilage)
                FoodSpoilRatePerDay = 0f;
            if (item.foodYieldBonus > 0f)
                FoodYieldMultiplier += item.foodYieldBonus;
        }

        // ── ใช้ผลตอนเลือกวิกฤต (choiceIndex 0=A/1=B/2=C) ──
        private void HandleDilemmaResolved(DilemmaData dilemma, int choiceIndex)
        {
            if (dilemma == null) return;
            var e = dilemma.GetEffects(choiceIndex);
            if (e == null) return;

            // ── เศรษฐกิจ ──
            if (e.foodYieldPct != 0f)
                FoodYieldMultiplier += e.foodYieldPct;            // Food A: +1.0 → ×2 ถาวร

            if (e.stopSpoilage)
                FoodSpoilRatePerDay = 0f;                          // Food B: หยุดเน่า
            else if (dilemma.inducedSpoilRatePerDay > 0f)
                FoodSpoilRatePerDay = dilemma.inducedSpoilRatePerDay; // Food A/C: ยังเน่าต่อ

            if (e.workerEfficiencyPct != 0f && e.efficiencyDays > 0)
            {
                WorkerEfficiencyMultiplier = Mathf.Max(0f, 1f + e.workerEfficiencyPct); // Food C: -0.5 → 0.5
                _efficiencyDaysRemaining = e.efficiencyDays;
            }

            if (e.busyWorkers > 0 && e.busyDays > 0)
            {
                _busyCounts.Add(e.busyWorkers);
                _busyDays.Add(e.busyDays);
            }

            // ── ความปลอดภัยเตา (สั่งตรง) ──
            if (e.coreHeatReduction > 0f)
                CoreTowerManager.Instance?.ReduceHeat(e.coreHeatReduction);
            if (e.coreReduction > 0f)
                CoreTowerManager.Instance?.ReduceCore(e.coreReduction);
            if (e.waterReductionPct != 0f && ResourceManager.Instance != null)
            {
                float water = ResourceManager.Instance.Current.water;
                EventManager.Instance.RaiseResourceDelta(ResourceType.Water, water * e.waterReductionPct);
            }

            // ── รังสี / สุขภาพ ──
            if (e.radExposureInjected > 0f)
                RadiationManager.Instance?.AddExposure(e.radExposureInjected);
            if (e.sickInjected > 0)
                EventManager.Instance.RaisePopulationSickInjected(e.sickInjected);
            if (e.setsSick)
                EventManager.Instance.RaisePopulationSickSet(Mathf.Max(0, e.sickValue));
            if (e.patientDeathRisk > 0f)
            {
                int sick = PopulationManager.Instance != null ? PopulationManager.Instance.Current.sick : 0;
                int dead = CrisisEffectMath.PatientDeaths(e.patientDeathRisk, sick, minVulnerable);
                if (dead > 0) EventManager.Instance.RaisePopulationDeaths(dead);
            }

            // ── Hope ต่อวัน (ล้อ DecreeSO.hopePerDay) ──
            if (e.hopePerDay != 0f && e.hopePerDayDays > 0)
            {
                _hopeDrainPerDay.Add(e.hopePerDay);
                _hopeDrainDays.Add(e.hopePerDayDays);
            }

            // ── จลาจล (deterministic ตาม Hope ปัจจุบัน) ──
            if (e.riotRisk)
            {
                float hope = PopulationManager.Instance != null ? PopulationManager.Instance.Current.hope : 100f;
                var (hopeDelta, deaths) = CrisisEffectMath.Riot(hope, riotHopeThreshold, riotHopePenalty, riotDeaths);
                if (hopeDelta != 0f) EventManager.Instance.RaiseMoraleDelta(hopeDelta);
                if (deaths > 0) EventManager.Instance.RaisePopulationDeaths(deaths);
            }
        }

        // ── นับถอยหลังผลแบบมีเวลา ทุกสิ้นวัน (ล้อ DecreeManager.HandleDayEnded) ──
        private void HandleDayEnded(int day)
        {
            // 0) อัตราเน่าตามรังสีเฉลี่ยวันนี้ (ไม่มีผลถ้าวิกฤตเสบียงยังไม่เกิด)
            RecomputeSpoilRate();

            // 1) Hope drain ต่อวัน — ยิงทุกช่องที่ยัง active ก่อน แล้วค่อยนับวันลง
            for (int i = 0; i < _hopeDrainPerDay.Count; i++)
                if (_hopeDrainPerDay[i] != 0f)
                    EventManager.Instance.RaiseMoraleDelta(_hopeDrainPerDay[i]);
            DecrementParallel(_hopeDrainDays, _hopeDrainPerDay);

            // 2) แรงงานถูกดึง — นับวันลง คืนกำลังเมื่อครบ
            DecrementParallel(_busyDays, _busyCounts);

            // 3) ประสิทธิภาพผลิต — ครบวันแล้วคืนเป็น 1
            if (_efficiencyDaysRemaining > 0)
            {
                _efficiencyDaysRemaining--;
                if (_efficiencyDaysRemaining <= 0)
                    WorkerEfficiencyMultiplier = 1f;
            }
        }

        // ลดตัวนับวัน 1 ทุกช่อง แล้วลบช่องที่หมดอายุ (≤0) ออกจากทั้งสอง list ที่คู่ index กัน
        private static void DecrementParallel(List<int> days, List<int> values)
        {
            for (int i = days.Count - 1; i >= 0; i--)
            {
                days[i]--;
                if (days[i] <= 0) { days.RemoveAt(i); values.RemoveAt(i); }
            }
        }
        private static void DecrementParallel(List<int> days, List<float> values)
        {
            for (int i = days.Count - 1; i >= 0; i--)
            {
                days[i]--;
                if (days[i] <= 0) { days.RemoveAt(i); values.RemoveAt(i); }
            }
        }

        // ── โหลดเซฟ (ล้อ StoryDirector.HandleSaveLoaded — เคลียร์แล้วเติมใหม่) ──
        private void HandleSaveLoaded(SaveData save)
        {
            FoodYieldMultiplier = save.foodYieldMultiplier;
            FoodSpoilRatePerDay = save.foodSpoilRatePerDay;
            WorkerEfficiencyMultiplier = save.workerEfficiencyMultiplier;
            _efficiencyDaysRemaining = save.workerEfficiencyDaysRemaining;

            _busyCounts.Clear(); _busyDays.Clear();
            if (save.busyWorkerCounts != null) _busyCounts.AddRange(save.busyWorkerCounts);
            if (save.busyWorkerDays != null) _busyDays.AddRange(save.busyWorkerDays);

            _hopeDrainPerDay.Clear(); _hopeDrainDays.Clear();
            if (save.hopeDrainPerDay != null) _hopeDrainPerDay.AddRange(save.hopeDrainPerDay);
            if (save.hopeDrainDays != null) _hopeDrainDays.AddRange(save.hopeDrainDays);

            // Spoilage flags. A save written before these existed has spoilageActive = false but may still
            // carry a rate from the old dilemma path — honour that stored rate instead of zeroing it,
            // otherwise loading an old save silently stops food from rotting.
            SpoilageActive = save.spoilageActive;
            SpoilMultiplier = save.spoilMultiplier > 0f ? save.spoilMultiplier : 1f;
            Co60Active = save.co60Active;

            if (!SpoilageActive && FoodSpoilRatePerDay > 0f) SpoilageActive = true; // legacy save: keep its rate
            else RecomputeSpoilRate();
        }
    }

    /// <summary>
    /// สูตร pure ของผลกระทบวิกฤต (Story Guide §4) — แยกให้ EditMode ตรวจ balance ได้โดยไม่ต้องมี scene/event
    /// deterministic ทั้งหมด (ไม่ใช้ RNG) — ผลตรงกับ afterText ที่เล่าไว้ + เทสต์ซ้ำได้แน่นอน
    /// </summary>
    public static class CrisisEffectMath
    {
        /// <summary>อาหารหลังเน่า = food × (1 − rate) · rate 0 = ไม่เน่า · clamp ≥ 0</summary>
        public static float Spoil(float food, float ratePerDay)
            => Mathf.Max(0f, food * (1f - Mathf.Clamp01(ratePerDay)));

        /// <summary>กำลังคนที่ผลิตจริง = คนงาน − คนที่ถูกดึงชั่วคราว (ไม่ต่ำกว่า 0)</summary>
        public static int EffectiveWorkers(int totalWorkers, int busy)
            => Mathf.Max(0, totalWorkers - busy);

        /// <summary>คนตายจากความเสี่ยง = round(risk × max(คนป่วย, กลุ่มเสี่ยงขั้นต่ำ)) — deterministic</summary>
        public static int PatientDeaths(float risk, int sick, int minVulnerable)
            => Mathf.RoundToInt(Mathf.Clamp01(risk) * Mathf.Max(sick, minVulnerable));

        /// <summary>จลาจล: ถ้า Hope น้อยกว่าเกณฑ์ → (Hope −penalty, ตาย deaths) ไม่งั้นไม่เกิด (0,0)</summary>
        public static (float hopeDelta, int deaths) Riot(float hope, float threshold, float penalty, int deaths)
            => hope < threshold ? (-penalty, deaths) : (0f, 0);
    }
}
