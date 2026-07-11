using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// อาคารต้องมีคนงานมาสร้างก่อนถึงเริ่มก่อสร้าง (V4 §5) —
    /// ConstructionController (ไม่มีคน = ไม่คืบหน้า) + WorkerAssignmentManager (เพดานผู้สร้าง ≥ 1 · คืนคนตอนเสร็จ)
    /// ครอบ Habitat (workerRequired=0) ที่เดิมจัดคนไม่ได้เลย → ต้องสร้างไม่ได้ ถ้าไม่มีเพดานผู้สร้าง
    /// </summary>
    public class ConstructionWorkerGateTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private BuildingRegistry registry;
        private WorkerAssignmentManager wam;
        private ConstructionController construction;
        private BuildingData habitat;   // workerRequired = 0 (เดินเครื่องไม่ต้องใช้คน)
        private BuildingData farm;       // workerRequired = 1 (ต้องใช้คนประจำ)

        private static readonly Vector2Int Cell = new Vector2Int(5, 5);

        [SetUp]
        public void SetUp()
        {
            NewComponent<EventManager>("EventManager");        // ต้องมาก่อน (subscribe ตอน AddComponent)
            NewComponent<GridManager>("GridManager");
            NewComponent<PopulationManager>("PopulationManager"); // default: workers 6 (มี idle ให้ assign)
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            wam = NewComponent<WorkerAssignmentManager>("WorkerAssignmentManager");
            construction = NewComponent<ConstructionController>("ConstructionController");

            habitat = MakeBuilding("Habitat", BuildingType.Habitat, workerRequired: 0, buildTicks: 3);
            farm = MakeBuilding("Farm", BuildingType.Farm, workerRequired: 1, buildTicks: 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned) Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        // ── ไม่มีคนงาน = ไม่คืบหน้า ──

        [Test]
        public void NoWorker_Placed_DoesNotProgress()
        {
            Place(habitat);
            for (int i = 0; i < 10; i++) Tick();

            Assert.IsTrue(construction.IsUnderConstruction(Cell), "ไม่มีคนงาน — ต้องค้างสร้าง ไม่เสร็จเอง");
            Assert.AreEqual(0, construction.GetProgress(Cell), "progress ต้องไม่ขยับเลย");
        }

        [Test]
        public void Farm_NoWorker_DoesNotProgress()
        {
            Place(farm);
            for (int i = 0; i < 10; i++) Tick();
            Assert.IsTrue(construction.IsUnderConstruction(Cell), "อาคารที่ต้องใช้คน ก็ต้องรอคนสร้างเช่นกัน");
        }

        // ── มีคนงาน = เริ่มสร้าง แล้วเสร็จ ──

        [Test]
        public void AssignWorker_ThenProgresses_AndCompletes()
        {
            Place(habitat);
            AssignPlus(); // จัดผู้สร้าง 1 คน
            Assert.AreEqual(1, wam.GetAssigned(Cell), "Habitat ต้องรับผู้สร้างได้ 1 แม้ workerRequired=0");

            for (int i = 0; i < 3; i++) Tick(); // buildTicks 3 · speed 1 → เสร็จใน 3 tick
            Assert.IsFalse(construction.IsUnderConstruction(Cell), "มีคนสร้างแล้วต้องสร้างเสร็จ");
        }

        // ── เพดานผู้สร้าง ──

        [Test]
        public void EffectiveCap_UnderConstruction_IsAtLeastOne_EvenForZeroStaffBuilding()
        {
            Place(habitat);
            Assert.AreEqual(1, wam.EffectiveCap(Cell, habitat), "ระหว่างสร้าง Habitat เพดาน = 1 (ผู้สร้าง)");
        }

        [Test]
        public void EffectiveCap_NotUnderConstruction_FallsBackToWorkerRequired()
        {
            // cell ที่ไม่เคยวาง = ไม่ได้สร้าง → เพดาน = workerRequired ปกติ (0 สำหรับ Habitat)
            Assert.AreEqual(0, wam.EffectiveCap(Cell, habitat));
            Assert.AreEqual(1, wam.EffectiveCap(Cell, farm));
        }

        // ── คืนผู้สร้างตอนสร้างเสร็จ ──

        [Test]
        public void ZeroStaffBuilding_ReleasesBuilder_OnComplete()
        {
            Place(habitat);
            AssignPlus();
            Assert.AreEqual(1, wam.GetAssigned(Cell));

            for (int i = 0; i < 3; i++) Tick();

            Assert.IsFalse(construction.IsUnderConstruction(Cell));
            Assert.AreEqual(0, wam.GetAssigned(Cell), "Habitat เดินเครื่องไม่ต้องใช้คน — ผู้สร้างต้องถูกปล่อยกลับ idle");
        }

        [Test]
        public void StaffedBuilding_KeepsWorker_OnComplete()
        {
            Place(farm);
            AssignPlus();
            Assert.AreEqual(1, wam.GetAssigned(Cell));

            for (int i = 0; i < 3; i++) Tick();

            Assert.IsFalse(construction.IsUnderConstruction(Cell));
            Assert.AreEqual(1, wam.GetAssigned(Cell), "Farm ต้องใช้คนเดินเครื่อง — คงคนประจำไว้หลังสร้างเสร็จ");
        }

        // ── helpers ──

        private void Place(BuildingData data)
        {
            var cell = GridManager.Instance.GetCell(Cell.x, Cell.y);
            cell.isOccupied = true;
            cell.buildingType = data.buildingType;
            EventManager.Instance.RaiseBuildingPlaced(cell, data); // → registry + construction queue
        }

        private void Tick() => EventManager.Instance.RaiseGameTick();
        private void AssignPlus() => EventManager.Instance.RaiseWorkerAssignRequested(Cell, +1);

        private BuildingData MakeBuilding(string name, BuildingType type, int workerRequired, int buildTicks)
        {
            var d = ScriptableObject.CreateInstance<BuildingData>();
            d.buildingName = name;
            d.buildingType = type;
            d.workerRequired = workerRequired;
            d.buildTicks = buildTicks;
            d.requiredClass = WorkerClass.Worker; // ดึงผู้สร้างจาก pool คนงานพื้นฐาน (default 6 คน)
            d.size = Vector2Int.one;
            _spawned.Add(d);
            return d;
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }
    }
}
