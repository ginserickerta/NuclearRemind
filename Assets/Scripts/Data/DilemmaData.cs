using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Moral Dilemma แต่ละสถานการณ์ (ScriptableObject, V4 §10) — 3 ทางเลือก A/B/C
    /// ผลกระทบ: Food/Energy/Water/Iron/Hope + ความสัมพันธ์ Aethon/Keran + สั่งเตา Idle (วิกฤต 2·B)
    /// implement IQuizTrigger (V4 §16): หลังเลือก → DilemmaManager เอา linkedQuizIds เข้าคิวถามควิซ
    /// </summary>
    [CreateAssetMenu(fileName = "NewDilemma", menuName = "NuclearReMind/Dilemma")]
    public class DilemmaData : ScriptableObject, IQuizTrigger
    {
        public string dilemmaId;

        [TextArea(3, 10)]
        public string scenarioText;
        public string choiceAText;
        public string choiceBText;
        public string choiceCText;

        [Header("Choice A Consequences")]
        public float choiceA_FoodChange;
        public float choiceA_EnergyChange;
        public float choiceA_WaterChange;
        public float choiceA_IronChange;
        public float choiceA_HopeChange;
        public int choiceA_AethonRelationChange;
        public int choiceA_KeranRelationChange;
        public int choiceA_ForceReactorIdleDays; // วิกฤต 2·B: บังคับเตา Idle N วัน (ผลิตไอโซโทป)
        public int choiceA_Deaths;               // คนตายจากทางเลือกนี้ (V4 §9/§10 — Hope −5/คน)

        [Header("Choice B Consequences")]
        public float choiceB_FoodChange;
        public float choiceB_EnergyChange;
        public float choiceB_WaterChange;
        public float choiceB_IronChange;
        public float choiceB_HopeChange;
        public int choiceB_AethonRelationChange;
        public int choiceB_KeranRelationChange;
        public int choiceB_ForceReactorIdleDays;
        public int choiceB_Deaths;

        [Header("Choice C Consequences")]
        public float choiceC_FoodChange;
        public float choiceC_EnergyChange;
        public float choiceC_WaterChange;
        public float choiceC_IronChange;
        public float choiceC_HopeChange;
        public int choiceC_AethonRelationChange;
        public int choiceC_KeranRelationChange;
        public int choiceC_ForceReactorIdleDays;
        public int choiceC_Deaths;

        [Header("Trigger")]
        // exact: "phase_1_complete" | "hope_below_30"
        // day-end (ประเมินตอน OnDayEnded): "heat_above_80" | "food_below_120" | "food_above_500"
        //   | "energy_below_100" | "water_below_80" | "day_reached_20" | "q_above_0.3" (Q = CORE%/100)
        // เชื่อมหลายเงื่อนไขแบบ "อย่างใดอย่างหนึ่ง" ด้วย | เช่น "heat_above_80|q_above_0.3" (วิกฤต 1 §10)
        public string triggerCondition;

        [Header("Linked Quizzes (V4 §16)")]
        // id ของ QuizQuestionSO ที่จะเข้าคิวถามหลัง resolve dilemma (เช่น Q2, Q3)
        // wire โดย QuizSetup — map เป็น SO จริงผ่าน QuizManager.GetById ตอน GetLinkedQuizzes()
        public string[] linkedQuizIds;

        /// <summary>
        /// IQuizTrigger: แปลง linkedQuizIds → QuizQuestionSO[] (ข้าม id ที่หาไม่เจอ/ว่าง)
        /// QuizManager.EnqueueQuizzes(this) เรียกเมธอดนี้เพื่อเอาควิซที่ผูกไว้เข้าคิว
        /// </summary>
        public QuizQuestionSO[] GetLinkedQuizzes()
        {
            if (linkedQuizIds == null || linkedQuizIds.Length == 0)
                return System.Array.Empty<QuizQuestionSO>();

            var quizzes = new List<QuizQuestionSO>(linkedQuizIds.Length);
            foreach (var id in linkedQuizIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var quiz = QuizManager.Instance != null ? QuizManager.Instance.GetById(id) : null;
                if (quiz != null) quizzes.Add(quiz);
            }
            return quizzes.ToArray();
        }
    }
}
