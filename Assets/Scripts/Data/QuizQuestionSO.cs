using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คำถามควิซหนึ่งข้อ (ScriptableObject) — V4 §12 (เนื้อหา) + §16 (ฟิลด์)
    /// ตอบถูก +rewardKnowledge (8) / ตอบผิด +3 Knowledge · explainText แสดงทั้งตอบถูกและตอบผิด
    /// ถ้า codexUnlockId ไม่ว่าง → ปลดล็อก CodexEntry นั้นตอนตอบ (ดู QuizManager.SubmitAnswer)
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuizQuestion", menuName = "NuclearReMind/QuizQuestion")]
    public class QuizQuestionSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;                 // "Q1".."Q10"
        public QuizCategory category;     // กำหนดไอคอน/สีของ popup (§17)

        [Header("Question")]
        public string topicTitle;         // หัวข้อสั้น เช่น "ทำไมพลาสมาถึงพัง" (โชว์มุมซ้ายบนของ popup) · ว่าง = ใช้ speaker แทน
        [TextArea]
        public string question;
        public string[] options;          // 3 ตัวเลือก
        public int correctIndex;          // index ของตัวเลือกที่ถูก

        [Header("Reward / Explanation")]
        public int rewardKnowledge = 8;   // ถูก +8 · ผิด +3 (ดู explainText)
        [TextArea]
        public string explainText;        // แสดงทั้งตอบถูกและตอบผิด
        public string speaker;            // "VESTA" / "Dr. Auren Vasek" / ...
        public string codexUnlockId;      // CodexEntry.entryId ที่จะปลด · "" = ไม่ปลด (V4 §16 / Gap G1)

        [Header("Explanation Popup (หน้าอธิบายหลังตอบ — QuizExplanationPopupController)")]
        public string explanationTitle;   // หัวหน้าอธิบาย (mockup "คำอธิบาย X") · ว่าง = ใช้ topicTitle
        public int scoreDelta = 8;        // badge มุมขวาบน: ถูก +N เขียว / ผิด -N แดง (display-only · ไม่แตะ Knowledge จริง)
    }
}
