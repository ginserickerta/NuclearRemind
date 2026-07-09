using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// เฟสเกมตามช่วงวัน (GDD §6 "ปลดล็อก"): 1–5→1 · 6–10→2 · 11–20→3 · 21+→4
    /// ตรวจขอบวันทุกจุด (5/6, 10/11, 20/21) — BuildingSelectionUI/PlacementController ใช้ค่านี้ล็อก hotbar
    /// </summary>
    public class GamePhaseTests
    {
        [TestCase(1, 1)]
        [TestCase(5, 1)]   // ขอบบนเฟส 1
        [TestCase(6, 2)]   // ขอบล่างเฟส 2
        [TestCase(10, 2)]  // ขอบบนเฟส 2
        [TestCase(11, 3)]  // ขอบล่างเฟส 3
        [TestCase(20, 3)]  // ขอบบนเฟส 3
        [TestCase(21, 4)]  // ขอบล่างเฟส 4
        [TestCase(30, 4)]  // วันสุดท้าย
        public void FromDay_MapsToPhase(int day, int expectedPhase)
        {
            Assert.AreEqual(expectedPhase, GamePhase.FromDay(day));
        }

        [Test]
        public void FromDay_NeverExceedsMaxPhase()
        {
            Assert.AreEqual(GamePhase.MaxPhase, GamePhase.FromDay(999));
        }
    }
}
