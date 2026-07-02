namespace NuclearReMind
{
    /// <summary>
    /// ผลลัพธ์จบเกม (V4 §14) — ประเมินด้วยค่า Q ตอน Day 30 หรือเหตุแพ้ระหว่างทาง
    /// True/Normal = ชนะ/ชนะบางส่วน · ที่เหลือคือแพ้ (lossReason)
    /// </summary>
    public enum GameEndType
    {
        TrueEnding,    // Q ≥ 1.0 (ดัน CORE 100% ทัน) — ชนะสมบูรณ์
        NormalEnding,  // Day 30 · Q 0.5–0.99 — ชนะบางส่วน
        HopeZero,      // Hope ถึง 0 (V4 §9)
        Meltdown,      // HEAT ≥ 100 (V4 §8)
        TimeoutLowQ    // Day 30 · Q < 0.5 (V4 §14)
    }
}
