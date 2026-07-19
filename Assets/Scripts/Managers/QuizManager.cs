using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ★ v6.3 — RETIRED. เหลือไว้เป็น "แคตตาล็อกควิซชุดเก่า" อย่างเดียว ไม่ใช่ระบบควิซอีกต่อไป
    ///
    /// เดิมเป็นระบบควิซบังคับของ v4.1: เด้งคำถามขวางจอ ตอบถูก +8 Knowledge ตอบผิด +3 ข้ามไม่ได้
    /// GDD หลักการข้อ 4 + ตาราง QUIZZES.md เปลี่ยนกติกาไปแล้ว — ควิซ "ไม่บังคับ อยู่ Codex หลังใช้จริง"
    /// รางวัลเป็น Mastery ถาวร ไม่ใช่ Knowledge · ระบบจริงคือ CodexQuizManager + QuizAppliedWatcher
    ///
    /// ที่ยังไม่ลบทั้งคลาส เพราะ:
    ///   • component นี้วางอยู่ในซีน และ QuizSetup (editor) ยัง wire allQuizzes ให้
    ///   • DilemmaData.GetLinkedQuizzes() ยังเรียก GetById() แปลง id → SO
    ///
    /// ที่ลบออกแล้ว (อย่าเอากลับโดยไม่แก้เอกสารก่อน):
    ///   • SubmitAnswer — เศรษฐกิจ Knowledge +8/+3 ของ v4.1 ที่เลิกใช้แล้ว
    ///   • ShowNext — เรียก Resume(PauseReason.QuizPopup) ทุกครั้งที่คิวหมด ซึ่งเป็น pause reason
    ///     ตัวเดียวกับที่ QuizPopupController ใช้ตอนนี้ → ปล่อยไว้ นาฬิกาวันจะเดินต่อทั้งที่ควิซค้างจอ
    /// </summary>
    public class QuizManager : MonoBehaviour
    {
        public static QuizManager Instance { get; private set; }

        [Header("Quiz Content — ใส่ QuizQuestionSO assets ทั้งหมดที่นี่ (เมนู Setup Quiz System)")]
        public QuizQuestionSO[] allQuizzes;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>คืน QuizQuestionSO จาก id (เช่น "Q4") — ใช้โดย DilemmaData · scan ตรง allQuizzes (N≤12)</summary>
        public QuizQuestionSO GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || allQuizzes == null) return null;

            foreach (var quiz in allQuizzes)
                if (quiz != null && quiz.id == id)
                    return quiz;

            return null;
        }

        // ── คิวควิซบังคับ: ปิดถาวร แต่ยังต้องคงลายเซ็นไว้ ──
        //
        // โค้ดใน Assets/Scripts/Narrative/_archive (DilemmaManager, StoryDirector) ยังเรียกสองเมธอดนี้อยู่
        // และ Unity คอมไพล์ _archive ด้วย (มันอยู่ใต้ Assets/Scripts) — ลบทิ้งแล้ว build พังทันที
        //
        // ทำเป็น no-op ไม่ใช่แค่ "ไม่มีใครเรียก": ShowNext() เดิมเรียก Resume(PauseReason.QuizPopup)
        // ทุกครั้งที่คิวหมด ซึ่งเป็น pause reason ตัวเดียวกับที่ QuizPopupController ใช้ตอนนี้ →
        // ถ้าโค้ด archive เผลอยิงเข้ามา นาฬิกาวันจะเดินต่อทั้งที่ควิซยังค้างจอ
        public void EnqueueQuizzes(IQuizTrigger trigger) { }

        public void TriggerByIds(params string[] ids) { }
    }
}
