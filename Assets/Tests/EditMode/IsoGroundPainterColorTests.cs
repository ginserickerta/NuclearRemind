using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ทดสอบ IsoGroundPainter.ColorForTile (pure) — สเปกงานยกเครื่องสีพื้น 4 สีโซน + ไล่เฟด A→B + มืดนอกกริด
    /// grid 43×43 · border 7 → เส้นแบ่ง col = 43−7 = 36 (Zone A col<36 เขียว · Zone B col≥36 น้ำตาล)
    /// </summary>
    public class IsoGroundPainterColorTests
    {
        private const int Cols = 43, Rows = 43, Border = 7;
        private static GroundPalette Pal => GroundPalette.Default;

        [Test]
        public void DeepZoneA_ApproxGreenBase()
        {
            // col=2 (ลึกฝั่ง Zone A) · (2+2) คู่ → base · t≈0 → zoneA_base
            var c = IsoGroundPainter.ColorForTile(2, 2, Cols, Rows, Border, Pal);
            Assert.Greater(c.g, c.r, "Zone A ลึกต้องเขียว (g>r)");
            Assert.Greater(c.g, c.b, "Zone A ลึกต้องเขียว (g>b)");
            AssertCloseRGB(c, Pal.zoneA_base, 0.06f);
        }

        [Test]
        public void DeepZoneB_ApproxBrownBase()
        {
            // col=42 (ลึกฝั่ง Zone B) · (42+0) คู่ → base · t≈1 → zoneB_base
            var c = IsoGroundPainter.ColorForTile(42, 0, Cols, Rows, Border, Pal);
            Assert.Greater(c.r, c.b, "Zone B ลึกต้องน้ำตาล (r>b)");
            AssertCloseRGB(c, Pal.zoneB_base, 0.06f);
        }

        [Test]
        public void MidTransition_IsBlend()
        {
            // ที่เส้นแบ่ง col=36 → t≈0.5 (± jitter) → สีผสม ไม่ตรง A/B ล้วน
            var mid = IsoGroundPainter.ColorForTile(36, 0, Cols, Rows, Border, Pal);
            Assert.IsFalse(ApproxRGB(mid, Pal.zoneA_base, 0.02f), "กลางแถบต้องไม่ใช่เขียวล้วน");
            Assert.IsFalse(ApproxRGB(mid, Pal.zoneB_base, 0.02f), "กลางแถบต้องไม่ใช่น้ำตาลล้วน");
        }

        [Test]
        public void Outside_IsZoneBTimesDarken()
        {
            // col=53 (นอกกริดฝั่ง NE ไกล ≥ edgeDarkenWidth) · (53+21) คู่ → base · = zoneB_base × outsideDarken
            var c = IsoGroundPainter.ColorForTile(Cols + 10, 21, Cols, Rows, Border, Pal);
            var expected = Pal.zoneB_base * Pal.outsideDarken;
            AssertCloseRGB(c, expected, 0.04f);
            Assert.Less(c.r + c.g + c.b,
                        Pal.zoneB_base.r + Pal.zoneB_base.g + Pal.zoneB_base.b,
                        "นอกกริดต้องมืดกว่า Zone B");
        }

        [Test]
        public void Checker_BaseAndAltDiffer()
        {
            // ช่องติดกัน (parity ต่าง) ลึกใน Zone A → base vs alt ต้องต่างกัน (ลายหมากรุกยังอยู่)
            var baseCol = IsoGroundPainter.ColorForTile(2, 2, Cols, Rows, Border, Pal); // คู่ → base
            var altCol = IsoGroundPainter.ColorForTile(3, 2, Cols, Rows, Border, Pal);  // คี่ → alt
            Assert.IsFalse(ApproxRGB(baseCol, altCol, 0.01f), "หมากรุก base/alt ต้องต่างกัน");
        }

        private static bool ApproxRGB(Color a, Color b, float tol)
            => Mathf.Abs(a.r - b.r) <= tol && Mathf.Abs(a.g - b.g) <= tol && Mathf.Abs(a.b - b.b) <= tol;

        private static void AssertCloseRGB(Color a, Color b, float tol)
        {
            Assert.LessOrEqual(Mathf.Abs(a.r - b.r), tol, $"R ต่างเกิน: {a} vs {b}");
            Assert.LessOrEqual(Mathf.Abs(a.g - b.g), tol, $"G ต่างเกิน: {a} vs {b}");
            Assert.LessOrEqual(Mathf.Abs(a.b - b.b), tol, $"B ต่างเกิน: {a} vs {b}");
        }
    }
}
