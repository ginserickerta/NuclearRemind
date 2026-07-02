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
        public void EnqueueQuizzes(IQuizTrigger trigger)
        {
            if (trigger == null) return;

            var quizzes = trigger.GetLinkedQuizzes();
            if (quizzes == null) return;

            foreach (var quiz in quizzes)
                Enqueue(quiz);

            // ถ้ายังไม่มีข้อไหนแสดงอยู่ → เริ่มข้อแรก
            if (_current == null)
                ShowNext();
        }

        /// <summary>เข้าคิวควิซตาม id โดยตรง (ฮุกสำหรับ trigger ที่ยัง stub เช่น TriggerByIds("Q8","Q9"))</summary>
        public void TriggerByIds(params string[] ids)
        {
            if (ids == null) return;

            foreach (var id in ids)
                Enqueue(GetById(id));

            if (_current == null)
                ShowNext();
        }

        /// <summary>
        /// ผู้เล่นตอบควิซข้อปัจจุบัน (ตอบบังคับ ข้ามไม่ได้ — V4 §12)
        /// ถูก → +rewardKnowledge · ผิด → +3 · ปลด Codex ตาม codexUnlockId ถ้ามี (ทั้งถูกและผิด — D6/G6)
        /// ไม่ว่าถูกหรือผิดก็ถือว่าตอบแล้ว → กันถามซ้ำ + เดินคิวข้อถัดไป/คืนเวลา
        /// </summary>
        public bool SubmitAnswer(int selectedIndex)
        {
            if (_current == null) return false;

            var quiz = _current;
            bool correct = selectedIndex == quiz.correctIndex;

            _answered.Add(quiz.id);

            float reward = correct ? quiz.rewardKnowledge : WrongAnswerKnowledge;
            EventManager.Instance.RaiseResourceDelta(ResourceType.Knowledge, reward);

            // ปลด Codex เมื่อมีลิงก์ — เห็น explain แล้วทั้งถูก/ผิด (D6/G6)
            // codexUnlockId ว่าง → UnlockById เป็น no-op (ปัจจุบันทั้ง 10 ข้อมี id ครบ — G1 ปิดแล้ว, fusion codex 5 ตัวอยู่บนดิสก์)
            if (!string.IsNullOrEmpty(quiz.codexUnlockId))
                CodexManager.Instance?.UnlockById(quiz.codexUnlockId);

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
