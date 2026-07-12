using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ผลิต+บริโภคแบบ batch จบวัน (5b — V4 §3/§4/§6):
    /// - อาคารต้องจ่ายค่าเดินระบบ (energy/water) ของวันก่อนจึงจะผลิต ถ้าคลังไม่พอ → หยุด (ไม่ผลิต ไม่กิน)
    /// - ประชากรบริโภค Food/Water คนละ 2 ต่อวัน ตอน OnDayProduction (หลังผลิต)
    /// </summary>
    public class ResourceManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private ResourceManager resources;
        private PopulationManager population;
        private WorkerAssignmentManager assignment;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            resources = NewComponent<ResourceManager>("ResourceManager");
            // V4: จำนวนคนงานสำหรับ worker-scaling อยู่ที่ PopulationManager (ไม่ใช่ ResourceData)
            population = NewComponent<PopulationManager>("PopulationManager");
            // V4 §5: จัดสรร Worker ประจำอาคารรายหลัง — ผลผลิต = assigned/workerRequired
            assignment = NewComponent<WorkerAssignmentManager>("WorkerAssignmentManager");
            InvokePrivate(resources, "Start");
        }

        /// <summary>จัดคน n คนไปประจำอาคารที่ (col,row) — ต้องเรียกหลัง Place และหลังตั้งจำนวน Worker</summary>
        private void Assign(int col, int row, int n)
        {
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(col, row), n);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        /// <summary>ตั้งค่าคลัง + จำนวน Worker ผ่าน path เดียวกับ load เกม (worker-scaling ใช้คลาส Worker)</summary>
        private void SetResources(float energy, float water, float food = 0f, int workers = 20)
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = energy, water = water, food = food },
                population = new PopulationData { workers = workers, hope = 100f }
            };
            eventManager.RaiseSaveLoaded(save);
        }

        private BuildingData MakeBuilding(string name, float energyConsumption, float waterConsumption,
            float waterProduction = 0f, float energyProduction = 0f, float foodProduction = 0f,
            int workerRequired = 0)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.buildingName = name;
            data.size = new Vector2Int(1, 1);
            data.energyConsumption = energyConsumption;
            data.waterConsumption = waterConsumption;
            data.waterProduction = waterProduction;
            data.energyProduction = energyProduction;
            data.foodProduction = foodProduction;
            data.workerRequired = workerRequired;
            _spawned.Add(data);
            return data;
        }

        private void Place(BuildingData data, int col, int row)
        {
            eventManager.RaiseBuildingPlaced(new Cell(col, row), data);
        }

        [Test]
        public void DailyProduction_StorageSufficient_DeductsConsumptionAndProduces()
        {
            // Water Plant L1: เดินระบบ 10⚡ → ผลิต 50💧
            SetResources(energy: 100f, water: 80f);
            Place(MakeBuilding("WaterPlant", energyConsumption: 10f, waterConsumption: 0f, waterProduction: 50f), 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(90f, resources.Current.energy, 1e-4f, "ต้องหัก energy 10 (เดินระบบ)");
            Assert.AreEqual(130f, resources.Current.water, 1e-4f, "ต้องผลิต water +50");
        }

        [Test]
        public void DailyProduction_StorageInsufficient_SkipsProductionAndConsumesNothing()
        {
            // คลังไฟ 5 < เดินระบบ 10 → อาคารหยุด ไม่ผลิต ไม่กิน
            SetResources(energy: 5f, water: 80f);
            Place(MakeBuilding("WaterPlant", energyConsumption: 10f, waterConsumption: 0f, waterProduction: 50f), 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(5f, resources.Current.energy, 1e-4f, "ไฟไม่พอ → ต้องไม่ถูกหัก");
            Assert.AreEqual(80f, resources.Current.water, 1e-4f, "ไฟไม่พอ → อาคารหยุด ไม่ผลิต water");
        }

        [Test]
        public void DailyProduction_WaterConsumptionInsufficient_AlsoSkips()
        {
            // Power Plant L1: เดินระบบ 2💧 → ผลิต 60⚡ ; แต่คลังน้ำ 1 < 2 → หยุด
            SetResources(energy: 30f, water: 1f);
            Place(MakeBuilding("PowerPlant", energyConsumption: 0f, waterConsumption: 2f, energyProduction: 60f), 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(30f, resources.Current.energy, 1e-4f, "น้ำไม่พอ → ไม่ผลิต energy");
            Assert.AreEqual(1f, resources.Current.water, 1e-4f, "น้ำไม่พอ → ไม่ถูกหัก");
        }

        [Test]
        public void DailyProduction_ZeroConsumptionBuilding_ProducesNormally()
        {
            // Food Plant ที่ไม่มี consumption (ค่า default 0) ต้องผลิตได้ตามปกติ
            SetResources(energy: 0f, water: 0f);
            Place(MakeBuilding("FoodPlant", energyConsumption: 0f, waterConsumption: 0f, foodProduction: 40f), 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "ไม่มี consumption → ผลิต food ได้แม้คลังว่าง");
        }

        // ---- Research Lab (V4 §6): knowledgeProduction ----
        [Test]
        public void DailyProduction_KnowledgeBuilding_AddsKnowledge()
        {
            SetResources(energy: 100f, water: 100f);
            var lab = MakeBuilding("Lab", energyConsumption: 0f, waterConsumption: 0f);
            lab.knowledgeProduction = 2f;
            Place(lab, 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(2f, resources.Current.knowledge, 1e-4f, "Lab ผลิต Knowledge 2/วัน");
        }

        [Test]
        public void DailyProduction_Knowledge_ClampsAtMax()
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = 100f, water = 100f, knowledge = 99.5f },
                population = new PopulationData { workers = 20, hope = 100f }
            };
            eventManager.RaiseSaveLoaded(save);
            var lab = MakeBuilding("Lab", energyConsumption: 0f, waterConsumption: 0f);
            lab.knowledgeProduction = 2f;
            Place(lab, 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(resources.maxKnowledge, resources.Current.knowledge, 1e-4f,
                "Knowledge ตันที่เพดาน (100) ไม่ทะลุ");
        }

        // ---- A2: worker-per-building (reserve pool + proportional) ----

        [Test]
        public void Placement_DoesNotConsumeWorkers()
        {
            // workers เป็น reserve pool (อยู่ที่ PopulationManager) — วางอาคารต้องไม่หัก
            SetResources(energy: 100f, water: 100f, workers: 10);
            var b = MakeBuilding("Mine", energyConsumption: 0f, waterConsumption: 0f, workerRequired: 4);

            eventManager.RaiseBuildingPlaced(new Cell(1, 1), b);

            Assert.AreEqual(10, population.Current.total, "วางอาคารต้องไม่ลดจำนวนคน (reserve ไม่ใช่ consume)");
        }

        [Test]
        public void DailyProduction_FullyStaffed_FullProduction()
        {
            // อาคารต้องการ 5, จัดครบ 5 → workerScale = 1 → ผลิตเต็ม
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            Assign(1, 1, 5);

            resources.ApplyDailyProduction();

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "จัดคนครบ → ผลิตเต็ม 40");
        }

        [Test]
        public void DailyProduction_PartiallyStaffed_ProductionScaledPerBuilding()
        {
            // อาคารต้องการ 5, จัดแค่ 3 → workerScale = 0.6 → 40 × 0.6 = 24 (สเกลรายอาคาร)
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            Assign(1, 1, 3);

            resources.ApplyDailyProduction();

            Assert.AreEqual(24f, resources.Current.food, 1e-4f, "จัด 3/5 → ผลิต 60% (40 × 0.6)");
        }

        [Test]
        public void DailyProduction_Unstaffed_NoProduction()
        {
            // อาคารต้องการคนแต่ไม่จัดใครเลย → idle: ไม่ผลิต ไม่จ่าย upkeep
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            // ไม่ Assign

            resources.ApplyDailyProduction();

            Assert.AreEqual(0f, resources.Current.food, 1e-4f, "ไม่มีคนประจำ → ผลิต 0");
        }

        [Test]
        public void DailyProduction_NoWorkerRequired_AlwaysFull()
        {
            // อาคาร workerRequired = 0 (เช่น auto plant) → workerScale = 1 เสมอ แม้ไม่จัดคน
            SetResources(energy: 100f, water: 100f, workers: 0);
            Place(MakeBuilding("AutoPlant", 0f, 0f, foodProduction: 40f, workerRequired: 0), 1, 1);

            resources.ApplyDailyProduction();

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "ไม่ต้องใช้คน → ผลิตเต็มแม้คน 0");
        }

        // ---- เฟส 6: building level scaling + fuel ----

        [Test]
        public void DailyProduction_UpgradedToL2_TriplesProduction()
        {
            SetResources(energy: 100f, water: 100f, workers: 20);
            eventManager.RaiseResourceDelta(ResourceType.Iron, 100f); // แร่เหล็กสำหรับอัป
            Place(MakeBuilding("Food", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            Assign(1, 1, 5);
            eventManager.RaiseUpgradeBuildingRequested(new Vector2Int(1, 1)); // → L2

            resources.ApplyDailyProduction();

            Assert.AreEqual(120f, resources.Current.food, 1e-3f, "L2 → ผลิต ×3 (40×3)");
        }

        [Test]
        public void DailyProduction_UpgradedToL3_ProducesDeuterium()
        {
            // §4: โรงน้ำ L3 สกัด D โดยกินน้ำ 25:1 เหนือ reserve 100 → ต้องมีน้ำ ≥ 100 + (8×25=200) = 300 จึงสกัดครบ 8
            SetResources(energy: 100f, water: 400f, workers: 20);
            eventManager.RaiseResourceDelta(ResourceType.Iron, 500f);
            var water = MakeBuilding("Water", 0f, 0f, workerRequired: 1);
            water.deuteriumProduction = 8f;
            Place(water, 1, 1);
            Assign(1, 1, 1);
            eventManager.RaiseUpgradeBuildingRequested(new Vector2Int(1, 1)); // L2
            eventManager.RaiseUpgradeBuildingRequested(new Vector2Int(1, 1)); // L3

            resources.ApplyDailyProduction();

            Assert.AreEqual(8f, resources.Current.deuterium, 1e-3f, "L3 → ผลิต Deuterium ป้อนเตา");
        }

        // ---- 5b: การบริโภคของประชากร (V4 §4 — Food/Water 2/คน/วัน) ----

        [Test]
        public void DailyConsumption_DeductsFoodAndWaterPerPerson()
        {
            SetResources(energy: 100f, water: 100f, food: 150f, workers: 10);

            resources.ApplyDailyConsumption();

            Assert.AreEqual(130f, resources.Current.food, 1e-4f, "10 คน × 2 → หัก food 20");
            Assert.AreEqual(80f, resources.Current.water, 1e-4f, "10 คน × 2 → หัก water 20");
        }

        [Test]
        public void DailyConsumption_InsufficientFood_ClampsZeroAndRaisesDepleted()
        {
            SetResources(energy: 100f, water: 100f, food: 10f, workers: 10);
            bool foodDepleted = false;
            eventManager.OnResourceDepleted += t => { if (t == ResourceType.Food) foodDepleted = true; };

            resources.ApplyDailyConsumption();

            Assert.AreEqual(0f, resources.Current.food, 1e-4f, "อาหารไม่พอ → clamp 0 (ไม่ติดลบ)");
            Assert.IsTrue(foodDepleted, "ต้องยิง OnResourceDepleted(Food) ให้ PopulationManager หัก Hope ตอน OnDayEnded");
        }

        [Test]
        public void DayProduction_Event_ProducesThenConsumes()
        {
            // จบวัน (OnDayProduction): ผลิต batch ก่อน แล้วค่อยหักบริโภค — โรงอาหารผลิต 40, คน 10 กิน 20
            SetResources(energy: 100f, water: 100f, food: 100f, workers: 10);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            Assign(1, 1, 5);

            eventManager.RaiseDayProduction(2);

            Assert.AreEqual(120f, resources.Current.food, 1e-4f, "100 + 40 (ผลิต) − 20 (บริโภค 10 คน)");
            Assert.AreEqual(80f, resources.Current.water, 1e-4f, "หักบริโภคน้ำ 10 คน × 2");
        }

        // ---- critical alert (ค่าสัมบูรณ์ — แยกจาก cap 9999 ตาม V4 §4) ----

        [Test]
        public void CriticalAlert_UsesAbsoluteThreshold()
        {
            // criticalEnergy default 60 (พฤติกรรมเดิม: scene E300 × ratio 0.2)
            SetResources(energy: 100f, water: 100f, food: 200f, workers: 0);
            var criticals = new List<ResourceType>();
            eventManager.OnResourceCritical += t => criticals.Add(t);

            eventManager.RaiseResourceDelta(ResourceType.Energy, -45f); // 100 → 55 < 60
            Assert.Contains(ResourceType.Energy, criticals, "energy 55 < เกณฑ์ 60 → ต้องยิง critical");

            criticals.Clear();
            eventManager.RaiseResourceDelta(ResourceType.Energy, +20f); // 55 → 75 ≥ 60
            Assert.IsFalse(criticals.Contains(ResourceType.Energy), "energy 75 ≥ เกณฑ์ 60 → ต้องไม่ยิง critical");
        }

        [Test]
        public void Clamp_UsesGddCap9999()
        {
            // cap ใหม่ตาม GDD §4: เหล็กสะสมทะลุ 1000 เดิมได้ แต่ไม่เกิน 9999
            SetResources(energy: 100f, water: 100f, workers: 0);
            eventManager.RaiseResourceDelta(ResourceType.Iron, 5000f);
            Assert.AreEqual(5000f, resources.Current.iron, 1e-3f, "cap 9999 → 5000 ต้องไม่โดน clamp");

            eventManager.RaiseResourceDelta(ResourceType.Iron, 9000f);
            Assert.AreEqual(9999f, resources.Current.iron, 1e-3f, "เกิน 9999 → clamp ที่เพดาน GDD");
        }

        // ---- reflection helpers (เหมือน IntegrationFlowTests) ----

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (TargetInvocationException) { }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"ไม่พบ method '{methodName}' บน {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
