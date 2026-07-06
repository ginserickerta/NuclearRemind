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
        [TextArea(2, 6)]
        public string choiceA_AfterText;         // บทหลังเลือก (Story Guide: [ระบบ]/NPC/ความคิด — ว่าง = ข้าม)

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
        [TextArea(2, 6)]
        public string choiceB_AfterText;

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
        [TextArea(2, 6)]
        public string choiceC_AfterText;

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

        [Header("Per-choice Quizzes (Story Guide — quizRef รายทางเลือก)")]
        // ควิซเฉพาะทางเลือก: ถ้าทางเลือกที่ผู้เล่นกดมีรายการของตัวเอง จะใช้แทน linkedQuizIds
        // (เช่น วิกฤตอาหาร: A → mutation_breeding, B → food_irradiation, C → ไม่มีควิซ)
        // ว่าง/ไม่กำหนด = fallback ไป linkedQuizIds ตามพฤติกรรมเดิม
        public string[] choiceA_QuizIds;
        public string[] choiceB_QuizIds;
        public string[] choiceC_QuizIds;

        /// <summary>
        /// IQuizTrigger: แปลง linkedQuizIds → QuizQuestionSO[] (ข้าม id ที่หาไม่เจอ/ว่าง)
        /// QuizManager.EnqueueQuizzes(this) เรียกเมธอดนี้เพื่อเอาควิซที่ผูกไว้เข้าคิว
        /// </summary>
        public QuizQuestionSO[] GetLinkedQuizzes() => MapIdsToQuizzes(linkedQuizIds);

        /// <summary>
        /// id ควิซของทางเลือกที่ผู้เล่นกด (0=A/1=B/2=C) — Story Guide quizRef รายทางเลือก
        /// ทางเลือกไม่มีรายการของตัวเอง (ว่าง/null) หรือ index นอกช่วง → fallback ไป linkedQuizIds
        /// เป็น pure function ให้เทสต์ตรวจ logic ได้โดยไม่ต้องมี QuizManager
        /// </summary>
        public string[] GetQuizIdsForChoice(int choiceIndex)
        {
            string[] perChoice = choiceIndex switch
            {
                0 => choiceA_QuizIds,
                1 => choiceB_QuizIds,
                2 => choiceC_QuizIds,
                _ => null,
            };
            return (perChoice != null && perChoice.Length > 0) ? perChoice : linkedQuizIds;
        }

        /// <summary>ควิซของทางเลือกที่กด (map id → SO) — DilemmaManager ใช้ตอน resolve</summary>
        public QuizQuestionSO[] GetLinkedQuizzesForChoice(int choiceIndex)
            => MapIdsToQuizzes(GetQuizIdsForChoice(choiceIndex));

        /// <summary>บทหลังเลือกของทางเลือกที่กด (0=A/1=B/2=C) — ว่าง/นอกช่วง = ไม่มี</summary>
        public string GetAfterText(int choiceIndex) => choiceIndex switch
        {
            0 => choiceA_AfterText,
            1 => choiceB_AfterText,
            2 => choiceC_AfterText,
            _ => null,
        };

        private static QuizQuestionSO[] MapIdsToQuizzes(string[] ids)
        {
            if (ids == null || ids.Length == 0)
                return System.Array.Empty<QuizQuestionSO>();

            var quizzes = new List<QuizQuestionSO>(ids.Length);
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var quiz = QuizManager.Instance != null ? QuizManager.Instance.GetById(id) : null;
                if (quiz != null) quizzes.Add(quiz);
            }
            return quizzes.ToArray();
        }
    }
}
