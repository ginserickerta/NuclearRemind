using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ construction queue: ทุก building ที่วางใหม่ต้องมี "คนงานเดินมาสร้าง" ถึงจะคืบ
    /// - ไม่มีคนประจำ → ไม่คืบเลย (Building สร้างเองไม่ได้)
    /// - มีคนแล้วต้องรอ Worker เดินไปถึงไซต์ก่อน (เข้าชุดกับงานขุดเหมือง OreDepositManager) แล้วจึงเริ่มสะสม tick
    /// - ก้าวหน้า/ tick = จำนวนคนที่ประจำ (คนแปรผกผันกับเวลา) · ระหว่างสร้างไม่ produce/ไม่เป็น CoreTower part
    /// Cancel = คืน energyCost (workers เป็น reserve pool ไม่ถูกหัก) | Prioritize = ให้ถึงไซต์ทันที + จบ tick ถัดไป (ถ้ามีคน)
    /// </summary>
    public class ConstructionController : MonoBehaviour
    {
        public static ConstructionController Instance { get; private set; }

        // ค่าตั้งต้นเวลาสร้าง (tick) เมื่อ BuildingData.buildTicks ไม่ได้ตั้ง — ต่ออาคารเก็บใน _totalTicks
        public const int DefaultConstructionTicks = 10;

        [Header("Worker walk (คนงานต้องเดินไปถึงไซต์ก่อนเริ่มสร้าง — เข้าชุดกับ OreDepositManager)")]
        [Tooltip("ความเร็วเดินคนงาน (world units/วินาที) — ให้ตรงกับ WorkerView.speed / OreDepositManager.workerSpeed")]
        public float workerSpeed = 1.0f;

        // ความคืบหน้าแสดงในแผง hover (BuildingUpgradeUI) + BuildingQueueUI ผ่าน OnConstructionProgressChanged
        // (บาร์ลอย world-space ถูกถอดแล้ว — GDD §6 UI polish)
        // List รักษาลำดับสำหรับ Prioritize, Dict ให้ O(1) lookup
        private readonly List<Vector2Int> _queue = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _progress = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _totalTicks = new Dictionary<Vector2Int, int>(); // เวลาสร้างเต็มต่ออาคาร (จาก buildTicks)
        // เวลาเดินของคนงานไปถึงไซต์ (real-time · ล้องานขุดเหมือง): _walkNeed = วินาทีที่ต้องเดิน · _walkTime = เดินสะสม
        // (รีเซ็ตเป็น 0 เมื่อไม่มีคนประจำ) — tick ก่อสร้างเริ่มสะสมเมื่อ _walkTime ≥ _walkNeed เท่านั้น
        private readonly Dictionary<Vector2Int, float> _walkNeed = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _walkTime = new Dictionary<Vector2Int, float>();

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

        /// <summary>มีคนประจำไซต์นี้ไหม (0 = ยังไม่มีคนมาสร้าง)</summary>
        public int AssignedWorkers(Vector2Int cell) =>
            WorkerAssignmentManager.Instance != null ? WorkerAssignmentManager.Instance.GetAssigned(cell) : 0;

        /// <summary>คนงานยังเดินไปไม่ถึงไซต์ (ยังไม่เริ่มสร้าง) — UI อ่าน read-only</summary>
        public bool IsWalking(Vector2Int cell)
            => (_walkTime.TryGetValue(cell, out float t) ? t : 0f)
             < (_walkNeed.TryGetValue(cell, out float n) ? n : 0f);

        /// <summary>เวลาที่เหลือก่อนคนเดินถึงไซต์ (วินาที)</summary>
        public float GetWalkRemaining(Vector2Int cell)
        {
            float need = _walkNeed.TryGetValue(cell, out float n) ? n : 0f;
            float walked = _walkTime.TryGetValue(cell, out float t) ? t : 0f;
            return Mathf.Max(0f, need - walked);
        }

        // ─────────────────────────────────────────
        //  Worker walk — คนงานต้องเดินไปถึงไซต์ก่อนเริ่มสร้าง (real-time · ล้อ OreDepositManager)
        // ─────────────────────────────────────────

        private void Update()
        {
            // หยุดตามนาฬิกาเกม (โหมดวาง/ทุบ/พอส) — สอดคล้องกับ ResourceManager/OreDepositManager
            if (TimeManager.Instance != null && !TimeManager.Instance.IsRunning) return;
            AdvanceWalk(Time.deltaTime);
        }

        /// <summary>เดินคนงานเข้าไซต์ที่กำลังสร้าง dt วินาที — แยกจาก Time.deltaTime ให้เทสต์คุมเวลาได้</summary>
        public void AdvanceWalk(float dt)
        {
            if (dt <= 0f) return;
            var assign = WorkerAssignmentManager.Instance;

            for (int i = 0; i < _queue.Count; i++)
            {
                var pos = _queue[i];
                int workers = assign != null ? assign.GetAssigned(pos) : 0;
                if (workers <= 0) { _walkTime[pos] = 0f; continue; } // ไม่มีคน → รีเซ็ตการเดิน

                float walked = _walkTime.TryGetValue(pos, out float t) ? t : 0f;
                float need = _walkNeed.TryGetValue(pos, out float n) ? n : 0f;
                if (walked < need) _walkTime[pos] = walked + dt; // เดินแบบ real-time (ไม่ขึ้นกับจำนวนคน)
            }
        }

        // เวลาเดินไปถึงไซต์ = ระยะจากจุดพัก idle (กลางกริด ~CORE TOWER) ถึงไซต์ ÷ ความเร็วเดิน
        // grid ยังไม่พร้อม/ความเร็ว ≤ 0 → 0 (เริ่มสร้างทันที) เพื่อไม่ให้ค้างในเทสต์/ระหว่าง init
        private float ComputeWalkSeconds(Vector2Int pos)
        {
            var grid = GridManager.Instance;
            if (grid == null || workerSpeed <= 0f) return 0f;
            Vector3 from = grid.IsoToWorldF((grid.columns - 1) * 0.5f, (grid.rows - 1) * 0.5f);
            Vector3 to = grid.IsoToWorld(pos.x, pos.y);
            return Vector3.Distance(from, to) / workerSpeed;
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
            _walkNeed[pos] = ComputeWalkSeconds(pos); // ระยะที่คนงานต้องเดินมาถึงไซต์ก่อนเริ่มสร้าง
            _walkTime[pos] = 0f;
            EventManager.Instance.RaiseConstructionProgressChanged(pos, 0);
        }

        private void HandleBuildingRemoved(Vector2Int pos)
        {
            _queue.Remove(pos);
            _progress.Remove(pos);
            _totalTicks.Remove(pos);
            _walkNeed.Remove(pos);
            _walkTime.Remove(pos);
        }

        private void HandleGameTick()
        {
            // snapshot ก่อน iterate เผื่อ Complete ลบ entry ออกระหว่างลูป
            var snapshot = new List<Vector2Int>(_queue);

            foreach (var pos in snapshot)
            {
                if (!_progress.TryGetValue(pos, out int current)) continue;

                // ต้องมีคนงานประจำ + เดินมาถึงไซต์แล้ว ถึงจะคืบ (Building สร้างเองไม่ได้)
                int workers = AssignedWorkers(pos);
                if (workers <= 0) continue;   // ยังไม่มีคนมาสร้าง → ไม่คืบ
                if (IsWalking(pos)) continue;  // คนยังเดินมาไม่ถึง → ยังไม่เริ่มสร้าง

                int next = current + workers;  // ก้าวหน้า/tick = จำนวนคน (คนมาก = เร็ว)

                if (next >= GetTotalTicks(pos))
                {
                    _progress.Remove(pos);
                    _totalTicks.Remove(pos);
                    _queue.Remove(pos);
                    _walkNeed.Remove(pos);
                    _walkTime.Remove(pos);

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
            _walkNeed.Remove(pos);
            _walkTime.Remove(pos);

            if (BuildingRegistry.Instance != null &&
                BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data))
                EventManager.Instance.RaiseConstructionComplete(pos, data);
        }

        private void HandlePrioritizeRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            // ตั้ง progress เป็น total-1 → จะเสร็จบน tick ถัดไป (ยังต้องมีคนประจำถึงจะจบ — สร้างเองไม่ได้)
            int nearDone = Mathf.Max(0, GetTotalTicks(pos) - 1);
            _progress[pos] = nearDone;
            _walkTime[pos] = _walkNeed.TryGetValue(pos, out float n) ? n : 0f; // เร่ง = ถือว่าคนถึงไซต์แล้ว

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
            _walkNeed.Clear();
            _walkTime.Clear();

            if (save.underConstructionCells == null) return;

            var registry = BuildingRegistry.Instance;
            for (int i = 0; i < save.underConstructionCells.Count; i++)
            {
                var pos = save.underConstructionCells[i];
                int prog = (save.constructionProgress != null && i < save.constructionProgress.Count)
                    ? save.constructionProgress[i] : 0;

                _queue.Add(pos);
                _progress[pos] = prog;
                _walkNeed[pos] = ComputeWalkSeconds(pos); // การเดินไม่ได้เซฟ — คนต้องเดินมาใหม่หลังโหลด
                _walkTime[pos] = 0f;
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
