using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// คนงานต้องไม่ถูก "สลับตัว/รีเป้าหมาย" เมื่อมีเหตุการณ์ที่ไม่เกี่ยวข้องเกิดขึ้นที่อื่นในเมือง (V4 §5) —
    /// บั๊กเดิม: RebuildLayout เดินลิสต์คนงานทั้งคลาสใหม่ทุกครั้งที่มี event (จัดคน/ประชากรเปลี่ยน "ที่ไหนก็ได้")
    /// แล้วแมป list[idx] → อาคารจากลำดับ Dictionary ล้วน ๆ (ไม่มีตัวตนคงที่) → คนงานเดินตัดกัน/ซ้อนกันเรื่อย ๆ
    /// ตรวจว่า WorkerView (AssignedCell, Slot) เสถียรข้ามเหตุการณ์ที่ไม่เกี่ยวข้อง — นี่คือแก่นของการแก้
    /// </summary>
    public class WorkerVisualSpawnerStabilityTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private WorkerAssignmentManager wam;

        private static readonly Vector2Int CellA = new Vector2Int(2, 2);
        private static readonly Vector2Int CellB = new Vector2Int(8, 4);

        [SetUp]
        public void SetUp()
        {
            NewComponent<EventManager>("EventManager");
            NewComponent<GridManager>("GridManager"); // default 20×12
            var pop = NewComponent<PopulationManager>("PopulationManager"); // default: workers 6
            var registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            wam = NewComponent<WorkerAssignmentManager>("WorkerAssignmentManager");

            var spawnerGo = new GameObject("WorkerVisualSpawner");
            _spawned.Add(spawnerGo);
            var spawner = spawnerGo.AddComponent<WorkerVisualSpawner>();
            spawner.workerSprite = DummySprite();
            TryInvokePrivate(spawner, "OnEnable");

            // จัด layout เริ่มต้น (ปกติมาจาก Start() ซึ่ง EditMode AddComponent ไม่เรียกให้) —
            // ใช้ event จริงแทนการยิง private method ตรง ๆ (สอดคล้องแนวเทสต์อื่นในโปรเจกต์)
            EventManager.Instance.RaisePopulationChanged(pop.Current);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned) Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        [Test]
        public void UnrelatedAssignmentElsewhere_DoesNotReshuffle_ExistingWorkers()
        {
            Place(CellA, MakeBuilding("A", workerRequired: 2));
            Place(CellB, MakeBuilding("B", workerRequired: 2));
            AssignPlus(CellA);
            AssignPlus(CellA);

            var before = WorkersAt(CellA);
            Assert.AreEqual(2, before.Count);

            // เหตุการณ์ที่ไม่เกี่ยวกับ A เลย — เดิมบั๊กนี้จะทำให้คนที่ A ถูกคำนวณตำแหน่งใหม่/สลับตัว
            AssignPlus(CellB);

            var after = WorkersAt(CellA);
            CollectionAssert.AreEqual(before, after,
                "คนงานที่ A ต้องเป็นตัวเดิมชุดเดิม (reference เดิม) เมื่อ B เปลี่ยน ไม่เกี่ยวข้องกัน");
        }

        [Test]
        public void ReducingCount_EvictsHighestSlotOnly_KeepsLowerSlotsUntouched()
        {
            Place(CellA, MakeBuilding("A3", workerRequired: 3));
            AssignPlus(CellA);
            AssignPlus(CellA);
            AssignPlus(CellA);

            var before = WorkersAt(CellA); // เรียงตาม Slot แล้ว (0,1,2)
            Assert.AreEqual(3, before.Count);

            AssignMinus(CellA); // เหลือ 2 — ต้องอัปเดต assignment ผ่าน WorkerAssignmentManager จริง

            var after = WorkersAt(CellA);
            Assert.AreEqual(2, after.Count);
            Assert.AreSame(before[0], after[0], "slot 0 ต้องเป็นคนเดิม");
            Assert.AreSame(before[1], after[1], "slot 1 ต้องเป็นคนเดิม");
            Assert.IsFalse(before[2].AssignedCell.HasValue, "คนที่ถูกถอด (slot สูงสุด) ต้องกลับไป idle");
        }

        [Test]
        public void IdleWorker_KeepsSlot_AcrossUnrelatedAssignment()
        {
            var idleBefore = AllWorkers().Where(w => !w.AssignedCell.HasValue)
                                          .OrderBy(w => w.Slot).ToList();
            Assert.GreaterOrEqual(idleBefore.Count, 4, "ประชากรเริ่ม 6 คน ยังไม่ assign ใคร ต้อง idle เกือบหมด");
            var slotsBefore = idleBefore.Select(w => w.Slot).ToList();

            Place(CellA, MakeBuilding("A", workerRequired: 1));
            AssignPlus(CellA); // ดึงคน 1 คนออกจาก idle pool ไปประจำ A

            var stillIdle = idleBefore.Where(w => !w.AssignedCell.HasValue).ToList();
            foreach (var w in stillIdle)
                Assert.AreEqual(slotsBefore[idleBefore.IndexOf(w)], w.Slot,
                    "คนที่ยังว่างอยู่ต้องคง slot พักเดิม ไม่ถูกเบอร์ใหม่ทุกครั้งที่มีใครถูกจ้างไปที่อื่น");
        }

        [Test]
        public void FreedIdleSlot_IsReused_NotUnboundedGrowth()
        {
            Place(CellA, MakeBuilding("A", workerRequired: 1));
            int maxSlotBefore = AllWorkers().Where(w => !w.AssignedCell.HasValue).Max(w => w.Slot);

            AssignPlus(CellA);   // ดึง 1 คนจาก idle ไปประจำ (ปลด slot พักเดิมของเขา)
            AssignMinus(CellA);  // คืนคนกลับ idle — ควรได้ slot ว่างต่ำสุดกลับมา ไม่ใช่เลขใหม่ที่สูงขึ้นเรื่อย ๆ

            int maxSlotAfter = AllWorkers().Where(w => !w.AssignedCell.HasValue).Max(w => w.Slot);
            Assert.AreEqual(maxSlotBefore, maxSlotAfter,
                "หมุนคนเข้า-ออก idle รอบเดียวไม่ควรดัน slot สูงสุดขึ้น (ต้องรีไซเคิลเลข slot ที่ว่างแล้ว)");
        }

        // ── helpers ──

        private static List<WorkerView> AllWorkers() =>
            Object.FindObjectsByType<WorkerView>(FindObjectsSortMode.None).ToList();

        private static List<WorkerView> WorkersAt(Vector2Int cell) =>
            AllWorkers().Where(w => w.AssignedCell == cell).OrderBy(w => w.Slot).ToList();

        private void Place(Vector2Int cell, BuildingData data)
        {
            var c = GridManager.Instance.GetCell(cell.x, cell.y);
            c.isOccupied = true;
            c.buildingType = data.buildingType;
            EventManager.Instance.RaiseBuildingPlaced(c, data);
        }

        private void AssignPlus(Vector2Int cell) => EventManager.Instance.RaiseWorkerAssignRequested(cell, +1);
        private void AssignMinus(Vector2Int cell) => EventManager.Instance.RaiseWorkerAssignRequested(cell, -1);

        private BuildingData MakeBuilding(string name, int workerRequired)
        {
            var d = ScriptableObject.CreateInstance<BuildingData>();
            d.buildingName = name;
            d.buildingType = BuildingType.Habitat;
            d.workerRequired = workerRequired;
            d.requiredClass = WorkerClass.Worker;
            d.size = Vector2Int.one;
            _spawned.Add(d);
            return d;
        }

        private static Sprite DummySprite()
        {
            var tex = new Texture2D(1, 1);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero);
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            TryInvokePrivate(c, "Awake");
            TryInvokePrivate(c, "OnEnable");
            return c;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (System.Reflection.TargetInvocationException) { }
        }
    }
}
