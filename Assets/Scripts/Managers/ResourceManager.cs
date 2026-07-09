using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ติดตามทรัพยากรหลัก (V4 §4) — หักต้นทุนเมื่อวางอาคาร
    /// ผลิต+บริโภคเรียลไทม์ต่อ tick (5 วิ · ApplyProductionTick) แล้ว reconcile จบวัน (OnDayProduction)
    ///   ให้ยอดสุทธิเท่าตาราง §6/§4 · ผลิต/upkeep รายอาคาร → บริโภค Food/Water 2/คน/วัน
    /// 6 ชนิด: Energy/Water/Food/Iron/Deuterium/Tritium + Knowledge (สะสม 0–100)
    /// จำนวนคนงานสำหรับ worker-scaling ดึงจาก PopulationManager.Current.workers (คลาส Worker)
    /// tick 5 วิ: เดิน progress ก่อสร้าง + ผลิต/บริโภคเรียลไทม์ (OnGameTick · เฉพาะวันที่จับเวลา)
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        /// <summary>
        /// ระดับความรู้ (V4 §9) — โบนัสเป็นบวกล้วน ไม่ลงโทษ
        /// 0–29 Novice · 30–59 Aware · 60–79 Skilled · 80–100 Expert (knowBonus +0.10)
        /// </summary>
        public enum KnowledgeTier { Novice, Aware, Skilled, Expert }

        // V4 §4: cap 9999 = "แทบไม่จำกัด" ทุกชนิดยกเว้น Food 500 (เกิน → วิกฤตเน่า §10) / Knowledge 100
        // แถบ HUD ไม่ใช้ค่านี้แล้ว (UIManagerHUD.display*) · critical alert แยกเป็นค่าสัมบูรณ์ด้านล่าง
        [Header("Max Capacity (clamp — V4 §4)")]
        public float maxEnergy = 9999f;
        public float maxWater = 9999f;
        public float maxFood = 500f;          // เกิน 500 → trigger วิกฤตเน่า (V4 §10) — ค่าตายตัว
        public float maxIron = 9999f;
        public float maxDeuterium = 9999f;
        public float maxTritium = 9999f;
        public float maxKnowledge = 100f;

        [Header("Tick (ใช้เฉพาะงานก่อสร้าง — production เป็น batch จบวัน)")]
        public float tickInterval = 5f;

        // V4 §4/§18: consumeFoodPerPerson / consumeWaterPerPerson = 2 ต่อวัน
        [Header("Daily Consumption (V4 §4 — ต่อคนต่อวัน)")]
        public float consumeFoodPerPerson = 2f;
        public float consumeWaterPerPerson = 2f;

        // ตัวคูณผลผลิตต่อระดับอาคาร L1/L2/L3 (V4 §18: 60→180→450 = ×1/×3/×7.5)
        private static readonly float[] LevelProductionMultiplier = { 1f, 3f, 7.5f };

        /// <summary>ตัวคูณผลผลิตของอาคารระดับ level (1-based) — single source of truth ให้ UI อ่านโชว์ค่าอัปเกรด</summary>
        public static float LevelMultiplier(int level) =>
            LevelProductionMultiplier[Mathf.Clamp(level - 1, 0, LevelProductionMultiplier.Length - 1)];

        // เดิมใช้ ratio ของ cap (criticalRatio 0.2) — cap ใหม่ 9999 จะทำ alert เด้งทั้งเกม
        // จึงแยกเป็นค่าสัมบูรณ์ = พฤติกรรมเดิมเป๊ะ (scene เก่า: E300/W400/F500/Fe1000 × 0.2)
        [Header("Critical Alert (ค่าสัมบูรณ์ — ไม่ผูกกับ cap)")]
        public float criticalEnergy = 60f;
        public float criticalWater = 80f;
        public float criticalFood = 100f;
        public float criticalIron = 200f;

        // ค่าเริ่มต้น Day 1 ตาม V4 §4 / §18 — ปรับได้ใน Inspector (bump food/water กันขาดวัน 1-3)
        // iron = 240: Day 1 ต้องสร้าง 3 โรง (100) + Habitat (60) + Lab (35) · Day 1 ไม่มี consumption
        [Header("Starting Resources (V4 §4/§18)")]
        [SerializeField] private float startEnergy = 200f;
        [SerializeField] private float startWater = 220f;  // เดิม 150 → 220 (กันน้ำขาดวันแรก ๆ)
        [SerializeField] private float startFood = 220f;   // เดิม 150 → 220 (กันอาหารขาดวันแรก ๆ)
        [SerializeField] private float startIron = 240f;

        public ResourceData Current { get; private set; }

        /// <summary>ระดับความรู้ปัจจุบันจาก Current.knowledge (0–100) — ใช้กับ HUD/tier bonus</summary>
        public KnowledgeTier Tier =>
            Current.knowledge < 30f ? KnowledgeTier.Novice :
            Current.knowledge < 60f ? KnowledgeTier.Aware  :
            Current.knowledge < 80f ? KnowledgeTier.Skilled :
                                      KnowledgeTier.Expert;

        /// <summary>CORE Efficiency bonus — Expert (Knowledge ≥ 80) ให้ +0.10 ต่อ ΔCORE% (§8)</summary>
        public float KnowBonus => (Current.knowledge >= 80f) ? 0.10f : 0f;

        private float _tickTimer;

        // เรียลไทม์ (V4 §3 ปรับใหม่): ผลิต/บริโภคเดินต่อ tick แล้ว reconcile ยอดรวมต่อวันให้เท่าตาราง §6
        private ResourceData _accruedProd;    // ผลิตสะสมที่ลงคลังแล้ววันนี้ (post-clamp)
        private ResourceData _accruedConsume; // บริโภคสะสมที่หักคลังแล้ววันนี้ (post-clamp)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Current = new ResourceData
            {
                energy = startEnergy, water = startWater, food = startFood, iron = startIron,
                deuterium = 0f, tritium = 0f, knowledge = 0f,
            };
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta += HandleResourceDelta;
            EventManager.Instance.OnDayProduction += HandleDayProduction;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnResourceDelta -= HandleResourceDelta;
            EventManager.Instance.OnDayProduction -= HandleDayProduction;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
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

            // V4 §3 (ปรับใหม่): ผลิต/บริโภคเรียลไทม์ต่อ tick — เฉพาะวันที่จับเวลา (Day 1 tutorial ไม่จับเวลา)
            if (GameManager.Instance != null && GameManager.Instance.DayTimerActive)
                ApplyProductionTick();
        }

        // จบวัน (V4 §3): reconcile — เติมส่วนที่ tick ยังไม่ได้ลงให้ยอดสุทธิทั้งวันเท่าตาราง §6/§4
        // (ผลิต → เน่า → บริโภค · Day 1 tutorial ไม่มี consumption)
        private void HandleDayProduction(int day)
        {
            ApplyDelta(Subtract(ComputeProductionDelta(1f), _accruedProd));
            ApplyDailySpoilage();   // Story Guide §4: อาหารเน่า (วิกฤตอาหาร) — ยังคิดรายวัน
            if (day > 1)
                ApplyDelta(Subtract(ComputeConsumptionDelta(1f), _accruedConsume));
            _accruedProd = default;
            _accruedConsume = default;
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
            _accruedProd = default;      // โหลดกลางวัน — ล้าง accrual ของวันเดิม
            _accruedConsume = default;
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

        // ─── ผลิต/บริโภคเรียลไทม์ (V4 §3 ปรับใหม่) ───────────────────────────
        // เดิม batch จบวันทีเดียว → ตอนนี้เดินต่อ tick (ApplyProductionTick) แล้ว reconcile จบวัน
        // ให้ยอดสุทธิเท่าตาราง §6/§4 · ApplyDailyProduction/Consumption คงไว้เป็น wrapper (เทส/เรียกตรง)

        /// <summary>ผลิตเต็มวันทีเดียว — คงพฤติกรรมเดิม (ใช้โดยเทส/เรียกตรง)</summary>
        public void ApplyDailyProduction() => ApplyDelta(ComputeProductionDelta(1f));

        /// <summary>วันเริ่มใหม่: ล้าง accrual ของวันก่อน</summary>
        private void HandleDayStarted(int day, bool timed)
        {
            _accruedProd = default;
            _accruedConsume = default;
        }

        /// <summary>ผลิต/บริโภคเสี้ยวหนึ่งของวันต่อ tick (5 วิ) แล้วสะสมยอดที่ลงจริงไว้ reconcile จบวัน</summary>
        private void ApplyProductionTick()
        {
            float dayLength = GameManager.Instance != null ? GameManager.Instance.dayLength : 0f;
            if (dayLength <= 0f) return;
            float dayFraction = tickInterval / dayLength; // ≈ 1/18 (90s ÷ 5s)

            _accruedProd    = Add(_accruedProd,    ApplyDelta(ComputeProductionDelta(dayFraction)));
            _accruedConsume = Add(_accruedConsume, ApplyDelta(ComputeConsumptionDelta(dayFraction)));
        }

        /// <summary>
        /// เดลตาสุทธิ (ผลิต − ค่าเดินระบบ) ของทุกอาคาร — ไม่แตะ Current ไม่ clamp
        /// dayFraction: 1 = เต็มวัน · tickInterval/dayLength = ต่อ tick
        /// gate ค่าเดินระบบใช้ running energy/water แบบเดียวกับลูป batch เดิม → ผลลัพธ์ที่ dayFraction=1 ตรงเป๊ะ
        /// ปรับตามกำลังคนรายอาคาร (assigned/workerRequired · §5) × busy factor × ตัวคูณระดับ L1/L2/L3 (§18)
        /// </summary>
        private ResourceData ComputeProductionDelta(float dayFraction)
        {
            var delta = new ResourceData();
            float runE = Current.energy; // สะท้อนลูปเดิมที่ลด/เพิ่ม c.energy ระหว่างวน (มีผลต่อ gate อาคารถัดไป)
            float runW = Current.water;

            var assign = WorkerAssignmentManager.Instance;
            // Story Guide §4: busy factor — หักคนงานที่ถูกดึงชั่วคราว (busy/quarantine จากวิกฤต) แบบสัดส่วนทั้งเมือง
            int totalAssigned = assign != null ? assign.TotalAssigned : 0;
            int busy = CrisisEffectManager.Instance != null ? CrisisEffectManager.Instance.BusyWorkers : 0;
            float busyFactor = totalAssigned > 0
                ? (float)CrisisEffectMath.EffectiveWorkers(totalAssigned, busy) / totalAssigned
                : 1f;
            // Story Guide §4: ตัวคูณจากทางเลือกวิกฤต — ประสิทธิภาพงาน (Food C −50%) และผลผลิตอาหาร (Food A +100%)
            float efficiency = CrisisEffectManager.Instance != null ? CrisisEffectManager.Instance.WorkerEfficiencyMultiplier : 1f;
            float foodYield  = CrisisEffectManager.Instance != null ? CrisisEffectManager.Instance.FoodYieldMultiplier : 1f;

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                if (!IsOperational(kvp.Key)) continue;

                var data = kvp.Value;

                int required = data.workerRequired;
                int assigned = assign != null ? assign.GetAssigned(kvp.Key) : required;
                if (required > 0 && assigned == 0) continue; // ไม่มีคนประจำ → idle: ไม่จ่าย upkeep ไม่ผลิต
                float workerScale = required > 0 ? Mathf.Clamp01((float)assigned / required) : 1f;

                // แหล่งแร่ (ore node): เหล็ก (+Tritium เฉพาะโซน B) = โควตาวันนี้ × กำลังคน
                // ไม่มี upkeep/ระดับ/foodYield (ภูมิประเทศ ไม่ใช่อาคาร)
                // ไม่แตะ runE/runW → gate ของอาคารถัดไปเหมือนเดิมทุกประการ
                if (data.isOreNode)
                {
                    var ore = OreDepositManager.Instance;
                    float quota = ore != null ? ore.GetDailyQuota(kvp.Key) : 0f;
                    float tritQuota = ore != null ? ore.GetDailyTritiumQuota(kvp.Key) : 0f;
                    float oreScale = workerScale * busyFactor * efficiency * dayFraction;
                    delta.iron += quota * oreScale;
                    delta.tritium += tritQuota * oreScale;
                    continue;
                }

                // ค่าเดินระบบต่อ tick = ต่อวัน × dayFraction · ต้องมีในคลังก่อนจึงเดินเครื่อง (gate แบบลูปเดิม)
                float upkeepE = data.energyConsumption * dayFraction;
                float upkeepW = data.waterConsumption * dayFraction;
                if (runE < upkeepE || runW < upkeepW) continue;

                runE -= upkeepE; runW -= upkeepW;
                delta.energy -= upkeepE; delta.water -= upkeepW;

                int level = BuildingRegistry.Instance.GetLevel(kvp.Key);
                float lvlMul = LevelProductionMultiplier[Mathf.Clamp(level - 1, 0, LevelProductionMultiplier.Length - 1)];
                float scale = workerScale * busyFactor * lvlMul * efficiency * dayFraction;

                float prodE = data.energyProduction * scale;
                float prodW = data.waterProduction * scale;
                runE += prodE; runW += prodW; // อาคารถัดไปเห็นไฟ/น้ำที่เพิ่งผลิต (เหมือนลูป batch เดิม)

                delta.food   += data.foodProduction * scale * foodYield;
                delta.water  += prodW;
                delta.energy += prodE;
                delta.iron   += data.ironProduction * scale;
                delta.knowledge += data.knowledgeProduction * scale; // Research Lab (V4 §6) — clamp ที่ maxKnowledge ใน ApplyDelta

                // เชื้อเพลิงฟิวชันเฉพาะระดับสูงสุด (Water L3 → Deuterium, Zone B/Lab L3 → Tritium)
                if (level >= BuildingRegistry.Instance.maxBuildingLevel)
                {
                    delta.deuterium += data.deuteriumProduction * workerScale * busyFactor * dayFraction;
                    delta.tritium   += data.tritiumProduction * workerScale * busyFactor * dayFraction;
                }
            }

            return delta;
        }

        /// <summary>บวกเดลตาเข้า Current + clamp ทุกชนิด แล้ว broadcast · คืนค่าที่ "ลงจริง" (post-clamp) ต่อชนิด</summary>
        private ResourceData ApplyDelta(ResourceData delta)
        {
            var before = Current;
            var c = Current;
            c.energy    = Mathf.Clamp(c.energy    + delta.energy,    0f, maxEnergy);
            c.water     = Mathf.Clamp(c.water     + delta.water,     0f, maxWater);
            c.food      = Mathf.Clamp(c.food      + delta.food,      0f, maxFood);
            c.iron      = Mathf.Clamp(c.iron      + delta.iron,      0f, maxIron);
            c.deuterium = Mathf.Clamp(c.deuterium + delta.deuterium, 0f, maxDeuterium);
            c.tritium   = Mathf.Clamp(c.tritium   + delta.tritium,   0f, maxTritium);
            c.knowledge = Mathf.Clamp(c.knowledge + delta.knowledge, 0f, maxKnowledge);
            Current = c;

            EventManager.Instance.RaiseResourceChanged(Current);
            CheckThresholds();
            return Subtract(c, before);
        }

        private static ResourceData Add(ResourceData a, ResourceData b) => new ResourceData
        {
            energy = a.energy + b.energy, water = a.water + b.water, food = a.food + b.food,
            iron = a.iron + b.iron, deuterium = a.deuterium + b.deuterium,
            tritium = a.tritium + b.tritium, knowledge = a.knowledge + b.knowledge,
        };

        private static ResourceData Subtract(ResourceData a, ResourceData b) => new ResourceData
        {
            energy = a.energy - b.energy, water = a.water - b.water, food = a.food - b.food,
            iron = a.iron - b.iron, deuterium = a.deuterium - b.deuterium,
            tritium = a.tritium - b.tritium, knowledge = a.knowledge - b.knowledge,
        };

        /// <summary>
        /// การบริโภคของประชากรต่อวัน (V4 §4): Food/Water คนละ 2 ต่อวัน (ทุกคลาส)
        /// คงพฤติกรรมเดิม (เทส/เรียกตรง) — คลังไม่พอ → clamp 0 แล้ว CheckThresholds ยิง OnResourceDepleted
        /// </summary>
        public void ApplyDailyConsumption() => ApplyDelta(ComputeConsumptionDelta(1f));

        /// <summary>เดลตาบริโภค Food/Water = −(2/คน × ประชากร) × dayFraction — ไม่แตะ Current ไม่ clamp</summary>
        private ResourceData ComputeConsumptionDelta(float dayFraction)
        {
            int population = PopulationManager.Instance != null
                ? PopulationManager.Instance.Current.total
                : 0;
            var delta = new ResourceData();
            if (population <= 0) return delta;
            delta.food  = -consumeFoodPerPerson  * population * dayFraction;
            delta.water = -consumeWaterPerPerson * population * dayFraction;
            return delta;
        }

        /// <summary>
        /// อาหารเน่าต่อวัน (Story Guide §4 วิกฤตอาหาร — "เน่าเพราะรังสีปนเปื้อน")
        /// อัตราเน่ามาจาก CrisisEffectManager.FoodSpoilRatePerDay (ปกติ 0 · ทางเลือก B ฉายรังสี→หยุดเน่า)
        /// เดินระหว่างผลิตกับบริโภค (produce → spoil → consume) · ไม่มี manager/rate 0 = ไม่ทำอะไร
        /// </summary>
        public void ApplyDailySpoilage()
        {
            float rate = CrisisEffectManager.Instance != null ? CrisisEffectManager.Instance.FoodSpoilRatePerDay : 0f;
            if (rate <= 0f) return;

            var c = Current;
            c.food = CrisisEffectMath.Spoil(c.food, rate);
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
            CheckResource(Current.energy, criticalEnergy, ResourceType.Energy);
            CheckResource(Current.water, criticalWater, ResourceType.Water);
            CheckResource(Current.food, criticalFood, ResourceType.Food);
            CheckResource(Current.iron, criticalIron, ResourceType.Iron);
            // Deuterium/Tritium/Knowledge เริ่มที่ 0 โดยตั้งใจ — ไม่ alert depletion
        }

        private void CheckResource(float amount, float threshold, ResourceType type)
        {
            if (amount <= 0f)
                EventManager.Instance.RaiseResourceDepleted(type);
            else if (amount < threshold)
                EventManager.Instance.RaiseResourceCritical(type);
        }
    }
}
