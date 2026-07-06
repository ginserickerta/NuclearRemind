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

        /// <summary>ระหว่างพายุ + เงื่อนไขเพิ่มใน triggerParam (เช่น "coolingWorkerShortage")</summary>
        OnStormActive,
    }

    /// <summary>
    /// หมวดของ InfoCard — กำหนดสีแถบหัวการ์ด (ล้อแนวเดียวกับ QuizCategory §17)
    /// </summary>
    public enum CardCategory { Energy, Reactor, Medical, Food, Ethics, Fusion }
}
