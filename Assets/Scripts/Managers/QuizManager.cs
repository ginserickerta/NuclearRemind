using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการควิซ 10 ข้อ (V4 §12/§16 — D6): หัวใจคะแนน NSC (Quiz/Knowledge)
    /// ตอบบังคับ ข้ามไม่ได้ · ถูก → +rewardKnowledge (ปกติ 8) · ผิด → +3 (ผ่าน RaiseResourceDelta(Knowledge,n))
    /// กันถามซ้ำด้วย _answered · เข้าคิวหลายข้อผ่าน _pending (เช่นวิกฤตผูก 2 ข้อ)
    /// ระบบอื่นสั่งเด้งควิซผ่าน EnqueueQuizzes(IQuizTrigger) หรือ TriggerByIds("Q8","Q9")
    /// </summary>
    public class QuizManager : MonoBehaviour
    {
        public static QuizManager Instance { get; private set; }

        [Header("Quiz Content — ใส่ QuizQuestionSO assets ทั้งหมดที่นี่ (เมนู Setup Quiz System)")]
        public QuizQuestionSO[] allQuizzes;

        // รางวัล Knowledge เมื่อตอบผิด (ตอบถูกใช้ rewardKnowledge ของแต่ละข้อ — ปกติ 8)
        private const float WrongAnswerKnowledge = 3f;

        private readonly HashSet<string> _answered = new HashSet<string>();   // กันถามซ้ำ (G5/G7 — in-memory เท่านั้น)
        private readonly Queue<QuizQuestionSO> _pending = new Queue<QuizQuestionSO>();
        private QuizQuestionSO _current;                                       // ควิซที่กำลังแสดง (null = ไม่มี)

        // Q1 (สกัด Deuterium ครั้งแรก) ไม่ยิงจากที่นี่แล้ว — ย้ายไป StoryBeat "deuterium_ignition"
        // (StoryDirector บังคับลำดับ InfoCard/Record ก่อนควิซตามกฎเหล็ก Story Guide — ความรู้มาก่อนควิซ)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>คืน QuizQuestionSO จาก id (เช่น "Q4") — ใช้โดย DilemmaData/สคริปต์อื่น · scan ตรง allQuizzes (N≤10)</summary>
        public QuizQuestionSO GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || allQuizzes == null) return null;

            foreach (var quiz in allQuizzes)
                if (quiz != null && quiz.id == id)
                    return quiz;

            return null;
        }

        /// <summary>เคยตอบข้อนี้ไปแล้วหรือยัง (กันถามซ้ำ — G7)</summary>
        public bool AlreadyAnswered(string id) => _answered.Contains(id);

        /// <summary>เข้าคิวควิซทั้งหมดที่ trigger ผูกไว้ (วิกฤต/decree/ignition ที่ implement IQuizTrigger)</summary>
        // ═══════════════════════════════════════════════════════════════════════════════
        // ★ v6.3 — คิวควิซบังคับถูกปิดถาวร (RETIRED)
        //
        // GDD หลักการข้อ 4 + ตาราง QUIZZES.md: ควิซ "ไม่บังคับ อยู่ Codex หลังใช้จริง" · ควิซชุดนี้
        // (Q1–Q10, QT1, QT2) เป็นของ v4.1 และทุกใบมี quizId ว่าง จึงส่งให้ CodexQuizManager ไม่ได้ —
        // ปล่อยไว้จะเด้งคำถามที่ตอบแล้วไม่ได้รางวัลอะไรเลยแบบเงียบๆ
        //
        // ที่ต้องปิดถึงระดับนี้ ไม่ใช่แค่ลบ call site: ShowNext() เรียก Resume(PauseReason.QuizPopup)
        // ทุกครั้งที่คิวหมด ซึ่งเป็น pause reason ตัวเดียวกับที่ QuizPopupController ใช้ตอนนี้ — ถ้ามีใคร
        // (เช่นโค้ดใน _archive) เผลอเรียกเข้ามา นาฬิกาวันจะเดินต่อทั้งที่ควิซยังค้างจอ
        // ═══════════════════════════════════════════════════════════════════════════════

        public void EnqueueQuizzes(IQuizTrigger trigger) { }

        public void TriggerByIds(params string[] ids) { }

        /// <summary>
        /// ผู้เล่นตอบควิซข้อปัจจุบัน (ตอบบังคับ ข้ามไม่ได้ — V4 §12)
        /// ถูก → +rewardKnowledge + ปลด Codex ตาม codexUnlockId (ผ่าน EventManager) · ผิด → +3 · ไม่ปลด Codex
        /// ไม่ว่าถูกหรือผิดก็ถือว่าตอบแล้ว → กันถามซ้ำ + เดินคิวข้อถัดไป/คืนเวลา · หน้าอธิบายโผล่ผ่าน OnQuizAnswered
        /// </summary>
        public bool SubmitAnswer(int selectedIndex)
        {
            if (_current == null) return false;

            var quiz = _current;
            bool correct = selectedIndex == quiz.correctIndex;

            _answered.Add(quiz.id);

            float reward = correct ? quiz.rewardKnowledge : WrongAnswerKnowledge;
            EventManager.Instance.RaiseResourceDelta(ResourceType.Knowledge, reward);

            // ปลด Codex ผ่าน EventManager — เฉพาะ "ตอบถูก" (mockup หน้าอธิบาย: ผิด→ไม่ปลด · ไม่เรียก CodexManager ตรง §5)
            // ตอบผิดยังเห็นคำอธิบายได้ (QuizExplanationPopupController) แต่ไม่ได้ปลดล็อก entry
            if (correct && !string.IsNullOrEmpty(quiz.codexUnlockId))
                EventManager.Instance.RaiseCodexUnlockRequested(quiz.codexUnlockId);

            EventManager.Instance.RaiseQuizAnswered(quiz.id, correct);

            // เดินคิวข้อถัดไป (หรือคืนเวลาเมื่อหมดคิว)
            ShowNext();
            return correct;
        }

        /// <summary>เข้าคิว 1 ข้อ — ข้ามข้อที่ตอบไปแล้ว/กำลังแสดง/ซ้ำในคิว (กันถามซ้ำ — G7)</summary>
        private void Enqueue(QuizQuestionSO quiz)
        {
            if (quiz == null) return;
            if (_answered.Contains(quiz.id)) return;
            if (_current != null && _current.id == quiz.id) return;
            if (_pending.Contains(quiz)) return;

            _pending.Enqueue(quiz);
        }

        /// <summary>
        /// แสดงควิซข้อถัดไปในคิว หรือถ้าหมดคิว → คืนเวลาให้เกมเดินต่อ
        /// หยุด "นาฬิกาวัน" ผ่าน TimeManager.Pause(QuizPopup) — ไม่แตะ timeScale (UI ยังทำงาน, V4 §15/§17)
        /// </summary>
        private void ShowNext()
        {
            // ดึงข้อถัดไปที่ยังไม่เคยตอบ (กันถามซ้ำ — G7)
            QuizQuestionSO next = null;
            while (_pending.Count > 0)
            {
                var quiz = _pending.Dequeue();
                if (quiz == null || _answered.Contains(quiz.id)) continue;
                next = quiz;
                break;
            }

            if (next == null)
            {
                // หมดคิว → ปิดควิซ + คืนเวลา
                _current = null;
                TimeManager.Instance?.Resume(PauseReason.QuizPopup);
                return;
            }

            _current = next;
            TimeManager.Instance?.Pause(PauseReason.QuizPopup); // หยุดนาฬิกาวันระหว่างตอบควิซ
            EventManager.Instance.RaiseQuizShown(_current);
        }
    }
}
