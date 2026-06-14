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
        public float choiceA_TrustChange;
        public int choiceA_AethonRelationChange;
        public int choiceA_KeranRelationChange;

        [Header("Choice B Consequences")]
        public float choiceB_FoodChange;
        public float choiceB_TrustChange;
        public int choiceB_AethonRelationChange;
        public int choiceB_KeranRelationChange;

        [Header("Trigger")]
        public string triggerCondition; // "phase_1_complete" | "trust_below_40" | etc.
    }
}
