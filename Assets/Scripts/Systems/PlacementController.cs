using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการการวางอาคารบน grid: แสดง ghost preview สีเขียว/แดง,
    /// ยืนยันการวางด้วยคลิกซ้าย, ยกเลิกด้วยคลิกขวา
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        [Header("Ghost Preview")]
        public SpriteRenderer ghostRenderer;

        [Header("Ghost Colors")]
        public Color validColor = new Color(0f, 1f, 0f, 0.5f);
        public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

        private BuildingData selectedBuilding;
        private Vector2Int currentCell;
        private bool isPlacing;

        private void Awake()
        {
            if (ghostRenderer != null)
                ghostRenderer.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isPlacing || selectedBuilding == null)
                return;

            UpdateGhost();

            if (Input.GetMouseButtonDown(0))
                ConfirmPlace();
            else if (Input.GetMouseButtonDown(1))
                CancelPlacement();
        }

        /// <summary>
        /// เริ่มโหมดวางอาคารด้วยข้อมูลอาคารที่เลือก
        /// </summary>
        public void BeginPlacement(BuildingData buildingData)
        {
            selectedBuilding = buildingData;
            isPlacing = true;

            if (ghostRenderer != null)
            {
                ghostRenderer.sprite = buildingData.sprite;
                ghostRenderer.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// อัปเดตตำแหน่งและสีของ ghost ตามตำแหน่งเมาส์บน grid
        /// </summary>
        private void UpdateGhost()
        {
            currentCell = InputManager.Instance.GetMouseGridPosition();

            if (ghostRenderer == null)
                return;

            ghostRenderer.transform.position = GridManager.Instance.IsoToWorld(currentCell.x, currentCell.y);
            ghostRenderer.color = IsPlacementValid(currentCell) ? validColor : invalidColor;
        }

        /// <summary>
        /// ตรวจสอบว่าวางอาคาร (ตาม footprint ของ selectedBuilding) ที่ตำแหน่งนี้ได้หรือไม่
        /// </summary>
        private bool IsPlacementValid(Vector2Int origin)
        {
            for (int dx = 0; dx < selectedBuilding.size.x; dx++)
            {
                for (int dy = 0; dy < selectedBuilding.size.y; dy++)
                {
                    Cell cell = GridManager.Instance.GetCell(origin.x + dx, origin.y + dy);
                    if (cell == null || cell.isOccupied)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ยืนยันการวางอาคารที่ตำแหน่งปัจจุบัน ถ้าตำแหน่งใช้ได้
        /// </summary>
        public void ConfirmPlace()
        {
            if (!IsPlacementValid(currentCell))
            {
                Debug.Log("[PlacementController] ตำแหน่งนี้วางอาคารไม่ได้");
                return;
            }

            OccupyFootprint(currentCell);

            Debug.Log($"[PlacementController] วาง {selectedBuilding.buildingName} ที่ ({currentCell.x}, {currentCell.y})");
            EventManager.Instance.RaiseBuildingPlaced(GridManager.Instance.GetCell(currentCell.x, currentCell.y), selectedBuilding);

            EndPlacement();
        }

        /// <summary>
        /// ทำเครื่องหมาย cell ทั้งหมดใน footprint ของอาคารว่าถูกครอบครองแล้ว
        /// </summary>
        private void OccupyFootprint(Vector2Int origin)
        {
            for (int dx = 0; dx < selectedBuilding.size.x; dx++)
            {
                for (int dy = 0; dy < selectedBuilding.size.y; dy++)
                {
                    Cell cell = GridManager.Instance.GetCell(origin.x + dx, origin.y + dy);
                    cell.isOccupied = true;
                    cell.buildingType = selectedBuilding.buildingType;
                }
            }
        }

        /// <summary>
        /// ยกเลิกการวางอาคาร
        /// </summary>
        public void CancelPlacement()
        {
            Debug.Log("[PlacementController] ยกเลิกการวางอาคาร");
            EventManager.Instance.RaisePlacementCancelled();
            EndPlacement();
        }

        /// <summary>
        /// ออกจากโหมดวางอาคารและซ่อน ghost
        /// </summary>
        private void EndPlacement()
        {
            isPlacing = false;
            selectedBuilding = null;

            if (ghostRenderer != null)
                ghostRenderer.gameObject.SetActive(false);
        }
    }
}
