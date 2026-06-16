using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แสดง building ที่กำลังสร้างอยู่สูงสุด 7 รายการ
    /// แต่ละ entry มีปุ่ม Cancel (คืน resource) และ Prioritize (เสร็จ tick ถัดไป)
    /// </summary>
    public class BuildingQueueUI : MonoBehaviour
    {
        public static BuildingQueueUI Instance { get; private set; }

        [Header("UI References")]
        public Transform queueContainer;    // parent ของ entry items
        public GameObject entryPrefab;      // prefab: Icon (Image) + ProgressText + CancelBtn + PrioritizeBtn
        public int maxVisible = 7;

        private readonly Dictionary<Vector2Int, GameObject> _entries =
            new Dictionary<Vector2Int, GameObject>();

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
            EventManager.Instance.OnBuildingPlaced             += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved            += HandleBuildingRemoved;
            EventManager.Instance.OnConstructionComplete        += HandleConstructionComplete;
            EventManager.Instance.OnConstructionProgressChanged += HandleProgressChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced             -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved            -= HandleBuildingRemoved;
            EventManager.Instance.OnConstructionComplete        -= HandleConstructionComplete;
            EventManager.Instance.OnConstructionProgressChanged -= HandleProgressChanged;
        }

        // ─────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (_entries.Count >= maxVisible) return;
            if (entryPrefab == null || queueContainer == null) return;

            var pos = new Vector2Int(cell.col, cell.row);
            if (_entries.ContainsKey(pos)) return;

            var go = Instantiate(entryPrefab, queueContainer);
            _entries[pos] = go;

            // wire icon
            var icon = go.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && data.sprite != null) icon.sprite = data.sprite;

            // wire progress text
            UpdateProgressText(go, 0);

            // wire Cancel button — raise event ผ่าน EventManager ตามสถาปัตยกรรม
            var cancelBtn = go.transform.Find("CancelBtn")?.GetComponent<Button>();
            if (cancelBtn != null)
            {
                var captured = pos;
                cancelBtn.onClick.AddListener(
                    () => EventManager.Instance.RaiseConstructionCancelRequested(captured));
            }

            // wire Prioritize button
            var prioBtn = go.transform.Find("PrioritizeBtn")?.GetComponent<Button>();
            if (prioBtn != null)
            {
                var captured = pos;
                prioBtn.onClick.AddListener(
                    () => EventManager.Instance.RaiseConstructionPrioritizeRequested(captured));
            }
        }

        private void HandleBuildingRemoved(Vector2Int pos)
        {
            if (!_entries.TryGetValue(pos, out var go)) return;
            _entries.Remove(pos);
            if (go != null) Destroy(go);
        }

        private void HandleConstructionComplete(Vector2Int pos, BuildingData _)
        {
            if (!_entries.TryGetValue(pos, out var go)) return;
            _entries.Remove(pos);
            if (go != null) Destroy(go);
        }

        private void HandleProgressChanged(Vector2Int pos, int progress)
        {
            if (!_entries.TryGetValue(pos, out var go)) return;
            UpdateProgressText(go, progress);
        }

        // ─────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────

        private static void UpdateProgressText(GameObject go, int progress)
        {
            var txt = go.transform.Find("ProgressText")?.GetComponent<Text>();
            if (txt != null)
                txt.text = $"{progress}/{ConstructionController.TotalConstructionTicks}";
        }
    }
}
