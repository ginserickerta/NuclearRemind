using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// พาเลตต์สีพื้น isometric (4 สี 2 โซน + พารามิเตอร์ไล่เฟด/มืดนอกกริด) — serializable
    /// ส่งเข้า IsoGroundPainter.ColorForTile (pure) ให้ GridSpriteFiller ทาสีต่อ tile (SetColor)
    /// ค่าเริ่มต้น = สเปกงาน (Zone A เขียว · Zone B น้ำตาล · alt = สลับลายหมากรุก)
    /// </summary>
    [System.Serializable]
    public struct GroundPalette
    {
        public Color zoneA_base, zoneA_alt;   // เขียว
        public Color zoneB_base, zoneB_alt;   // น้ำตาล
        public float transitionWidth;         // ความกว้างแถบไล่เฟด A→B (tile)
        public float jitterStrength;          // ยึกยักขอบแถบ (tile ~1) — กันเส้นตรง
        [Range(0f, 1f)] public float outsideDarken; // นอกกริด: คูณ RGB (0.80 = มืด ~20%)
        public int edgeDarkenWidth;           // ไล่มืดเนียนที่ขอบ→นอก (tile)

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        public static GroundPalette Default => new GroundPalette
        {
            zoneA_base = Hex(0x3E4A2E), zoneA_alt = Hex(0x4D5936),
            zoneB_base = Hex(0x4A4327), zoneB_alt = Hex(0x5C5230),
            transitionWidth = 6f, jitterStrength = 1f,
            outsideDarken = 0.80f, edgeDarkenWidth = 3,
        };
    }

    /// <summary>
    /// เลือกไทล์พื้นต่อช่อง (col,row) แบบ "พื้นเนียนใบเดียว + โรย variety" ตามโซน (V4 §5) —
    /// pure ล้วน ไม่มี scene/asset ให้เทสต์ตรวจได้ (GridSpriteFiller เรียกใช้ตอนระบาย Ground)
    ///
    /// แนวคิด:
    ///   • Zone A = สี่เหลี่ยมกลางแมพ (หญ้า/เมือง) · Zone B = กรอบรอบนอกหนา border ช่อง (ดิน/หิน/รังสี)
    ///   • ฐาน = ไทล์เดียวทั้งโซน (ไม่สลับ parity) → พื้นเนียน ไม่มีลายหมากรุกกวนตา
    ///   • ~VarietyPercent% ของช่อง สุ่มเป็นไทล์ variety (หญ้าหนา/ดินรอยแตก) เพิ่มชีวิตชีวาแบบกระจาย ไม่เป็นตาราง
    ///
    /// ★ สุ่มด้วย Hash(col,row) ไม่ใช่ Random — ลายคงที่ทุกครั้งที่ fill/โหลดเซฟ
    ///   (Random จะสลับลายทุก re-fill = บั๊กเงียบแบบเดียวกับที่เลี่ยงใน WorkerSeparation/SpriteAnimation)
    ///
    /// index อ้างอิง tile_XXX.png ของ IsoNature tileset (ดู IsoTilesetSetup)
    /// </summary>
    public static class IsoGroundPainter
    {
        // Zone A (หญ้า) — ฐาน = ไทล์แรก (22) ทั้งโซน · variety = หญ้าหนา/พุ่ม (27–34)
        // ([1] เก็บไว้เผื่อสลับ tileset — TileIndexFor ใช้ [0] อย่างเดียว ไม่สลับ parity แล้ว)
        public static readonly int[] GrassBase = { 22, 23 };
        public static readonly int[] GrassVariety = { 27, 28, 29, 30, 31, 32, 33, 34 };

        // Zone B (ดิน/หิน) — ฐาน = ไทล์แรก (0) ทั้งโซน · variety = ดินรอยแตก/ลาย (12–16)
        public static readonly int[] DirtBase = { 0, 1 };
        public static readonly int[] DirtVariety = { 12, 13, 14, 15, 16 };

        /// <summary>
        /// สัดส่วนช่องที่เป็นไทล์ variety (ที่เหลือเป็นฐานเนียนใบเดียว)
        /// 0 = พื้นเรียบสนิท ไม่โรยพุ่ม (ผู้ใช้ขอ "ไม่รกตา") — เพิ่มเป็น 5–8 ถ้าอยากได้หญ้าหนากระจายบ้าง
        /// </summary>
        public const int VarietyPercent = 0;

        /// <summary>index ของไทล์ที่ช่อง (col,row) ควรใช้ · border = ความหนากรอบ Zone B รอบนอก</summary>
        public static int TileIndexFor(int col, int row, int columns, int rows, int border)
        {
            bool zoneA = IsZoneA(col, row, columns, rows, border);
            int[] baseTiles = zoneA ? GrassBase : DirtBase;
            int[] variety = zoneA ? GrassVariety : DirtVariety;

            int h = Hash(col, row);
            if (h % 100 < VarietyPercent && variety.Length > 0)
                return variety[(h / 100) % variety.Length];

            // ฐานเนียนใบเดียวทั้งโซน — เลิกสลับ parity ที่ทำให้เป็นลายหมากรุกกวนตา
            return baseTiles[0];
        }

        /// <summary>
        /// true = Zone A (หญ้า/เมือง — ฝั่ง SW ของแนวรั้ว) · false = Zone B (ดิน/หิน/รังสี — ฝั่ง NE)
        /// โมเดลใหม่ (2026-07): แบ่งครึ่งด้วยแนวรั้วตั้งที่ col = columns-border (เส้นทแยง NW↔SE บนจอ) —
        /// Zone B = แถบ NE หนา border คอลัมน์ (col ≥ columns-border) · row ไม่มีผล (เดิมเป็นกรอบรอบนอก)
        /// รั้วจริงวาดโดย ZoneBarrierRenderer.barrierColumn = columns-border
        /// </summary>
        public static bool IsZoneA(int col, int row, int columns, int rows, int border)
            => col < columns - border;

        /// <summary>
        /// hash (col,row) กระจายดี คงที่ (ไม่ใช้ Random) — บวกเสมอด้วย mask 0x7fffffff
        /// เพื่อให้ % ให้ผลไม่ติดลบ (variety roll + variety pick พึ่งค่านี้)
        /// </summary>
        public static int Hash(int col, int row)
        {
            unchecked
            {
                int h = col * 73856093 ^ row * 19349663;
                h ^= h >> 13;
                h *= (int)0x85ebca6b; // 0x85ebca6b เป็น uint (เกิน int.MaxValue) — cast ใน unchecked ให้ห่อเป็น int ลบ
                h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }

        /// <summary>
        /// สีพื้นต่อช่อง (pure · testได้) — ในกริด: ไล่เฟด A(เขียว)→B(น้ำตาล) เนียนต่อ tile · นอกกริด: น้ำตาลคูณ outsideDarken
        /// signedDist = ระยะจากเส้นแบ่ง (col = columns−border · ใช้ boundary เดิม ไม่แตะ logic โซน) ตามแกน SW→NE
        ///   + jitter deterministic ต่อ (col,row) กันขอบเป็นเส้นตรง · t = smoothstep(±transitionWidth/2) ไม่ quantize
        ///   · Lerp ใน linear space กันสีขุ่นช่วงกลาง · หมากรุก base/alt ด้วย (col+row)&1
        /// </summary>
        public static Color ColorForTile(int col, int row, int columns, int rows, int border, GroundPalette pal)
        {
            bool alt = ((col + row) & 1) == 1;
            bool outside = col < 0 || col >= columns || row < 0 || row >= rows;

            if (outside)
            {
                Color bOut = alt ? pal.zoneB_alt : pal.zoneB_base;
                return MulRGB(bOut, OutsideDarkenFactor(col, row, columns, rows, pal));
            }

            float boundary = columns - border;                       // เส้นแบ่ง A/B เดิม (IsZoneA: col < columns−border)
            float jitter = (Hash01(col, row) - 0.5f) * 2f * pal.jitterStrength;
            float signed = (col - boundary) + jitter;                // − = SW/เขียว · + = NE/น้ำตาล
            float hw = Mathf.Max(1e-4f, pal.transitionWidth * 0.5f);
            float t = Smoothstep(-hw, hw, signed);

            Color a = alt ? pal.zoneA_alt : pal.zoneA_base;
            Color b = alt ? pal.zoneB_alt : pal.zoneB_base;
            return LerpLinear(a, b, t);
        }

        // ── helpers (pure) ──
        private static float Hash01(int col, int row) => Hash(col, row) / 2147483647f; // Hash ∈ [0, 0x7fffffff]

        private static float Smoothstep(float e0, float e1, float v)
        {
            float t = Mathf.Clamp01((v - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // Lerp ใน linear space แล้วกลับ gamma — ช่วงกลางไม่หม่น/ขุ่น
        private static Color LerpLinear(Color a, Color b, float t)
        {
            Color lin = Color.LerpUnclamped(a.linear, b.linear, t);
            var g = lin.gamma; g.a = 1f; return g;
        }

        private static Color MulRGB(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        // นอกกริด: 1.0 ที่ขอบกริด → outsideDarken เมื่อห่าง ≥ edgeDarkenWidth (smoothstep กันเส้นคม)
        private static float OutsideDarkenFactor(int col, int row, int columns, int rows, GroundPalette pal)
        {
            int dx = col < 0 ? -col : (col >= columns ? col - (columns - 1) : 0);
            int dy = row < 0 ? -row : (row >= rows ? row - (rows - 1) : 0);
            int d = Mathf.Max(dx, dy);
            if (pal.edgeDarkenWidth <= 0) return pal.outsideDarken;
            float t = Mathf.Clamp01((float)d / pal.edgeDarkenWidth);
            float s = t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, pal.outsideDarken, s);
        }
    }
}
