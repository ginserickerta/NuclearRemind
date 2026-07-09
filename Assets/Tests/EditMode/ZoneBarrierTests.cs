using NUnit.Framework;

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
    }
}
