using System.Collections.Generic;
using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// พื้นแบ่งโซน "ฐานเนียนใบเดียว + โรย variety" (V4 §5) — IsoGroundPainter
    /// โมเดลกรอบ: Zone A = สี่เหลี่ยมกลาง · Zone B = กรอบรอบนอกหนา Border ช่อง
    /// ตรวจ: deterministic · แบ่งโซนถูก (กลาง=หญ้า/รอบนอก=ดิน) · index อยู่ในเซ็ตของโซน · ฐานเนียนไม่มีหมากรุก
    /// </summary>
    public class IsoGroundPainterTests
    {
        private const int Cols = 43;
        private const int Rows = 43;
        private const int Border = 7; // Zone B กรอบหนา 7 · Zone A = สี่เหลี่ยมกลาง cols/rows 7..35 (29×29)

        private static int Idx(int x, int y) => IsoGroundPainter.TileIndexFor(x, y, Cols, Rows, Border);
        private static bool ZoneA(int x, int y) => IsoGroundPainter.IsZoneA(x, y, Cols, Rows, Border);

        private static HashSet<int> Set(params int[][] arrs)
        {
            var s = new HashSet<int>();
            foreach (var a in arrs) foreach (var v in a) s.Add(v);
            return s;
        }

        [Test]
        public void Deterministic_SameCell_SameTile()
        {
            // เรียกซ้ำต้องได้ผลเท่าเดิมเสมอ — ไม่งั้นลายสลับทุก re-fill/โหลดเซฟ
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Assert.AreEqual(Idx(x, y), Idx(x, y), $"({x},{y}) ต้องคงที่");
        }

        [Test]
        public void ZoneA_CenterRect_UsesOnlyGrassTiles()
        {
            var grass = Set(IsoGroundPainter.GrassBase, IsoGroundPainter.GrassVariety);
            for (int x = Border; x < Cols - Border; x++)
                for (int y = Border; y < Rows - Border; y++)
                    Assert.IsTrue(grass.Contains(Idx(x, y)),
                        $"Zone A ({x},{y}) ต้องเป็นไทล์หญ้า");
        }

        [Test]
        public void ZoneB_OuterBorder_UsesOnlyDirtTiles()
        {
            var dirt = Set(IsoGroundPainter.DirtBase, IsoGroundPainter.DirtVariety);
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (!ZoneA(x, y)) // ทุกช่องนอกสี่เหลี่ยมกลาง = กรอบ Zone B
                        Assert.IsTrue(dirt.Contains(Idx(x, y)),
                            $"Zone B ({x},{y}) ต้องเป็นไทล์ดิน");
        }

        [Test]
        public void ZoneBoundary_BorderRingExactlyThick()
        {
            // ขอบใน (border) = Zone A · หนึ่งช่องนอกนั้น = Zone B — ทั้ง 4 ด้านสมมาตร
            Assert.IsTrue(ZoneA(Border, 20), $"col {Border} = Zone A (ขอบในซ้าย)");
            Assert.IsFalse(ZoneA(Border - 1, 20), $"col {Border - 1} = Zone B (กรอบซ้าย)");
            Assert.IsTrue(ZoneA(Cols - 1 - Border, 20), $"col {Cols - 1 - Border} = Zone A (ขอบในขวา)");
            Assert.IsFalse(ZoneA(Cols - Border, 20), $"col {Cols - Border} = Zone B (กรอบขวา)");
            Assert.IsTrue(ZoneA(20, Border), $"row {Border} = Zone A (ขอบในล่าง)");
            Assert.IsFalse(ZoneA(20, Border - 1), $"row {Border - 1} = Zone B (กรอบล่าง)");
            Assert.IsTrue(ZoneA(20, Rows - 1 - Border), $"row {Rows - 1 - Border} = Zone A (ขอบในบน)");
            Assert.IsFalse(ZoneA(20, Rows - Border), $"row {Rows - Border} = Zone B (กรอบบน)");
            // มุมทั้งสี่ต้องเป็น Zone B เสมอ
            Assert.IsFalse(ZoneA(0, 0), "มุม (0,0) = Zone B");
            Assert.IsFalse(ZoneA(Cols - 1, Rows - 1), "มุม (42,42) = Zone B");
        }

        [Test]
        public void VarietyShare_MatchesVarietyPercent()
        {
            var grassBase = new HashSet<int>(IsoGroundPainter.GrassBase);
            int variety = 0, total = 0;
            for (int x = Border; x < Cols - Border; x++)
                for (int y = Border; y < Rows - Border; y++, total++)
                    if (!grassBase.Contains(Idx(x, y))) variety++;

            double pct = 100.0 * variety / total;
            // ยึดตามค่าคงที่ (0 = พื้นเรียบสนิท) ยอมคลาดจาก hash distribution ±8%
            double lo = System.Math.Max(0, IsoGroundPainter.VarietyPercent - 8);
            double hi = IsoGroundPainter.VarietyPercent + 8;
            Assert.That(pct, Is.InRange(lo, hi),
                $"variety ควร ~{IsoGroundPainter.VarietyPercent}% (ได้ {pct:0.#}%)");
        }

        [Test]
        public void BaseCells_UseSingleUniformTile_NoChecker()
        {
            // เลิกลายหมากรุก — ช่องฐาน (ไม่ใช่ variety) ทุกช่องต้องเป็นไทล์ฐานใบเดียว ไม่ว่า parity ใด
            var grassBase = new HashSet<int>(IsoGroundPainter.GrassBase);
            for (int x = Border; x < Cols - Border; x++)
                for (int y = Border; y < Rows - Border; y++)
                {
                    int t = Idx(x, y);
                    if (grassBase.Contains(t))
                        Assert.AreEqual(IsoGroundPainter.GrassBase[0], t,
                            $"ฐาน ({x},{y}) ต้องเป็นไทล์เดียว (ไม่สลับ parity)");
                }
        }

        [Test]
        public void AllIndices_AreValidTileRange()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    Assert.That(Idx(x, y), Is.InRange(0, 114), $"({x},{y}) index ต้องอยู่ในช่วงไทล์ที่มีจริง");
        }

        [Test]
        public void Hash_NonNegative_ForModuloSafety()
        {
            // % ต้องไม่ติดลบ — variety roll/pick พึ่งค่านี้
            for (int x = -5; x < 50; x++)
                for (int y = -5; y < 50; y++)
                    Assert.GreaterOrEqual(IsoGroundPainter.Hash(x, y), 0, $"hash({x},{y}) ต้อง ≥ 0");
        }
    }
}
