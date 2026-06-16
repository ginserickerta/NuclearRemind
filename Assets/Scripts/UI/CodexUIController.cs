using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Codex panel UI: แสดง entry ที่ unlock แล้ว + entry ที่ล็อคพร้อม RP cost
    /// เปิด/ปิด panel ผ่าน Toggle() ที่เรียกจาก button ใน HUD
    /// เนื้อหาอัปเดตทุกครั้งที่ OnCodexEntryUnlocked ถูก raise
    /// </summary>
    public class CodexUIController : MonoBehaviour
    {
        public static CodexUIController Instance { get; private set; }

        [Header("Panel")]
        public GameObject codexPanel;

        [Header("Entry List")]
        public Transform entryListParent;
        public GameObject entryButtonPrefab;   // Button + Text child ชื่อ entry

        [Header("Detail View")]
        public Text detailTitle;
        public Text detailContent;
        public Text detailBranch;
        public Image detailIllustration;
        public Text researchPointText;         // แสดง RP ปัจจุบัน

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
            EventManager.Instance.OnCodexEntryUnlocked += HandleEntryUnlocked;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnCodexEntryUnlocked -= HandleEntryUnlocked;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        private void Start()
        {
            if (codexPanel != null) codexPanel.SetActive(false);
        }

        public void Toggle()
        {
            if (codexPanel == null) return;
            bool nowOpen = !codexPanel.activeSelf;
            codexPanel.SetActive(nowOpen);
            if (nowOpen) RefreshList();
        }

        public void ShowEntry(CodexEntry entry)
        {
            if (entry == null) return;

            if (detailTitle != null) detailTitle.text = entry.title;
            if (detailContent != null) detailContent.text = entry.content;
            if (detailBranch != null) detailBranch.text = entry.branch;
            if (detailIllustration != null)
                detailIllustration.sprite = entry.illustration;
        }

        public void TryUnlock(CodexEntry entry)
        {
            CodexManager.Instance.TryUnlock(entry);
        }

        private void HandleEntryUnlocked(CodexEntry entry)
        {
            if (codexPanel != null && codexPanel.activeSelf)
                RefreshList();
        }

        private void HandleResourceChanged(ResourceData data)
        {
            if (researchPointText != null)
                researchPointText.text = $"RP: {Mathf.FloorToInt(data.researchPoints)}";
        }

        private void RefreshList()
        {
            if (entryListParent == null || entryButtonPrefab == null) return;

            foreach (Transform child in entryListParent)
                Destroy(child.gameObject);

            foreach (var kvp in CodexManager.Instance.AllEntries)
            {
                var entry = kvp.Value;
                bool unlocked = CodexManager.Instance.IsUnlocked(entry.entryId);

                var go = Instantiate(entryButtonPrefab, entryListParent);
                var label = go.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = unlocked ? entry.title : $"[{entry.researchPointCost} RP] {entry.title}";

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    var captured = entry;
                    if (unlocked)
                        btn.onClick.AddListener(() => ShowEntry(captured));
                    else
                        btn.onClick.AddListener(() => TryUnlock(captured));
                }
            }
        }
    }
}
