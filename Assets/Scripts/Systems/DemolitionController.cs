using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการโหมดทุบอาคาร:
    /// — hover บน occupied cell → แสดง highlight สีแดง
    /// — คลิกซ้าย → ทุบอาคาร (raise OnBuildingRemoved + คืน workers)
    /// — คลิกขวา หรือกดปุ่ม Demolish อีกครั้ง → ออกจากโหมด
    /// อาคารที่กำลังก่อสร้างต้องยกเลิกผ่าน BuildingQueueUI แทน
    /// </summary>
    public class DemolitionController : MonoBehaviour
    {
        public static DemolitionController Instance { get; private set; }

        [Header("Highlight overlay ใน demolish mode (ใส่ prefab หรือ child GameObject)")]
        public SpriteRenderer highlightRenderer;

        [Header("สีไฮไลต์")]
        public Color canDemolishColor    = new Color(1f, 0.2f, 0.2f, 0.55f);
        public Color cannotDemolishColor = new Color(0.55f, 0.55f, 0.55f, 0.3f);

        public bool IsDemolishing => _isDemolishing;

        private bool _isDemolishing;
        private Vector2Int _hoveredCell;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnDemolishModeToggled += HandleDemolishModeToggled;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDemolishModeToggled -= HandleDemolishModeToggled;
        }

        private void HandleDemolishModeToggled(bool active)
        {
            _isDemolishing = active;
            if (highlightRenderer != null)
                highlightRenderer.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isDemolishing) return;

            UpdateHighlight();

            if (Input.GetMouseButtonDown(0))
                TryDemolish();
            else if (Input.GetMouseButtonDown(1))
                EventManager.Instance.RaiseDemolishModeToggled(false);
        }

        private void UpdateHighlight()
        {
            _hoveredCell = InputManager.Instance.GetMouseGridPosition();
            var cell = GridManager.Instance.GetCell(_hoveredCell.x, _hoveredCell.y);

            if (highlightRenderer == null) return;

            bool isOccupied  = cell != null && cell.isOccupied;
            bool inProgress  = ConstructionController.Instance != null &&
                               ConstructionController.Instance.IsUnderConstruction(_hoveredCell);

            highlightRenderer.transform.position =
                GridManager.Instance.IsoToWorld(_hoveredCell.x, _hoveredCell.y);
            highlightRenderer.color = (isOccupied && !inProgress)
                ? canDemolishColor
                : cannotDemolishColor;
            highlightRenderer.gameObject.SetActive(true);
        }

        private void TryDemolish()
        {
            var cell = GridManager.Instance.GetCell(_hoveredCell.x, _hoveredCell.y);
            if (cell == null || !cell.isOccupied) return;

            if (ConstructionController.Instance != null &&
                ConstructionController.Instance.IsUnderConstruction(_hoveredCell))
            {
                Debug.Log("[DemolitionController] อาคารนี้ยังก่อสร้างอยู่ — ยกเลิกผ่าน BuildingQueueUI แทน");
                return;
            }

            if (!BuildingRegistry.Instance.PlacedBuildings.TryGetValue(_hoveredCell, out var data))
                return;

            Debug.Log($"[DemolitionController] ทุบ {data.buildingName} ที่ ({_hoveredCell.x},{_hoveredCell.y})");

            // cascade: GridManager.HandleBuildingRemoved → free cells
            //          BuildingVisualSpawner.HandleBuildingRemoved → destroy sprite
            //          BuildingRegistry.HandleBuildingRemoved → remove from dict
            //          ConstructionController.HandleBuildingRemoved → no-op (not in queue)
            EventManager.Instance.RaiseBuildingRemoved(_hoveredCell);

            // workers เป็น reserve pool — ไม่ถูกหักตอนวาง จึงไม่ต้องคืนตอนทุบ
            // (อาคารหายไปจาก registry แล้ว demand รวมจะลดเอง ทำให้ workerScale ของอาคารอื่นดีขึ้น)
        }
    }
}
