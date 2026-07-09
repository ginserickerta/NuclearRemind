using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §5 — assignment แยกคลาส (WorkerAssignmentManager class-aware):
    /// อาคารดึงคนจาก idle pool ของ requiredClass เท่านั้น · pool แต่ละคลาสอิสระต่อกัน ·
    /// evict เมื่อคนคลาสหนึ่งลดต้องไม่เตะคลาสอื่น · save/load clamp remaining ต่อคลาส
    /// (bootstrap เริ่มเกม: workers 6 · farmers 2 · engineers 2 · medics 0)
    /// </summary>
    public class WorkerAssignmentTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private PopulationManager population;
        private WorkerAssignmentManager wam;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
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

        private BuildingData NewBuilding(string name, int workerRequired, WorkerClass cls)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = name;
            b.size = new Vector2Int(1, 1);
            b.workerRequired = workerRequired;
            b.requiredClass = cls;
            _spawned.Add(b);
            return b;
        }

        private void Place(int col, int row, BuildingData b) =>
            eventManager.RaiseBuildingPlaced(new Cell(col, row), b);

        private void Assign(int col, int row, int delta) =>
            eventManager.RaiseWorkerAssignRequested(new Vector2Int(col, row), delta);

        // ── pool แยกตามคลาส ──────────────────────────────────────
        [Test]
        public void WorkerBuilding_DrawsFromWorkerPool()
        {
            Place(1, 1, NewBuilding("Plant", 3, WorkerClass.Worker));
            Assign(1, 1, 3);

            Assert.AreEqual(3, wam.GetAssigned(new Vector2Int(1, 1)));
            Assert.AreEqual(3, wam.IdleOfClass(WorkerClass.Worker), "worker idle 6 − 3 = 3");
            Assert.AreEqual(2, wam.IdleOfClass(WorkerClass.Engineer), "pool engineer ไม่ถูกแตะ");
        }

        [Test]
        public void EngineerBuilding_DrawsFromEngineerPool_IndependentOfWorkers()
        {
            Place(1, 1, NewBuilding("Lab", 2, WorkerClass.Engineer));
            Assign(1, 1, 2);

            Assert.AreEqual(2, wam.GetAssigned(new Vector2Int(1, 1)));
            Assert.AreEqual(0, wam.IdleOfClass(WorkerClass.Engineer), "engineer idle 2 − 2 = 0");
            Assert.AreEqual(6, wam.IdleOfClass(WorkerClass.Worker), "worker pool ยังเต็ม 6");
        }

        [Test]
        public void CannotAssignBeyondClassIdle()
        {
            // Lab ต้องการ 3 แต่มี Engineer แค่ 2 → assign ได้แค่ 2
            Place(1, 1, NewBuilding("Lab", 3, WorkerClass.Engineer));
            Assign(1, 1, 3);

            Assert.AreEqual(2, wam.GetAssigned(new Vector2Int(1, 1)), "จำกัดที่ engineer idle (2) ไม่ใช่ required (3)");
        }

        [Test]
        public void MixedBuildings_PoolsCoexist()
        {
            Place(1, 1, NewBuilding("Plant", 4, WorkerClass.Worker));
            Place(2, 2, NewBuilding("Lab", 2, WorkerClass.Engineer));
            Place(3, 3, NewBuilding("Farm", 2, WorkerClass.Farmer));
            Assign(1, 1, 4);
            Assign(2, 2, 2);
            Assign(3, 3, 2);

            Assert.AreEqual(4, wam.AssignedOfClass(WorkerClass.Worker));
            Assert.AreEqual(2, wam.AssignedOfClass(WorkerClass.Engineer));
            Assert.AreEqual(2, wam.AssignedOfClass(WorkerClass.Farmer));
            Assert.AreEqual(2, wam.IdleOfClass(WorkerClass.Worker), "6 − 4 = 2");
            Assert.AreEqual(0, wam.IdleOfClass(WorkerClass.Engineer));
            Assert.AreEqual(0, wam.IdleOfClass(WorkerClass.Farmer));
        }

        // ── evict แยกคลาส ────────────────────────────────────────
        [Test]
        public void Eviction_IsClassScoped()
        {
            Place(1, 1, NewBuilding("Lab", 2, WorkerClass.Engineer));
            Place(2, 2, NewBuilding("Plant", 3, WorkerClass.Worker));
            Assign(1, 1, 2);
            Assign(2, 2, 3);

            // เสีย Engineer ไป 1 (engineers 2 → 1) — เช่นตายหรือ reconcile
            eventManager.RaisePopulationChanged(new PopulationData
            {
                workers = 6, engineers = 1, medics = 0, farmers = 2, hope = 100f, shelterCap = 10,
            });

            Assert.AreEqual(1, wam.GetAssigned(new Vector2Int(1, 1)), "Lab ถูกเตะ Engineer ออก 1 (2→1)");
            Assert.AreEqual(3, wam.GetAssigned(new Vector2Int(2, 2)), "อาคาร Worker ไม่ถูกแตะ");
        }

        // ── save/load clamp ต่อคลาส ───────────────────────────────
        [Test]
        public void SaveLoad_ClampsPerClass()
        {
            var lab = NewBuilding("Lab", 2, WorkerClass.Engineer);
            var plant = NewBuilding("Plant", 3, WorkerClass.Worker);
            registry.allBuildingData = new[] { lab, plant };

            // เซฟ over-assign: Lab สองหลังรวม 4 Engineer แต่มีแค่ 2 · Plant 3 Worker (พอ)
            var save = new SaveData
            {
                // ต้องตั้ง population ด้วย — PopulationManager.HandleSaveLoaded รันก่อน WAM (subscription order)
                // ถ้าปล่อย default (0 ทุกคลาส) ClassCount จะเป็น 0 แล้ว clamp assignment เหลือ 0 หมด
                population = new PopulationData
                    { workers = 6, engineers = 2, farmers = 2, hope = 100f, shelterCap = 10 },
                placedBuildings = new List<Vector2Int>
                    { new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3) },
                buildingTypes = new List<string> { "Lab", "Lab", "Plant" },
                workerAssignmentCells = new List<Vector2Int>
                    { new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3) },
                workerAssignmentCounts = new List<int> { 2, 2, 3 },
            };
            eventManager.RaiseSaveLoaded(save);

            Assert.AreEqual(2, wam.GetAssigned(new Vector2Int(1, 1)), "Lab หลังแรกได้ 2 (engineer idle เต็ม)");
            Assert.AreEqual(0, wam.GetAssigned(new Vector2Int(2, 2)), "Lab หลังสองได้ 0 (engineer หมดแล้ว)");
            Assert.AreEqual(3, wam.GetAssigned(new Vector2Int(3, 3)), "Plant ได้ 3 (worker pool อิสระ ไม่โดน clamp ของ engineer)");
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
