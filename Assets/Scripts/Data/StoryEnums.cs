namespace NuclearReMind
{
    /// <summary>
    /// ชนิดเงื่อนไขปล่อย StoryBeat (Story Guide §2) — StoryDirector ฟัง event ของเมือง
    /// แล้วเทียบกับ triggerType + triggerParam ของแต่ละ beat
    /// </summary>
    public enum StoryTriggerType
    {
        /// <summary>triggerParam = เลขวัน เช่น "1", "20" — ยิงตอนเริ่มวันนั้น (OnDayStarted)</summary>
        OnDay,

        /// <summary>triggerParam = buildingName ของอาคาร (สตริงไทยตาม BuildingData เช่น "ห้องปฏิบัติการ")
        /// — ยิงเมื่อก่อสร้างเสร็จ (OnConstructionComplete) · ระวัง: ต้องตรงกับ buildingName จริง
        /// ห้ามใช้ชื่ออังกฤษ (บทเรียนบั๊ก PowerGrid ที่ match ชื่อผิดจนการผลิตหยุดทั้งเมือง)</summary>
        OnBuildingBuilt,

        /// <summary>ได้ Deuterium เข้าคลังครั้งแรก (latch แบบเดียวกับ Q1 ใน QuizManager) — ไม่ใช้ triggerParam</summary>
        OnDeuteriumExtracted,

        /// <summary>CORE TOWER ปลดล็อก/เริ่มเดินเครื่องครั้งแรก — ไม่ใช้ triggerParam</summary>
        OnReactorStart,

        /// <summary>triggerParam ใช้ syntax เดียวกับ DilemmaData.triggerCondition
        /// เช่น "heat_above_80|q_above_0.3" (| = อย่างใดอย่างหนึ่ง) — ประเมินตอนจบวัน</summary>
        OnStatThreshold,

        /// <summary>พายุรังสีมาถึง (วันแรกของช่วงพายุ — CoreTowerManager.StormStartDay)</summary>
        OnStormApproach,

        /// <summary>ระหว่างพายุ + เงื่อนไขเพิ่มใน triggerParam — ประเมินตอนจบวัน
        /// คีย์เวิร์ดพิเศษ "coolingWorkerShortage" = HEAT ≥ 70 (หล่อเย็นตามพายุ +12/วันไม่ทัน)
        /// คีย์อื่นประเมินแบบ StatCondition ปกติ · ว่าง = ยิงทันทีที่พายุมา</summary>
        OnStormActive,

        /// <summary>วิกฤตซ้อนจากทางเลือกของวิกฤตก่อนหน้า (Story Guide deferredCrisis)
        /// triggerParam = คีย์ เช่น "water"/"food" — StoryDirector จดคีย์ตอน resolve
        /// (DilemmaData.choiceX_DeferredCrisis) แล้วยิง beat นี้หลังหน่วง 2 วัน</summary>
        OnDeferredCrisis,

        /// <summary>triggerParam = buildingName (สตริงไทย เช่น "ห้องปฏิบัติการ") — ยิงเมื่ออาคารนั้น
        /// "มีคนเข้าประจำครั้งแรก" (OnWorkerAssignmentChanged count > 0) ★ สเปกโรงวิจัย:
        /// บันทึก Elara #01 ปลดตอน "สร้าง+จัดคนเข้าห้องวิจัย" — อาคาร pre-placed ใช้ OnBuildingBuilt
        /// ไม่ได้เพราะ event ยิงตอนเริ่มเกมก่อนผู้เล่นทำอะไร (enum ต่อท้ายเท่านั้น — กันเลื่อนค่า serialize)</summary>
        OnBuildingStaffed,
    }

    /// <summary>
    /// หมวดของ InfoCard — กำหนดสีแถบหัวการ์ด (ล้อแนวเดียวกับ QuizCategory §17)
    /// </summary>
    public enum CardCategory { Energy, Reactor, Medical, Food, Ethics, Fusion }
}
