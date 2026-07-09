using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// โรงพยาบาล (GDD §6): รักษาคนป่วยเพิ่ม + ลดรังสี — เฉพาะเมื่อ Medic ประจำครบ
    ///   • heal/วัน = medics + โรงพยาบาลประจำครบ × hospitalHealPerDay (PopulationManager)
    ///   • mitigation นับเฉพาะโรงพยาบาลที่ GetAssigned ≥ workerRequired (RadiationManager.HandleDayEnded)
    /// </summary>
    public class HospitalTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private BuildingRegistry registry;
        private PopulationManager population;
        private WorkerAssignmentManager wam;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            population = NewComponent<PopulationManager>("PopulationManager");
            wam = NewComponent<WorkerAssignmentManager>("WorkerAssignmentManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private BuildingData NewHospital(int workerRequired = 2)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "โรงพยาบาล";
            b.size = new Vector2Int(1, 1);
            b.buildingType = BuildingType.Hospital;
            b.workerRequired = workerRequired;
            b.requiredClass = WorkerClass.Medic;
            _spawned.Add(b);
            return b;
        }

        /// <summary>ตั้งประชากร + คนป่วยผ่าน save (มี Medic ตามระบุ · อาหารพอไม่ block การรักษา)</summary>
        private void InjectPop(int medics, int sick)
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = 100f, water = 100f, food = 100f },
                population = new PopulationData
                {
                    workers = 4, medics = medics, hope = 100f, shelterCap = 20, sick = sick,
                },
            };
            eventManager.RaiseSaveLoaded(save);
        }

        // ── heal (PopulationManager.HandleDayEnded) ──────────────────

        [Test]
        public void StaffedHospital_HealsExtraSick()
        {
            InjectPop(medics: 2, sick: 6);
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewHospital(workerRequired: 2));
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(1, 1), 2); // Medic ประจำครบ

            eventManager.RaiseDayEnded(2);

            // heal = medics 2 + hospital 1 × hospitalHealPerDay 2 = 4 → sick 6 − 4 = 2
            Assert.AreEqual(2, population.Current.sick, "โรงพยาบาลประจำครบ → รักษาเพิ่ม 2/วัน");
        }

        [Test]
        public void UnstaffedHospital_NoExtraHeal()
        {
            InjectPop(medics: 2, sick: 6);
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewHospital(workerRequired: 2));
            // ไม่ assign ใคร — โรงพยาบาลว่าง

            eventManager.RaiseDayEnded(2);

            // heal = medics 2 เท่านั้น → sick 6 − 2 = 4
            Assert.AreEqual(4, population.Current.sick, "โรงพยาบาลไม่มี Medic ประจำ → ไม่ได้โบนัสรักษา");
        }

        [Test]
        public void PartiallyStaffedHospital_NoExtraHeal()
        {
            InjectPop(medics: 2, sick: 6);
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewHospital(workerRequired: 2));
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(1, 1), 1); // ประจำแค่ 1/2

            eventManager.RaiseDayEnded(2);

            Assert.AreEqual(4, population.Current.sick, "ประจำไม่ครบ (1/2) → ไม่นับเป็นโรงพยาบาลทำงาน");
        }

        // ── mitigation (RadiationManager.HandleDayEnded — นับเฉพาะประจำครบ) ──

        [Test]
        public void HospitalMitigation_OnlyWhenStaffed()
        {
            var radiation = NewComponent<RadiationManager>("RadiationManager");
            radiation.medicMitigationPerDay = 0f; // ตัดผล Medic ออก — วัดผลโรงพยาบาลล้วน

            // เตาเดินเครื่อง (isUnlocked) → แหล่งรังสี = exposurePerReactorDay 2/วัน
            var save = new SaveData
            {
                resources = new ResourceData { energy = 100f, water = 100f, food = 100f },
                population = new PopulationData { workers = 4, medics = 2, hope = 100f, shelterCap = 20 },
                tower = new TowerData { isUnlocked = true },
            };
            eventManager.RaiseSaveLoaded(save);
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewHospital(workerRequired: 2));

            // ยังไม่ประจำ → mitigation 0 → exposure +2
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(2f, radiation.CurrentExposure, 0.01f, "โรงพยาบาลว่าง → ไม่ลดรังสี");

            // ประจำครบ → mitigation 6 > แหล่ง 2 → วันถัดไปไม่สะสมเพิ่ม
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(1, 1), 2);
            eventManager.RaiseDayEnded(3);
            Assert.AreEqual(2f, radiation.CurrentExposure, 0.01f, "ประจำครบ → รังสีรายวันโดนกดเหลือ 0 (ค้างที่ 2)");
        }

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
    }
}
