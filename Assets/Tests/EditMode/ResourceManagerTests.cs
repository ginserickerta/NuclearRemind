using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// A1 — ongoing consumption: อาคารต้องจ่ายต้นทุนเดินระบบ (energy/water) ต่อ tick
    /// ก่อนจึงจะผลิต ถ้าคลังไม่พอ → อาคารหยุด (skip production) และไม่กิน resource
    /// </summary>
    public class ResourceManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private ResourceManager resources;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            resources = NewComponent<ResourceManager>("ResourceManager");
            InvokePrivate(resources, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        /// <summary>ตั้งค่าคลังเริ่มต้นให้รู้ค่าแน่นอนผ่าน path เดียวกับ load เกม</summary>
        private void SetResources(float energy, float water, float food = 0f, int workers = 20)
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = energy, water = water, food = food, workers = workers }
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
        public void Tick_StorageSufficient_DeductsConsumptionAndProduces()
        {
            // Water Plant L1: เดินระบบ 10⚡ → ผลิต 50💧
            SetResources(energy: 100f, water: 80f);
            Place(MakeBuilding("WaterPlant", energyConsumption: 10f, waterConsumption: 0f, waterProduction: 50f), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(90f, resources.Current.energy, 1e-4f, "ต้องหัก energy 10 (เดินระบบ)");
            Assert.AreEqual(130f, resources.Current.water, 1e-4f, "ต้องผลิต water +50");
        }

        [Test]
        public void Tick_StorageInsufficient_SkipsProductionAndConsumesNothing()
        {
            // คลังไฟ 5 < เดินระบบ 10 → อาคารหยุด ไม่ผลิต ไม่กิน
            SetResources(energy: 5f, water: 80f);
            Place(MakeBuilding("WaterPlant", energyConsumption: 10f, waterConsumption: 0f, waterProduction: 50f), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(5f, resources.Current.energy, 1e-4f, "ไฟไม่พอ → ต้องไม่ถูกหัก");
            Assert.AreEqual(80f, resources.Current.water, 1e-4f, "ไฟไม่พอ → อาคารหยุด ไม่ผลิต water");
        }

        [Test]
        public void Tick_WaterConsumptionInsufficient_AlsoSkips()
        {
            // Power Plant L1: เดินระบบ 2💧 → ผลิต 60⚡ ; แต่คลังน้ำ 1 < 2 → หยุด
            SetResources(energy: 30f, water: 1f);
            Place(MakeBuilding("PowerPlant", energyConsumption: 0f, waterConsumption: 2f, energyProduction: 60f), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(30f, resources.Current.energy, 1e-4f, "น้ำไม่พอ → ไม่ผลิต energy");
            Assert.AreEqual(1f, resources.Current.water, 1e-4f, "น้ำไม่พอ → ไม่ถูกหัก");
        }

        [Test]
        public void Tick_ZeroConsumptionBuilding_ProducesNormally()
        {
            // Food Plant ที่ไม่มี consumption (ค่า default 0) ต้องผลิตได้ตามปกติ
            SetResources(energy: 0f, water: 0f);
            Place(MakeBuilding("FoodPlant", energyConsumption: 0f, waterConsumption: 0f, foodProduction: 40f), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "ไม่มี consumption → ผลิต food ได้แม้คลังว่าง");
        }

        // ---- A2: worker-per-building (reserve pool + proportional) ----

        [Test]
        public void Placement_DoesNotConsumeWorkers()
        {
            // workers เป็น reserve pool — วางอาคารต้องไม่หัก workers
            SetResources(energy: 100f, water: 100f, workers: 10);
            var b = MakeBuilding("Mine", energyConsumption: 0f, waterConsumption: 0f, workerRequired: 4);

            eventManager.RaiseBuildingPlaced(new Cell(1, 1), b);

            Assert.AreEqual(10, resources.Current.workers, "วางอาคารต้องไม่ลด workers (reserve ไม่ใช่ consume)");
        }

        [Test]
        public void Tick_WorkersSufficient_FullProduction()
        {
            // คน 20 ≥ ต้องการ 5 → workerScale = 1 → ผลิตเต็ม
            SetResources(energy: 100f, water: 100f, workers: 20);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "คนพอ → ผลิตเต็ม 40");
        }

        [Test]
        public void Tick_WorkersHalfOfDemand_ProductionScaledProportionally()
        {
            // คน 5, สองอาคารต้องการรวม 10 → workerScale = 0.5
            // สองอาคาร foodProduction 40 → เต็ม 80, ปรับสัดส่วน = 40
            SetResources(energy: 100f, water: 100f, workers: 5);
            Place(MakeBuilding("FoodPlantA", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);
            Place(MakeBuilding("FoodPlantB", 0f, 0f, foodProduction: 40f, workerRequired: 5), 2, 2);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "คนได้ครึ่ง → ผลิตครึ่ง (80 × 0.5)");
        }

        [Test]
        public void Tick_ZeroWorkers_NoProduction()
        {
            // ไม่มีคนเลย → workerScale = 0 → ไม่ผลิต (แต่ consumption 0 จึงไม่หยุดด้วย gate A1)
            SetResources(energy: 100f, water: 100f, workers: 0);
            Place(MakeBuilding("FoodPlant", 0f, 0f, foodProduction: 40f, workerRequired: 5), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(0f, resources.Current.food, 1e-4f, "ไม่มีคน → ผลิต 0");
        }

        [Test]
        public void Tick_NoWorkerDemand_ScaleIsOne()
        {
            // ทุกอาคาร workerRequired = 0 → totalNeeded = 0 → workerScale = 1 (กัน div-by-zero)
            SetResources(energy: 100f, water: 100f, workers: 0);
            Place(MakeBuilding("AutoPlant", 0f, 0f, foodProduction: 40f, workerRequired: 0), 1, 1);

            InvokePrivate(resources, "Tick");

            Assert.AreEqual(40f, resources.Current.food, 1e-4f, "ไม่มี demand → ผลิตเต็มแม้คน 0");
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
