using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Popup สำหรับ Decision Quiz — แสดงคำถาม + 3 ตัวเลือกตาม EventManager.OnQuizShown
    /// ควิซตอบบังคับ (ไม่มีปุ่มข้าม): ปุ่มยืนยันใช้ได้ต่อเมื่อเลือกคำตอบแล้ว
    /// ตอบเสร็จ → ไฮไลต์ข้อถูก(เขียว)/ข้อที่เลือกผิด(แดง) + explainText 1 ย่อหน้า + ปุ่มปิด
    /// สีหัวข้อตาม QuizCategory · เมื่อกดปิดจึง QuizManager.SubmitAnswer เพื่อให้คะแนน/ปลด Codex/คิวข้อต่อไป
    /// (เลียนโครงจาก DilemmaPopupController — ไม่ใช้ Update/Time.deltaTime เพราะระหว่างควิซ timeScale=0)
    /// </summary>
    public class QuizPopupController : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject popupPanel;
        public Image categoryBar; // แถบสีหัวข้อ — เปลี่ยนสีตามหมวดควิซ (§17)

        [Header("Texts")]
        public Text speakerText;   // "VESTA" / "Dr. Auren Vasek"
        public Text questionText;
        public Text explainText;   // คำอธิบายความรู้ — โชว์หลังตอบ (ถูกหรือผิดก็อันเดียวกัน)

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

        private QuizQuestionSO _activeQuiz;
        private int _selectedIndex = -1;
        private bool _revealed;              // เฉลยแล้ว — กันเปลี่ยนคำตอบ
        private Color[] _baseColors;         // สีปุ่มเดิม (ไว้รีเซ็ตตอนถามข้อใหม่)

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
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null) continue;
                    if (btn.image != null) _baseColors[i] = btn.image.color;
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

            if (popupPanel != null) popupPanel.SetActive(true);
            if (categoryBar != null) categoryBar.color = ColorFor(quiz.category);
            if (speakerText != null) speakerText.text = quiz.speaker;
            if (questionText != null) questionText.text = quiz.question;

            // เติมป้าย + รีเซ็ตสี/สถานะปุ่มตัวเลือก
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    bool hasOption = quiz.options != null && i < quiz.options.Length;
                    if (btn != null)
                    {
                        btn.gameObject.SetActive(hasOption);
                        btn.interactable = true;
                        if (btn.image != null && _baseColors != null && i < _baseColors.Length)
                            btn.image.color = _baseColors[i];
                    }
                    if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
                        optionTexts[i].text = hasOption ? quiz.options[i] : "";
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
            if (closeButton != null) closeButton.gameObject.SetActive(false);
        }

        /// <summary>ผู้เล่นเลือกตัวเลือก index (ผูกกับ onClick ของปุ่มแต่ละตัวตอน runtime)</summary>
        public void SelectOption(int index)
        {
            if (_revealed || _activeQuiz == null) return;
            _selectedIndex = index;

            // ไฮไลต์ข้อที่เลือก (เหลือง) — ข้ออื่นกลับเป็นสีเดิม
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null || btn.image == null) continue;
                    btn.image.color = (i == index)
                        ? ColSelected
                        : (_baseColors != null && i < _baseColors.Length ? _baseColors[i] : btn.image.color);
                }
            }

            if (confirmButton != null) confirmButton.interactable = true;
        }

        /// <summary>ยืนยันคำตอบ — เฉลย (ไฮไลต์ถูก/ผิด) + โชว์คำอธิบาย แล้วเปิดปุ่มปิด</summary>
        public void Confirm()
        {
            if (_activeQuiz == null || _selectedIndex < 0 || _revealed) return;
            _revealed = true;

            int correct = _activeQuiz.correctIndex;
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    var btn = optionButtons[i];
                    if (btn == null) continue;
                    btn.interactable = false;
                    if (btn.image == null) continue;
                    if (i == correct) btn.image.color = ColCorrect;
                    else if (i == _selectedIndex) btn.image.color = ColWrong;
                }
            }

            if (explainText != null)
            {
                explainText.text = _activeQuiz.explainText;
                explainText.gameObject.SetActive(true);
            }
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        /// <summary>ปิด popup แล้วส่งคำตอบให้ QuizManager (ให้คะแนน/ปลด Codex/คิวข้อต่อไป-resume)</summary>
        public void Close()
        {
            if (_activeQuiz == null) return;

            int answer = _selectedIndex;
            _activeQuiz = null;
            _selectedIndex = -1;
            _revealed = false;

            // ซ่อน panel ก่อน — ถ้ามีข้อต่อไปในคิว SubmitAnswer จะ raise OnQuizShown เปิด panel ใหม่เอง
            if (popupPanel != null) popupPanel.SetActive(false);
            QuizManager.Instance.SubmitAnswer(answer);
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
