using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// บทสนทนาหลายตัวละคร (v8.5) — สไตล์ visual novel: portrait ซ้าย/ขวา + บอลลูนคำพูดล่างจอ
    /// StoryDirector ยิง OnStoryDialogueShown(DialogueLine[]) → เล่นทีละบรรทัด คลิกเพื่อไปต่อ
    /// จบทั้งชุด → RaiseStoryCardDismissed (ใช้ท่อเดียวกับการ์ด → director เดินลำดับต่อ)
    ///
    /// การจัดฝั่ง (dynamic): ผู้พูด NPC คนใหม่ไปนั่งฝั่งตรงข้ามคนที่เพิ่งพูด → คู่สนทนาปัจจุบัน
    /// เห็นทั้งสองฝั่งเสมอ (รองรับบทสามคน เช่น Dorn↔Kova↔Mira) · คนที่ไม่ได้พูดถูกหรี่แสง
    /// เสียงในใจ (Auren) / ระบบ = ข้อความกลาง ตัวเอียง ไม่มี portrait (หรี่ทั้งสองฝั่ง)
    ///
    /// เปิดบท → หยุดนาฬิกา (PauseReason.StoryCard เดียวกับการ์ด · ไม่เล่นพร้อมกัน)
    /// OnEnable ตั้ง StoryDirector.DialogueUIAvailable = true — ก่อนหน้านั้น director degrade เป็น toast
    /// </summary>
    public class DialogueUIController : MonoBehaviour
    {
        public static DialogueUIController Instance { get; private set; }

        [Header("Panel (wire โดย Setup Story UI)")]
        public GameObject overlayPanel;
        public Button advanceButton;   // ปุ่มคลุมทั้งจอ — คลิกที่ไหนก็ไปบรรทัดถัดไป

        [Header("Portraits (placeholder สีตามตัวละคร — สลับภาพจริงภายหลัง)")]
        public Image leftPortrait;
        public Text  leftInitial;
        public Image rightPortrait;
        public Text  rightInitial;

        [Header("บอลลูนคำพูด")]
        public GameObject dialogBox;
        public Image namePlate;        // แถบชื่อ — เปลี่ยนสีตามผู้พูด
        public Text  nameText;
        public Text  bodyText;
        public Text  hintText;         // "▼ คลิกเพื่อไปต่อ"

        public bool IsShowing { get; private set; }

        private DialogueLine[] _lines = System.Array.Empty<DialogueLine>();
        private int _index;

        // ช่อง portrait ปัจจุบัน (dynamic) — คนใหม่ไปฝั่งตรงข้ามคนที่เพิ่งพูด
        private Speaker _left, _right;
        private bool _leftSet, _rightSet;
        private bool _lastActiveIsRight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnStoryDialogueShown += HandleDialogueShown;
            StoryDirector.DialogueUIAvailable = true;
        }

        private void OnDisable()
        {
            StoryDirector.DialogueUIAvailable = false;
            if (IsShowing) // teardown ระหว่างบทค้าง — อย่าทิ้ง pause ค้างไว้
            {
                IsShowing = false;
                TimeManager.Instance?.Resume(PauseReason.StoryCard);
            }
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnStoryDialogueShown -= HandleDialogueShown;
        }

        private void Start()
        {
            if (!IsShowing && overlayPanel != null) overlayPanel.SetActive(false);
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
        }

        private void HandleDialogueShown(DialogueLine[] lines)
        {
            if (lines == null || lines.Length == 0) // ไม่มีบท → เดินลำดับต่อทันที (กันค้าง)
            {
                EventManager.Instance.RaiseStoryCardDismissed();
                return;
            }
            Show(lines);
        }

        private void Show(DialogueLine[] lines)
        {
            IsShowing = true;
            TimeManager.Instance?.Pause(PauseReason.StoryCard);

            _lines = lines;
            _index = 0;
            _leftSet = _rightSet = false;
            _lastActiveIsRight = true; // → คนแรกที่พูด (ฝั่งตรงข้าม) ไปนั่งซ้าย

            if (overlayPanel != null) overlayPanel.SetActive(true);
            RenderCurrent();
        }

        /// <summary>คลิก → บรรทัดถัดไป · หมดบท → ปิดแล้วบอก director เดินต่อ</summary>
        public void Advance()
        {
            if (!IsShowing) return;
            _index++;
            if (_index >= _lines.Length) { Finish(); return; }
            RenderCurrent();
        }

        private void Finish()
        {
            IsShowing = false;
            if (overlayPanel != null) overlayPanel.SetActive(false);
            // Resume ก่อน raise — director อาจโชว์การ์ด/บทถัดไปทันที (Pause ใหม่ต้องไม่โดน Resume เก่าลบทิ้ง)
            TimeManager.Instance?.Resume(PauseReason.StoryCard);
            EventManager.Instance.RaiseStoryCardDismissed();
        }

        private void RenderCurrent()
        {
            var line = _lines[_index];
            Speaker sp = line.speaker;

            // -1 = กลาง (เสียงในใจ/ระบบ) · 0 = ซ้าย · 1 = ขวา
            int activeSide = ResolveActiveSide(sp);

            // portrait: แสดงเฉพาะช่องที่มีคนนั่ง · ฝั่งที่พูดสว่าง ฝั่งอื่นหรี่
            SetPortrait(leftPortrait,  leftInitial,  _leftSet,  _left,  activeSide == 0);
            SetPortrait(rightPortrait, rightInitial, _rightSet, _right, activeSide == 1);

            // แถบชื่อ + ข้อความ
            if (nameText != null)
            {
                nameText.text = SpeakerMeta.DisplayName(sp);
                nameText.alignment = activeSide == 1 ? TextAnchor.MiddleRight
                                   : activeSide == 0 ? TextAnchor.MiddleLeft
                                   : TextAnchor.MiddleCenter;
            }
            if (namePlate != null) namePlate.color = SpeakerMeta.AccentColor(sp);

            if (bodyText != null)
            {
                bodyText.text = line.textTH;
                bool center = activeSide < 0;                 // เสียงในใจ/ระบบ = กลาง
                bodyText.alignment = center ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
                bodyText.fontStyle = sp == Speaker.InnerVoice ? FontStyle.Italic : FontStyle.Normal;
            }

            if (hintText != null) // บรรทัดสุดท้าย → บอกว่าปิด
                hintText.text = _index >= _lines.Length - 1 ? "▼ คลิกเพื่อจบบท" : "▼ คลิกเพื่อไปต่อ";
        }

        // จัดฝั่งผู้พูด · คืน -1 กลาง / 0 ซ้าย / 1 ขวา · อัปเดตช่อง portrait แบบ dynamic
        private int ResolveActiveSide(Speaker sp)
        {
            if (!SpeakerMeta.HasPortrait(sp)) return -1; // เสียงในใจ/ระบบ ไม่กินช่อง

            if (_leftSet && _left == sp)  { _lastActiveIsRight = false; return 0; }
            if (_rightSet && _right == sp) { _lastActiveIsRight = true;  return 1; }

            // ผู้พูดใหม่ → ฝั่งตรงข้ามคนที่เพิ่งพูด
            if (_lastActiveIsRight) { _left = sp;  _leftSet = true;  _lastActiveIsRight = false; return 0; }
            _right = sp; _rightSet = true; _lastActiveIsRight = true; return 1;
        }

        private static void SetPortrait(Image img, Text initial, bool used, Speaker sp, bool active)
        {
            if (img == null) return;
            if (!used) { img.gameObject.SetActive(false); return; }

            img.gameObject.SetActive(true);
            Color accent = SpeakerMeta.AccentColor(sp);
            img.color = active ? accent : Color.Lerp(accent, Color.black, 0.6f); // หรี่ = คล้ำลง
            if (initial != null)
            {
                initial.text = SpeakerMeta.Initial(sp);
                initial.color = active ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
        }
    }
}
