using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตึกที่มากับแมพตั้งแต่เริ่มเกม (เช่น CORE TOWER กลางเมือง — ผู้เล่นไม่ต้องลากวางเอง)
    /// ตอน Start: จอง footprint บนกริด → raise OnBuildingPlaced (เข้า pipeline ปกติ:
    /// registry/visual/power ฯลฯ) → ข้ามคิวก่อสร้างด้วย OnConstructionCompleteRequested
    /// หมายเหตุ: ตึกต้องราคา 0 (ResourceManager หักค่าสร้างตามปกติตอนรับ event)
    /// การโหลดเซฟไม่ชนกัน — OnSaveLoaded มาทีหลัง Start และ rebuild registry จากเซฟ (ที่มีตึกนี้อยู่แล้ว)
    /// </summary>
    public class PrePlacedBuilding : MonoBehaviour
    {
        [Header("Wiring (ผูกโดย setup script)")]
        public BuildingData building;
        public Vector2Int cell;

        private void Start()
        {
            if (building == null || GridManager.Instance == null || EventManager.Instance == null)
                return;

            // กันวางซ้ำ (setup รันซ้ำ/มีของอยู่แล้ว)
            var registry = BuildingRegistry.Instance;
            if (registry != null && registry.PlacedBuildings.ContainsKey(cell))
                return;

            // ตรวจ footprint ว่าง + อยู่ในกริดทั้งผืน
            for (int dx = 0; dx < building.size.x; dx++)
                for (int dy = 0; dy < building.size.y; dy++)
                {
                    var c = GridManager.Instance.GetCell(cell.x + dx, cell.y + dy);
                    if (c == null || c.isOccupied)
                    {
                        Debug.LogWarning($"[PrePlacedBuilding] วาง {building.buildingName} ที่ ({cell.x},{cell.y}) ไม่ได้ — ช่องไม่ว่าง/นอกกริด");
                        return;
                    }
                }

            // จอง footprint (เหมือน PlacementController.OccupyFootprint)
            for (int dx = 0; dx < building.size.x; dx++)
                for (int dy = 0; dy < building.size.y; dy++)
                {
                    var c = GridManager.Instance.GetCell(cell.x + dx, cell.y + dy);
                    c.isOccupied = true;
                    c.buildingType = building.buildingType;
                }

            EventManager.Instance.RaiseBuildingPlaced(GridManager.Instance.GetCell(cell.x, cell.y), building);
            EventManager.Instance.RaiseConstructionCompleteRequested(cell); // มากับแมพ = สร้างเสร็จแล้ว

            Debug.Log($"[PrePlacedBuilding] {building.buildingName} พร้อมใช้งานที่ ({cell.x},{cell.y})");
        }
    }
}
