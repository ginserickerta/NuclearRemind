using System.Collections.Generic;
using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// พื้นแบ่งโซน "ฐานเนียนใบเดียว + โรย variety" (V4 §5) — IsoGroundPainter
    /// โมเดลใหม่ (2026-07): แบ่งครึ่งด้วยแนวรั้วตั้ง — Zone A = ฝั่ง SW (col &lt; Cols-Border) · Zone B = แถบ NE (col ≥ Cols-Border)
    /// ตรวจ: deterministic · แบ่งโซนถูก (SW=หญ้า/NE=ดิน) · index อยู่ในเซ็ตของโซน · ฐานเนียนไม่มีหมากรุก
    /// </summary>
    public class IsoGroundPainterTests
    {
        private const int Cols = 43;
        private const int Rows = 43;
        private const int Border = 7; // Zone B = แถบ NE หนา 7 คอลัมน์ (col 36..42) · Zone A = col 0..35

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
        public void ZoneA_SWside_UsesOnlyGrassTiles()
        {
            // Zone A = ฝั่ง SW ของแนวรั้ว (col 0..Cols-Border-1) ทุกแถว → หญ้าล้วน
            var grass = Set(IsoGroundPainter.GrassBase, IsoGroundPainter.GrassVariety);
            for (int x = 0; x < Cols - Border; x++)
                for (int y = 0; y < Rows; y++)
                    Assert.IsTrue(grass.Contains(Idx(x, y)),
                        $"Zone A ({x},{y}) ต้องเป็นไทล์หญ้า");
        }

        [Test]
        public void ZoneB_NEstrip_UsesOnlyDirtTiles()
        {
            var dirt = Set(IsoGroundPainter.DirtBase, IsoGroundPainter.DirtVariety);
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (!ZoneA(x, y)) // แถบ NE (col ≥ Cols-Border) = Zone B
                        Assert.IsTrue(dirt.Contains(Idx(x, y)),
                            $"Zone B ({x},{y}) ต้องเป็นไทล์ดิน");
        }

        [Test]
        public void ZoneBoundary_SplitAtColumn_RowIrrelevant()
        {
            // แนวรั้วตั้งที่ col = Cols-Border : col-1 = Zone A · col = Zone B — row ไม่มีผล
            int boundary = Cols - Border; // 36
            Assert.IsTrue(ZoneA(boundary - 1, 20), $"col {boundary - 1} = Zone A (ก่อนรั้ว)");
            Assert.IsFalse(ZoneA(boundary, 20), $"col {boundary} = Zone B (หลังรั้ว · NE)");
            // row ไม่มีผล — คอลัมน์เดียวกันเป็นโซนเดียวกันทุกแถว
            Assert.IsTrue(ZoneA(boundary - 1, 0), $"col {boundary - 1} = Zone A ทุกแถว");
            Assert.IsTrue(ZoneA(boundary - 1, Rows - 1), $"col {boundary - 1} = Zone A ทุกแถว");
            Assert.IsFalse(ZoneA(boundary, 0), $"col {boundary} = Zone B ทุกแถว");
            Assert.IsFalse(ZoneA(boundary, Rows - 1), $"col {boundary} = Zone B ทุกแถว");
            // ฝั่ง SW = Zone A (เมือง) · มุม N/E = Zone B (รังสี)
            Assert.IsTrue(ZoneA(0, 0), "มุม SW (0,0) = Zone A");
            Assert.IsTrue(ZoneA(0, Rows - 1), "มุม W (0,42) = Zone A");
            Assert.IsFalse(ZoneA(Cols - 1, Rows - 1), "มุม N (42,42) = Zone B");
            Assert.IsFalse(ZoneA(Cols - 1, 0), "มุม E (42,0) = Zone B");
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
