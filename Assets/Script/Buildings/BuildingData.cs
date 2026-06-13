using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ต้นทุนทรัพยากรสำหรับการก่อสร้างหรือ Tick ของอาคาร
    /// </summary>
    [System.Serializable]
    public class ResourceCost
    {
        public int materials;
        public int energy;
        public int food;
        public int water;
        public int radiationProtection;
        public int workers;
    }

    /// <summary>
    /// ข้อมูลของอาคารแต่ละชนิด ใช้สำหรับ Building Placement System
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingData", menuName = "NuclearReMind/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("ข้อมูลพื้นฐาน")]
        public string buildingName;
        public Sprite icon;
        public BuildingType buildingType;

        [Header("ขนาด (จำนวน cell ที่ครอบคลุม)")]
        public Vector2Int size = Vector2Int.one;

        [Header("ต้นทุนทรัพยากร")]
        public ResourceCost cost;

        [Header("คำอธิบาย")]
        [TextArea]
        public string description;
    }
}
