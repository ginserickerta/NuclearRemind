using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ประเภทสิ่งก่อสร้างที่สามารถวางบน Cell ได้
    /// </summary>
    public enum BuildingType
    {
        None,
        Habitat,
        Farm,
        WaterPlant,
        PowerPlant,
        RadiationShelter,
        Laboratory,
        CoreTower
    }

    /// <summary>
    /// ข้อมูลของแต่ละช่องในตาราง Isometric Grid
    /// </summary>
    [System.Serializable]
    public class Cell
    {
        public int col;
        public int row;
        public bool isOccupied;
        public BuildingType buildingType;
        public int radiationLevel;

        public Cell(int col, int row)
        {
            this.col = col;
            this.row = row;
            isOccupied = false;
            buildingType = BuildingType.None;
            radiationLevel = 0;
        }
    }

    /// <summary>
    /// จัดการ Isometric Grid ขนาด 12x9 ของเมือง Veltara
    /// แปลงพิกัด grid (col, row) <-> world position และเก็บสถานะของแต่ละ Cell
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("Grid Size")]
        public int columns = 12;
        public int rows = 9;

        [Header("Tile Dimensions (Isometric)")]
        public float tileWidth = 1f;
        public float tileHeight = 0.5f;

        [Header("Origin")]
        public Vector3 originOffset = Vector3.zero;

        [Header("Gizmos")]
        public bool showGridGizmos = true;
        public Color gridColor = Color.cyan;
        public Color occupiedColor = Color.red;

        private Cell[,] grid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeGrid();
        }

        private void OnEnable()
        {
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        /// <summary>
        /// รีเซ็ต grid แล้วทำเครื่องหมาย cell ที่ถูกครอบครองใหม่ตาม placedBuildings ใน save
        /// (อ่าน footprint จาก BuildingRegistry.GetBuildingDataByName ซึ่งเป็น read-only lookup)
        /// </summary>
        private void HandleSaveLoaded(SaveData save)
        {
            InitializeGrid();

            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = BuildingRegistry.Instance.GetBuildingDataByName(save.buildingTypes[i]);
                if (data == null)
                    continue;

                Vector2Int origin = save.placedBuildings[i];
                for (int dx = 0; dx < data.size.x; dx++)
                {
                    for (int dy = 0; dy < data.size.y; dy++)
                    {
                        Cell cell = GetCell(origin.x + dx, origin.y + dy);
                        if (cell == null) continue;

                        cell.isOccupied = true;
                        cell.buildingType = data.buildingType;
                    }
                }
            }
        }

        /// <summary>
        /// สร้าง Cell ทั้งหมดของ grid ตามขนาด columns x rows
        /// (เรียกได้จากภายนอกเพื่อรีเซ็ต grid เมื่อเริ่มเกมใหม่)
        /// </summary>
        public void InitializeGrid()
        {
            grid = new Cell[columns, rows];
            for (int col = 0; col < columns; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    grid[col, row] = new Cell(col, row);
                }
            }
        }

        /// <summary>
        /// แปลงพิกัด grid (col, row) เป็นตำแหน่งใน world space แบบ isometric
        /// </summary>
        public Vector3 IsoToWorld(int col, int row)
        {
            float x = (col - row) * (tileWidth / 2f);
            float y = (col + row) * (tileHeight / 2f);
            return originOffset + new Vector3(x, y, 0f);
        }

        /// <summary>
        /// แปลงตำแหน่ง world space เป็นพิกัด grid (col, row) ที่ใกล้ที่สุด
        /// </summary>
        public Vector2Int WorldToIso(Vector3 worldPos)
        {
            Vector3 local = worldPos - originOffset;

            float col = (local.x / tileWidth) + (local.y / tileHeight);
            float row = (local.y / tileHeight) - (local.x / tileWidth);

            return new Vector2Int(Mathf.RoundToInt(col), Mathf.RoundToInt(row));
        }

        /// <summary>
        /// ตรวจสอบว่าพิกัด (col, row) อยู่ในขอบเขตของ grid หรือไม่
        /// </summary>
        public bool IsInBounds(int col, int row)
        {
            return col >= 0 && col < columns && row >= 0 && row < rows;
        }

        /// <summary>
        /// คืนค่า Cell ที่ตำแหน่ง (col, row) หรือ null ถ้าอยู่นอกขอบเขต
        /// </summary>
        public Cell GetCell(int col, int row)
        {
            if (!IsInBounds(col, row))
                return null;

            return grid[col, row];
        }

        private void OnDrawGizmos()
        {
            if (!showGridGizmos)
                return;

            for (int col = 0; col < columns; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    Vector3 center = IsoToWorld(col, row);

                    Vector3 top = center + new Vector3(0f, tileHeight / 2f, 0f);
                    Vector3 right = center + new Vector3(tileWidth / 2f, 0f, 0f);
                    Vector3 bottom = center + new Vector3(0f, -tileHeight / 2f, 0f);
                    Vector3 left = center + new Vector3(-tileWidth / 2f, 0f, 0f);

                    bool occupied = Application.isPlaying && grid != null
                        && col < grid.GetLength(0) && row < grid.GetLength(1)
                        && grid[col, row].isOccupied;
                    Gizmos.color = occupied ? occupiedColor : gridColor;

                    Gizmos.DrawLine(top, right);
                    Gizmos.DrawLine(right, bottom);
                    Gizmos.DrawLine(bottom, left);
                    Gizmos.DrawLine(left, top);
                }
            }
        }
    }
}
