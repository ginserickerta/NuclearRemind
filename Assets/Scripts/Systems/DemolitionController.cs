using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการโหมดทุบอาคาร:
    /// — hover บน occupied cell → แสดง highlight สีแดง
    /// — คลิกซ้าย → ทุบอาคาร (raise OnBuildingRemoved + คืน workers)
    /// — คลิกขวา / Esc หรือกดปุ่ม Demolish อีกครั้ง → ออกจากโหมด
    /// อาคารที่กำลังก่อสร้างต้องยกเลิกผ่าน BuildingQueueUI แทน
    /// ระหว่างอยู่ในโหมด นาฬิกาวันหยุด (เจตนาเดียวกับโหมดวาง — V4 §15 "ให้คิด ไม่ใช่รีบ")
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

        /// <summary>เฟรมล่าสุดที่ ESC ถูกใช้ออกจากโหมดทุบ — กัน PauseMenu เปิดซ้อนในเฟรมเดียวกัน</summary>
        public int LastEscCancelFrame { get; private set; } = -1;

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
            // ถ้าถูกปิดกลางโหมดทุบ (เช่นเปลี่ยนซีน) — ปลดเหตุหยุดเวลาไว้ก่อน กันนาฬิกาค้าง
            if (_isDemolishing) TimeManager.Instance?.Resume(PauseReason.Demolition);

            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDemolishModeToggled -= HandleDemolishModeToggled;
        }

        private void HandleDemolishModeToggled(bool active)
        {
            _isDemolishing = active;
            if (highlightRenderer != null)
                highlightRenderer.gameObject.SetActive(false);

            // §15: โหมดทุบ = การตัดสินใจผังเช่นเดียวกับโหมดวาง → หยุดนาฬิกาวันจนกว่าจะออกจากโหมด
            // (Resume ตอน active=false ปลอดภัยเสมอ — ถ้าไม่มีเหตุนี้ค้างอยู่ HashSet.Remove เป็น no-op)
            if (active) TimeManager.Instance?.Pause(PauseReason.Demolition);
            else TimeManager.Instance?.Resume(PauseReason.Demolition);
        }

        private void Update()
        {
            if (!_isDemolishing) return;

            UpdateHighlight();

            if (Input.GetMouseButtonDown(0))
                TryDemolish();
            else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) // §15: ออกจากโหมด = คลิกขวา หรือ Esc
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    LastEscCancelFrame = Time.frameCount;
                EventManager.Instance.RaiseDemolishModeToggled(false);
            }
        }

        private void UpdateHighlight()
        {
            _hoveredCell = InputManager.Instance.GetMouseGridPosition();

            if (highlightRenderer == null) return;

            // แปลงช่องใต้เมาส์ → อาคารที่ footprint ครอบอยู่ (ทุกช่องของตัวอาคาร ไม่ใช่แค่ origin)
            // sprite อาคารวาดที่กึ่งกลาง footprint คลิกบนตัวอาคารจึงมักตกช่องที่ไม่ใช่ origin — ต้อง resolve ก่อน
            bool demolishable = CanDemolishAt(_hoveredCell, out _, out _);

            highlightRenderer.transform.position =
                GridManager.Instance.IsoToWorld(_hoveredCell.x, _hoveredCell.y);
            highlightRenderer.color = demolishable ? canDemolishColor : cannotDemolishColor;
            highlightRenderer.gameObject.SetActive(true);
        }

        /// <summary>
        /// อาคารที่ footprint ครอบ cell นี้ทุบได้ไหม — คืน origin ที่ใช้สั่งทุบ + data
        /// (ทุบไม่ได้ = ไม่มีอาคาร / กำลังก่อสร้าง / CoreTower / Laboratory / แหล่งแร่)
        /// </summary>
        private bool CanDemolishAt(Vector2Int cell, out Vector2Int origin, out BuildingData data)
        {
            origin = default; data = null;
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.TryGetBuildingAt(cell, out origin, out data))
                return false;

            if (ConstructionController.Instance != null &&
                ConstructionController.Instance.IsUnderConstruction(origin))
                return false;

            if (data.buildingType == BuildingType.CoreTower ||
                data.buildingType == BuildingType.Laboratory ||
                data.isOreNode)
                return false;

            return true;
        }

        private void TryDemolish()
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.TryGetBuildingAt(_hoveredCell, out var origin, out var data))
                return; // ไม่มีอาคารที่ footprint ครอบช่องนี้

            if (ConstructionController.Instance != null &&
                ConstructionController.Instance.IsUnderConstruction(origin))
            {
                Debug.Log("[DemolitionController] อาคารนี้ยังก่อสร้างอยู่ — ยกเลิกผ่าน BuildingQueueUI แทน");
                return;
            }

            // CORE TOWER มากับแมพและสร้างคืนไม่ได้ (ไม่อยู่ใน hotbar) — ทุบแล้วเกมตัน จึงห้ามทุบ
            if (data.buildingType == BuildingType.CoreTower)
            {
                EventManager.Instance.RaiseNotice("CORE TOWER คือหัวใจของภารกิจ — ทุบทิ้งไม่ได้");
                return;
            }

            // โรงวิจัยมากับแมพหลังเดียว สร้างคืนไม่ได้ — ทุบแล้วฝึกคลาส/ผลิต Knowledge ตันถาวร จึงห้ามทุบ
            if (data.buildingType == BuildingType.Laboratory)
            {
                EventManager.Instance.RaiseNotice("โรงวิจัยมากับเมืองแต่แรกและมีหลังเดียว — ทุบทิ้งไม่ได้");
                return;
            }

            // แหล่งแร่เป็นภูมิประเทศ (มากับแมพ สร้างคืนไม่ได้) — ห้ามทุบ เอาคนออกได้อย่างเดียว
            if (data.isOreNode)
            {
                EventManager.Instance.RaiseNotice("แหล่งแร่เป็นภูมิประเทศธรรมชาติ — ทุบไม่ได้");
                return;
            }

            Debug.Log($"[DemolitionController] ทุบ {data.buildingName} ที่ origin ({origin.x},{origin.y})");

            // cascade: GridManager.HandleBuildingRemoved → free cells
            //          BuildingVisualSpawner.HandleBuildingRemoved → destroy sprite
            //          BuildingRegistry.HandleBuildingRemoved → remove from dict
            //          ConstructionController.HandleBuildingRemoved → no-op (not in queue)
            // ต้องสั่งด้วย origin เสมอ — dict/footprint ทั้งหมดคีย์ด้วย origin (ไม่ใช่ช่องที่คลิก)
            EventManager.Instance.RaiseBuildingRemoved(origin);

            // workers เป็น reserve pool — ไม่ถูกหักตอนวาง จึงไม่ต้องคืนตอนทุบ
            // (อาคารหายไปจาก registry แล้ว demand รวมจะลดเอง ทำให้ workerScale ของอาคารอื่นดีขึ้น)
        }
    }
}
