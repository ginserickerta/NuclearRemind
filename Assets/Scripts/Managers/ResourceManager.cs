using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ติดตามทรัพยากรหลักของเมือง Veltara (ResourceData) — หักต้นทุนเมื่อวางอาคาร
    /// และบวก production ของอาคารที่วางแล้วทุก tick
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Max Capacity")]
        public float maxFood = 500f;
        public float maxWater = 400f;
        public float maxRadiationProtection = 200f;
        public float maxEnergy = 1000f; // §10 เริ่มที่ 400 → ต้อง > 400 (ค่า rough, ปรับบาลานซ์ภายหลัง)
        public int maxWorkers = 100;
        public float maxResearchPoints = 500f;

        [Header("Tick")]
        public float tickInterval = 5f;

        [Header("Critical Threshold (ratio of max)")]
        [Range(0f, 1f)] public float criticalRatio = 0.2f;

        // ค่าเริ่มต้นตาม GDD §10 (หลังซ่อม Shelter): energy 400 / water 100 / food 100 / workers 10
        // radiationProtection เป็น field legacy (ไม่อยู่ใน §2.1 5-resource ของ v2.1) — คงไว้ก่อน
        // material / fusionFuel ยังไม่มีใน ResourceData (รอ resource-model refactor)
        public ResourceData Current { get; private set; } = new ResourceData
        {
            food = 100f,
            water = 100f,
            radiationProtection = 50f,
            energy = 400f,
            workers = 10
        };

        private float _tickTimer;

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
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta += HandleResourceDelta;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta -= HandleResourceDelta;
        }

        private void Start()
        {
            EventManager.Instance.RaiseResourceChanged(Current);
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
                return;

            _tickTimer -= tickInterval;
            Tick();
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            // หักเฉพาะ energyCost (ต้นทุนสร้างครั้งเดียว) — materialCost ยังไม่มี ResourceType ของตัวเอง
            // workers ไม่ถูกหักถาวร: เป็น reserve pool ที่ถูกจองตาม workerRequired ตอน Tick (ดู worker staffing scale)
            var c = Current;
            c.energy = Mathf.Max(0f, c.energy - data.energyCost);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.resources;
            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleResourceDelta(ResourceType type, float amount)
        {
            var c = Current;

            switch (type)
            {
                case ResourceType.Food:
                    c.food = Mathf.Clamp(c.food + amount, 0f, maxFood);
                    break;
                case ResourceType.Water:
                    c.water = Mathf.Clamp(c.water + amount, 0f, maxWater);
                    break;
                case ResourceType.RadiationProtection:
                    c.radiationProtection = Mathf.Clamp(c.radiationProtection + amount, 0f, maxRadiationProtection);
                    break;
                case ResourceType.Energy:
                    c.energy = Mathf.Clamp(c.energy + amount, 0f, maxEnergy);
                    break;
                case ResourceType.Workers:
                    c.workers = Mathf.Clamp(c.workers + Mathf.RoundToInt(amount), 0, maxWorkers);
                    break;
                case ResourceType.ResearchPoints:
                    c.researchPoints = Mathf.Clamp(c.researchPoints + amount, 0f, maxResearchPoints);
                    break;
            }

            Current = c;
            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void Tick()
        {
            EventManager.Instance.RaiseGameTick();

            var c = Current;

            // กำลังคน (§A2): workers เป็น reserve pool — รวม workerRequired ของอาคารที่พร้อมเดินเครื่อง
            // ถ้า demand รวม > จำนวนคนที่มี → ทุกอาคารผลิตตามสัดส่วน workerScale (0..1)
            int totalWorkersNeeded = 0;
            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (!IsOperational(kvp.Key)) continue;
                totalWorkersNeeded += kvp.Value.workerRequired;
            }
            float workerScale = totalWorkersNeeded > 0
                ? Mathf.Min(1f, (float)c.workers / totalWorkersNeeded)
                : 1f;

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (!IsOperational(kvp.Key)) continue;

                var data = kvp.Value;

                // เดินระบบ (§2.4): อาคารต้องจ่ายต้นทุน energy/water ต่อ tick ก่อนจึงจะผลิต
                // ถ้าคลังไม่พอจ่าย → อาคารหยุด (skip production) และไม่กิน resource tick นั้น
                if (c.energy < data.energyConsumption || c.water < data.waterConsumption)
                    continue;

                c.energy -= data.energyConsumption;
                c.water -= data.waterConsumption;

                // production ปรับตามกำลังคน (§A2) — คนไม่พอ → ผลิตได้สัดส่วน workerScale
                c.food += data.foodProduction * workerScale;
                c.water += data.waterProduction * workerScale;
                c.radiationProtection += data.radiationProtectionBonus * workerScale;
                c.energy += data.energyProduction * workerScale;
                c.researchPoints += data.researchPointsPerTick * workerScale;
            }

            c.food = Mathf.Clamp(c.food, 0f, maxFood);
            c.water = Mathf.Clamp(c.water, 0f, maxWater);
            c.radiationProtection = Mathf.Clamp(c.radiationProtection, 0f, maxRadiationProtection);
            c.energy = Mathf.Clamp(c.energy, 0f, maxEnergy);
            c.workers = Mathf.Clamp(c.workers, 0, maxWorkers);
            c.researchPoints = Mathf.Clamp(c.researchPoints, 0f, maxResearchPoints);

            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        /// <summary>
        /// อาคารพร้อมเดินเครื่องไหม — ไม่อยู่ระหว่างสร้าง และได้รับพลังงานจาก Power Grid
        /// ใช้ทั้งตอนรวม worker demand และตอนผลิตจริง ให้เกณฑ์ตรงกัน
        /// </summary>
        private bool IsOperational(Vector2Int cell)
        {
            if (ConstructionController.Instance != null &&
                ConstructionController.Instance.IsUnderConstruction(cell))
                return false;

            if (PowerGridManager.Instance != null &&
                !PowerGridManager.Instance.IsBuildingPowered(cell))
                return false;

            return true;
        }

        private void CheckThresholds()
        {
            CheckResource(Current.food, maxFood, ResourceType.Food);
            CheckResource(Current.water, maxWater, ResourceType.Water);
            CheckResource(Current.radiationProtection, maxRadiationProtection, ResourceType.RadiationProtection);
            CheckResource(Current.energy, maxEnergy, ResourceType.Energy);
            CheckResource(Current.workers, maxWorkers, ResourceType.Workers);
        }

        private void CheckResource(float amount, float max, ResourceType type)
        {
            if (amount <= 0f)
                EventManager.Instance.RaiseResourceDepleted(type);
            else if (amount < max * criticalRatio)
                EventManager.Instance.RaiseResourceCritical(type);
        }
    }
}
