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
    public class MemorialPanelController : MonoBehaviour, GameUIStack.IPanel
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

            // ★ grid footprint ก่อน — ทางเดียวกับ hover nameplate ที่พิสูจน์แล้วว่าทำงานบน WebGL
            var im = InputManager.Instance;
            var reg = BuildingRegistry.Instance;
            if (im != null && reg != null
                && reg.TryGetBuildingAt(im.GetMouseGridPosition(), out _, out var data) && data != null
                && data.buildingType == BuildingType.Memorial)
            { Open(); return; }

            // สำรอง: คลิกตัวสไปรต์สูงเหนือ footprint (bounds เรขาคณิตล้วน ไม่พึ่ง Physics2D)
            Vector2 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            if (BuildingClickTarget.PickAt(world, t => t.data.buildingType == BuildingType.Memorial) != null)
                Open();
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
            if (panel != null) { UIPopIn.Ensure(panel); panel.SetActive(true); }
            GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
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
            GameUIStack.Pop(this);
            UIPopIn.PlayClose(panel); // หุบออก (Windows 11) แล้วปิดเอง
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(panel);
        GameObject GameUIStack.IPanel.PanelRoot => panel;
        void GameUIStack.IPanel.CloseFromStack() => Close();
    }
}
