using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Popup วิกฤต/Moral Dilemma (V4 §10) — กรอบโลหะสนิม: หัวเรื่อง + กล่องคำอธิบาย (คอลัมน์ซ้าย) +
    /// ปุ่ม 3 ตัวเลือก A/B/C (คอลัมน์ขวา) + ปุ่ม "ยืนยัน" (ล่างเต็มกว้าง)
    ///
    /// flow: แตะ A/B/C = ไฮไลต์ (ยังไม่ commit) → กด "ยืนยัน" = raise OnDilemmaResolved(dilemma, index)
    /// ให้ DilemmaManager/CrisisEffectManager นำผลไปปรับ state · ยืนยันไม่ได้ถ้ายังไม่เลือก (interactable=false)
    /// ทุก field มาจาก DilemmaData (data-driven) — เปิดวิกฤตคนละใบ = ส่ง SO คนละตัว ทุกช่องเปลี่ยนตาม
    /// ปุ่ม C ซ่อนอัตโนมัติถ้า dilemma ไม่มี choiceCText (รองรับ dilemma 2 ทางแบบเดิม)
    /// (เลียนโครง select-then-confirm จาก QuizPopupController — ไม่ใช้ Update/Time.deltaTime เพราะระหว่างวิกฤต timeScale=0)
    /// </summary>
    public class DilemmaPopupController : MonoBehaviour, GameUIStack.IPanel
    {
        [Header("Panel")]
        public GameObject popupPanel;

        [Header("Texts")]
        public Text titleText;       // หัวเรื่อง "วิกฤต …" (dilemma.title)
        public Text scenarioText;    // กล่องคำอธิบาย (dilemma.scenarioText)
        public Text choiceAText;
        public Text choiceBText;
        public Text choiceCText;

        [Header("Buttons")]
        public Button choiceAButton;
        public Button choiceBButton;
        public Button choiceCButton;
        public Button confirmButton; // ยืนยัน — เปิดใช้เมื่อเลือกแล้วเท่านั้น

        [Header("Fallback")]
        [Tooltip("หัวเรื่องเมื่อ dilemma.title ว่าง")]
        public string fallbackTitle = "เหตุการณ์วิกฤต";

        // ===== สีไฮไลต์ตัวเลือกที่เลือก (ยังไม่ยืนยัน) — ให้เข้าชุดกับ QuizPopupController =====
        private static readonly Color ColSelected = new Color(0.85f, 0.85f, 0.45f); // tint ปุ่มที่เลือก
        private static readonly Color GlowSelected = new Color(0.95f, 0.80f, 0.30f); // เรืองขอบเหลือง (Outline)

        private DilemmaData _activeDilemma;
        private int _selectedIndex = -1;
        private Button[] _choiceButtons;     // {A,B,C} — cache ตอน Start
        private Color[] _baseColors;         // สีปุ่มเดิม (รีเซ็ตตอนเปิดวิกฤตใหม่)
        private Outline[] _choiceOutlines;   // เรืองขอบปุ่มตัวเลือก (setup ใส่ไว้ ปิดอยู่)

        private void OnEnable()
        {
            EventManager.Instance.OnDilemmaTriggered += HandleDilemmaTriggered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDilemmaTriggered -= HandleDilemmaTriggered;
        }

        private void Start()
        {
            _choiceButtons = new[] { choiceAButton, choiceBButton, choiceCButton };
            _baseColors = new Color[_choiceButtons.Length];
            _choiceOutlines = new Outline[_choiceButtons.Length];
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                var btn = _choiceButtons[i];
                if (btn == null) continue;
                if (btn.image != null) _baseColors[i] = btn.image.color;
                _choiceOutlines[i] = btn.GetComponent<Outline>();
                if (_choiceOutlines[i] != null) _choiceOutlines[i].enabled = false;
            }

            if (popupPanel != null) popupPanel.SetActive(false);
            if (confirmButton != null) confirmButton.interactable = false;
        }

        private void HandleDilemmaTriggered(DilemmaData dilemma)
        {
            _activeDilemma = dilemma;
            _selectedIndex = -1;

            if (popupPanel != null) { UIPopIn.Ensure(popupPanel); popupPanel.SetActive(true); }
            GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause · Esc เงียบ = ต้องเลือก+ยืนยัน)
            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(dilemma.title) ? fallbackTitle : dilemma.title;
            if (scenarioText != null) scenarioText.text = dilemma.scenarioText;
            if (choiceAText != null) choiceAText.text = dilemma.choiceAText;
            if (choiceBText != null) choiceBText.text = dilemma.choiceBText;
            if (choiceCText != null) choiceCText.text = dilemma.choiceCText;

            // ปุ่ม C โผล่เฉพาะเมื่อ dilemma มีทางเลือก C (รองรับทั้ง 2 และ 3 ทาง)
            bool hasC = !string.IsNullOrEmpty(dilemma.choiceCText);
            if (choiceCButton != null) choiceCButton.gameObject.SetActive(hasC);

            // รีเซ็ตไฮไลต์ + สีปุ่มกลับค่าเดิม (กันสีค้างจากวิกฤตก่อน)
            if (_choiceButtons != null)
            {
                for (int i = 0; i < _choiceButtons.Length; i++)
                {
                    var btn = _choiceButtons[i];
                    if (btn == null) continue;
                    btn.interactable = true;
                    if (btn.image != null && _baseColors != null && i < _baseColors.Length)
                        btn.image.color = _baseColors[i];
                    SetOutline(i, false, GlowSelected);
                }
            }

            if (confirmButton != null) confirmButton.interactable = false; // ต้องเลือกก่อน
        }

        // ===== เลือกตัวเลือก (ไฮไลต์ · ยังไม่ commit) — ผูก persistent listener จาก setup =====
        public void SelectA() => Select(0);
        public void SelectB() => Select(1);
        public void SelectC() => Select(2);

        private void Select(int index)
        {
            if (_activeDilemma == null) return;
            _selectedIndex = index;

            // ไฮไลต์ข้อที่เลือก — tint + เรืองขอบเหลือง · ข้ออื่นกลับสภาพเดิม
            if (_choiceButtons != null)
            {
                for (int i = 0; i < _choiceButtons.Length; i++)
                {
                    var btn = _choiceButtons[i];
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

        /// <summary>ยืนยันตัวเลือกที่เลือกไว้ — ใช้ผล raise OnDilemmaResolved (ผูก persistent listener จาก setup)</summary>
        public void Confirm()
        {
            if (_activeDilemma == null || _selectedIndex < 0) return;
            Resolve(_selectedIndex);
        }

        private void Resolve(int choiceIndex)
        {
            if (_activeDilemma == null) return;

            var dilemma = _activeDilemma;
            _activeDilemma = null;
            _selectedIndex = -1;

            if (popupPanel != null) popupPanel.SetActive(false);
            GameUIStack.Pop(this);
            EventManager.Instance.RaiseDilemmaResolved(dilemma, choiceIndex);
        }

        // ── GameUIStack (แผงบังคับ: Esc เงียบ ปิดเองไม่ได้ · บล็อก Pause · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => false;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(popupPanel);
        void GameUIStack.IPanel.CloseFromStack() { } // ไม่ถูกเรียก (ClosableByEscape=false — ต้องเลือก+ยืนยัน)

        // เปิด/ปิดเรืองขอบปุ่มตัวเลือก index (setup ใส่ Outline component ไว้แล้ว ปิดอยู่)
        private void SetOutline(int index, bool on, Color color)
        {
            if (_choiceOutlines == null || index < 0 || index >= _choiceOutlines.Length) return;
            var o = _choiceOutlines[index];
            if (o == null) return;
            o.effectColor = color;
            o.enabled = on;
        }
    }
}
