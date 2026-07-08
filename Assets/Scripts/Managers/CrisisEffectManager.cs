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

        private void OnEnable()
        {
            EventManager.Instance.OnDilemmaResolved += HandleDilemmaResolved;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDilemmaResolved -= HandleDilemmaResolved;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
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
