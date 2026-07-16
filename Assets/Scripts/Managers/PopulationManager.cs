using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ประชากร 3 คลาส (V4 §5) + ขวัญกำลังใจ Hope เดี่ยว (V4 §9)
    /// - ฝึก Worker → Engineer/Medic (จ่าย Food+Energy, ใช้เวลา 1 วัน, ต้องมีอาคารปลดล็อก)
    /// - เติมประชากร +1 Worker/วัน เมื่ออาหารไม่ขาด & Hope ≥ 50 & ยังไม่เต็ม shelterCap
    /// - Hope recalc ทุกสิ้นวัน · Hope = 0 → Game Over (HopeZero)
    /// - AssignedCoolingEngineers ป้อนสูตรหล่อเย็น CORE (§8)
    /// </summary>
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [Header("Morale Tuning (V4 §9 — Hope deltas ตามตาราง §18)")]
        public float hopeLossFoodShortage = 10f; // อาหารขาด −10/วัน
        public float hopeRecoveryWithMedic = 2f; // มี Medic ≥1 + ไม่ขาดของ → +2/วัน
        public float hopeLossPerDeath = 5f;      // คนตาย −5/คน (จากทางเลือกวิกฤต/Decree)

        [Header("Sensitivity / ความกดดัน (V5 — ให้สมเกมบริหาร: Hope ลดไวขึ้น)")]
        [Tooltip("Hope ลดทุกวันเป็นฐาน (ความยากลำบากเอาตัวรอด) — ต้องบริหารเชิงรุกถึงจะทรง · 0 = ปิด")]
        public float hopeDailyDrift = 1.5f;                   // −1.5/วัน baseline
        [Tooltip("อาหารเหลือกินได้ < X วัน → เริ่ม 'หิว' Hope ลด (ก่อนหมดเกลี้ยง)")]
        public float lowFoodBufferDays = 2f;
        public float hopeLossLowFood = 4f;                    // หิว −4/วัน
        [Tooltip("คนทำงานหนักเกินสัดส่วน (assigned/total ≥ ratio) → เหนื่อยล้า Hope ลด")]
        [Range(0f, 1f)] public float overworkRatio = 0.7f;   // ทำงาน ≥70% ของประชากร = หนักเกิน
        public float hopeLossOverwork = 3f;                   // เหนื่อย −3/วัน

        [Header("Training Cost (V4 §5)")]
        public int trainEngineerFood = 30, trainEngineerEnergy = 50;
        public int trainMedicFood = 40, trainMedicEnergy = 60;
        public int trainFarmerFood = 30, trainFarmerEnergy = 40;

        [Header("Population Growth (V4 §5)")]
        public float growthHopeThreshold = 50f; // Hope ≥ ค่านี้จึงเติมประชากร

        [Header("Hospital (GDD §6)")]
        public int hospitalHealPerDay = 2; // รักษาเพิ่ม/วัน ต่อโรงพยาบาลที่ Medic ประจำครบ

        [Header("Shelter (V4 §5)")]
        public int baseShelterCap = 10; // Shelter L1 เริ่มเกม (ไม่ต้องสร้าง) — ตึก Shelter ที่วางเพิ่มต่อยอดจากฐานนี้

        public PopulationData Current { get; private set; } = new PopulationData
        {
            // V4 §5 เริ่ม 10 คน — มีคลาสติดตัว (bootstrap) กัน "วันแรกผลิตอะไรไม่ได้"
            // เพราะ Farm ต้องใช้ Farmer และ Lab ต้องใช้ Engineer ตั้งแต่วางหลังแรก
            workers = 6,
            farmers = 2,
            engineers = 2,
            hope = 100f,      // V4 §9 เริ่ม 100
            shelterCap = 10,  // = baseShelterCap (Shelter L1 §5) — ขยายด้วยตึก Shelter ผ่าน RecalcShelterCap
        };

        /// <summary>วิศวกรที่พร้อมประจำหล่อเย็น CORE (เข้าสูตร +4/คน เมื่อมี Poloidal — §8)</summary>
        public int AssignedCoolingEngineers => Current.engineers;

        public bool EngineerTrainingUnlocked => _engineerUnlocked;
        public bool MedicTrainingUnlocked => _medicUnlocked;
        public bool FarmerTrainingUnlocked => _farmerUnlocked;

        private readonly HashSet<ResourceType> _depletedResources = new HashSet<ResourceType>();
        private bool _gameOverRaised;
        private int _pendingEngineers, _pendingMedics, _pendingFarmers; // ฝึกค้าง (เสร็จสิ้นวัน — ใช้เวลา 1 วัน)
        private bool _engineerUnlocked, _medicUnlocked, _farmerUnlocked; // ปลดจากอาคาร (Research Lab — ศูนย์ฝึกทุกคลาส)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnResourceDepleted += HandleResourceDepleted;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnMoraleDelta += HandleMoraleDelta;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnBuildingUpgraded += HandleBuildingUpgraded;
            EventManager.Instance.OnTrainEngineerRequested += TrainEngineer;
            EventManager.Instance.OnTrainMedicRequested += TrainMedic;
            EventManager.Instance.OnTrainFarmerRequested += TrainFarmer;
            EventManager.Instance.OnPopulationDeaths += HandlePopulationDeaths;
            EventManager.Instance.OnPopulationSickInjected += HandlePopulationSickInjected;
            EventManager.Instance.OnPopulationSickSet += HandlePopulationSickSet;
            EventManager.Instance.OnPopulationSickCured += HandlePopulationSickCured;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceDepleted -= HandleResourceDepleted;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnMoraleDelta -= HandleMoraleDelta;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnBuildingUpgraded -= HandleBuildingUpgraded;
            EventManager.Instance.OnTrainEngineerRequested -= TrainEngineer;
            EventManager.Instance.OnTrainMedicRequested -= TrainMedic;
            EventManager.Instance.OnTrainFarmerRequested -= TrainFarmer;
            EventManager.Instance.OnPopulationDeaths -= HandlePopulationDeaths;
            EventManager.Instance.OnPopulationSickInjected -= HandlePopulationSickInjected;
            EventManager.Instance.OnPopulationSickSet -= HandlePopulationSickSet;
            EventManager.Instance.OnPopulationSickCured -= HandlePopulationSickCured;
        }

        private void Start()
        {
            // v6.3: when WorkerManager is active, HopeLedger is the sole owner of Hope — do NOT
            // broadcast the legacy hope field (100), or it races WorkerManager's ledger value (70)
            // and the HUD can latch the wrong number. Population count broadcast still stands.
            if (WorkerManager.Instance == null)
                EventManager.Instance.RaiseMoraleChanged(Current.hope);
            EventManager.Instance.RaisePopulationChanged(Current);
        }

        private void HandleResourceDepleted(ResourceType type) => _depletedResources.Add(type);

        private void HandleResourceChanged(ResourceData data)
        {
            if (data.food > 0f) _depletedResources.Remove(ResourceType.Food);
            if (data.water > 0f) _depletedResources.Remove(ResourceType.Water);
            if (data.energy > 0f) _depletedResources.Remove(ResourceType.Energy);
            if (data.iron > 0f) _depletedResources.Remove(ResourceType.Iron);
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (data == null) return;
            if (data.unlocksEngineerTraining) _engineerUnlocked = true;
            if (data.unlocksMedicTraining) _medicUnlocked = true;
            if (data.unlocksFarmerTraining) _farmerUnlocked = true;

            if (data.shelterCapacity > 0)
                RecalcShelterCap(placedCell: new Vector2Int(cell.col, cell.row), placedData: data);
        }

        // ── เพดานประชากรจากตึก Shelter (V4 §5) ────────────────────
        // เพดาน = ฐานเริ่มเกม 10 + ผลรวมตึก Shelter ที่วางแล้ว (สเกลตามระดับ L1/L2/L3 → +10/+30/+70)
        // อ่าน BuildingRegistry.PlacedBuildings/GetLevel ตรงได้ — เป็น read-only query ของ registry กลาง

        private void HandleBuildingUpgraded(Vector2Int cell, int newLevel) => RecalcShelterCap();

        private void HandleBuildingRemoved(Vector2Int position) => RecalcShelterCap(removedCell: position);

        private void RecalcShelterCap(Vector2Int? placedCell = null, BuildingData placedData = null,
            Vector2Int? removedCell = null)
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null) return;

            int cap = baseShelterCap;
            foreach (var kvp in registry.PlacedBuildings)
            {
                if (removedCell.HasValue && kvp.Key == removedCell.Value) continue; // ตึกที่กำลังถูกทุบ
                cap += ShelterContribution(kvp.Value, registry.GetLevel(kvp.Key));
            }

            // ลำดับ subscriber ของ OnBuildingPlaced ไม่การันตี — ถ้า registry ยังไม่บันทึกตึกที่เพิ่งวาง บวกเองที่ L1
            if (placedCell.HasValue && !registry.PlacedBuildings.ContainsKey(placedCell.Value))
                cap += ShelterContribution(placedData, 1);

            var pop = Current;
            if (pop.shelterCap == cap) return;
            pop.shelterCap = cap;
            Current = pop;
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        // L1/L2/L3 → ×1/×3/×7 ของ shelterCapacity (10 → +10/+30/+70; รวมฐาน = 20/40/80 ตาม §5 L2–L4)
        private static int ShelterContribution(BuildingData data, int level)
        {
            if (data == null || data.shelterCapacity <= 0) return 0;
            return data.shelterCapacity * ((1 << level) - 1);
        }

        // ── ฝึกคลาส (Worker → Engineer/Medic) ────────────────────
        // เหตุผลที่ฝึกไม่ได้ทุกกรณี → toast ผ่าน Notice (AlertController) แทนที่จะเงียบ (เดิม Debug.Log อย่างเดียว)
        /// <summary>ฝึกวิศวกร — ต้องมีห้องปฏิบัติการ, มี Worker ว่าง, จ่าย Food/Energy · เสร็จวันถัดไป</summary>
        public void TrainEngineer()
        {
            if (!_engineerUnlocked) { Notice("ต้องสร้างห้องปฏิบัติการก่อนจึงจะฝึกวิศวกรได้"); return; }
            TryTrain(trainEngineerFood, trainEngineerEnergy, WorkerClass.Engineer, className: "วิศวกร");
        }

        /// <summary>ฝึกแพทย์ — ต้องมีโรงพยาบาล (สเปกโรงวิจัย: Medic ฝึกที่ รพ.), มี Worker ว่าง, จ่าย Food/Energy · เสร็จวันถัดไป</summary>
        public void TrainMedic()
        {
            if (!_medicUnlocked) { Notice("ต้องสร้างโรงพยาบาลก่อนจึงจะฝึกแพทย์ได้"); return; }
            TryTrain(trainMedicFood, trainMedicEnergy, WorkerClass.Medic, className: "แพทย์");
        }

        /// <summary>ฝึกเกษตรกร — ต้องมีห้องปฏิบัติการ, มี Worker ว่าง, จ่าย Food/Energy · เสร็จวันถัดไป</summary>
        public void TrainFarmer()
        {
            if (!_farmerUnlocked) { Notice("ต้องสร้างห้องปฏิบัติการก่อนจึงจะฝึกเกษตรกรได้"); return; }
            TryTrain(trainFarmerFood, trainFarmerEnergy, WorkerClass.Farmer, className: "เกษตรกร");
        }

        private void TryTrain(int foodCost, int energyCost, WorkerClass target, string className)
        {
            var pop = Current;
            if (pop.workers <= 0) { Notice("ไม่มีคนงานว่างให้ฝึก (ต้องมี Worker อย่างน้อย 1 คน)"); return; }

            var rm = ResourceManager.Instance;
            if (rm != null && (rm.Current.food < foodCost || rm.Current.energy < energyCost))
            {
                Notice($"ทรัพยากรไม่พอ — ฝึก{className}ต้องใช้ Food {foodCost} + Energy {energyCost}");
                return;
            }

            // จ่ายต้นทุน + ดึง Worker เข้าฝึก (เพิ่มคลาสจริงตอนสิ้นวัน — 1 วัน)
            if (rm != null)
            {
                EventManager.Instance.RaiseResourceDelta(ResourceType.Food, -foodCost);
                EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -energyCost);
            }
            pop.workers -= 1;
            Current = pop;
            switch (target)
            {
                case WorkerClass.Engineer: _pendingEngineers++; break;
                case WorkerClass.Medic:    _pendingMedics++;    break;
                case WorkerClass.Farmer:   _pendingFarmers++;   break;
            }

            EventManager.Instance.RaisePopulationChanged(Current);
            // OnClassTrained(bool isEngineer) — สัญญาณ feedback ฝึกสำเร็จ (tutorial Day 1 ไม่ใช้แล้ว —
            // เปลี่ยนเป็นภารกิจจัดคน เพราะ Lab ปลดเฟส 2 · คง event ไว้ให้ UI/ระบบอื่น subscribe ได้)
            EventManager.Instance.RaiseClassTrained(target == WorkerClass.Engineer);
            Notice($"เริ่มฝึก{className} — จะพร้อมใช้งานเมื่อจบวัน");
        }

        // แจ้งเหตุผล/ผลการฝึกเป็น toast (AlertController) + log ไว้ debug
        private static void Notice(string message)
        {
            Debug.Log($"[Population] {message}");
            EventManager.Instance?.RaiseNotice(message);
        }

        // ── คนตาย (V4 §9): จากทางเลือกวิกฤต/Decree — Hope −5/คน · ดึงจาก Worker ก่อน ──
        private void HandlePopulationDeaths(int count)
        {
            if (count <= 0) return;

            var pop = Current;
            int remaining = Mathf.Min(count, pop.total);
            if (remaining <= 0) return;

            int dead = remaining;
            int fromWorkers = Mathf.Min(pop.workers, remaining);
            pop.workers -= fromWorkers; remaining -= fromWorkers;
            int fromFarmers = Mathf.Min(pop.farmers, remaining);
            pop.farmers -= fromFarmers; remaining -= fromFarmers;
            int fromMedics = Mathf.Min(pop.medics, remaining);
            pop.medics -= fromMedics; remaining -= fromMedics;
            pop.engineers -= Mathf.Min(pop.engineers, remaining);
            pop.sick = Mathf.Max(0, pop.sick - dead); // คนตายลดจำนวนผู้ป่วยด้วย (Story Guide §4 — patientDeathRisk)

            pop.hope -= hopeLossPerDeath * dead;
            Notice($"สูญเสียประชากร {dead} คน — ขวัญกำลังใจสั่นคลอน (Hope −{hopeLossPerDeath * dead:0})");
            ApplyAndBroadcast(pop);
        }

        // ── ป่วยจากรังสี (Story Guide §4 วิกฤตโรครังสี) — CrisisEffectManager สั่งผ่าน event ──
        // sick = ป้ายกำกับ "จำนวนคนป่วย" เหนือประชากรเดิม (subset · clamp ≤ total) ไม่กระทบการบริโภค/กำลังผลิตโดยตรง
        private void HandlePopulationSickInjected(int count)
        {
            if (count <= 0) return;
            var pop = Current;
            pop.sick = Mathf.Clamp(pop.sick + count, 0, pop.total);
            Current = pop;
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        private void HandlePopulationSickSet(int count)
        {
            var pop = Current;
            pop.sick = Mathf.Clamp(count, 0, pop.total);
            Current = pop;
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        // รักษาป่วยสูงสุด count คน (ResearchLab_Spec: ยาไอโซโทปรักษา ≤15) — ResearchManager สั่งผ่าน event
        private void HandlePopulationSickCured(int count)
        {
            if (count <= 0) return;
            var pop = Current;
            if (pop.sick <= 0) return;
            pop.sick = Mathf.Max(0, pop.sick - count);
            Current = pop;
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        // จำนวนโรงพยาบาลที่ Medic ประจำครบ (GDD §6) — read-only query ของ registry + WAM
        // (idiom เดียวกับ RecalcShelterCap — อนุญาต query ข้าม manager ห้ามเฉพาะเรียก method เปลี่ยนสถานะ)
        private static int StaffedHospitals()
        {
            var registry = BuildingRegistry.Instance;
            var wam = WorkerAssignmentManager.Instance;
            if (registry == null || wam == null) return 0;

            int count = 0;
            foreach (var kvp in registry.PlacedBuildings)
            {
                if (kvp.Value == null || kvp.Value.buildingType != BuildingType.Hospital) continue;
                if (wam.GetAssigned(kvp.Key) >= kvp.Value.workerRequired) count++;
            }
            return count;
        }

        // ── สิ้นวัน: ขวัญ + ฝึกเสร็จ + เติมประชากร (V4 §5/§9) ──────
        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 tutorial ไม่คิด

            // v6.3 (Sprint 1): WorkerManager active → Hope เป็นของ HopeLedger + คนเป็น per-worker state
            // ระบบขวัญ/เติมประชากรเดิมทั้งก้อนต้องหลบ ไม่งั้นคำนวณ Hope ซ้อนสองระบบ
            if (WorkerManager.Instance != null) return;

            var pop = Current;

            // 1) ขวัญกำลังใจ (ตาราง Hope deltas §18): อาหารขาด −10/วัน · ฟื้น +2/วัน เมื่อมี Medic และไม่ขาดของ
            if (_depletedResources.Contains(ResourceType.Food))
                pop.hope -= hopeLossFoodShortage;
            else if (_depletedResources.Count == 0 && pop.medics > 0)
                pop.hope += hopeRecoveryWithMedic;

            // 1b) Sensitivity V5 — แรงกดดันรายวัน (เดิม Hope ลดเฉพาะตอนอาหารหมดเกลี้ยง เลยรู้สึกลดยาก)
            pop.hope -= hopeDailyDrift; // ฐานความยากลำบาก → ต้องบริหารเชิงรุก (Medic ฟื้น +2 ยังกลบ drift ได้)

            // หิวง่ายขึ้น: อาหารเหลือกินได้ < buffer วัน → Hope ลด (ก่อนจะหมดเกลี้ยง)
            var rm = ResourceManager.Instance;
            if (rm != null && !_depletedResources.Contains(ResourceType.Food))
            {
                float foodPerDay = rm.consumeFoodPerPerson * pop.total;
                if (foodPerDay > 0f && rm.Current.food < foodPerDay * lowFoodBufferDays)
                    pop.hope -= hopeLossLowFood;
            }

            // ทำงานหนัก → เหนื่อยล้า: สัดส่วนคนที่ถูกมอบหมายงานสูงเกิน overworkRatio → Hope ลด
            var wam = WorkerAssignmentManager.Instance;
            if (wam != null && pop.total > 0 && (float)wam.TotalAssigned / pop.total >= overworkRatio)
                pop.hope -= hopeLossOverwork;

            // 1.5) ฟื้นจากรังสี (Story Guide §4 + GDD §6): กำลังรักษา/วัน = Medic + โรงพยาบาลที่ประจำครบ × heal
            // เมื่ออาหารไม่ขาด (สมมาตรกับ +Hope)
            int healCapacity = pop.medics + StaffedHospitals() * hospitalHealPerDay;
            if (pop.sick > 0 && healCapacity > 0 && !_depletedResources.Contains(ResourceType.Food))
                pop.sick = Mathf.Max(0, pop.sick - healCapacity);

            // 2) ฝึกคลาสเสร็จ (1 วัน) — Worker ถูกดึงไปแล้วตอนสั่งฝึก
            pop.engineers += _pendingEngineers; _pendingEngineers = 0;
            pop.medics += _pendingMedics; _pendingMedics = 0;
            pop.farmers += _pendingFarmers; _pendingFarmers = 0;

            // 3) เติมประชากร +1 Worker: อาหารไม่ขาด & Hope ≥ 50 & ยังไม่เต็มเพดาน
            if (!_depletedResources.Contains(ResourceType.Food)
                && pop.hope >= growthHopeThreshold
                && pop.total < pop.shelterCap)
                pop.workers += 1;

            ApplyAndBroadcast(pop);
        }

        private void HandleMoraleDelta(float hopeDelta)
        {
            // v6.3 (Sprint 1): Hope ห้ามเขียนตรง — ส่งเข้า HopeLedger แทน (กติกาข้อ 8)
            // dilemma/decree เดิมที่ยิง OnMoraleDelta จะเข้าบัญชีเป็น card.* รอสรุปจบวัน
            if (WorkerManager.Instance != null)
            {
                WorkerManager.Instance.Hope.Report("card.legacy", "ผลจากทางเลือก", hopeDelta, HopeCategory.Card);
                return;
            }

            var pop = Current;
            pop.hope += hopeDelta;
            ApplyAndBroadcast(pop);
        }

        private void ApplyAndBroadcast(PopulationData pop)
        {
            pop.hope = Mathf.Clamp(pop.hope, 0f, 100f);
            Current = pop;

            EventManager.Instance.RaiseMoraleChanged(pop.hope);
            EventManager.Instance.RaisePopulationChanged(pop);

            if (pop.hope <= 0f && !_gameOverRaised)
            {
                _gameOverRaised = true;
                EventManager.Instance.RaiseGameOver(GameEndType.HopeZero);
            }
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.population;
            _gameOverRaised = false;
            // v6.3: same as Start — don't fight HopeLedger for the morale broadcast when active
            if (WorkerManager.Instance == null)
                EventManager.Instance.RaiseMoraleChanged(Current.hope);
            EventManager.Instance.RaisePopulationChanged(Current);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ───────────────────────── Debug / Cheat (เฉพาะทดสอบ — คอมไพล์ทิ้งใน release build) ─────────────────────────

        /// <summary>[DEBUG] เพิ่ม/ลดประชากรตามคลาสตรง ๆ (เฉพาะทดสอบ)</summary>
        public void DebugAddPopulation(WorkerClass cls, int count)
        {
            var pop = Current;
            switch (cls)
            {
                case WorkerClass.Engineer: pop.engineers = Mathf.Max(0, pop.engineers + count); break;
                case WorkerClass.Medic:    pop.medics    = Mathf.Max(0, pop.medics + count);    break;
                case WorkerClass.Farmer:   pop.farmers   = Mathf.Max(0, pop.farmers + count);   break;
                default:                   pop.workers   = Mathf.Max(0, pop.workers + count);    break;
            }
            Current = pop;
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        /// <summary>[DEBUG] ตั้งค่า Hope ตรง ๆ 0–100 (เฉพาะทดสอบ)</summary>
        public void DebugSetHope(float hope)
        {
            _gameOverRaised = false;
            var pop = Current;
            pop.hope = Mathf.Clamp(hope, 0f, 100f);
            Current = pop;
            EventManager.Instance.RaiseMoraleChanged(pop.hope);
            EventManager.Instance.RaisePopulationChanged(pop);
        }

        /// <summary>[DEBUG] ปลดล็อกการฝึกทุกคลาส (ข้ามเงื่อนไขต้องมีห้องปฏิบัติการ)</summary>
        public void DebugUnlockAllTraining()
        {
            _engineerUnlocked = _medicUnlocked = _farmerUnlocked = true;
        }
#endif
    }
}
