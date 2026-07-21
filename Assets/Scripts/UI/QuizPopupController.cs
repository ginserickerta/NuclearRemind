using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// ★ SUPERSEDED (2026-07-21) by QuizCardPanelUI — the v6.3 code-built quiz popup in the crisis-card
    /// visual language auto-spawns and disables this scene-authored controller on Start, so it no longer
    /// shows. Kept compiled because MakeShuffledIndices is still covered by QuizShuffleTests and because the
    /// scene still references the component; do not delete without clearing both.
    ///
    /// Popup สำหรับ Decision Quiz — แสดงคำถาม + 3 ตัวเลือกตาม EventManager.OnQuizShown
    /// ควิซตอบบังคับ (ไม่มีปุ่มข้าม): ปุ่มยืนยันใช้ได้ต่อเมื่อเลือกคำตอบแล้ว
    /// ตอบเสร็จ → ไฮไลต์ข้อถูก(เขียว)/ข้อที่เลือกผิด(แดง) + explainText 1 ย่อหน้า + ปุ่มปิด
    /// สีหัวข้อตาม QuizCategory · เมื่อกดปิดจึง QuizManager.SubmitAnswer เพื่อให้คะแนน/ปลด Codex/คิวข้อต่อไป
    /// (เลียนโครงจาก DilemmaPopupController — ไม่ใช้ Update/Time.deltaTime เพราะระหว่างควิซ timeScale=0)
    /// </summary>
    public class QuizPopupController : MonoBehaviour, GameUIStack.IPanel
    {
        [Header("Panel")]
        public GameObject popupPanel;
        public Image categoryBar; // แถบสีหัวข้อ — เปลี่ยนสีตามหมวดควิซ (§17)

        [Header("Texts")]
        public Text topicText;     // หัวข้อสั้นมุมซ้ายบน (quiz.topicTitle) — ว่างใช้ speaker
        public Text speakerText;   // "VESTA" / "Dr. Auren Vasek"
        public Text questionText;
        public Text explainText;   // คำอธิบายความรู้ — โชว์หลังตอบ (ถูกหรือผิดก็อันเดียวกัน)

        [Header("Codex reward footer")]
        public GameObject codexFooter;  // แถบ footer (ไอคอน + ป้าย + ชื่อ) — ซ่อนถ้าควิซนี้ไม่ปลด Codex
        public Text codexRewardText;    // ชื่อ Codex ที่จะปลด (สีฟ้า) เช่น "Plasma Confinement"

        [Header("Options (3)")]
        public Button[] optionButtons; // 3 ปุ่มตัวเลือก
        public Text[] optionTexts;     // ป้ายของแต่ละปุ่ม

        [Header("Buttons")]
        public Button confirmButton; // ยืนยันคำตอบ — เปิดใช้เมื่อเลือกแล้ว
        public Button closeButton;   // ปิด — โผล่หลังตอบ

        // ===== สีไฮไลต์ =====
        private static readonly Color ColSelected = new Color(0.85f, 0.85f, 0.45f); // เลือกไว้ (ยังไม่ยืนยัน)
        private static readonly Color ColCorrect  = new Color(0.35f, 0.75f, 0.40f); // ข้อถูก (เขียว)
        private static readonly Color ColWrong    = new Color(0.80f, 0.35f, 0.35f); // ข้อที่เลือกผิด (แดง)

        // สี Outline เรืองขอบ (อ่านชัดบนแผ่นโลหะเข้ม — tint สี image คูณแล้วจมมองไม่เห็น)
        private static readonly Color GlowSelected = new Color(0.95f, 0.80f, 0.30f); // เลือกไว้ (เหลือง)
        private static readonly Color GlowCorrect  = new Color(0.35f, 0.85f, 0.45f); // ถูก (เขียว)
        private static readonly Color GlowWrong    = new Color(0.90f, 0.35f, 0.35f); // ผิด (แดง)

        private QuizQuestionSO _activeQuiz;
        private int _selectedIndex = -1;     // index ตามตำแหน่งบนจอ (แปลงกลับเป็น index จริงตอน submit)
        private bool _revealed;              // เฉลยแล้ว — กันเปลี่ยนคำตอบ
        private Color[] _baseColors;         // สีปุ่มเดิม (ไว้รีเซ็ตตอนถามข้อใหม่)
        private Outline[] _optionOutlines;   // เรืองขอบปุ่มตัวเลือก (เลือก/ถูก/ผิด) — cache ตอน Start
        private int[] _displayToOriginal;    // ตำแหน่งบนจอ → index จริงใน quiz.options (สับใหม่ทุกข้อ กันจำตำแหน่งข้อถูก)

        private void OnEnable()
        {
            EventManager.Instance.OnQuizShown += HandleQuizShown;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnQuizShown -= HandleQuizShown;
        }

        private void Start()
        {
            // ผูก onClick ของปุ่มตัวเลือกตอน runtime (ต้องส่ง index → ใช้ lambda ที่ editor ทำ persistent ไม่สะดวก)
            if (optionButtons != null)
            {
                _baseColors = new Color[optionButtons.Length];
                _optionOutlines = new Outline[optionButtons.Length];
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null) continue;
                    if (btn.image != null) _baseColors[i] = btn.image.color;
                    _optionOutlines[i] = btn.GetComponent<Outline>(); // setup ใส่ Outline ไว้ (ปิดอยู่)
                    if (_optionOutlines[i] != null) _optionOutlines[i].enabled = false;
                    int index = i; // capture ต่อ iteration
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectOption(index));
                }
            }

            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void HandleQuizShown(QuizQuestionSO quiz)
        {
            _activeQuiz = quiz;
            _selectedIndex = -1;
            _revealed = false;

            if (popupPanel != null) { UIPopIn.Ensure(popupPanel); popupPanel.SetActive(true); }
            GameUIStack.Push(this);
            // ★ v6.3: the day clock used to be paused by the legacy QuizManager, which no longer runs —
            // QuizAppliedWatcher raises OnQuizShown directly, so this panel owns the pause now.
            TimeManager.Instance?.Pause(PauseReason.QuizPopup);
            if (categoryBar != null) categoryBar.color = ColorFor(quiz.category);
            if (topicText != null)
                topicText.text = string.IsNullOrEmpty(quiz.topicTitle) ? quiz.speaker : quiz.topicTitle;
            if (speakerText != null) speakerText.text = quiz.speaker;
            if (questionText != null) questionText.text = quiz.question;

            // footer "ปลดล็อก Codex : …" — โชว์ชื่อ entry จาก codexUnlockId (ไม่มี = ซ่อนแถบ)
            string rewardName = ResolveCodexName(quiz.codexUnlockId);
            if (codexRewardText != null) codexRewardText.text = rewardName;
            if (codexFooter != null) codexFooter.SetActive(!string.IsNullOrEmpty(rewardName));

            // สับตำแหน่งตัวเลือก (V4 §12) — ข้อถูกไม่อยู่ตำแหน่งเดิมทุกครั้งที่เด้ง
            int optionCount = quiz.options != null ? quiz.options.Length : 0;
            _displayToOriginal = MakeShuffledIndices(optionCount);

            // เติมป้าย (ตามลำดับที่สับแล้ว) + รีเซ็ตสี/สถานะปุ่มตัวเลือก
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    bool hasOption = i < optionCount;
                    if (btn != null)
                    {
                        btn.gameObject.SetActive(hasOption);
                        btn.interactable = true;
                        if (btn.image != null && _baseColors != null && i < _baseColors.Length)
                            btn.image.color = _baseColors[i];
                        if (_optionOutlines != null && i < _optionOutlines.Length && _optionOutlines[i] != null)
                            _optionOutlines[i].enabled = false;
                    }
                    if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
                        optionTexts[i].text = hasOption ? quiz.options[_displayToOriginal[i]] : "";
                }
            }

            if (explainText != null)
            {
                explainText.text = "";
                explainText.gameObject.SetActive(false);
            }
            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = false; // ต้องเลือกก่อน
            }
            // ★ v6.3: the close button is now visible from the start as the skip affordance. Reusing the
            // button already wired in the scene avoids adding a new one that nobody would hook up, and a
            // locked-away skip is the same as no skip at all (rule: ควิซข้ามได้).
            SetCloseLabel("ไว้ทีหลัง — อยู่ใน Codex (กด C)");
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        /// <summary>Relabels the shared close/skip button. No-op if the scene wired no label.</summary>
        private void SetCloseLabel(string text)
        {
            if (closeButton == null) return;
            var lbl = closeButton.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = text;
        }

        /// <summary>ผู้เล่นเลือกตัวเลือก index (ผูกกับ onClick ของปุ่มแต่ละตัวตอน runtime)</summary>
        public void SelectOption(int index)
        {
            if (_revealed || _activeQuiz == null) return;
            _selectedIndex = index;

            // ไฮไลต์ข้อที่เลือก — เรืองขอบเหลือง (Outline) + tint อ่อน · ข้ออื่นกลับสภาพเดิม
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null) continue;
                    if (btn.image != null)
                        btn.image.color = (i == index)
                            ? ColSelected
                            : (_baseColors != null && i < _baseColors.Length ? _baseColors[i] : btn.image.color);
                    SetOutline(i, i == index, GlowSelected);
                }
            }

            if (confirmButton != null) confirmButton.interactable = true;
        }

        /// <summary>ยืนยันคำตอบ — เฉลย (ไฮไลต์ถูก/ผิด) + โชว์คำอธิบาย แล้วเปิดปุ่มปิด</summary>
        public void Confirm()
        {
            if (_activeQuiz == null || _selectedIndex < 0 || _revealed) return;
            _revealed = true;

            // แปลง correctIndex (index จริง) → ตำแหน่งบนจอ เพื่อไฮไลต์ให้ถูกปุ่ม
            int correct = _displayToOriginal != null
                ? System.Array.IndexOf(_displayToOriginal, _activeQuiz.correctIndex)
                : _activeQuiz.correctIndex;
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null) continue;
                    btn.interactable = false;
                    // เรืองขอบ: ถูก=เขียว · ข้อที่เลือกผิด=แดง · ที่เหลือดับ
                    if (i == correct) { SetOutline(i, true, GlowCorrect); if (btn.image != null) btn.image.color = ColCorrect; }
                    else if (i == _selectedIndex) { SetOutline(i, true, GlowWrong); if (btn.image != null) btn.image.color = ColWrong; }
                    else SetOutline(i, false, GlowSelected);
                }
            }

            if (explainText != null)
            {
                explainText.text = _activeQuiz.explainText;
                explainText.gameObject.SetActive(true);
            }
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            SetCloseLabel("ปิด"); // answered — the same button stops being a skip
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// ปิด popup — ★ v6.3: ส่งคำตอบให้ CodexQuizManager (Mastery ถาวร + ปลด Codex) ไม่ใช่ QuizManager
        /// เดิมที่ให้ Knowledge +8 ตามกติกา v4.1 ที่เลิกใช้แล้ว · ยังไม่ได้ตอบ (กด "ไว้ทีหลัง"/Esc) = ข้าม
        /// ไม่ส่งอะไรทั้งนั้น ควิซยังอยู่ใน Codex ตอบทีหลังได้ ไม่มีโทษ (QUIZZES.md)
        /// </summary>
        public void Close()
        {
            if (_activeQuiz == null) return;

            bool submitted = _revealed;
            string quizId = _activeQuiz.quizId;
            // แปลงตำแหน่งบนจอกลับเป็น index จริงใน quiz.options ก่อนตัดสินถูก/ผิด
            int answer = (_displayToOriginal != null && _selectedIndex >= 0 && _selectedIndex < _displayToOriginal.Length)
                ? _displayToOriginal[_selectedIndex]
                : _selectedIndex;

            _activeQuiz = null;
            _selectedIndex = -1;
            _revealed = false;

            if (popupPanel != null) popupPanel.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.QuizPopup);

            if (submitted) CodexQuizManager.Instance.Submit(quizId, answer);
        }

        // ── GameUIStack ──
        // ★ v6.3: Esc ปิดได้ ต่างจาก v4.1 ที่ควิซบังคับตอบ — GDD หลักการข้อ 4 + ตาราง QUIZZES.md
        // กำหนดให้ควิซ "ข้ามได้ ไม่ตอบก็เล่นต่อได้" อย่าเปลี่ยนกลับโดยไม่แก้เอกสารก่อน
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(popupPanel);
        GameObject GameUIStack.IPanel.PanelRoot => popupPanel;
        void GameUIStack.IPanel.CloseFromStack() => Close();

        /// <summary>
        /// สร้าง permutation 0..count-1 แบบ Fisher–Yates สำหรับสับตำแหน่งตัวเลือก
        /// (static + คืน array ให้เทสต์ตรวจว่าเป็น permutation ครบตัวได้)
        /// </summary>
        public static int[] MakeShuffledIndices(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            return indices;
        }

        // เปิด/ปิดเรืองขอบปุ่มตัวเลือก index (setup ใส่ Outline component ไว้แล้ว)
        private void SetOutline(int index, bool on, Color color)
        {
            if (_optionOutlines == null || index < 0 || index >= _optionOutlines.Length) return;
            var o = _optionOutlines[index];
            if (o == null) return;
            o.effectColor = color;
            o.enabled = on;
        }

        // แปลง codexUnlockId → ชื่อที่แสดง (อังกฤษก่อน ไม่งั้นไทย) จาก CodexManager (query .Instance อ่านอย่างเดียว)
        // คืน "" ถ้าควิซนี้ไม่ปลด Codex หรือหา entry ไม่พบ → footer จะถูกซ่อน
        private static string ResolveCodexName(string codexId)
        {
            if (string.IsNullOrEmpty(codexId) || CodexManager.Instance == null) return "";
            if (!CodexManager.Instance.AllEntries.TryGetValue(codexId, out var entry) || entry == null) return "";
            return !string.IsNullOrEmpty(entry.titleEn) ? entry.titleEn : entry.title;
        }

        private static Color ColorFor(QuizCategory category)
        {
            switch (category)
            {
                case QuizCategory.Reactor:     return new Color(0.20f, 0.50f, 0.85f); // น้ำเงิน
                case QuizCategory.Agriculture: return new Color(0.30f, 0.70f, 0.35f); // เขียว
                case QuizCategory.Medical:     return new Color(0.80f, 0.30f, 0.30f); // แดง
                case QuizCategory.Ethics:      return new Color(0.50f, 0.50f, 0.55f); // เทา
                default:                       return Color.white;
            }
        }
    }
}
