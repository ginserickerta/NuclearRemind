using UnityEngine;

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
        public int materialCost;
        public int energyCost;
        public int workerRequired;

        [Header("Production (per tick)")]
        public float foodProduction;
        public float waterProduction;
        public float radiationProtectionBonus;
        public float energyProduction;
        public int researchPointsPerTick;

        [Header("CORE TOWER")]
        public bool isCoreTowerPart;
        public int towerPhaseRequired; // 0 = all phases
    }
}
