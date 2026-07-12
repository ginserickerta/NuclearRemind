namespace NuclearReMind
{
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
    }
}
