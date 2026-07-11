using System.Collections.Generic;
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
        CoreTower,
        PowerConduit,
        Mine,
        Memorial,  // อนุสรณ์ทีมสร้างหอคอย (Story Guide §4) — pre-placed, คลิกเปิดแผงรายชื่อ
        OreDeposit, // แหล่งแร่เหล็ก โซน A/B (V4 §5) — scatter โดย OreDepositManager, ทุบไม่ได้
        Hospital   // โรงพยาบาล (GDD §6) — รักษาคนป่วย + ลดรังสี เมื่อมี Medic ประจำครบ (ต่อท้ายเสมอ — กันเลื่อนค่า serialize)
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
    /// จัดการ Isometric Grid ของเมือง Veltara (ขนาดกำหนดใน Inspector — ค่า default 20×12)
    /// แปลงพิกัด grid (col, row) <-> world position และเก็บสถานะของแต่ละ Cell
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("Grid Size")]
        public int columns = 20;
        public int rows = 12;

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
        // ติดตาม footprint ของแต่ละอาคาร (origin → size) เพื่อ free cells เมื่อทุบ
        private readonly Dictionary<Vector2Int, Vector2Int> _buildingFootprints =
            new Dictionary<Vector2Int, Vector2Int>();

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
            EventManager.Instance.OnBuildingPlaced  += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded      += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced  -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded      -= HandleSaveLoaded;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            var origin = new Vector2Int(cell.col, cell.row);
            _buildingFootprints[origin] = data.size;
        }

        private void HandleBuildingRemoved(Vector2Int origin)
        {
            if (!_buildingFootprints.TryGetValue(origin, out Vector2Int size)) return;

            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                {
                    var cell = GetCell(origin.x + dx, origin.y + dy);
                    if (cell == null) continue;
                    cell.isOccupied   = false;
                    cell.buildingType = BuildingType.None;
                }

            _buildingFootprints.Remove(origin);
        }

        /// <summary>
        /// รีเซ็ต grid แล้วทำเครื่องหมาย cell ที่ถูกครอบครองใหม่ตาม placedBuildings ใน save
        /// (อ่าน footprint จาก BuildingRegistry.GetBuildingDataByName ซึ่งเป็น read-only lookup)
        /// </summary>
        private void HandleSaveLoaded(SaveData save)
        {
            InitializeGrid();
            _buildingFootprints.Clear();

            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = BuildingRegistry.Instance.GetBuildingDataByName(save.buildingTypes[i]);
                if (data == null) continue;

                Vector2Int origin = save.placedBuildings[i];
                _buildingFootprints[origin] = data.size;

                for (int dx = 0; dx < data.size.x; dx++)
                    for (int dy = 0; dy < data.size.y; dy++)
                    {
                        Cell cell = GetCell(origin.x + dx, origin.y + dy);
                        if (cell == null) continue;
                        cell.isOccupied   = true;
                        cell.buildingType = data.buildingType;
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
                for (int row = 0; row < rows; row++)
                    grid[col, row] = new Cell(col, row);

            _buildingFootprints.Clear();
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
        /// เวอร์ชัน float ของ IsoToWorld — สำหรับตำแหน่งลื่นระหว่าง cell (เช่น worker เดิน/jitter)
        /// สูตรเดียวกับ IsoToWorld ทุกประการ (ไม่แตะของเดิมที่ถูกเทสต์)
        /// </summary>
        public Vector3 IsoToWorldF(float col, float row)
        {
            float x = (col - row) * (tileWidth / 2f);
            float y = (col + row) * (tileHeight / 2f);
            return originOffset + new Vector3(x, y, 0f);
        }

        /// <summary>
        /// จุดกึ่งกลางเชิงภาพของ footprint (size = a×b tiles) ที่มุมเริ่มที่ origin
        /// อาคาร multi-tile ต้อง anchor ที่นี่ (ไม่ใช่ origin corner) sprite ฐานล่างกลางจึงนั่งตรงช่อง
        /// 1×1 → คืนค่าเท่ากับ IsoToWorld(origin) เดิม (ไม่กระทบอาคารช่องเดียว)
        /// </summary>
        public Vector3 FootprintCenterWorld(Vector2Int origin, Vector2Int size)
        {
            int sx = Mathf.Max(1, size.x);
            int sy = Mathf.Max(1, size.y);
            return IsoToWorldF(origin.x + (sx - 1) * 0.5f, origin.y + (sy - 1) * 0.5f);
        }

        // ── sorting order ร่วม (iso depth) ─────────────────────────
        // sprite ที่ base อยู่ "หน้ากว่า" (col+row มาก) ต้องวาดทับ → sortingOrder สูงกว่า
        // ×SortScale ให้ละเอียดระดับเศษ tile (อาคาร multi-tile ฐานกึ่งกลาง / worker ที่เดินต่อเนื่อง)
        public const float SortScale = 16f;
        public static int SortOrder(float col, float row) => Mathf.RoundToInt((col + row) * SortScale);

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
        /// เวอร์ชัน float ของ WorldToIso — ไม่ปัดเป็น cell (worker เดินลื่นระหว่างช่อง ต้องรู้เศษ)
        /// สูตรเดียวกับ WorldToIso ทุกประการ (ไม่แตะของเดิมที่ถูกเทสต์)
        /// </summary>
        public Vector2 WorldToIsoF(Vector3 worldPos)
        {
            Vector3 local = worldPos - originOffset;

            float col = (local.x / tileWidth) + (local.y / tileHeight);
            float row = (local.y / tileHeight) - (local.x / tileWidth);

            return new Vector2(col, row);
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
