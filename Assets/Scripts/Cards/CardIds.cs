namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: รวม id การ์ดวิกฤตทั้ง 8 ใบเป็นค่าคงที่ (คอมไพเลอร์เช็คชื่อให้) — เก็บเฉพาะชื่อ
    /// [TH] ตัวเลขทั้งหมด (เกณฑ์ trigger, cooldown) อยู่ใน GameConfigSO / asset การ์ด ตามกติกาข้อ 2
    /// The 8 Crisis Card ids (GDD §25 / CARDS.md / CONFIG.md 🔒 CARDS), as constants so triggers
    /// and the manager reference compile-checked names. Identifiers only — every number
    /// (thresholds, cooldowns) lives in GameConfigSO / the card asset (CLAUDE.md rule #2).
    /// </summary>
    public static class CardIds
    {
        public const string Heat     = "heat";      // 1 · เตาร้อน
        public const string Sick     = "sick";      // 2 · คนป่วย
        public const string Spoil    = "spoil";     // 3 · เสบียงเน่า
        public const string Hunger   = "hunger";    // 4 · คนงานหิว
        public const string Overwork = "overwork";  // 5 · คนงานหมดแรง
        public const string ZoneB    = "zoneb";     // 6 · Zone B หมดคน
        public const string Triage   = "triage";    // 7 · Triage
        public const string Decree   = "decree";    // 8 · Decree

        public static readonly string[] All =
        {
            Heat, Sick, Spoil, Hunger, Overwork, ZoneB, Triage, Decree,
        };
    }
}
