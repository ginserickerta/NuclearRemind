using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Moral Dilemma แต่ละสถานการณ์ (ScriptableObject) - 2 ทางเลือกพร้อมผลกระทบต่อ
    /// Food/Trust/ความสัมพันธ์กับ Aethon และ Keran
    /// </summary>
    [CreateAssetMenu(fileName = "NewDilemma", menuName = "NuclearReMind/Dilemma")]
    public class DilemmaData : ScriptableObject
    {
        public string dilemmaId;

        [TextArea(3, 10)]
        public string scenarioText;
        public string choiceAText;
        public string choiceBText;

        [Header("Choice A Consequences")]
        public float choiceA_FoodChange;
        public float choiceA_EnergyChange;
        public float choiceA_WaterChange;
        public float choiceA_TrustChange;
        public int choiceA_AethonRelationChange;
        public int choiceA_KeranRelationChange;

        [Header("Choice B Consequences")]
        public float choiceB_FoodChange;
        public float choiceB_EnergyChange;
        public float choiceB_WaterChange;
        public float choiceB_TrustChange;
        public int choiceB_AethonRelationChange;
        public int choiceB_KeranRelationChange;

        [Header("Trigger")]
        // exact: "phase_1_complete" | "trust_below_40"
        // day-end (ประเมินตอน OnDayEnded): "heat_above_70" | "food_below_120" | "food_above_500"
        //   | "energy_below_100" | "water_below_80" | "day_reached_18"
        public string triggerCondition;
    }
}
