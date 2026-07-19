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

        [Tooltip("สไปรต์ตามระดับอัปเกรด L1/L2/L3 (index 0 = L1) — ว่าง = ใช้ sprite เดี่ยวด้านบนทุกระดับ · " +
                 "BuildingVisualSpawner สลับภาพให้เมื่ออัปเกรด")]
        public Sprite[] levelSprites;

        [Tooltip("เลื่อนตำแหน่งภาพ sprite เทียบกับกึ่งกลางช่องที่วาง (world units) — " +
                 "X = ซ้าย/ขวา, Y = ขึ้น/ลง · (0,0) = นั่งกลาง footprint พอดี · ปรับทีละหลังได้ที่นี่")]
        public Vector2 spriteOffset = Vector2.zero;

        [Tooltip("ปรับขนาดภาพ sprite ต่อหลัง (1 = ปกติ, 0.8 = เล็กลง 20%) — ไม่กระทบ footprint/การวาง")]
        public float spriteScale = 1f;

        [Tooltip("เฟรมอนิเมชัน (idle loop) — ว่าง = ภาพนิ่ง (ใช้ sprite ด้านบน) · " +
                 "มี ≥2 เฟรม → BuildingVisualSpawner ใส่ SpriteFrameAnimator วนเล่นให้อัตโนมัติ")]
        public Sprite[] animationFrames;

        [Tooltip("ความเร็วอนิเมชัน (เฟรม/วินาที) — ใช้เมื่อมี animationFrames เท่านั้น")]
        public float animationFps = 6f;

        [Header("เงา (override รายอาคาร — ลากปรับได้ที่ NuclearReMind → Tools → Shadow Editor)")]
        [Tooltip("true = ใช้ค่าเงาด้านล่างแทนค่ากลางของ BuildingVisualSpawner\n" +
                 "false = ใช้ค่ากลาง (อาคารเดิมทุกหลังไม่กระทบ)")]
        public bool overrideShadow = false;

        [Tooltip("เลื่อนตำแหน่งเงา เทียบฐานอาคาร (world units) — X ซ้าย/ขวา · Y ขึ้น/ลง")]
        public Vector2 shadowOffset = new Vector2(0f, -0.02f);

        [Tooltip("ความยาวเงา เทียบความสูงอาคาร (0.42 = 42% ของตัวอาคาร)")]
        [Range(0.05f, 1.2f)] public float shadowSquash = 0.42f;

        [Tooltip("เอียงเงาตามทิศแสง (องศา) — ลบ = เอียงขวา · บวก = เอียงซ้าย")]
        [Range(-70f, 70f)] public float shadowLeanDegrees = 0f;

        [Tooltip("ความเข้มเงา 0 = ใส · 1 = ดำทึบ")]
        [Range(0f, 1f)] public float shadowAlpha = 0.30f;

        [Header("Depth-sort base collider (override)")]
        [Tooltip("ใช้กล่องฐานที่กำหนดเองแทนการคำนวณอัตโนมัติ (depthBaseFraction ที่ BuildingVisualSpawner)\n" +
                 "worker ที่เดินทับ 'โซนฐาน' นี้ + อยู่หน้า จะวาดเหนืออาคาร · ปรับด้วยเมนู NuclearReMind → Tools → Base Collider Editor (ลาก handle ใน Scene)")]
        public bool overrideBaseCollider = false;
        [Tooltip("ขนาดกล่องฐาน (world units, สเกลสไปรต์ปกติ = local ของสไปรต์) — ใช้เมื่อ overrideBaseCollider = true")]
        public Vector2 baseColliderSize = Vector2.zero;
        [Tooltip("ตำแหน่งกึ่งกลางกล่องฐาน เทียบ pivot สไปรต์ (world units) — ใช้เมื่อ overrideBaseCollider = true")]
        public Vector2 baseColliderOffset = Vector2.zero;

        [Header("Depth-sort worker collider (override)")]
        [Tooltip("กล่องคนละชุดกับ base/top ด้านบน — ชุดนั้นตัดสิน 'อาคารทับอาคาร' ชุดนี้ตัดสิน 'worker ทับอาคาร' อย่างเดียว\n" +
                 "แยกกันเพราะกล่องเดียวรับสองหน้าที่ไม่ได้: จูนให้อาคารถูก worker จะพัง และกลับกัน\n" +
                 "worker base = แถบพื้นเตี้ย ๆ · เท้าต่ำกว่ากึ่งกลางแถบนี้ = ยืนหน้าอาคาร → วาดทับ\n" +
                 "worker top  = โซนที่ 'ยืนตรงนี้ = อยู่หลังอาคาร' → ไม่ยกขึ้นมาไม่ว่าเท้าจะต่ำแค่ไหน\n" +
                 "ปรับด้วยเมนู NuclearReMind → Tools → Base Collider Editor (สลับกล่องที่แก้ได้ในนั้น)")]
        public bool overrideWorkerCollider = false;
        public Vector2 workerBaseColliderSize = Vector2.zero;
        public Vector2 workerBaseColliderOffset = Vector2.zero;
        public Vector2 workerTopColliderSize = Vector2.zero;
        public Vector2 workerTopColliderOffset = Vector2.zero;

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

        [Tooltip("คนงานสูงสุด (= เพดานผลิต) ต่อเลเวล L1/L2/L3 ตาม GDD §6 — เว้นว่างไว้จะใช้ workerRequired ทุกเลเวล\n" +
                 "เช่น โรงไฟ/น้ำ/อาหาร = 1,2,3 · เหมือง = 2,2,3 · ผลิตแปรผันตรงกับคน cap ที่ค่านี้ (กันเฟ้อ)")]
        public int[] workersPerLevel;

        // เฟสที่อาคารปลดล็อกให้กดวางได้ (GDD §6 คอลัมน์ "ปลดล็อก" · GamePhase.FromDay)
        // default 1 = วางได้ตั้งแต่วันแรก → asset เดิมทุกตัวไม่กระทบ
        public int unlockPhase = 1;

        // v6.3 §19: ต้องวิจัย note นี้เสร็จก่อนถึงจะวางได้ (Extractor ← deuterium, Zone B ← tritium, ...)
        // "" = ไม่ต้องวิจัย (อาคารผลิตพื้นฐาน) → asset เดิมทุกตัวไม่กระทบ · gate ที่ KnowledgeDB.IsBuildingUnlocked
        public string requiredNoteId = "";

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
        public float deuteriumProduction; // ผลิต/วัน ที่ระดับสูงสุด (Water Plant L3 §4)
        public float tritiumProduction;   // ผลิต/วัน เฉพาะเมื่อถึงระดับสูงสุด (Zone B / Lab L3)

        // ResearchLab_System_Spec: extraction unlocks from RESEARCH, not from hitting max level — but the
        // plant still has to be big enough. These two make the early tier data instead of code: set
        // deuteriumMinLevel = 2 and the plant can extract from L2 at the lower rate below.
        // 0 = the old behaviour (max level only), so every other building is unaffected.
        public int deuteriumMinLevel = 0;
        public float deuteriumProductionMinLevel = 0f; // ผลิต/วัน ตั้งแต่ deuteriumMinLevel ถึงก่อนระดับสูงสุด

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

        /// <summary>
        /// คนงานสูงสุด (= เพดานผลิต) ที่เลเวลนี้ — อ่านจาก workersPerLevel ถ้าตั้งไว้ ไม่งั้น fallback workerRequired
        /// เพดานนี้คือกลไก "กันเฟ้อ": ผลิตแปรผันตรงกับคน assigned/เพดาน คนเกินเพดานไม่เพิ่มผลผลิต
        /// </summary>
        public int WorkersForLevel(int level)
        {
            if (workersPerLevel != null && workersPerLevel.Length > 0)
            {
                int idx = Mathf.Clamp(level - 1, 0, workersPerLevel.Length - 1);
                return Mathf.Max(0, workersPerLevel[idx]);
            }
            return Mathf.Max(0, workerRequired);
        }

        /// <summary>
        /// สไปรต์สำหรับระดับอัปเกรด level (1-based) — ใช้ levelSprites[level-1] ถ้ามี
        /// ไม่งั้น fallback เป็น sprite เดี่ยว (อาคารที่ยังไม่มี art แยกระดับ)
        /// </summary>
        public Sprite SpriteForLevel(int level)
        {
            if (levelSprites != null && levelSprites.Length > 0)
            {
                int idx = Mathf.Clamp(level - 1, 0, levelSprites.Length - 1);
                if (levelSprites[idx] != null) return levelSprites[idx];
            }
            return sprite;
        }
    }
}
