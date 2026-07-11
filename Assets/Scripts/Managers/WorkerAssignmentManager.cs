using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดสรรแรงงานประจำอาคารรายหลัง (V4 §5) — layer ใหม่ที่วางข้าง PopulationData
    /// - PopulationData.{workers/engineers/medics/farmers} = จำนวนที่ "มีอยู่" ต่อคลาส (source of truth เดิม ไม่แตะ)
    /// - manager นี้เก็บเฉพาะ "ใครถูก assign ไปอาคารไหน" (cell -> จำนวน)
    /// - อาคารระบุคลาสที่ต้องใช้ผ่าน BuildingData.requiredClass → assignment ดึงจาก idle pool "ของคลาสนั้น"
    /// - idle pool ต่อคลาสคำนวณเสมอ (derived): idleOfClass(c) = count(c) - Σ assigned ที่อาคารคลาส c
    ///   คลาสของแต่ละ assignment เดาจาก requiredClass ของอาคารที่ cell นั้น → save format เดิม (cell,count) พอ ไม่ต้องเพิ่ม SaveData field
    /// ผลผลิตอ่านค่า GetAssigned() ใน ResourceManager (ไม่ต้องรู้คลาส — gate ที่ตอน assign แล้ว)
    /// อ่าน BuildingRegistry/PopulationManager แบบ read-only query (เทียบเท่า .Current) — ไม่เรียก method เปลี่ยนสถานะข้าม manager
    /// </summary>
    // รันหลัง BuildingRegistry/PopulationManager (default 0) — HandleSaveLoaded ต้องอ่าน placedBuildings + workers ที่ restore แล้ว
    [DefaultExecutionOrder(50)]
    public class WorkerAssignmentManager : MonoBehaviour
    {
        public static WorkerAssignmentManager Instance { get; private set; }

        // cell (origin ของอาคาร) -> จำนวน Worker ที่ประจำ
        private readonly Dictionary<Vector2Int, int> _assigned = new Dictionary<Vector2Int, int>();

        /// <summary>จำนวน Worker ที่ประจำอาคารที่ cell นี้ (0 ถ้าไม่พบ)</summary>
        public int GetAssigned(Vector2Int cell) => _assigned.TryGetValue(cell, out int n) ? n : 0;

        /// <summary>ผลรวม Worker ที่ถูก assign ทั้งเมือง</summary>
        public int TotalAssigned
        {
            get
            {
                int sum = 0;
                foreach (var v in _assigned.Values) sum += v;
                return sum;
            }
        }

        /// <summary>Worker (คลาสพื้นฐาน) ว่าง — คง property เดิมไว้ให้ HUD pool event/โค้ดเก่าใช้ต่อ</summary>
        public int IdleWorkers => IdleOfClass(WorkerClass.Worker);

        private static int TotalWorkers => ClassCount(WorkerClass.Worker);

        // ── per-class pools (derived — ไม่เก็บซ้ำ) ────────────────────
        /// <summary>จำนวนคนของคลาสนี้ที่ "มีอยู่" (อ่านจาก PopulationData)</summary>
        private static int ClassCount(WorkerClass c)
        {
            var pm = PopulationManager.Instance;
            if (pm == null) return 0;
            var pop = pm.Current;
            switch (c)
            {
                case WorkerClass.Engineer: return pop.engineers;
                case WorkerClass.Medic:    return pop.medics;
                case WorkerClass.Farmer:   return pop.farmers;
                default:                   return pop.workers;
            }
        }

        /// <summary>คลาสที่อาคารที่ cell นี้ต้องใช้ (Worker ถ้าไม่พบอาคาร)</summary>
        private static WorkerClass ClassOfCell(Vector2Int cell)
        {
            var reg = BuildingRegistry.Instance;
            if (reg != null && reg.PlacedBuildings.TryGetValue(cell, out var data) && data != null)
                return data.requiredClass;
            return WorkerClass.Worker;
        }

        /// <summary>ผลรวมคนคลาส c ที่ถูก assign แล้ว (เฉพาะ cell ที่อาคารเป็นคลาส c)</summary>
        public int AssignedOfClass(WorkerClass c)
        {
            int sum = 0;
            foreach (var kvp in _assigned)
                if (ClassOfCell(kvp.Key) == c) sum += kvp.Value;
            return sum;
        }

        /// <summary>คนคลาส c ที่ว่าง (ยังไม่ถูก assign) = มีอยู่ − ที่ประจำแล้ว</summary>
        public int IdleOfClass(WorkerClass c) => Mathf.Max(0, ClassCount(c) - AssignedOfClass(c));

        /// <summary>read-only export สำหรับ SaveManager (เทียบเท่า CodexManager.UnlockedIds)</summary>
        public IReadOnlyDictionary<Vector2Int, int> Assignments => _assigned;

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
            EventManager.Instance.OnWorkerAssignRequested += HandleAssignRequested;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            EventManager.Instance.OnConstructionComplete += HandleConstructionComplete;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnWorkerAssignRequested -= HandleAssignRequested;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnConstructionComplete -= HandleConstructionComplete;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        // ── assign / unassign (UI → ที่นี่) ─────────────────────────
        private void HandleAssignRequested(Vector2Int cell, int delta)
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.PlacedBuildings.TryGetValue(cell, out var data) || data == null)
                return;

            int cap = EffectiveCap(cell, data);
            int current = GetAssigned(cell);
            int desired = Mathf.Clamp(current + delta, 0, cap);

            // เพิ่มคนได้เฉพาะเท่าที่มี idle เหลือ "ของคลาสที่อาคารต้องใช้"
            if (desired > current)
                desired = current + Mathf.Min(desired - current, IdleOfClass(data.requiredClass));

            if (desired == current) return;

            SetAssigned(cell, desired);
            RaisePool();
        }

        private void SetAssigned(Vector2Int cell, int count)
        {
            if (count <= 0) _assigned.Remove(cell);
            else _assigned[cell] = count;
            EventManager.Instance.RaiseWorkerAssignmentChanged(cell, count);
        }

        /// <summary>
        /// เพดานคนงานของ cell นี้ = workerRequired ปกติ · แต่ระหว่างสร้างต้องรับผู้สร้างได้ ≥ 1
        /// (อาคารที่เดินเครื่องไม่ต้องใช้คน เช่น Habitat workerRequired=0 ก็ยังต้องมีคนมาสร้าง — V4 §5)
        /// public ให้ UI (BuildingUpgradeUI) ใช้เพดานชุดเดียวกับ HandleAssignRequested — แหล่งความจริงเดียว
        /// </summary>
        public int EffectiveCap(Vector2Int cell, BuildingData data)
        {
            if (data == null) return 0;
            int cap = Mathf.Max(0, data.workerRequired);
            var construction = ConstructionController.Instance;
            if (construction != null && construction.IsUnderConstruction(cell))
                cap = Mathf.Max(cap, 1);
            return cap;
        }

        // สร้างเสร็จ → คืน "ผู้สร้างส่วนเกิน" ที่เกิน workerRequired กลับเป็น idle
        // (เช่น Habitat: ระหว่างสร้างจัดคนได้ 1 · เสร็จแล้ว workerRequired=0 → ปล่อยคนกลับ)
        private void HandleConstructionComplete(Vector2Int cell, BuildingData data)
        {
            int cap = data != null ? Mathf.Max(0, data.workerRequired) : 0;
            if (GetAssigned(cell) <= cap) return; // ไม่เกินเพดานปกติ — คงคนประจำไว้เดินเครื่องต่อ

            SetAssigned(cell, cap);
            RaisePool();
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            if (!_assigned.ContainsKey(position)) return;
            _assigned.Remove(position); // คนคืน idle อัตโนมัติ (pool เป็น derived)
            EventManager.Instance.RaiseWorkerAssignmentChanged(position, 0);
            RaisePool();
        }

        // ── reconcile: จำนวนคนลดลง (ฝึก Worker→คลาสอื่น หัก workers ทันที / คนตาย) ──
        // ต้องเช็ค overflow "ต่อคลาส" — ฝึก Worker (workers−1) ต้องไม่เตะ Engineer ออกจาก Lab
        private void HandlePopulationChanged(PopulationData pop)
        {
            EvictOverflow(WorkerClass.Worker,   pop.workers);
            EvictOverflow(WorkerClass.Engineer, pop.engineers);
            EvictOverflow(WorkerClass.Medic,    pop.medics);
            EvictOverflow(WorkerClass.Farmer,   pop.farmers);
            RaisePool();
        }

        private void EvictOverflow(WorkerClass c, int available)
        {
            int overflow = AssignedOfClass(c) - available;
            if (overflow > 0) EvictClass(c, overflow);
        }

        // ลดคนที่ประจำออกจนกว่า assigned ของคลาส c จะไม่เกินจำนวนที่มี — ไล่เฉพาะอาคารคลาส c
        private void EvictClass(WorkerClass c, int count)
        {
            var cells = new List<Vector2Int>(_assigned.Keys);
            foreach (var cell in cells)
            {
                if (count <= 0) break;
                if (ClassOfCell(cell) != c) continue;

                int have = _assigned[cell];
                int take = Mathf.Min(have, count);
                int remaining = have - take;
                count -= take;

                if (remaining <= 0) _assigned.Remove(cell);
                else _assigned[cell] = remaining;
                EventManager.Instance.RaiseWorkerAssignmentChanged(cell, remaining);
            }
        }

        private void RaisePool() => EventManager.Instance.RaiseWorkerPoolChanged(IdleWorkers, TotalWorkers);

        // ── save/load ────────────────────────────────────────────
        private void HandleSaveLoaded(SaveData save)
        {
            _assigned.Clear();
            if (save.workerAssignmentCells != null && save.workerAssignmentCounts != null)
            {
                var registry = BuildingRegistry.Instance;
                // clamp remaining "ต่อคลาส" — กันเซฟที่ assign เกินจำนวนคนของคลาสนั้น
                // (เซฟเก่า count เป็น Worker ล้วน → คลาสเดียว ทำงานเหมือนเดิม)
                var usedByClass = new Dictionary<WorkerClass, int>();
                int n = Mathf.Min(save.workerAssignmentCells.Count, save.workerAssignmentCounts.Count);
                for (int i = 0; i < n; i++)
                {
                    var cell = save.workerAssignmentCells[i];
                    int count = save.workerAssignmentCounts[i];

                    // ไม่มีอาคารที่ cell นี้แล้ว (ถูกทุบ/ยังไม่โหลด) — ข้าม
                    if (registry == null || !registry.PlacedBuildings.TryGetValue(cell, out var data) || data == null)
                        continue;

                    var cls = data.requiredClass;
                    usedByClass.TryGetValue(cls, out int used);
                    int remaining = Mathf.Max(0, ClassCount(cls) - used);

                    count = Mathf.Clamp(count, 0, Mathf.Max(0, data.workerRequired));
                    count = Mathf.Min(count, remaining);
                    if (count <= 0) continue;

                    _assigned[cell] = count;
                    usedByClass[cls] = used + count;
                }
            }

            // แจ้งภาพ/HUD ให้ respawn + refresh
            foreach (var kvp in _assigned)
                EventManager.Instance.RaiseWorkerAssignmentChanged(kvp.Key, kvp.Value);
            RaisePool();
        }
    }
}
