using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// แผงอนุสรณ์ทีมสร้างหอคอย (Story Guide §4 — memorial_veltara)
    /// คลิกซ้ายบนตึก BuildingType.Memorial (pre-placed) → เปิดแผงรายชื่อ 6 คน
    /// interaction ปกติชิ้นเดียวของระบบเนื้อเรื่อง — เกมไม่ชี้ว่า ELARA VANE อยู่ในรายชื่อ
    /// เสียงในใจ (innerVoiceOnFirstOpen) โชว์เป็น Notice ครั้งเดียวตอนเปิดครั้งแรกของรอบเล่น
    /// </summary>
    public class MemorialPanelController : MonoBehaviour
    {
        public static MemorialPanelController Instance { get; private set; }

        [Header("Data (wire โดย Story Setup เฟส 4)")]
        public MemorialSO memorialData;

        [Header("Panel (wire โดย Setup Story UI)")]
        public GameObject panel;
        public Text headerText;
        public Text namesText;
        public Button closeButton;

        private Camera _camera;
        private bool _innerVoiceShown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _camera = Camera.main;
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void Update()
        {
            if (panel == null || panel.activeSelf) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // คลิกบน UI / กำลังวาง / กำลังทุบ → ไม่ใช่การคลิกสำรวจตึก
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (PlacementController.Instance != null && PlacementController.Instance.IsPlacing) return;
            if (DemolitionController.Instance != null && DemolitionController.Instance.IsDemolishing) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // คลิกโดน "ตัวสไปรต์" อนุสรณ์ (raycast Collider2D) — เดิมเช็ก footprint ต้องเล็งฐาน
            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            foreach (var h in Physics2D.OverlapPointAll((Vector2)world))
            {
                var t = h.GetComponent<BuildingClickTarget>();
                if (t != null && t.data != null && t.data.buildingType == BuildingType.Memorial)
                {
                    Open();
                    return;
                }
            }
        }

        /// <summary>เปิดแผงถ้า cell อยู่บน footprint ของตึกอนุสรณ์ (query registry แบบ read-only)</summary>
        public bool TryOpenAtCell(Vector2Int cell)
        {
            if (panel == null || panel.activeSelf) return false;

            var registry = BuildingRegistry.Instance;
            if (registry == null) return false;

            foreach (var kvp in registry.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null || data.buildingType != BuildingType.Memorial) continue;

                if (cell.x >= kvp.Key.x && cell.x < kvp.Key.x + data.size.x
                    && cell.y >= kvp.Key.y && cell.y < kvp.Key.y + data.size.y)
                {
                    Open();
                    return true;
                }
            }
            return false;
        }

        public void Open()
        {
            if (panel != null) panel.SetActive(true);
            if (memorialData == null) return;

            if (headerText != null) headerText.text = memorialData.headerTH;
            if (namesText != null)
                namesText.text = memorialData.names != null ? string.Join("\n", memorialData.names) : "";

            if (!_innerVoiceShown && !string.IsNullOrEmpty(memorialData.innerVoiceOnFirstOpen))
            {
                _innerVoiceShown = true;
                EventManager.Instance?.RaiseNotice($"▸ ความคิด: {memorialData.innerVoiceOnFirstOpen}");
            }
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
