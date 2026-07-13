using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// วางสไปรต์พื้นจริงต่อช่อง (หญ้า Zone A / ดิน Zone B) แบบ "ไล่รอยต่อ dither" — IsoGroundPainter
    /// helper ใหม่ที่ GridSpriteFiller.FillZonedSprites เรียกใช้ · ต้อง deterministic + เลือกโซน/variety ถูกช่วง
    /// (เส้นแบ่งเดียวกับ ColorForTile: col = Cols−Border · แถบ transition ±transitionWidth/2)
    /// </summary>
    public class IsoGroundSpriteZoneTests
    {
        private const int Cols = 43;
        private const int Rows = 43;
        private const int Border = 7;   // boundary = 36 : col<36 SW/หญ้า · col≥36 NE/ดิน
        private const float Transition = 6f;
        private const float Jitter = 1f;

        private static bool Pick(int x, int y) =>
            IsoGroundPainter.PickZoneA(x, y, Cols, Rows, Border, Transition, Jitter);

        [Test]
        public void PickZoneA_Deterministic_SameCell_SameZone()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Assert.AreEqual(Pick(x, y), Pick(x, y), $"({x},{y}) โซนต้องคงที่ทุกครั้ง");
        }

        [Test]
        public void PickZoneA_FarSW_AlwaysGrass()
        {
            // ห่างเส้นแบ่งมากทางฝั่ง SW (พ้นแถบ transition + jitter) → หญ้าแน่นอน = IsZoneA
            for (int x = 0; x <= 31; x++)   // boundary(36) − hw(3) − jitter(1) − 1
                for (int y = 0; y < Rows; y++)
                    Assert.IsTrue(Pick(x, y), $"({x},{y}) ฝั่ง SW ต้องเป็นหญ้า (Zone A)");
        }

        [Test]
        public void PickZoneA_FarNE_AlwaysDirt()
        {
            // ห่างเส้นแบ่งมากทางฝั่ง NE → ดินแน่นอน (hash01 ∈ [0,1) ไม่มีทาง ≥ 1)
            for (int x = 40; x < Cols; x++) // boundary(36) + hw(3) + jitter(1)
                for (int y = 0; y < Rows; y++)
                    Assert.IsFalse(Pick(x, y), $"({x},{y}) ฝั่ง NE ต้องเป็นดิน (Zone B)");
        }

        [Test]
        public void VarietyPick_AlwaysInRange()
        {
            foreach (int count in new[] { 1, 6, 16 })
                for (int x = -5; x < Cols + 5; x++)
                    for (int y = -5; y < Rows + 5; y++)
                    {
                        int i = IsoGroundPainter.VarietyPick(x, y, count);
                        Assert.That(i, Is.InRange(0, count - 1), $"({x},{y}) count={count} index ต้องอยู่ในช่วง");
                    }
        }

        [Test]
        public void VarietyPick_Deterministic()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Assert.AreEqual(IsoGroundPainter.VarietyPick(x, y, 16),
                                    IsoGroundPainter.VarietyPick(x, y, 16), $"({x},{y}) variety ต้องคงที่");
        }

        [Test]
        public void OutsideDarken_WithinPaletteBounds()
        {
            var pal = GroundPalette.Default; // outsideDarken 0.80 · edgeDarkenWidth 3
            for (int x = -10; x < Cols + 10; x++)
                for (int y = -10; y < Rows + 10; y++)
                {
                    float f = IsoGroundPainter.OutsideDarken(x, y, Cols, Rows, pal);
                    Assert.That(f, Is.InRange(pal.outsideDarken - 1e-4f, 1f + 1e-4f),
                        $"({x},{y}) ตัวคูณมืดต้องอยู่ระหว่าง outsideDarken..1");
                }
        }
    }
}
