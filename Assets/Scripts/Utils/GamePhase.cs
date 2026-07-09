namespace NuclearReMind
{
    /// <summary>
    /// เฟสของเกมตามช่วงวัน (GDD §6 คอลัมน์ "ปลดล็อก") — pure static ให้เทสต์ตรวจขอบวันได้
    ///   Phase 1 = วัน 1–5 · Phase 2 = 6–10 · Phase 3 = 11–20 · Phase 4 = 21+
    /// ใช้คู่ BuildingData.unlockPhase: อาคารกดวางได้เมื่อ CurrentPhase ≥ unlockPhase
    /// </summary>
    public static class GamePhase
    {
        public const int MaxPhase = 4;

        public static int FromDay(int day)
        {
            if (day <= 5) return 1;
            if (day <= 10) return 2;
            if (day <= 20) return 3;
            return 4;
        }
    }
}
