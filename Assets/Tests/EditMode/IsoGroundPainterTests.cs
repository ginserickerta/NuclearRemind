using System.Collections.Generic;
using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// พื้นแบ่งโซน "ฐานเนียนใบเดียว + โรย variety" (V4 §5) — IsoGroundPainter
    /// ตรวจ: deterministic (คงที่ทุก fill) · แบ่งโซนถูก · index อยู่ในเซ็ตของโซนนั้น · variety ~12% · ฐานเนียนไม่มีหมากรุก
    /// </summary>
    public class IsoGroundPainterTests
    {
        private const int ZoneA = 29; // กริด 43×28: Zone A cols 0..28 · Zone B 29..42

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
            for (int x = 0; x < 43; x++)
                for (int y = 0; y < 28; y++)
                    Assert.AreEqual(
                        IsoGroundPainter.TileIndexFor(x, y, ZoneA),
                        IsoGroundPainter.TileIndexFor(x, y, ZoneA),
                        $"({x},{y}) ต้องคงที่");
        }

        [Test]
        public void ZoneA_UsesOnlyGrassTiles()
        {
            var grass = Set(IsoGroundPainter.GrassBase, IsoGroundPainter.GrassVariety);
            for (int x = 0; x < ZoneA; x++)
                for (int y = 0; y < 28; y++)
                    Assert.IsTrue(grass.Contains(IsoGroundPainter.TileIndexFor(x, y, ZoneA)),
                        $"Zone A ({x},{y}) ต้องเป็นไทล์หญ้า");
        }

        [Test]
        public void ZoneB_UsesOnlyDirtTiles()
        {
            var dirt = Set(IsoGroundPainter.DirtBase, IsoGroundPainter.DirtVariety);
            for (int x = ZoneA; x < 43; x++)
                for (int y = 0; y < 28; y++)
                    Assert.IsTrue(dirt.Contains(IsoGroundPainter.TileIndexFor(x, y, ZoneA)),
                        $"Zone B ({x},{y}) ต้องเป็นไทล์ดิน");
        }

        [Test]
        public void ZoneBoundary_SplitsExactlyAtZoneAColumns()
        {
            var grass = Set(IsoGroundPainter.GrassBase, IsoGroundPainter.GrassVariety);
            var dirt = Set(IsoGroundPainter.DirtBase, IsoGroundPainter.DirtVariety);
            // คอลัมน์สุดท้ายของ A = หญ้า · คอลัมน์แรกของ B = ดิน (ไม่เหลื่อม)
            Assert.IsTrue(grass.Contains(IsoGroundPainter.TileIndexFor(ZoneA - 1, 5, ZoneA)), "col 28 = หญ้า");
            Assert.IsTrue(dirt.Contains(IsoGroundPainter.TileIndexFor(ZoneA, 5, ZoneA)), "col 29 = ดิน");
        }

        [Test]
        public void VarietyShare_MatchesVarietyPercent()
        {
            var grassBase = new HashSet<int>(IsoGroundPainter.GrassBase);
            int variety = 0, total = 0;
            for (int x = 0; x < ZoneA; x++)
                for (int y = 0; y < 28; y++, total++)
                    if (!grassBase.Contains(IsoGroundPainter.TileIndexFor(x, y, ZoneA))) variety++;

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
            for (int x = 0; x < ZoneA; x++)
                for (int y = 0; y < 28; y++)
                {
                    int t = IsoGroundPainter.TileIndexFor(x, y, ZoneA);
                    if (grassBase.Contains(t))
                        Assert.AreEqual(IsoGroundPainter.GrassBase[0], t,
                            $"ฐาน ({x},{y}) ต้องเป็นไทล์เดียว (ไม่สลับ parity)");
                }
        }

        [Test]
        public void AllIndices_AreValidTileRange()
        {
            for (int x = 0; x < 43; x++)
                for (int y = 0; y < 28; y++)
                {
                    int t = IsoGroundPainter.TileIndexFor(x, y, ZoneA);
                    Assert.That(t, Is.InRange(0, 114), $"({x},{y}) index {t} ต้องอยู่ในช่วงไทล์ที่มีจริง");
                }
        }

        [Test]
        public void Hash_NonNegative_ForModuloSafety()
        {
            // % ต้องไม่ติดลบ — variety roll/pick พึ่งค่านี้
            for (int x = -5; x < 50; x++)
                for (int y = -5; y < 40; y++)
                    Assert.GreaterOrEqual(IsoGroundPainter.Hash(x, y), 0, $"hash({x},{y}) ต้อง ≥ 0");
        }
    }
}
