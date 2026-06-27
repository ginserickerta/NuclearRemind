using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// คำนวณการกระจายพลังงานจาก Core Tower / Power Plant ไปยังอาคารต่างๆ
    /// ใช้ BFS + Chebyshev distance เหมือน Frostpunk heat system
    /// Power Conduit สามารถ relay ต่อได้ถ้าอยู่ในช่วง power source
    /// </summary>
    public class PowerGridManager : MonoBehaviour
    {
        public static PowerGridManager Instance { get; private set; }

        [Header("Core Tower Power Range ต่อ phase (index 0..3)")]
        public int[] coreTowerRangeByPhase = { 2, 4, 6, 8 };

        private readonly HashSet<Vector2Int> _poweredCells = new HashSet<Vector2Int>();

        /// <summary>ชุด cell ทั้งหมดที่ได้รับพลังงาน (read-only snapshot)</summary>
        public HashSet<Vector2Int> PoweredCells => _poweredCells;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced       += HandleBuildingChanged;
            EventManager.Instance.OnBuildingRemoved      += HandleBuildingRemoved;
            EventManager.Instance.OnConstructionComplete += HandleConstructionComplete;
            EventManager.Instance.OnTowerPhaseComplete   += HandleTowerPhaseComplete;
            EventManager.Instance.OnSaveLoaded           += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced       -= HandleBuildingChanged;
            EventManager.Instance.OnBuildingRemoved      -= HandleBuildingRemoved;
            EventManager.Instance.OnConstructionComplete -= HandleConstructionComplete;
            EventManager.Instance.OnTowerPhaseComplete   -= HandleTowerPhaseComplete;
            EventManager.Instance.OnSaveLoaded           -= HandleSaveLoaded;
        }

        private void Start() => RecalculatePowerGrid();

        private void HandleBuildingChanged(Cell _, BuildingData __) => RecalculatePowerGrid();
        private void HandleBuildingRemoved(Vector2Int _)            => RecalculatePowerGrid();
        private void HandleConstructionComplete(Vector2Int _, BuildingData __) => RecalculatePowerGrid();
        private void HandleTowerPhaseComplete(int _)                => RecalculatePowerGrid();
        private void HandleSaveLoaded(SaveData _)                   => RecalculatePowerGrid();

        /// <summary>true ถ้าอาคารที่ origin ได้รับพลังงาน</summary>
        public bool IsBuildingPowered(Vector2Int origin) => _poweredCells.Contains(origin);

        /// <summary>
        /// คำนวณ coverage ใหม่ทั้งหมด:
        ///  1. รวบรวม primary sources (powerRange > 0 และ ไม่ใช่ relay, สร้างเสร็จแล้ว)
        ///  2. BFS: mark cell ในรัศมี Chebyshev จากแต่ละ source
        ///  3. Power Conduit ที่ origin อยู่ใน poweredCells → กลายเป็น source ด้วย (chain relay)
        ///  4. วนซ้ำจนไม่มี source ใหม่ → fire OnPowerGridChanged
        /// </summary>
        public void RecalculatePowerGrid()
        {
            _poweredCells.Clear();

            if (BuildingRegistry.Instance == null || GridManager.Instance == null) return;

            int phase  = CoreTowerManager.Instance != null ? CoreTowerManager.Instance.Current.currentPhase : 0;
            var placed = BuildingRegistry.Instance.PlacedBuildings;

            var queue            = new Queue<(Vector2Int center, int range)>();
            var processedSources = new HashSet<Vector2Int>();

            // รวบรวม primary sources (ไม่ใช่ relay)
            foreach (var kvp in placed)
            {
                var data = kvp.Value;
                if (data.powerRange <= 0 || data.isPowerRelay) continue;
                if (IsUnderConstruction(kvp.Key)) continue;

                int range = data.buildingType == BuildingType.CoreTower
                    ? GetCoreTowerRange(phase)
                    : data.powerRange;

                queue.Enqueue((kvp.Key, range));
            }

            // BFS — รองรับ relay chain
            while (queue.Count > 0)
            {
                var (center, range) = queue.Dequeue();
                if (processedSources.Contains(center)) continue;
                processedSources.Add(center);

                MarkCellsInRange(center, range);

                // ตรวจสอบ relay ที่เพิ่งถูก power → เพิ่มเข้า queue
                foreach (var kvp in placed)
                {
                    if (!kvp.Value.isPowerRelay)            continue;
                    if (processedSources.Contains(kvp.Key)) continue;
                    if (!_poweredCells.Contains(kvp.Key))   continue;
                    if (IsUnderConstruction(kvp.Key))        continue;

                    queue.Enqueue((kvp.Key, kvp.Value.powerRange));
                }
            }

            EventManager.Instance.RaisePowerGridChanged(_poweredCells);
        }

        private int GetCoreTowerRange(int phase)
        {
            int idx = Mathf.Clamp(phase, 0, coreTowerRangeByPhase.Length - 1);
            return coreTowerRangeByPhase[idx];
        }

        // Chebyshev range = ลูป dc/dr จาก -range ถึง range ครอบคลุมทุก cell ในระยะพอดี
        private void MarkCellsInRange(Vector2Int center, int range)
        {
            var grid = GridManager.Instance;
            for (int dc = -range; dc <= range; dc++)
                for (int dr = -range; dr <= range; dr++)
                {
                    int c = center.x + dc;
                    int r = center.y + dr;
                    if (grid.IsInBounds(c, r))
                        _poweredCells.Add(new Vector2Int(c, r));
                }
        }

        private static bool IsUnderConstruction(Vector2Int origin)
            => ConstructionController.Instance != null
            && ConstructionController.Instance.IsUnderConstruction(origin);
    }
}
