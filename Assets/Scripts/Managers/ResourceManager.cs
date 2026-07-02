using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ติดตามทรัพยากรหลัก (V4 §4) — หักต้นทุนเมื่อวางอาคาร
    /// ผลิต+บริโภคแบบ batch วันละครั้งตอนจบวัน (OnDayProduction — V4 §3):
    ///   ผลิต/upkeep รายอาคาร (§6 ค่าต่อวัน) → บริโภค Food/Water 2/คน/วัน (§4)
    /// 6 ชนิด: Energy/Water/Food/Iron/Deuterium/Tritium + Knowledge (สะสม 0–100)
    /// จำนวนคนงานสำหรับ worker-scaling ดึงจาก PopulationManager.Current.workers (คลาส Worker)
    /// tick 5 วิ เหลือหน้าที่เดียว: เดิน progress งานก่อสร้าง (OnGameTick)
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        /// <summary>
        /// ระดับความรู้ (V4 §9) — โบนัสเป็นบวกล้วน ไม่ลงโทษ
        /// 0–29 Novice · 30–59 Aware · 60–79 Skilled · 80–100 Expert (knowBonus +0.10)
        /// </summary>
        public enum KnowledgeTier { Novice, Aware, Skilled, Expert }

        // V4 §4 ระบุ cap 9999 (= "แทบไม่จำกัด") สำหรับส่วนใหญ่ — แต่ใช้ค่าที่ normalize HUD bar
        // + critical alert (ratio ของ max) ได้จริง ค่าจริงจะ playtest/จูนในเฟส 8
        [Header("Max Capacity (display/clamp — V4 §4)")]
        public float maxEnergy = 2000f;
        public float maxWater = 1000f;
        public float maxFood = 500f;          // เกิน 500 → trigger วิกฤตเน่า (V4 §10) — ค่าตายตัว
        public float maxIron = 1000f;
        public float maxDeuterium = 500f;
        public float maxTritium = 500f;
        public float maxKnowledge = 100f;

        [Header("Tick (ใช้เฉพาะงานก่อสร้าง — production เป็น batch จบวัน)")]
        public float tickInterval = 5f;

        // V4 §4/§18: consumeFoodPerPerson / consumeWaterPerPerson = 2 ต่อวัน
        [Header("Daily Consumption (V4 §4 — ต่อคนต่อวัน)")]
        public float consumeFoodPerPerson = 2f;
        public float consumeWaterPerPerson = 2f;

        // ตัวคูณผลผลิตต่อระดับอาคาร L1/L2/L3 (V4 §18: 60→180→450 = ×1/×3/×7.5)
        private static readonly float[] LevelProductionMultiplier = { 1f, 3f, 7.5f };

        [Header("Critical Threshold (ratio of max)")]
        [Range(0f, 1f)] public float criticalRatio = 0.2f;

        // ค่าเริ่มต้น Day 1 ตาม V4 §4 / §18
        public ResourceData Current { get; private set; } = new ResourceData
        {
            energy = 200f,
            water = 150f,
            food = 150f,
            iron = 100f,
            deuterium = 0f,
            tritium = 0f,
            knowledge = 0f,
        };

        /// <summary>ระดับความรู้ปัจจุบันจาก Current.knowledge (0–100) — ใช้กับ HUD/tier bonus</summary>
        public KnowledgeTier Tier =>
            Current.knowledge < 30f ? KnowledgeTier.Novice :
            Current.knowledge < 60f ? KnowledgeTier.Aware  :
            Current.knowledge < 80f ? KnowledgeTier.Skilled :
                                      KnowledgeTier.Expert;

        /// <summary>CORE Efficiency bonus — Expert (Knowledge ≥ 80) ให้ +0.10 ต่อ ΔCORE% (§8)</summary>
        public float KnowBonus => (Current.knowledge >= 80f) ? 0.10f : 0f;

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
            EventManager.Instance.OnDayProduction += HandleDayProduction;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta -= HandleResourceDelta;
            EventManager.Instance.OnDayProduction -= HandleDayProduction;
        }

        private void Start()
        {
            EventManager.Instance.RaiseResourceChanged(Current);
        }

        private void Update()
        {
            // tick หยุดเมื่อนาฬิกาวันถูก pause (วางอาคาร/ควิซ/วิกฤต — V4 §15)
            if (TimeManager.Instance != null && !TimeManager.Instance.IsRunning)
                return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval)
                return;

            _tickTimer -= tickInterval;
            EventManager.Instance.RaiseGameTick(); // เดิน progress ก่อสร้าง (ConstructionController)
        }

        // จบวัน (V4 §3): ผลิตทั้งวันทีเดียว (batch) → หักการบริโภคของประชากร
        private void HandleDayProduction(int day)
        {
            ApplyDailyProduction();
            ApplyDailyConsumption();
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            // หักต้นทุนสร้างครั้งเดียว (Energy + Iron) — workers เป็น reserve pool (ไม่หักถาวร)
            var c = Current;
            c.energy = Mathf.Max(0f, c.energy - data.energyCost);
            c.iron   = Mathf.Max(0f, c.iron - data.ironCost);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        private void HandleSaveLoaded(SaveData save)
        {
            Current = save.resources;
            EventManager.Instance.RaiseResourceChanged(Current);
            EventManager.Instance.RaiseKnowledgeChanged(Current.knowledge); // sync HUD/tier หลังโหลด
            CheckThresholds();
        }

        private void HandleResourceDelta(ResourceType type, float amount)
        {
            var c = Current;
            float prevKnowledge = c.knowledge;

            switch (type)
            {
                case ResourceType.Energy:
                    c.energy = Mathf.Clamp(c.energy + amount, 0f, maxEnergy);
                    break;
                case ResourceType.Water:
                    c.water = Mathf.Clamp(c.water + amount, 0f, maxWater);
                    break;
                case ResourceType.Food:
                    c.food = Mathf.Clamp(c.food + amount, 0f, maxFood);
                    break;
                case ResourceType.Iron:
                    c.iron = Mathf.Clamp(c.iron + amount, 0f, maxIron);
                    break;
                case ResourceType.Deuterium:
                    c.deuterium = Mathf.Clamp(c.deuterium + amount, 0f, maxDeuterium);
                    break;
                case ResourceType.Tritium:
                    c.tritium = Mathf.Clamp(c.tritium + amount, 0f, maxTritium);
                    break;
                case ResourceType.Knowledge:
                    c.knowledge = Mathf.Clamp(c.knowledge + amount, 0f, maxKnowledge);
                    break;
            }

            Current = c;
            EventManager.Instance.RaiseResourceChanged(Current);
            // OnKnowledgeChanged ยิงเฉพาะเมื่อ knowledge ขยับจริง (G8: ไม่มี broadcast ตั้งต้น)
            if (!Mathf.Approximately(c.knowledge, prevKnowledge))
                EventManager.Instance.RaiseKnowledgeChanged(c.knowledge);
            CheckThresholds();
        }

        /// <summary>
        /// ผลิตทั้งวันทีเดียว (batch — V4 §3/§6): อาคารจ่าย upkeep ต่อวันก่อนจึงผลิต
        /// ปรับตามกำลังคน (workerScale) × ตัวคูณระดับ L1/L2/L3
        /// </summary>
        public void ApplyDailyProduction()
        {
            var c = Current;

            // กำลังคน (§A2): ใช้เฉพาะคลาส Worker (Engineer/Medic ไม่ประจำโรงผลิต)
            // ถ้า demand รวม > จำนวน Worker → ทุกอาคารผลิตตามสัดส่วน workerScale (0..1)
            int totalWorkers = PopulationManager.Instance != null
                ? PopulationManager.Instance.Current.workers
                : 0;

            int totalWorkersNeeded = 0;
            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (!IsOperational(kvp.Key)) continue;
                totalWorkersNeeded += kvp.Value.workerRequired;
            }
            float workerScale = totalWorkersNeeded > 0
                ? Mathf.Min(1f, (float)totalWorkers / totalWorkersNeeded)
                : 1f;

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (!IsOperational(kvp.Key)) continue;

                var data = kvp.Value;

                // ค่าเดินระบบ/วัน (V4 §6): อาคารต้องจ่าย energy/water ของวันนั้นก่อนจึงจะผลิต
                if (c.energy < data.energyConsumption || c.water < data.waterConsumption)
                    continue;

                c.energy -= data.energyConsumption;
                c.water -= data.waterConsumption;

                // ผลิตปรับตามกำลังคน (§A2) × ตัวคูณระดับอาคาร L1/L2/L3 = ×1/×3/×7.5 (V4 §18)
                int level = BuildingRegistry.Instance.GetLevel(kvp.Key);
                float lvlMul = LevelProductionMultiplier[Mathf.Clamp(level - 1, 0, LevelProductionMultiplier.Length - 1)];
                float scale = workerScale * lvlMul;

                c.food   += data.foodProduction * scale;
                c.water  += data.waterProduction * scale;
                c.energy += data.energyProduction * scale;
                c.iron   += data.ironProduction * scale;

                // เชื้อเพลิงฟิวชันผลิตเฉพาะเมื่ออัปถึงระดับสูงสุด (Water L3 → Deuterium, Zone B/Lab L3 → Tritium)
                if (level >= BuildingRegistry.Instance.maxBuildingLevel)
                {
                    c.deuterium += data.deuteriumProduction * workerScale;
                    c.tritium   += data.tritiumProduction * workerScale;
                }
            }

            c.energy = Mathf.Clamp(c.energy, 0f, maxEnergy);
            c.water  = Mathf.Clamp(c.water, 0f, maxWater);
            c.food   = Mathf.Clamp(c.food, 0f, maxFood);
            c.iron   = Mathf.Clamp(c.iron, 0f, maxIron);
            c.deuterium = Mathf.Clamp(c.deuterium, 0f, maxDeuterium);
            c.tritium   = Mathf.Clamp(c.tritium, 0f, maxTritium);
            c.knowledge = Mathf.Clamp(c.knowledge, 0f, maxKnowledge);

            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        /// <summary>
        /// การบริโภคของประชากรต่อวัน (V4 §4): Food/Water คนละ 2 ต่อวัน (ทุกคลาส)
        /// คลังไม่พอ → clamp 0 แล้ว CheckThresholds ยิง OnResourceDepleted
        /// ให้ PopulationManager หัก Hope ตอน OnDayEnded (V4 §9 อาหารขาด)
        /// </summary>
        public void ApplyDailyConsumption()
        {
            int population = PopulationManager.Instance != null
                ? PopulationManager.Instance.Current.total
                : 0;
            if (population <= 0) return;

            var c = Current;
            c.food  = Mathf.Max(0f, c.food  - consumeFoodPerPerson  * population);
            c.water = Mathf.Max(0f, c.water - consumeWaterPerPerson * population);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
        }

        /// <summary>
        /// อาคารพร้อมเดินเครื่องไหม — แค่ "สร้างเสร็จแล้ว" ก็เดินเครื่องได้
        /// (V4 §6: ต้นทุนพลังงานของอาคาร = upkeep ต่อวัน — GDD ไม่มีกลไกรัศมีไฟ
        /// power grid เดิม gate การผลิตด้วย powerRange ที่ไม่เคยถูกตั้ง → ทุกตึกผลิตไม่ได้ทั้งเกม)
        /// </summary>
        private bool IsOperational(Vector2Int cell)
        {
            return ConstructionController.Instance == null ||
                   !ConstructionController.Instance.IsUnderConstruction(cell);
        }

        private void CheckThresholds()
        {
            CheckResource(Current.energy, maxEnergy, ResourceType.Energy);
            CheckResource(Current.water, maxWater, ResourceType.Water);
            CheckResource(Current.food, maxFood, ResourceType.Food);
            CheckResource(Current.iron, maxIron, ResourceType.Iron);
            // Deuterium/Tritium/Knowledge เริ่มที่ 0 โดยตั้งใจ — ไม่ alert depletion
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
