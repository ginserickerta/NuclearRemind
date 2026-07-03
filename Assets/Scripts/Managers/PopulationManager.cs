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

        [Header("Morale Tuning (V4 §9 — จูนจริงเฟส 8)")]
        public float hopeLossPerShortage = 5f;
        public float hopeRecoveryPerDay = 3f;

        [Header("Training Cost (V4 §5)")]
        public int trainEngineerFood = 30, trainEngineerEnergy = 50;
        public int trainMedicFood = 40, trainMedicEnergy = 60;

        [Header("Population Growth (V4 §5)")]
        public float growthHopeThreshold = 50f; // Hope ≥ ค่านี้จึงเติมประชากร

        [Header("Shelter (V4 §5)")]
        public int baseShelterCap = 10; // Shelter L1 เริ่มเกม (ไม่ต้องสร้าง) — ตึก Shelter ที่วางเพิ่มต่อยอดจากฐานนี้

        public PopulationData Current { get; private set; } = new PopulationData
        {
            workers = 10,     // V4 §5 เริ่ม 10 Worker
            hope = 100f,      // V4 §9 เริ่ม 100
            shelterCap = 10,  // = baseShelterCap (Shelter L1 §5) — ขยายด้วยตึก Shelter ผ่าน RecalcShelterCap
        };

        /// <summary>วิศวกรที่พร้อมประจำหล่อเย็น CORE (เข้าสูตร +4/คน เมื่อมี Poloidal — §8)</summary>
        public int AssignedCoolingEngineers => Current.engineers;

        public bool EngineerTrainingUnlocked => _engineerUnlocked;
        public bool MedicTrainingUnlocked => _medicUnlocked;

        private readonly HashSet<ResourceType> _depletedResources = new HashSet<ResourceType>();
        private bool _gameOverRaised;
        private int _pendingEngineers, _pendingMedics; // ฝึกค้าง (เสร็จสิ้นวัน — ใช้เวลา 1 วัน)
        private bool _engineerUnlocked, _medicUnlocked; // ปลดจากอาคาร (Research Lab / Hospital)

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
        }

        private void Start()
        {
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
            TryTrain(trainEngineerFood, trainEngineerEnergy, isEngineer: true, className: "วิศวกร");
        }

        /// <summary>ฝึกแพทย์ — ต้องมีห้องปฏิบัติการ (ชั่วคราวจน Hospital มา), มี Worker ว่าง, จ่าย Food/Energy · เสร็จวันถัดไป</summary>
        public void TrainMedic()
        {
            if (!_medicUnlocked) { Notice("ต้องสร้างห้องปฏิบัติการก่อนจึงจะฝึกแพทย์ได้"); return; }
            TryTrain(trainMedicFood, trainMedicEnergy, isEngineer: false, className: "แพทย์");
        }

        private void TryTrain(int foodCost, int energyCost, bool isEngineer, string className)
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
            if (isEngineer) _pendingEngineers++; else _pendingMedics++;

            EventManager.Instance.RaisePopulationChanged(Current);
            Notice($"เริ่มฝึก{className} — จะพร้อมใช้งานเมื่อจบวัน");
        }

        // แจ้งเหตุผล/ผลการฝึกเป็น toast (AlertController) + log ไว้ debug
        private static void Notice(string message)
        {
            Debug.Log($"[Population] {message}");
            EventManager.Instance?.RaiseNotice(message);
        }

        // ── สิ้นวัน: ขวัญ + ฝึกเสร็จ + เติมประชากร (V4 §5/§9) ──────
        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 tutorial ไม่คิด

            var pop = Current;

            // 1) ขวัญกำลังใจ
            int shortages = _depletedResources.Count;
            if (shortages > 0) pop.hope -= hopeLossPerShortage * shortages;
            else pop.hope += hopeRecoveryPerDay;

            // 2) ฝึกคลาสเสร็จ (1 วัน) — Worker ถูกดึงไปแล้วตอนสั่งฝึก
            pop.engineers += _pendingEngineers; _pendingEngineers = 0;
            pop.medics += _pendingMedics; _pendingMedics = 0;

            // 3) เติมประชากร +1 Worker: อาหารไม่ขาด & Hope ≥ 50 & ยังไม่เต็มเพดาน
            if (!_depletedResources.Contains(ResourceType.Food)
                && pop.hope >= growthHopeThreshold
                && pop.total < pop.shelterCap)
                pop.workers += 1;

            ApplyAndBroadcast(pop);
        }

        private void HandleMoraleDelta(float hopeDelta)
        {
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
            EventManager.Instance.RaiseMoraleChanged(Current.hope);
            EventManager.Instance.RaisePopulationChanged(Current);
        }
    }
}
