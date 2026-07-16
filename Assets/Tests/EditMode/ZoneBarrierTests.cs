using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// แนวรั้ว/ประตูเส้นแบ่งโซน A-B (V4 §5) — ZoneBarrierMath.GateRange
    /// ประตูต้องอยู่ในแนวรั้วเสมอ (กริด 43×28 → แนวรั้ว 28 ช่วง) และปิดสนิทได้เมื่อ width = 0
    /// </summary>
    public class ZoneBarrierTests
    {
        [Test]
        public void GateRange_AutoCenter_CentersOnBarrier()
        {
            ZoneBarrierMath.GateRange(28, -1, 3, out int start, out int end);

            Assert.AreEqual(13, start, "ประตูกว้าง 3 บนแนว 28 ช่วง → เริ่มแถว 13");
            Assert.AreEqual(15, end, "ปิดท้ายแถว 15 (คร่อมกึ่งกลาง 14)");
        }

        [Test]
        public void GateRange_ExplicitCenter_HonouredWhenInside()
        {
            ZoneBarrierMath.GateRange(28, 5, 3, out int start, out int end);

            Assert.AreEqual(4, start);
            Assert.AreEqual(6, end);
        }

        [Test]
        public void GateRange_NearEdge_ClampedInsideBarrier()
        {
            ZoneBarrierMath.GateRange(28, 0, 4, out int start, out int end);
            Assert.AreEqual(0, start, "ประตูชิดขอบล่าง → clamp ที่แถว 0");
            Assert.AreEqual(3, end);

            ZoneBarrierMath.GateRange(28, 99, 4, out start, out end);
            Assert.AreEqual(24, start, "ประตูชิดขอบบน → clamp ให้จบพอดีแถวสุดท้าย");
            Assert.AreEqual(27, end);
        }

        [Test]
        public void GateRange_ZeroWidth_NoGate()
        {
            ZoneBarrierMath.GateRange(28, -1, 0, out int start, out int end);
            Assert.Greater(start, end, "width 0 → รั้วปิดสนิท (start > end = ไม่มีช่วงประตู)");
        }

        [Test]
        public void GateRange_WidthBeyondBarrier_ClampedToFullLine()
        {
            ZoneBarrierMath.GateRange(28, -1, 999, out int start, out int end);

            Assert.AreEqual(0, start);
            Assert.AreEqual(27, end, "ประตูกว้างเกินแนว → เปิดทั้งแนว (ไม่ล้นขอบ)");
        }

        [Test]
        public void GateRange_EmptyBarrier_NoGate()
        {
            ZoneBarrierMath.GateRange(0, -1, 3, out int start, out int end);
            Assert.Greater(start, end, "ไม่มีแนวรั้ว → ไม่มีประตู (ไม่ throw)");
        }

        // ─────────── CrossingBlocked: กันคนงานข้ามรั้วโซน (barrierCol=36 · ประตูแถว 13..15) ───────────
        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        [Test]
        public void Crossing_WithinSameZone_NotBlocked()
        {
            // เดินภายในโซน A (ทั้งคู่ col < 36) — ไม่ข้ามเส้น → ไม่กั้น (แม้ประตูปิด)
            Assert.IsFalse(ZoneBarrierMath.CrossingBlocked(C(10, 5), C(11, 5), 36, false, 13, 15));
            // เดินภายในโซน B (ทั้งคู่ col ≥ 36)
            Assert.IsFalse(ZoneBarrierMath.CrossingBlocked(C(37, 20), C(38, 20), 36, false, 13, 15));
        }

        [Test]
        public void Crossing_Locked_BlocksEveryRow()
        {
            // ประตูปิด (gateOpen=false) → ข้ามเข้าโซน B ไม่ได้ทุกแถว รวมถึงแถวที่ปกติเป็นประตู
            Assert.IsTrue(ZoneBarrierMath.CrossingBlocked(C(35, 14), C(36, 14), 36, false, 13, 15),
                "โซน B ล็อก → แม้แถวประตูก็ข้ามไม่ได้");
            Assert.IsTrue(ZoneBarrierMath.CrossingBlocked(C(35, 2), C(36, 2), 36, false, 13, 15));
        }

        [Test]
        public void Crossing_Unlocked_AllowsGateRowOnly()
        {
            // ประตูเปิด: ข้ามได้เฉพาะแถวประตู 13..15 · นอกนั้นยังกั้น (ต้องเดินอ้อมไปประตู)
            Assert.IsFalse(ZoneBarrierMath.CrossingBlocked(C(35, 14), C(36, 14), 36, true, 13, 15),
                "แถวประตู → ข้ามได้");
            Assert.IsTrue(ZoneBarrierMath.CrossingBlocked(C(35, 2), C(36, 2), 36, true, 13, 15),
                "นอกแถวประตู → ยังกั้น");
        }

        [Test]
        public void Crossing_Symmetric_LeavingZoneBSameRules()
        {
            // ออกจากโซน B → A ใช้กติกาเดียวกัน (แถวฝั่ง B เป็นตัวตัดสิน)
            Assert.IsFalse(ZoneBarrierMath.CrossingBlocked(C(36, 15), C(35, 15), 36, true, 13, 15),
                "ออกทางประตู → ได้");
            Assert.IsTrue(ZoneBarrierMath.CrossingBlocked(C(36, 25), C(35, 25), 36, true, 13, 15),
                "ออกนอกประตู → กั้น");
        }

        // ─────────── RouteThroughGate: เล็ง waypoint ที่ประตูเมื่อปลายทางคนละฝั่ง ───────────
        [Test]
        public void Route_SameSide_ReturnsTargetUnchanged()
        {
            var to = new Vector2(40f, 25f);
            Assert.AreEqual(to, ZoneBarrierMath.RouteThroughGate(new Vector2(38f, 5f), to, 36, true, 13, 15),
                "อยู่โซน B ด้วยกัน → ไม่อ้อม");
        }

        [Test]
        public void Route_CrossingToZoneB_AimsAtGateRow()
        {
            // ปลายทางโซน B แถว 25 (นอกประตู) · คนอยู่โซน A → waypoint ต้องเป็นช่องติดประตู แถว = กึ่งกลางประตู 14
            var wp = ZoneBarrierMath.RouteThroughGate(new Vector2(20f, 30f), new Vector2(40f, 25f), 36, true, 13, 15);
            Assert.AreEqual(36f, wp.x, 1e-5f, "เล็งช่องแรกฝั่งโซน B ติดประตู");
            Assert.AreEqual(14f, wp.y, 1e-5f, "แถว = กึ่งกลางประตู (13..15 → 14) เพื่อ funnel เข้าประตู");
        }

        [Test]
        public void Route_Locked_DoesNotRedirect()
        {
            var to = new Vector2(40f, 25f);
            Assert.AreEqual(to, ZoneBarrierMath.RouteThroughGate(new Vector2(20f, 25f), to, 36, false, 13, 15),
                "ประตูปิด → ไม่เล็งประตู (assignment ถูกกันไว้อยู่แล้ว)");
        }
    }
}
