namespace NuclearReMind
{
    /// <summary>
    /// ⚠ ARCHIVED (2026-07-17, v6.3 cutover) — ตารางวิกฤตผูกวัน (Day 17/20/24/25) ขัด GDD v6.3 กฎข้อ 1
    /// (ห้าม day-based trigger) · แทนที่ด้วย CardTriggers (state-only) + StormSystem (pressure gauge §23)
    /// เก็บ compile ไว้เพื่อ EditMode tests เดิม — ห้ามใช้อ้างอิงในระบบใหม่
    ///
    /// ★ ตารางวิกฤต — แหล่งความจริงเดียวของ "เงื่อนไข + วันที่วิกฤตเด้ง" (GDD v4.0 §10 + Victory Loop §14)
    ///
    /// GDD §14 กำหนดจังหวะมาตรฐานไว้: Day 17 พลาสมา · Day 20 โรครังสี · Day 24 เสบียงเน่า · Day 25 พายุ
    /// เงื่อนไขใน §10 เป็น "สาเหตุ" ที่ทำให้วิกฤตเด้งเร็วกว่ากำหนดได้ถ้าผู้เล่นเล่นเสี่ยง —
    /// ส่วน day_reached_N คือ "เพดานเวลา" กันผู้เล่นพลาดเนื้อหา/ควิซถ้าไม่เข้าเงื่อนไขสาเหตุเลย
    ///
    /// อยู่ในแอสเซมบลีรันไทม์ (ไม่ใช่ Editor) เพื่อให้ EditMode test ตรวจวันเด้งจริงกับสตริงชุดเดียวกับที่ StorySetup ใช้
    /// </summary>
    public static class CrisisSchedule
    {
        // ── วันตามจังหวะมาตรฐาน (GDD §14 Victory Loop) ──
        public const int PlasmaDay = 17;    // วิกฤต 1 · เสถียรภาพพลาสมา
        public const int OutbreakDay = 20;  // วิกฤต 2 · โรคกลายพันธุ์/โรครังสี
        public const int FoodDay = 24;      // วิกฤต 3 · เสบียงเน่า
        public const int StormDay = 25;     // พายุ / FIRST LIGHT (= CoreTowerManager.StormStartDay)

        /// <summary>
        /// วิกฤต 1 (§10): "HEAT &gt; 80 หรือ Q &gt; 0.3 (จบเฟส 1)"
        ///
        /// ★ "จบเฟส 1" = CORE% แตะ 50 (§8 ตารางเฟส: เฟส 1 = 30→50%) จึงใช้ core_above_50 ไม่ใช่ q_above_0.3 —
        /// ในเกมนี้ Q = CORE%/100 ดังนั้น q_above_0.3 คือ CORE% ≥ 30 ซึ่ง "เป็นจริงทันทีที่เตาปลดล็อก Day 11"
        /// (เตาเริ่มที่ 30%) วิกฤตจึงเคยเด้ง Day 11 แทนที่จะเป็น Day 17
        ///
        /// เดิน Normal (เฟส 1 ล็อกโหมด Normal): CORE% = 30 + 3×(day−10) → แตะ 51 ตอนจบ Day 17 พอดีตาม §14
        /// heat_above_80 = ผู้เล่นเร่ง Overdrive จนร้อนก่อนกำหนด → วิกฤตเด้งเร็วขึ้นอย่างสมเหตุสมผล
        /// </summary>
        public static readonly string PlasmaTrigger = "heat_above_80|core_above_50|day_reached_" + PlasmaDay;

        /// <summary>
        /// วิกฤต 2 (§10): "ส่งคนขุดโซนเสี่ยงมากเกินไป → คนงาน 15 คนป่วยกะทันหัน"
        /// จำลองด้วยรังสีสะสม (RadiationManager) — ขุดหนัก/ไม่ป้องกันตาม ALARA = เด้งเร็ว · เพดาน Day 20 (§14)
        /// </summary>
        public static readonly string OutbreakTrigger = "exposure_above_60|day_reached_" + OutbreakDay;

        /// <summary>
        /// วิกฤต 3 (§10): "อาหารเก็บ &gt; 500 หรือไม่มี Agri Dome"
        ///
        /// ★ เกมนี้ยังไม่มีอาคาร Agri Dome ดังนั้นวงเล็บ "หรือไม่มี Agri Dome" เป็นจริงเสมอ →
        /// ตัวกำหนดจริงคือวันตาม §14 (Day 24) · ส่วน food_above_500 ใช้ไม่ได้เพราะเพดานคลังอาหาร = 500
        /// (ResourceManager.maxFood) ผู้เล่นจึงชนเพดานตั้งแต่ ~Day 12 → วิกฤต+ควิซ Q6/Q7 เด้งก่อน Q4/Q5
        /// เมื่อใดมี Agri Dome ให้เปลี่ยนเป็น "food_above_500|day_reached_24" (ยังคุมเพดานวันไว้)
        /// </summary>
        public static readonly string FoodTrigger = "day_reached_" + FoodDay;
    }
}
