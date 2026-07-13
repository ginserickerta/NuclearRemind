using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// หน้า "คำอธิบายหลังตอบควิส" (mockup กรอบโลหะ) — โผล่หลังผู้เล่นตอบเสร็จ ทั้งถูกและผิด
    /// subscribe EventManager.OnQuizAnswered(quizId, correct) → resolve SO ด้วย QuizManager.GetById → Show
    ///
    /// data-driven จาก QuizQuestionSO:
    ///   • explanationTitle (ว่าง = topicTitle) → หัวกลางบน
    ///   • scoreDelta → badge มุมขวาบน: ถูก +N เขียว / ผิด -N แดง (display-only ไม่แตะ Knowledge จริง)
    ///   • explainText → เนื้อหากลาง (word-wrap + best-fit ในกล่อง)
    ///   • codexUnlockId → แถบล่าง "ปลดล็อก Codex: <titleEn>" + ไอคอน (โชว์เฉพาะตอบถูก = ปลดจริง)
    /// ปิด → กลับ flow เดิม (คืนนาฬิกาวันที่ pause ไว้) · การปลดล็อก Codex ทำที่ QuizManager (ผ่าน event) — popup แค่ "แสดง"
    /// </summary>
    public class QuizExplanationPopupController : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject popupPanel;

        [Header("Header")]
        public Text titleText;        // explanationTitle (fallback topicTitle)
        public Text scoreBadgeText;   // มุมขวาบน "+N" เขียว / "-N" แดง

        [Header("Body")]
        public Text bodyText;         // explainText

        [Header("Reward footer (Codex)")]
        public GameObject rewardBar;  // แถบล่าง — ซ่อนถ้าตอบผิด/ไม่ปลด Codex
        public Image rewardIcon;      // ไอคอน Codex (CodexEntry.illustration) — ซ่อนถ้าไม่มี
        public Text rewardText;       // "ปลดล็อก Codex: <ชื่อ>"

        [Header("Buttons")]
        public Button closeButton;

        [Header("Colors")]
        public Color correctColor = new Color(0.35f, 0.80f, 0.42f); // เขียว (ถูก)
        public Color wrongColor = new Color(0.85f, 0.35f, 0.35f);   // แดง (ผิด)

        private bool _paused;

        private void OnEnable()
        {
            EventManager.Instance.OnQuizAnswered += HandleQuizAnswered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnQuizAnswered -= HandleQuizAnswered;
        }

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void HandleQuizAnswered(string quizId, bool correct)
        {
            var quiz = QuizManager.Instance != null ? QuizManager.Instance.GetById(quizId) : null;
            if (quiz == null) return;
            Show(quiz, correct);
        }

        /// <summary>แสดงหน้าอธิบายของควิซ + ผลถูก/ผิด (public — เรียกตรงได้ในเทสต์/ดีบัก)</summary>
        public void Show(QuizQuestionSO quiz, bool wasCorrect)
        {
            if (quiz == null) return;

            if (popupPanel != null) popupPanel.SetActive(true);

            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(quiz.explanationTitle) ? quiz.topicTitle : quiz.explanationTitle;

            if (scoreBadgeText != null)
            {
                int n = Mathf.Abs(quiz.scoreDelta);
                scoreBadgeText.text = wasCorrect ? $"+{n}" : $"-{n}";
                scoreBadgeText.color = wasCorrect ? correctColor : wrongColor;
            }

            if (bodyText != null) bodyText.text = quiz.explainText;

            // แถบรางวัล Codex — โชว์เฉพาะ "ตอบถูก + ปลดจริง" (ตอบผิด = ไม่ปลด = ซ่อน)
            bool showReward = wasCorrect && !string.IsNullOrEmpty(quiz.codexUnlockId);
            string codexName = showReward ? ResolveCodexName(quiz.codexUnlockId) : "";
            showReward = showReward && !string.IsNullOrEmpty(codexName);

            if (rewardBar != null) rewardBar.SetActive(showReward);
            if (showReward)
            {
                if (rewardText != null) rewardText.text = $"ปลดล็อก Codex: {codexName}";
                if (rewardIcon != null)
                {
                    var sprite = ResolveCodexIcon(quiz.codexUnlockId);
                    rewardIcon.sprite = sprite;
                    rewardIcon.enabled = sprite != null;
                }
            }

            // หยุดนาฬิกาวันจนกว่าจะปิด (กลับ flow เดิมตอนปิด) — ไม่แตะ timeScale, UI ยังลื่น
            if (!_paused)
            {
                TimeManager.Instance?.Pause(PauseReason.QuizExplanation);
                _paused = true;
            }
        }

        /// <summary>ปิดหน้าอธิบาย → กลับ flow เดิม (คืนนาฬิกาวัน)</summary>
        public void Close()
        {
            if (popupPanel != null) popupPanel.SetActive(false);
            if (_paused)
            {
                TimeManager.Instance?.Resume(PauseReason.QuizExplanation);
                _paused = false;
            }
        }

        // codexUnlockId → ชื่อแสดง (อังกฤษก่อน ไม่งั้นไทย) จาก CodexManager (query .Instance อ่านอย่างเดียว §5)
        private static string ResolveCodexName(string codexId)
        {
            if (string.IsNullOrEmpty(codexId) || CodexManager.Instance == null) return "";
            if (!CodexManager.Instance.AllEntries.TryGetValue(codexId, out var entry) || entry == null) return "";
            return !string.IsNullOrEmpty(entry.titleEn) ? entry.titleEn : entry.title;
        }

        // codexUnlockId → ภาพประกอบ entry (null = ไม่มี → ซ่อนไอคอน)
        private static Sprite ResolveCodexIcon(string codexId)
        {
            if (string.IsNullOrEmpty(codexId) || CodexManager.Instance == null) return null;
            if (!CodexManager.Instance.AllEntries.TryGetValue(codexId, out var entry) || entry == null) return null;
            return entry.illustration;
        }
    }
}
