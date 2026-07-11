using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ construction queue: ทุก building ที่วางใหม่ต้องรอ 10 tick จึงจะ active
    /// ระหว่างสร้าง building ไม่ produce resource และไม่นับเป็น CoreTower part
    /// Cancel = คืน energyCost (workers เป็น reserve pool ไม่ถูกหัก) | Prioritize = completes บน tick ถัดไป
    /// </summary>
    public class ConstructionController : MonoBehaviour
    {
        public static ConstructionController Instance { get; private set; }

        // ค่าตั้งต้นเวลาสร้าง (tick) เมื่อ BuildingData.buildTicks ไม่ได้ตั้ง — ต่ออาคารเก็บใน _totalTicks
        public const int DefaultConstructionTicks = 10;

        // ความคืบหน้าแสดงในแผง hover (BuildingUpgradeUI) + BuildingQueueUI ผ่าน OnConstructionProgressChanged
        // (บาร์ลอย world-space ถูกถอดแล้ว — GDD §6 UI polish)
        // List รักษาลำดับสำหรับ Prioritize, Dict ให้ O(1) lookup
        private readonly List<Vector2Int> _queue = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _progress = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _totalTicks = new Dictionary<Vector2Int, int>(); // เวลาสร้างเต็มต่ออาคาร (จาก buildTicks)

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
            EventManager.Instance.OnBuildingPlaced            += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved           += HandleBuildingRemoved;
            EventManager.Instance.OnGameTick                  += HandleGameTick;
            EventManager.Instance.OnSaveLoaded                += HandleSaveLoaded;
            EventManager.Instance.OnConstructionCancelRequested     += HandleCancelRequested;
            EventManager.Instance.OnConstructionPrioritizeRequested += HandlePrioritizeRequested;
            EventManager.Instance.OnConstructionCompleteRequested   += HandleCompleteRequested;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced            -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved           -= HandleBuildingRemoved;
            EventManager.Instance.OnGameTick                  -= HandleGameTick;
            EventManager.Instance.OnSaveLoaded                -= HandleSaveLoaded;
            EventManager.Instance.OnConstructionCancelRequested     -= HandleCancelRequested;
            EventManager.Instance.OnConstructionPrioritizeRequested -= HandlePrioritizeRequested;
            EventManager.Instance.OnConstructionCompleteRequested   -= HandleCompleteRequested;
        }

        // ─────────────────────────────────────────
        //  Public queries (read-only — ใช้ได้โดย ResourceManager / CoreTowerManager)
        // ─────────────────────────────────────────

        public bool IsUnderConstruction(Vector2Int cell) => _progress.ContainsKey(cell);

        public int GetProgress(Vector2Int cell) =>
            _progress.TryGetValue(cell, out int p) ? p : GetTotalTicks(cell);

        /// <summary>เวลาสร้างเต็ม (tick) ของอาคารที่ cell นี้ — จาก BuildingData.buildTicks (fallback ค่าตั้งต้น)</summary>
        public int GetTotalTicks(Vector2Int cell) =>
            _totalTicks.TryGetValue(cell, out int t) ? t : DefaultConstructionTicks;

        // ─────────────────────────────────────────
        //  Construction speed — แปรผกผันกับจำนวนคนงาน (V4 §5)
        // ─────────────────────────────────────────

        /// <summary>
        /// ก้าวหน้าการสร้างต่อ tick = จำนวน Worker ที่ประจำ cell นั้น
        /// ★ ไม่มีคนงาน = 0 = ไม่คืบหน้า (อาคารสร้างเองไม่ได้ ต้องจัดคนเข้าก่อน — V4 §5)
        /// → เวลาสร้าง (ticks) = TotalConstructionTicks / คนงาน → 1 คน = 10, 2 คน = 5, 5 คน = 2
        /// อ่าน GetAssigned() แบบ read-only query (รูปแบบเดียวกับ ResourceManager.ApplyDailyProduction —
        /// อนุญาตให้ query ข้าม manager ได้ ห้ามเฉพาะการเรียก method ที่เปลี่ยนสถานะ)
        /// </summary>
        private static int ConstructionSpeed(Vector2Int pos)
        {
            int workers = WorkerAssignmentManager.Instance != null
                ? WorkerAssignmentManager.Instance.GetAssigned(pos)
                : 0;
            return Mathf.Max(0, workers);
        }

        // ─────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            var pos = new Vector2Int(cell.col, cell.row);
            if (_progress.ContainsKey(pos)) return;

            _queue.Add(pos);
            _progress[pos] = 0;
            _totalTicks[pos] = data != null ? Mathf.Max(1, data.buildTicks) : DefaultConstructionTicks;
            EventManager.Instance.RaiseConstructionProgressChanged(pos, 0);
        }

        private void HandleBuildingRemoved(Vector2Int pos)
        {
            _queue.Remove(pos);
            _progress.Remove(pos);
            _totalTicks.Remove(pos);
        }

        private void HandleGameTick()
        {
            // snapshot ก่อน iterate เผื่อ Complete ลบ entry ออกระหว่างลูป
            var snapshot = new List<Vector2Int>(_queue);

            foreach (var pos in snapshot)
            {
                if (!_progress.TryGetValue(pos, out int current)) continue;

                int speed = ConstructionSpeed(pos);
                if (speed <= 0) continue; // ไม่มีคนงาน → หยุดรอ ไม่คืบหน้า (ไม่ยิง event ซ้ำทุก tick)

                int next = current + speed;

                if (next >= GetTotalTicks(pos))
                {
                    _progress.Remove(pos);
                    _totalTicks.Remove(pos);
                    _queue.Remove(pos);

                    if (BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data))
                        EventManager.Instance.RaiseConstructionComplete(pos, data);
                }
                else
                {
                    _progress[pos] = next;
                    EventManager.Instance.RaiseConstructionProgressChanged(pos, next);
                }
            }
        }

        private void HandleCancelRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            // ดึง BuildingData ก่อนที่ OnBuildingRemoved จะลบออกจาก Registry
            BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data);

            // cascade: GridManager, BuildingVisualSpawner, BuildingRegistry,
            // และ HandleBuildingRemoved ของ ConstructionController เอง
            EventManager.Instance.RaiseBuildingRemoved(pos);

            if (data == null) return;

            // คืนเฉพาะ energyCost (ต้นทุนสร้าง) — workers เป็น reserve pool ไม่ถูกหักตอนวาง จึงไม่ต้องคืน
            EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, data.energyCost);
        }

        // สร้างเสร็จทันที (ข้ามคิว) — ใช้กับตึกที่มากับแมพ (PrePlacedBuilding เช่น CORE TOWER กลางเมือง)
        private void HandleCompleteRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            _progress.Remove(pos);
            _totalTicks.Remove(pos);
            _queue.Remove(pos);

            if (BuildingRegistry.Instance != null &&
                BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data))
                EventManager.Instance.RaiseConstructionComplete(pos, data);
        }

        private void HandlePrioritizeRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            // ตั้ง progress เป็น total-1 → จะเสร็จบน tick ถัดไป
            int nearDone = Mathf.Max(0, GetTotalTicks(pos) - 1);
            _progress[pos] = nearDone;

            // เลื่อนขึ้นหน้าสุดของ queue เพื่อให้ tick ก่อนตัวอื่น
            _queue.Remove(pos);
            _queue.Insert(0, pos);

            EventManager.Instance.RaiseConstructionProgressChanged(pos, nearDone);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _queue.Clear();
            _progress.Clear();
            _totalTicks.Clear();

            if (save.underConstructionCells == null) return;

            var registry = BuildingRegistry.Instance;
            for (int i = 0; i < save.underConstructionCells.Count; i++)
            {
                var pos = save.underConstructionCells[i];
                int prog = (save.constructionProgress != null && i < save.constructionProgress.Count)
                    ? save.constructionProgress[i] : 0;

                _queue.Add(pos);
                _progress[pos] = prog;
                // buildTicks ไม่ได้เซฟ — ดึงจากอาคารใน registry (fallback ค่าตั้งต้นถ้ายังไม่พร้อม)
                if (registry != null && registry.PlacedBuildings.TryGetValue(pos, out var data) && data != null)
                    _totalTicks[pos] = Mathf.Max(1, data.buildTicks);
            }
        }

        // เรียกโดย SaveManager ตอน Save()
        public (List<Vector2Int> cells, List<int> progress) GetSaveState()
        {
            var cells = new List<Vector2Int>(_queue);
            var progress = new List<int>();
            foreach (var pos in _queue)
                progress.Add(_progress.TryGetValue(pos, out int p) ? p : 0);
            return (cells, progress);
        }
    }
}
