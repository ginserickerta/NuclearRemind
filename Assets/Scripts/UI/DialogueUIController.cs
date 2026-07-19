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

        [Header("Portraits (โฟกัสทีละคน — v9)")]
        public Image leftPortrait;      // Auren (เสียงในใจ) — อยู่ซ้าย
        public Text  leftInitial;       // (v9) ไม่ใช้แล้ว — Auren ใช้ภาพจริง
        public Image rightPortrait;     // Kova — อยู่ขวา

        [Header("Real portraits (index = Emotion) + speech frames (v9)")]
        public Sprite[] kovaEmotionSprites;   // Kova (ขวา) · index = (int)Emotion
        public Sprite[] aurenEmotionSprites;  // Auren/เสียงในใจ (ซ้าย) · index = (int)Emotion
        public Sprite[] miraEmotionSprites;   // Mira/หมอ (ซ้าย) · index = (int)Emotion
        public Sprite[] dornEmotionSprites;   // Dorn/ชาวสวน (ซ้าย) · index = (int)Emotion
        public Sprite frameShort, frameMedium, frameLong; // กรอบพูด — เลือกตามความยาวประโยค
        public float boxHeight = 230f;        // สูงกรอบคงที่ · กว้างตามอัตราส่วนกรอบ (สั้น=แคบ ยาว=กว้าง)
        public int shortMaxChars = 45;        // ≤ = กรอบสั้น
        public int mediumMaxChars = 95;       // ≤ = กรอบกลาง · เกิน = กรอบยาว

        [Header("บอลลูนคำพูด")]
        public GameObject dialogBox;
        public Image namePlate;        // แถบชื่อ — เปลี่ยนสีตามผู้พูด
        public Text  nameText;
        public Text  bodyText;
        public Text  hintText;         // "▼ คลิกเพื่อไปต่อ"

        public bool IsShowing { get; private set; }

        private DialogueLine[] _lines = System.Array.Empty<DialogueLine>();
        private int _index;
        // v9 Option A "สลับโฟกัส": โชว์ portrait เฉพาะคนพูด (Auren ซ้าย / Kova ขวา) · กรอบอยู่ฝั่งตรงข้าม

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
            // ★ อย่าหยุดนาฬิกาก่อนรู้ว่าแผงโผล่จริง — overlayPanel ที่ยังไม่ได้ wire (Setup Story UI ยังไม่รัน)
            // จะทำให้เกมค้างโดยไม่มีอะไรให้กด และไม่มีข้อความบอกว่าเกิดอะไรขึ้น (บั๊กแบบเดียวกับ record card วันที่ 9)
            if (overlayPanel == null)
            {
                Debug.LogError("[Dialogue] มีบทจะเล่นแต่ overlayPanel ยังไม่ถูก wire — ข้ามบทนี้ไป " +
                               "(รัน NuclearReMind ▸ Setup Story UI เพื่อต่อ reference ให้ครบ)");
                EventManager.Instance.RaiseStoryCardDismissed();
                return;
            }

            IsShowing = true;
            TimeManager.Instance?.Pause(PauseReason.StoryCard);

            _lines = lines;
            _index = 0;

            // โฟกัสทีละคน → เริ่มด้วยซ่อนทั้งสอง portrait (RenderCurrent จะโชว์เฉพาะคนพูด)
            if (leftInitial != null) leftInitial.gameObject.SetActive(false);
            if (leftPortrait != null) { leftPortrait.preserveAspect = true; leftPortrait.gameObject.SetActive(false); }
            if (rightPortrait != null) { rightPortrait.preserveAspect = true; rightPortrait.gameObject.SetActive(false); }

            // เปิดแบบ Windows 11 (scale+fade ทั้งชิ้น) · PlayOpen กันบั๊กตอนบทเล่นต่อเนื่อง (ปิดแล้วเปิดซ้ำทันที)
            UIPopIn.PlayOpen(overlayPanel, self: true);
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
            // หุบออกแบบ Windows 11 (ถ้า director โชว์บทถัดไปทันที Show() จะ PlayOpen ยกเลิก close นี้ให้เอง)
            if (overlayPanel != null) UIPopIn.PlayClose(overlayPanel);
            // Resume ก่อน raise — director อาจโชว์การ์ด/บทถัดไปทันที (Pause ใหม่ต้องไม่โดน Resume เก่าลบทิ้ง)
            TimeManager.Instance?.Resume(PauseReason.StoryCard);
            EventManager.Instance.RaiseStoryCardDismissed();
        }

        private void RenderCurrent()
        {
            var line = _lines[_index];
            Speaker sp = line.speaker;
            bool kova  = sp == Speaker.Kova;                              // ขวา
            bool auren = sp == Speaker.InnerVoice;                        // ซ้าย (เสียงในใจ)
            bool leftSpeaker = auren || sp == Speaker.Mira || sp == Speaker.Dorn; // ทีมซ้าย (โชว์ทีละคน)
            Sprite[] leftSprites = auren ? aurenEmotionSprites
                                 : sp == Speaker.Mira ? miraEmotionSprites
                                 : dornEmotionSprites;

            // โฟกัสทีละคน: โชว์เฉพาะ portrait ของคนพูด · กรอบไปฝั่งตรงข้าม (ซ้ายพูด→กรอบขวา · Kova→กรอบซ้าย)
            if (leftPortrait != null)
            {
                if (leftSpeaker) { leftPortrait.sprite = Pick(leftSprites, line.emotion); leftPortrait.color = Color.white; }
                leftPortrait.gameObject.SetActive(leftSpeaker);
            }
            if (rightPortrait != null)
            {
                if (kova) { rightPortrait.sprite = Pick(kovaEmotionSprites, line.emotion); rightPortrait.color = Color.white; }
                rightPortrait.gameObject.SetActive(kova);
            }

            SetBoxSide(leftSpeaker);   // กรอบอยู่ข้างๆ ตัวละคร (ซ้ายพูด→กรอบขวา · Kova→กรอบซ้าย)
            ApplyFrame(line.textTH);       // กว้างตามความยาวประโยค

            // แถบชื่อ (สีตามผู้พูด) — เสียงในใจโชว์ "Auren" (มี portrait แล้ว)
            if (nameText != null)
            {
                nameText.text = auren ? "Auren" : SpeakerMeta.DisplayName(sp);
                nameText.alignment = TextAnchor.MiddleLeft;
            }
            if (namePlate != null) namePlate.color = SpeakerMeta.AccentColor(sp);

            if (bodyText != null)
            {
                bodyText.text = line.textTH;
                bool center = sp == Speaker.System; // เฉพาะระบบ = กลาง (ไม่มี portrait)
                bodyText.alignment = center ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
                bodyText.fontStyle = auren ? FontStyle.Italic : FontStyle.Normal; // เสียงในใจ = เอียง
            }

            if (hintText != null) // บรรทัดสุดท้าย → บอกว่าปิด
                hintText.text = _index >= _lines.Length - 1 ? "▼ คลิกเพื่อจบบท" : "▼ คลิกเพื่อไปต่อ";
        }

        // เลือก sprite ตามอารมณ์ (index หลุด/ว่าง → ตัวแรกที่ไม่ null)
        private static Sprite Pick(Sprite[] arr, Emotion e)
        {
            if (arr == null || arr.Length == 0) return null;
            int i = (int)e;
            if (i >= 0 && i < arr.Length && arr[i] != null) return arr[i];
            foreach (var s in arr) if (s != null) return s;
            return null;
        }

        // วางกรอบ "ข้างๆ ตัวละคร" (เว้นคอลัมน์ portrait ~CharColumn) แล้วงอกกว้างออกไปด้านนอก
        //   ตัวละครซ้าย (Auren/Mira/Dorn) → กรอบอยู่ด้านขวาของตัวละคร
        //   Kova (ขวา) → กรอบอยู่ด้านซ้ายของตัวละคร
        [Header("เลย์เอาต์กล่องบทพูด")]
        [Tooltip("ล็อก = ใช้ตำแหน่ง+ขนาด+กรอบ จาก RectTransform ใน prefab/scene ล้วนๆ (แก้ด้วยตาแล้ว 'อยู่' ไม่โดนเขียนทับ) · ปิด = controller ย้ายฝั่ง+รีไซซ์ตามผู้พูด/ความยาวเอง (พฤติกรรมเดิม)")]
        [SerializeField] private bool lockBoxLayout = true;
        [Tooltip("ระยะจากขอบจอถึงกล่อง (px) — ใช้เฉพาะเมื่อ 'ปิด' lockBoxLayout · ยิ่งน้อยยิ่งชิดตัวละคร")]
        [SerializeField] private float charColumn = 300f;
        [Tooltip("ระยะกล่องจากขอบล่าง (px)")]
        [SerializeField] private float boxBottom = 56f;

        private void SetBoxSide(bool leftSpeaker)
        {
            if (dialogBox == null || lockBoxLayout) return; // ล็อก → ใช้ตำแหน่งจาก prefab/scene (ไม่ย้าย/ไม่ทับ)
            var rt = dialogBox.GetComponent<RectTransform>();
            if (leftSpeaker)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); // งอกขวาจากคอลัมน์ซ้าย
                rt.anchoredPosition = new Vector2(charColumn, boxBottom);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f); // งอกซ้ายจากคอลัมน์ขวา
                rt.anchoredPosition = new Vector2(-charColumn, boxBottom);
            }
        }

        // เลือกกรอบตามจำนวนตัวอักษร → set sprite + ปรับกว้างตามอัตราส่วน (สูงคงที่ boxHeight)
        private void ApplyFrame(string text)
        {
            if (lockBoxLayout) return; // ล็อก → ใช้กรอบ+ขนาดจาก prefab/scene ทั้งหมด (ไม่สลับ sprite/ไม่รีไซซ์)
            int len = text != null ? text.Length : 0;
            Sprite fr = len <= shortMaxChars ? frameShort
                      : len <= mediumMaxChars ? frameMedium
                      : frameLong;
            if (dialogBox == null || fr == null) return;

            var img = dialogBox.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = fr;
                img.type = Image.Type.Simple;
                img.preserveAspect = false; // ปรับกว้างตามอัตราส่วนเองแล้ว → ไม่ต้อง letterbox
                img.color = Color.white;
            }
            var rt = dialogBox.GetComponent<RectTransform>();
            float aspect = fr.rect.height > 0 ? fr.rect.width / fr.rect.height : 3f;
            rt.sizeDelta = new Vector2(boxHeight * aspect, boxHeight);
        }
    }
}
