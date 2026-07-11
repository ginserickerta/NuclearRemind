using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// กันคนงานเดินทะลุอาคาร (V4 §5) — WorkerPathing.Step (ไถลตามกำแพง)
    /// ตรวจ: ทางโล่ง · ชนแล้วไถลทีละแกน · มุมอับ · หนีออกจากอาคารได้ · ปัดช่องตรงกับ WorldToIso
    /// </summary>
    public class WorkerPathingTests
    {
        // ด่านจำลอง: ช่องที่ระบุ = เดินไม่ได้
        private static System.Func<Vector2Int, bool> Walls(params Vector2Int[] blocked)
        {
            var set = new HashSet<Vector2Int>(blocked);
            return c => set.Contains(c);
        }

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        [Test]
        public void CellOf_RoundsToNearest_LikeWorldToIso()
        {
            // ต้องปัดแบบเดียวกับ GridManager.WorldToIso — ไม่งั้นคนงานเช็คผิดช่องกับที่ตัวเองยืน
            Assert.AreEqual(C(3, 5), WorkerPathing.CellOf(new Vector2(2.6f, 5.4f)));
            Assert.AreEqual(C(0, 0), WorkerPathing.CellOf(new Vector2(0.49f, -0.49f)));
        }

        [Test]
        public void OpenPath_MovesStraightToTarget()
        {
            var to = new Vector2(4.2f, 3.1f);
            Assert.AreEqual(to, WorkerPathing.Step(new Vector2(4f, 3f), to, Walls()));
        }

        [Test]
        public void NullPredicate_NeverBlocks()
        {
            var to = new Vector2(9f, 9f);
            Assert.AreEqual(to, WorkerPathing.Step(Vector2.zero, to, null));
        }

        [Test]
        public void BlockedAhead_SlidesAlongColumnAxis()
        {
            // เดินเฉียงเข้าหาอาคารที่ (5,5) · ช่อง (5,4) โล่ง ⇒ ไถลตามแกน col (คงค่า row เดิม)
            var from = new Vector2(4.6f, 4.4f); // ช่อง (5,4)
            var to = new Vector2(4.8f, 4.6f);   // ช่อง (5,5) = อาคาร
            var result = WorkerPathing.Step(from, to, Walls(C(5, 5)));

            Assert.AreEqual(to.x, result.x, 1e-5f, "แกน col ต้องเดินต่อได้");
            Assert.AreEqual(from.y, result.y, 1e-5f, "แกน row ต้องถูกกั้นไว้");
            Assert.AreNotEqual(from, result, "ต้องขยับจริง ไม่ใช่แค่ยืนนิ่งแล้วผ่านเทสต์");
        }

        [Test]
        public void BlockedAhead_SlidesAlongRowAxis_WhenColumnAxisAlsoBlocked()
        {
            // ปลายทาง (5,5) และช่องไถลแกน col (5,4) ตันทั้งคู่ ⇒ เหลือทางไถลแกน row (4,5)
            var from = new Vector2(4.4f, 4.6f); // ช่อง (4,5)
            var to = new Vector2(4.6f, 4.8f);   // ช่อง (5,5)
            var result = WorkerPathing.Step(from, to, Walls(C(5, 5), C(5, 4)));

            Assert.AreEqual(from.x, result.x, 1e-5f, "แกน col ถูกกั้น");
            Assert.AreEqual(to.y, result.y, 1e-5f, "แกน row ต้องเดินต่อได้");
            Assert.AreNotEqual(from, result, "ต้องขยับจริง ไม่ใช่แค่ยืนนิ่งแล้วผ่านเทสต์");
        }

        [Test]
        public void DeadEndCorner_StaysPut()
        {
            // มุมอับ: ทั้งปลายทางและช่องไถลทั้งสองแกนตันหมด ⇒ ยืนนิ่ง ไม่จมเข้าอาคาร
            var from = new Vector2(4.4f, 4.4f); // ช่อง (4,4)
            var to = new Vector2(4.6f, 4.6f);   // ช่อง (5,5)
            var result = WorkerPathing.Step(from, to, Walls(C(5, 5), C(5, 4), C(4, 5)));

            Assert.AreEqual(from, result, "มุมอับต้องหยุด ไม่ทะลุ");
        }

        [Test]
        public void AlreadyInsideBuilding_CanWalkOut_NotTrappedForever()
        {
            // ★ กันคนงานติดในตึกถาวร: ถ้าถูกดัน/ตึกสร้างทับจนยืนในช่องต้องห้าม
            //   ต้องยอมให้ขยับ แม้ช่องปลายทางจะยังเป็นอาคารอยู่ (เดินหาทางออกเอง)
            var from = new Vector2(5f, 5f); // ยืนในอาคาร
            var to = new Vector2(5.2f, 5f);
            var result = WorkerPathing.Step(from, to, Walls(C(5, 5)));

            Assert.AreEqual(to, result, "อยู่ในอาคารแล้วต้องเดินออกได้ ไม่ใช่ค้างตลอดกาล");
        }

        [Test]
        public void InsideBuilding_ReachingFreeCell_Works()
        {
            var from = new Vector2(5f, 5f);   // ในอาคาร (5,5)
            var to = new Vector2(5.6f, 5f);   // ออกไปช่อง (6,5) ที่โล่ง
            Assert.AreEqual(to, WorkerPathing.Step(from, to, Walls(C(5, 5))));
        }

        [Test]
        public void WalkingAlongWall_KeepsMoving_DoesNotStall()
        {
            // เดินขนานกำแพงแถว row=5 ทั้งแถว — ต้องเลื่อนไปตามแกน col ได้เรื่อย ๆ
            var walls = Walls(C(3, 5), C(4, 5), C(5, 5), C(6, 5));
            var from = new Vector2(4f, 4.4f);  // ช่อง (4,4) ชิดกำแพง
            var to = new Vector2(4.4f, 4.6f);  // อยากเข้า (4,5) = กำแพง

            var result = WorkerPathing.Step(from, to, walls);
            Assert.AreEqual(to.x, result.x, 1e-5f, "ต้องไถลไปตามกำแพงต่อได้");
            Assert.AreEqual(from.y, result.y, 1e-5f);
            Assert.AreNotEqual(from, result, "ห้ามหยุดนิ่งขณะเดินเลียบกำแพง");
        }

        [Test]
        public void OutOfBoundsTreatedAsWall_KeepsWorkerOnMap()
        {
            // WorkerView.IsBlocked คืน true เมื่อ GetCell = null (นอกกริด) — จำลองด้วย predicate เดียวกัน
            var from = new Vector2(0f, 0f);
            var to = new Vector2(-0.6f, 0f); // ช่อง (-1,0) นอกแมพ
            var result = WorkerPathing.Step(from, to, c => c.x < 0 || c.y < 0);

            Assert.AreEqual(from, result, "ขอบแมพต้องกันไว้เหมือนกำแพง");
        }
    }
}
