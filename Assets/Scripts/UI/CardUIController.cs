using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// การ์ดเนื้อเรื่องกลางจอ (Story Guide §2) — แสดง RecordCard / InfoCard / Outcome ทีละใบ
    /// อยู่บน StoryCanvas แยกจาก HUDCanvas (รัน Setup HUD Canvas ซ้ำแล้วการ์ดไม่หาย)
    /// เปิดการ์ด → หยุดนาฬิกาวัน (PauseReason.StoryCard) · กดปุ่มปิด → raise OnStoryCardDismissed
    /// ให้ StoryDirector เดินลำดับบังคับต่อ (record → info → crisis → outcome → quiz)
    /// OnEnable ตั้ง StoryDirector.CardUIAvailable = true — ก่อนหน้านั้น director degrade เป็น toast
    /// </summary>
    public class CardUIController : MonoBehaviour
    {
        public static CardUIController Instance { get; private set; }

        [Header("Panel (wire โดย Setup Story UI)")]
        public GameObject overlayPanel;
        public Image categoryBar;      // แถบสีหัวการ์ด — เปลี่ยนตามชนิด/หมวด

        [Header("Texts")]
        public Text kickerText;        // บรรทัดหัว เช่น "บันทึกกู้คืน · Dr. Elara Vane"
        public Text titleText;
        public Text bodyText;

        [Header("Dismiss")]
        public Button dismissButton;
        public Text dismissLabel;

        public bool IsShowing { get; private set; }

        // สีแถบหัวการ์ดตามชนิด (outcome = เทา · info ใช้สีหมวด)
        private static readonly Color OutcomeBar = new Color(0.55f, 0.60f, 0.68f);

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
            // record card แยกไป RecordCardUI แล้ว (mockup v2) — ตัวนี้เหลือ Info/Outcome
            EventManager.Instance.OnStoryInfoShown += HandleInfoShown;
            EventManager.Instance.OnStoryOutcomeShown += HandleOutcomeShown;
            StoryDirector.CardUIAvailable = true; // จากนี้ director รอผู้เล่นกดปิดการ์ดเอง
        }

        private void OnDisable()
        {
            StoryDirector.CardUIAvailable = false;
            if (IsShowing) // teardown ระหว่างการ์ดค้าง — อย่าทิ้ง pause ค้างไว้
            {
                IsShowing = false;
                TimeManager.Instance?.Resume(PauseReason.StoryCard);
            }
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnStoryInfoShown -= HandleInfoShown;
            EventManager.Instance.OnStoryOutcomeShown -= HandleOutcomeShown;
        }

        private void Start()
        {
            if (!IsShowing && overlayPanel != null) overlayPanel.SetActive(false);
            if (dismissButton != null) dismissButton.onClick.AddListener(Dismiss);
        }

        private void HandleInfoShown(InfoCardSO card)
        {
            if (card == null) return;
            Show($"คลังความรู้ · {CategoryLabel(card.category)}",
                 card.title,
                 card.bodyTH,
                 string.IsNullOrEmpty(card.buttonLabel) ? "รับทราบ" : card.buttonLabel,
                 CategoryColor(card.category));
        }

        private void HandleOutcomeShown(string afterText)
        {
            if (string.IsNullOrEmpty(afterText)) return;
            Show("บทสรุป", "", afterText, "รับทราบ", OutcomeBar);
        }

        private void Show(string kicker, string title, string body, string button, Color barColor)
        {
            IsShowing = true;
            TimeManager.Instance?.Pause(PauseReason.StoryCard); // อ่านการ์ดโดยไม่เสียเวลาเกม (แบบเดียวกับ quiz/crisis)

            if (overlayPanel != null) overlayPanel.SetActive(true);
            if (kickerText != null) kickerText.text = kicker;
            if (titleText != null)
            {
                titleText.text = title;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(title)); // การ์ด Outcome ไม่มีหัวเรื่อง
            }
            if (bodyText != null) bodyText.text = body;
            if (dismissLabel != null) dismissLabel.text = button;
            if (categoryBar != null) categoryBar.color = barColor;
        }

        /// <summary>ปิดการ์ด → เดินนาฬิกาต่อ แล้วบอก StoryDirector ให้เล่นขั้นถัดไป</summary>
        public void Dismiss()
        {
            if (!IsShowing) return;
            IsShowing = false;

            if (overlayPanel != null) overlayPanel.SetActive(false);
            // Resume ก่อน raise — director อาจโชว์การ์ดใบถัดไปทันที (Pause ใหม่ต้องไม่โดน Resume เก่าลบทิ้ง)
            TimeManager.Instance?.Resume(PauseReason.StoryCard);
            EventManager.Instance.RaiseStoryCardDismissed();
        }

        private static string CategoryLabel(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Energy:  return "พลังงาน";
                case CardCategory.Reactor: return "เครื่องปฏิกรณ์";
                case CardCategory.Medical: return "การแพทย์";
                case CardCategory.Food:    return "อาหาร";
                case CardCategory.Ethics:  return "จริยธรรม";
                case CardCategory.Fusion:  return "ฟิวชัน";
                default:                   return category.ToString();
            }
        }

        private static Color CategoryColor(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Energy:  return new Color(1f, 0.8f, 0.2f);
                case CardCategory.Reactor: return new Color(1f, 0.5f, 0.2f);
                case CardCategory.Medical: return new Color(0.3f, 0.85f, 0.7f);
                case CardCategory.Food:    return new Color(0.4f, 0.8f, 0.2f);
                case CardCategory.Ethics:  return new Color(0.62f, 0.5f, 1f);
                case CardCategory.Fusion:  return new Color(0.3f, 0.8f, 1f);
                default:                   return new Color(0.2f, 0.5f, 0.85f);
            }
        }
    }
}
