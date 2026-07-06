using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แผง Records (Story Guide §2) — ย้อนอ่านบันทึกของ Dr. Elara Vane ที่กู้คืนแล้ว
    /// รายการอ่านจาก StoryDirector.ArchivedRecords (read-only query — precedent: CodexManager.UnlockedIds)
    /// เปิด/ปิดด้วยปุ่ม "บันทึก" บน StoryCanvas · ปุ่มรายการสร้างจาก template ลูกของ list
    /// (reuse ปุ่มเดิมแทน Destroy — เรียกได้ทั้ง PlayMode และ EditMode test)
    /// </summary>
    public class RecordsPanelController : MonoBehaviour
    {
        public static RecordsPanelController Instance { get; private set; }

        [Header("Panel (wire โดย Setup Story UI)")]
        public GameObject recordsPanel;
        public Button toggleButton;
        public Button closeButton;

        [Header("Entry List")]
        public Transform entryListParent;
        public GameObject entryButtonTemplate; // ลูกของ list — inactive ต้นแบบ (Button + Text ลูก)

        [Header("Detail View")]
        public Text detailTitle;
        public Text detailAuthor;
        public Text detailBody;

        private readonly List<GameObject> _entryButtons = new List<GameObject>();

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
            EventManager.Instance.OnRecordArchived += HandleRecordArchived;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnRecordArchived -= HandleRecordArchived;
        }

        private void Start()
        {
            if (recordsPanel != null) recordsPanel.SetActive(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Toggle);
        }

        public void Toggle()
        {
            if (recordsPanel == null) return;
            bool nowOpen = !recordsPanel.activeSelf;
            recordsPanel.SetActive(nowOpen);
            if (nowOpen) RefreshList();
        }

        private void HandleRecordArchived(RecordCardSO record)
        {
            if (recordsPanel != null && recordsPanel.activeSelf)
                RefreshList();
        }

        public void ShowRecord(RecordCardSO record)
        {
            if (record == null) return;
            if (detailTitle != null) detailTitle.text = record.archiveTitle;
            if (detailAuthor != null) detailAuthor.text = record.authorLabel;
            if (detailBody != null) detailBody.text = record.bodyTH;
        }

        private void RefreshList()
        {
            if (entryListParent == null || entryButtonTemplate == null) return;

            IReadOnlyList<RecordCardSO> records = StoryDirector.Instance != null
                ? StoryDirector.Instance.ArchivedRecords
                : null;
            int count = records != null ? records.Count : 0;

            // เพิ่มปุ่มให้พอ (reuse ของเดิม — ไม่ Destroy ทุกรอบแบบ Codex)
            while (_entryButtons.Count < count)
            {
                var go = Instantiate(entryButtonTemplate, entryListParent);
                _entryButtons.Add(go);
            }

            for (int i = 0; i < _entryButtons.Count; i++)
            {
                var go = _entryButtons[i];
                bool used = i < count;
                go.SetActive(used);
                if (!used) continue;

                var record = records[i];
                var label = go.GetComponentInChildren<Text>();
                if (label != null) label.text = record != null ? record.archiveTitle : "—";

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var captured = record;
                    btn.onClick.AddListener(() => ShowRecord(captured));
                }
            }

            // ยังไม่กู้บันทึกเลย → บอกผู้เล่นแทนจอว่าง
            if (count == 0 && detailBody != null)
            {
                if (detailTitle != null) detailTitle.text = "ยังไม่มีบันทึกที่กู้คืน";
                if (detailAuthor != null) detailAuthor.text = "";
                detailBody.text = "ระบบกู้คืนข้อมูลจะดึงบันทึกเก่าของเครือข่าย Veltara ขึ้นมาเองเมื่อเมืองก้าวหน้า";
            }
        }
    }
}
