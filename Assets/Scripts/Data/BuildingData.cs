using UnityEngine;
using UnityEngine.Serialization;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลอาคารแต่ละชนิด (ScriptableObject) - cost, production, tooltip 3 ชั้น
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuilding", menuName = "NuclearReMind/Building")]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public string buildingName;

        [Header("Tooltip Layer 2 - Gameplay")]
        [TextArea]
        public string description;

        [Header("Tooltip Layer 3 - Nuclear Science")]
        [TextArea]
        public string nuclearKnowledge;

        [Header("Visual")]
        public Sprite sprite;

        [Header("Grid")]
        public Vector2Int size = Vector2Int.one;

        // DEVIATION from literal Improve spec: BuildingType is required by
        // PlacementController.OccupyFootprint() to mark Cell.buildingType,
        // and the enum already lives in GridManager.cs. Kept intentionally.
        public BuildingType buildingType;

        [Header("Cost")]
        [FormerlySerializedAs("materialCost")]
        public int ironCost;            // V4: แร่เหล็ก (แทน material) — migrate ค่าเดิมอัตโนมัติ
        public int energyCost;
        public int workerRequired;

        // เฟสที่อาคารปลดล็อกให้กดวางได้ (GDD §6 คอลัมน์ "ปลดล็อก" · GamePhase.FromDay)
        // default 1 = วางได้ตั้งแต่วันแรก → asset เดิมทุกตัวไม่กระทบ
        public int unlockPhase = 1;

        [Header("Construction (V4 §5 — เวลาสร้าง)")]
        // เวลาสร้างเต็ม (tick · 5วิ/tick) ที่คนงาน 1 คน — คนมากขึ้น → เร็วขึ้นแบบผกผัน
        // (ConstructionController.ConstructionSpeed: ก้าวหน้า/tick = จำนวนคนงานที่ประจำ cell)
        // ค่าเริ่มต้น 6 = ~30 วิ ต่อ 1 คน (ปรับต่ออาคารได้ใน Inspector · ตึกใหญ่ตั้งสูงขึ้น)
        public int buildTicks = 6;

        [Header("Production (ต่อวัน — batch ตอนจบวัน V4 §3/§6)")]
        public float foodProduction;
        public float waterProduction;
        public float energyProduction;
        public float ironProduction;    // V4: ผลิตจาก Mine

        [Header("Consumption (ต่อวัน) — ค่าเดินระบบ V4 §6")]
        // ต้นทุนเดินเครื่องต่อวันที่อาคารกินจากคลัง (ตาราง "ค่าเดินระบบ/วัน" ใน V4 §6)
        // ถ้าคลังไม่พอจ่าย consumption → อาคารหยุดผลิตวันนั้น (idle) และไม่กิน resource
        public float energyConsumption;
        public float waterConsumption;

        [Header("CORE TOWER")]
        public bool isCoreTowerPart;
        public int towerPhaseRequired; // 0 = all phases

        [Header("Population Training (V4 §5)")]
        public bool unlocksEngineerTraining; // Research Lab → ฝึก Engineer ได้
        public bool unlocksMedicTraining;    // Research Lab → ฝึก Medic ได้ (Lab เป็นศูนย์ฝึกทุกคลาส)
        public bool unlocksFarmerTraining;   // Research Lab → ฝึก Farmer ได้

        // คลาสแรงงานที่อาคารนี้ต้องใช้ประจำ (V4 §5) — assignment ดึงจาก idle pool ของคลาสนี้
        // Worker = 0 (default) → asset เดิมทุกตัวคง Worker โดยไม่ต้องแก้ · Lab/CORE=Engineer · Hospital=Medic · Farm/Agri=Farmer
        public WorkerClass requiredClass = WorkerClass.Worker;

        [Header("Research (V4 §6 — Research Lab)")]
        // Knowledge/วัน ที่อาคารผลิต (สเกลตามกำลังคน×ระดับเหมือนผลผลิตอื่น · clamp ที่ maxKnowledge 100)
        // default 0 → อาคารอื่นไม่ผลิต Knowledge
        public float knowledgeProduction;

        [Header("Shelter (V4 §5)")]
        // เพดานประชากรฐานที่ตึกนี้เพิ่ม (Shelter = 10) — PopulationManager สเกลตามระดับ
        // L1/L2/L3 → +10/+30/+70 (รวมฐานเริ่มเกม 10 = 20/40/80 ตามตาราง §5 L2–L4)
        public int shelterCapacity;

        [Header("Upgrade / Fuel (V4 §7/§18 — เฟส 6)")]
        public int upgradeIronCost = 40;  // ต้นทุนอัป 1 ระดับ (×ระดับปัจจุบัน) — L1→L2, L2→L3
        public int upgradeEnergyCost = 0; // ต้นทุนพลังงานต่ออัป (×ระดับปัจจุบัน — V4 §6 เช่นโรงผลิต E150)
        public float deuteriumProduction; // ผลิต/วัน เฉพาะเมื่อถึงระดับสูงสุด (Water Plant L3 §4)
        public float tritiumProduction;   // ผลิต/วัน เฉพาะเมื่อถึงระดับสูงสุด (Zone B / Lab L3)

        [Header("Power Grid")]
        public int powerRange = 0;        // จำนวน cell รัศมีที่ปล่อยพลังงาน (0 = ผู้บริโภค)
        public bool isPowerRelay = false; // true = Power Conduit (ต้องอยู่ในระยะ source ก่อนถึงจะ relay ต่อได้)

        [Header("Ore Node (แหล่งแร่ V4 §5 — ระบบ OreDepositManager)")]
        // true = ภูมิประเทศแหล่งแร่: เหล็ก = โควตา/วัน (สุ่มใหม่ทุกเช้า) × กำลังคน
        // ไม่ใช้ ironProduction/upkeep/ระดับ · default ทั้งหมด 0 → asset อาคารเดิมไม่กระทบ
        public bool isOreNode = false;
        public float oreQuotaMin = 0f;               // โควตาขุดเหล็ก/วัน ต่ำสุด
        public float oreQuotaMax = 0f;               // โควตาขุดเหล็ก/วัน สูงสุด
        public float oreTritiumMin = 0f;             // โควตา Tritium/วัน ต่ำสุด (โซน B เท่านั้น — V4 §4 เชื้อเพลิงขั้นสูง)
        public float oreTritiumMax = 0f;             // โควตา Tritium/วัน สูงสุด
        public float oreExposurePerWorkerDay = 0f;   // >0 = โซนเสี่ยง (B): รังสีสะสม +ค่านี้ ×คนงาน ทุกจบวัน
        public float oreSickChancePerWorkerDay = 0f; // โอกาสป่วย/คน/วัน (โซน B)
    }
}
