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
        public bool unlocksMedicTraining;    // Hospital → ฝึก Medic ได้

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
    }
}
