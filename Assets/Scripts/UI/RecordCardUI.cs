using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// การ์ด "บันทึกกู้คืน" ธีมไม้ (mockup v2) — แยกจาก CardUIController (ซึ่งคุม Info/Outcome ต่อ)
    /// เด้งกลางจอเมื่อ StoryDirector raise OnStoryRecordShown(RecordCardSO) · หยุดนาฬิกา (PauseReason.StoryCard)
    ///
    /// ปุ่ม 2 ปุ่ม (mockup):
    ///   • "รับทราบ"            → ปิดการ์ดเฉย ๆ (ไม่ archive) → RaiseStoryCardDismissed (director เดินต่อ)
    ///   • "เก็บเข้าแผง Record" → RaiseRecordArchiveRequested (StoryDirector archive → RecordsPanel) แล้วปิด
    ///
    /// ทุก field มาจาก RecordCardSO (data-driven): statusLabel / recorderName / bodyTH — เปิดคนละใบ = อัปเดตครบ
    /// ส่วนคงที่ ("การ์ดบันทึก", "- ผู้บันทึก:", ปุ่ม, กรอบไม้) อยู่ใน prefab/setup ไม่เปลี่ยนตามการ์ด
    /// </summary>
    public class RecordCardUI : MonoBehaviour
    {
        public static RecordCardUI Instance { get; private set; }

        [Header("Panel (wire โดย Setup Record Card UI)")]
        public GameObject overlayPanel;
        public RectTransform cardRoot;   // แผ่นไม้ (ลูกของ overlay) — ตัวที่ pop-in scale

        [Header("Texts (binding จาก RecordCardSO)")]
        public Text statusText;          // มุมขวาบน — statusLabel
        public Text recorderText;        // "- ผู้บันทึก: {recorderName}"
        public Text bodyText;            // เนื้อความ (auto-size ตั้งใน setup)

        [Header("Buttons")]
        public Button ackButton;         // "รับทราบ" — ปิด ไม่ archive
        public Button archiveButton;     // "เก็บเข้าแผง Record" — archive + ปิด

        [Header("Pop-in")]
        public float popInSeconds = 0.18f;
        public float popInStartScale = 0.6f;

        public bool IsShowing { get; private set; }

        private RecordCardSO _current;
        private Coroutine _popIn;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnStoryRecordShown += HandleRecordShown;
        }

        private void OnDisable()
        {
            if (IsShowing) // teardown ระหว่างการ์ดค้าง — อย่าทิ้ง pause ค้าง
            {
                IsShowing = false;
                TimeManager.Instance?.Resume(PauseReason.StoryCard);
            }
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnStoryRecordShown -= HandleRecordShown;
        }

        private void Start()
        {
            if (!IsShowing && overlayPanel != null) overlayPanel.SetActive(false);
            if (ackButton != null) ackButton.onClick.AddListener(Acknowledge);
            if (archiveButton != null) archiveButton.onClick.AddListener(ArchiveAndClose);
        }

        private void HandleRecordShown(RecordCardSO record)
        {
            if (record == null) return;
            _current = record;

            IsShowing = true;
            TimeManager.Instance?.Pause(PauseReason.StoryCard);

            if (statusText != null)
                statusText.text = string.IsNullOrEmpty(record.statusLabel) ? "กู้คืนสำเร็จ" : record.statusLabel;
            if (recorderText != null)
                recorderText.text = "- ผู้บันทึก:  " + ResolveRecorder(record);
            if (bodyText != null)
                bodyText.text = record.bodyTH;

            if (overlayPanel != null) overlayPanel.SetActive(true);
            if (cardRoot != null)
            {
                if (_popIn != null) StopCoroutine(_popIn);
                _popIn = StartCoroutine(PopIn());
            }
        }

        // ชื่อผู้บันทึก: ใช้ recorderName ถ้ามี · ไม่งั้นดึงจาก authorLabel หลัง "ผู้บันทึก:" · สุดท้าย authorLabel ทั้งก้อน
        private static string ResolveRecorder(RecordCardSO record)
        {
            if (!string.IsNullOrEmpty(record.recorderName)) return record.recorderName;
            string a = record.authorLabel ?? "";
            int i = a.IndexOf("ผู้บันทึก:", System.StringComparison.Ordinal);
            if (i >= 0) return a.Substring(i + "ผู้บันทึก:".Length).Trim();
            return a;
        }

        private IEnumerator PopIn()
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, popInSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // เกม pause (timeScale อาจ 0) → ใช้ unscaled
                float k = Mathf.Clamp01(t / dur);
                float ease = 1f - (1f - k) * (1f - k); // ease-out quad
                cardRoot.localScale = Vector3.one * Mathf.LerpUnclamped(popInStartScale, 1f, ease);
                yield return null;
            }
            cardRoot.localScale = Vector3.one;
            _popIn = null;
        }

        /// <summary>"รับทราบ" — ปิดการ์ด ไม่ archive</summary>
        public void Acknowledge() => Close();

        /// <summary>"เก็บเข้าแผง Record" — archive ผ่าน StoryDirector (event) แล้วปิด</summary>
        public void ArchiveAndClose()
        {
            if (_current != null)
                EventManager.Instance.RaiseRecordArchiveRequested(_current);
            Close();
        }

        private void Close()
        {
            if (!IsShowing) return;
            IsShowing = false;
            if (overlayPanel != null) overlayPanel.SetActive(false);
            // Resume ก่อน raise — director อาจโชว์การ์ดใบถัดไปทันที (Pause ใหม่ต้องไม่โดน Resume เก่าลบ)
            TimeManager.Instance?.Resume(PauseReason.StoryCard);
            EventManager.Instance.RaiseStoryCardDismissed();
        }
    }
}
