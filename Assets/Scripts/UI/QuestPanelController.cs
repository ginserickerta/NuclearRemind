using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: แผงภารกิจประจำวัน (Day 2-30) — เด้งมุมจอตอนเริ่มวัน บอกผู้เล่นว่าวันนี้ควรโฟกัสอะไร
    /// อ่านรายการภารกิจจาก QuestScheduleSO (data-driven — ไม่มีเลขวัน hardcode ในโค้ด) ไม่บล็อกการเล่น กดปิดได้
    ///
    /// Day quest guidance panel (Day 2-30) — pops up at day start like the Day-1
    /// tutorial checklist, telling the player what to focus on today.
    /// Which days show a quest is pure data (QuestScheduleSO) — no day numbers in code.
    /// Corner panel, non-blocking; the player dismisses it with the close button.
    /// </summary>
    public class QuestPanelController : MonoBehaviour
    {
        [Header("UI (wired by QuestPanelSetup)")]
        public GameObject panel;
        public RectTransform panelRect;
        public Text titleText;
        public Text bodyText;
        public Button closeButton;

        [Header("Data")]
        public QuestScheduleSO schedule; // fallback: Resources.Load("Quests/QuestSchedule")

        // panel height = header + body(preferred) + footer — grows with task count
        private const float HeaderHeight = 52f;
        private const float FooterHeight = 60f;
        private const float MinBodyHeight = 24f;

        // Subscribe may run before EventManager exists in a built player (auto-spawn timing),
        // so guard + retry from Start like the other panels.
        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        // สมัครรับ event เริ่มวัน — EventManager อาจยังไม่เกิดตอน OnEnable ในบิลด์จริง จึงลองซ้ำจาก Start
        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            _subscribed = false;
        }

        private void Start()
        {
            TrySubscribe();
            if (schedule == null)
                schedule = Resources.Load<QuestScheduleSO>("Quests/QuestSchedule");
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // Sync to the current day at scene start (covers day already begun before Start ran)
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            ShowForDay(day);
        }

        private void HandleDayStarted(int day, bool timed) => ShowForDay(day);

        /// <summary>
        /// [TH] แสดงภารกิจของวันที่กำหนด (วันนั้นไม่มีภารกิจ = ซ่อนแผง) — ปรับความสูงแผงตามจำนวนบรรทัด
        /// Show the quest for the given day, or hide when that day has none.</summary>
        public void ShowForDay(int day)
        {
            var entry = schedule != null ? schedule.ForDay(day) : null;
            if (entry == null || panel == null) { Hide(); return; }

            if (titleText != null)
                titleText.text = $"ภารกิจ Day {entry.day} — {entry.title}";

            float bodyHeight = MinBodyHeight;
            if (bodyText != null)
            {
                var sb = new StringBuilder();
                foreach (var task in entry.tasks)
                {
                    if (string.IsNullOrEmpty(task)) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("▸ ").Append(task);
                }
                bodyText.text = sb.ToString();
                // preferredHeight accounts for wrapped lines (uses the rect's current width)
                bodyHeight = Mathf.Max(MinBodyHeight, bodyText.preferredHeight);
                var bodyRect = bodyText.rectTransform;
                bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, bodyHeight);
            }

            if (panelRect != null)
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, HeaderHeight + bodyHeight + FooterHeight);

            panel.SetActive(true);
        }

        // ซ่อนแผงภารกิจ (ผูกกับปุ่มปิด)
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
