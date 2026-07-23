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
    ///
    /// ★ v6.3 cutover (worker-click assignment): the click panels (BuildingUpgradeUI/LabPanelUI) and the
    ///   CORE TOWER panel raise OnWorkerAssignRequested(cell, ±1) and read GetAssigned/IdleOfClass. v6.3
    ///   workers are a global JOB pool (§17), NOT per-cell — but construction, per-building caps, and the
    ///   panel display all need per-cell granularity. So _assigned (per cell) stays the SOURCE OF TRUTH
    ///   even when WorkerManager is live; ReconcileJobPool() then PROJECTS it onto the global job pool so
    ///   production (Σ SumEfficiency per job), world avatars (positioned by job) and reactor cooling
    ///   (GetWorkers("cool").Count) all reflect the clicks. The single per-cell assign path (EffectiveCap
    ///   clamp + idle check) enforces the building cap for EVERY entry point (buttons, Q/E, CORE TOWER),
    ///   and lets a job-less building (Habitat) still take a builder while under construction.
    /// </summary>
    // รันหลัง BuildingRegistry/PopulationManager (default 0) — HandleSaveLoaded ต้องอ่าน placedBuildings + workers ที่ restore แล้ว
    [DefaultExecutionOrder(50)]
    public class WorkerAssignmentManager : MonoBehaviour
    {
        public static WorkerAssignmentManager Instance { get; private set; }

        // cell (origin ของอาคาร) -> จำนวน Worker ที่ประจำ (source of truth, per-cell)
        private readonly Dictionary<Vector2Int, int> _assigned = new Dictionary<Vector2Int, int>();

        // ── v6.3 job mapping (per-cell _assigned → global job pool) ────────────────
        // แปลงชนิดอาคาร → ชื่องาน (job) ใน WorkerManager (เช่น Farm → farm, CoreTower → cool)
        public static string JobForBuildingType(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.Farm:       return WorkerJobs.Farm;
                case BuildingType.WaterPlant: return WorkerJobs.Water;
                case BuildingType.PowerPlant: return WorkerJobs.Power;
                case BuildingType.Mine:       return WorkerJobs.Mine;
                case BuildingType.Laboratory: return WorkerJobs.Lab;
                case BuildingType.CoreTower:  return WorkerJobs.Cool;
                case BuildingType.OreDeposit: return WorkerJobs.Mine; // ore node diggers are miners (Mine rad zone)
                default:                      return null; // not a staffable production building
            }
        }

        // งานของอาคารที่ cell นี้ (null = ไม่มีงาน/กำลังก่อสร้าง — คนที่จัดไว้เป็น "ผู้สร้าง")
        /// <summary>Production job for the building at this cell, or null (job-less / under construction).</summary>
        public static string JobForCell(Vector2Int cell)
        {
            var reg = BuildingRegistry.Instance;
            if (reg == null || !reg.PlacedBuildings.TryGetValue(cell, out var d) || d == null) return null;
            if (d.isOreNode) return WorkerJobs.Mine;   // ore nodes are dug, never constructed
            // Under construction → the worker is a BUILDER (held in the Build job), not staff yet.
            var cc = ConstructionController.Instance;
            if (cc != null && cc.IsUnderConstruction(cell)) return null;
            if (d.buildingType == BuildingType.CoreTower || d.isCoreTowerPart) return WorkerJobs.Cool;
            return JobForBuildingType(d.buildingType);
        }

        // Jobs this manager projects onto (shrink pass iterates these). Build = holding job for builders.
        private static readonly string[] ProjectedJobs =
        {
            WorkerJobs.Farm, WorkerJobs.Power, WorkerJobs.Water, WorkerJobs.Mine, WorkerJobs.Lab,
            WorkerJobs.Cool, WorkerJobs.ZoneB, WorkerJobs.Extract, WorkerJobs.Build,
        };

        /// <summary>จำนวน Worker ที่ประจำอาคารที่ cell นี้ (0 ถ้าไม่พบ) — per-cell truth (ทั้ง legacy และ v6.3)</summary>
        public int GetAssigned(Vector2Int cell)
        {
            EnsureWmHook();
            return _assigned.TryGetValue(cell, out int n) ? n : 0;
        }

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

        /// <summary>
        /// คนคลาส c ที่ว่าง = มีอยู่ − ที่ประจำแล้ว
        /// v6.3: คนว่างคือ AliveCount ของ WorkerManager − ที่ประจำทั้งเมือง (workforce เดียว ทุก job/สร้าง ดึงจากนี่)
        /// </summary>
        public int IdleOfClass(WorkerClass c)
        {
            var wm = WorkerManager.Instance;
            if (wm != null) return Mathf.Max(0, wm.AliveCount - TotalAssigned);
            return Mathf.Max(0, ClassCount(c) - AssignedOfClass(c));
        }

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
            EnsureWmHook();
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnWorkerAssignRequested -= HandleAssignRequested;
                EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
                EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
                EventManager.Instance.OnConstructionComplete -= HandleConstructionComplete;
                EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            }
            if (_wmHooked != null) { _wmHooked.OnWorkersChanged -= HandleWorkersChanged; _wmHooked = null; }
        }

        // ── assign / unassign (UI → ที่นี่) ─────────────────────────
        // ★ Single per-cell path for BOTH legacy and v6.3 — enforces the building cap for every entry
        //   point (BuildingUpgradeUI +/−, Q/E keys, LabPanelUI, CORE TOWER cooling). v6.3 adds one step:
        //   after writing the per-cell plan, ReconcileJobPool projects it onto WorkerManager's job pool.
        // จุดรับคำสั่งจัด/ถอนคน ±1 จาก UI ทุกทาง (ปุ่ม, Q/E, แผงเตา) — เช็คเพดาน + idle ก่อนเสมอ
        private void HandleAssignRequested(Vector2Int cell, int delta)
        {
            EnsureWmHook();
            var registry = BuildingRegistry.Instance;
            if (registry == null || !registry.PlacedBuildings.TryGetValue(cell, out var data) || data == null)
                return;

            // โซน B ยังล็อก (ประตูปิด) → ห้ามส่งคนเข้าโหนดโซน B (เดินเข้าไม่ได้ จะไปติดหน้ารั้ว) · ถอนคนออกได้เสมอ
            if (delta > 0 && !ZoneBarrierRenderer.ZoneBUnlocked && ZoneBarrierRenderer.IsZoneBColumn(cell.x))
            {
                EventManager.Instance.RaiseNotice("เขต Zone B ยังปิดอยู่ — ปลดล็อกตามเนื้อเรื่องก่อนจึงส่งคนงานเข้าไปได้");
                return;
            }

            int cap = EffectiveCap(cell, data);
            int current = GetAssigned(cell);
            int desired = Mathf.Clamp(current + delta, 0, cap);

            // เพิ่มคนได้เฉพาะเท่าที่มี idle เหลือ "ของคลาสที่อาคารต้องใช้"
            if (desired > current)
                desired = current + Mathf.Min(desired - current, IdleOfClass(data.requiredClass));

            if (desired == current) return;

            SetAssigned(cell, desired);
            if (WorkerManager.Instance != null) ReconcileJobPool();
            RaisePool();
        }

        private void SetAssigned(Vector2Int cell, int count)
        {
            if (count <= 0) _assigned.Remove(cell);
            else _assigned[cell] = count;
            EventManager.Instance.RaiseWorkerAssignmentChanged(cell, count);
        }

        /// <summary>
        /// เพดานคนงานของ cell นี้ = workerRequired ตามระดับ · ระหว่างสร้างรับผู้สร้างได้ ≥ 1
        /// (อาคารที่เดินเครื่องไม่ต้องใช้คน เช่น Habitat workerRequired=0 ก็ยังต้องมีคนมาสร้าง — V4 §5)
        /// public ให้ UI (BuildingUpgradeUI) ใช้เพดานชุดเดียวกับ HandleAssignRequested — แหล่งความจริงเดียว
        /// </summary>
        public int EffectiveCap(Vector2Int cell, BuildingData data)
        {
            if (data == null) return 0;

            // CORE TOWER "หล่อเย็น" (job cool) เป็นการจัดสรรของเตา ไม่ใช่ slot คงที่ของอาคาร —
            // เพดานจำกัดแค่จำนวนคนที่มี (v6.3: คลิกเตาแล้ว +/− เพื่อจัดคนหล่อเย็น)
            if ((data.buildingType == BuildingType.CoreTower || data.isCoreTowerPart)
                && WorkerManager.Instance != null)
                return WorkerManager.Instance.AliveCount;

            // เพดานตาม "ระดับปัจจุบัน" (WorkersForLevel ผ่าน registry) — อัปเกรดแล้วรับคนได้มากขึ้น
            var reg = BuildingRegistry.Instance;
            int cap = reg != null ? reg.WorkersRequired(cell) : Mathf.Max(0, data.workerRequired);
            var construction = ConstructionController.Instance;
            if (construction != null && construction.IsUnderConstruction(cell))
                cap = Mathf.Max(cap, 1); // ระหว่างสร้าง: รับผู้สร้างได้ ≥1 แม้ตอนเดินเครื่องไม่ใช้คน (Habitat)
            return cap;
        }

        // ── v6.3: project the per-cell plan onto WorkerManager's global job pool ──────────────
        private bool _projecting;

        /// <summary>
        /// [TH] ฉายแผนจัดคนรายอาคาร (_assigned) ลง job pool กลางของ WorkerManager —
        /// หดงานที่คนเกินก่อน (คืน idle) แล้วค่อยเติมงานที่ขาดจาก idle เพื่อไม่ให้ avatar สลับตัวมั่ว
        /// Project _assigned (per-cell truth) onto WorkerManager's job pool: each production-job building's
        /// count fills that job; job-less / under-construction cells fill the Build holding job (reserved,
        /// not producing). Incremental — shrink over-full jobs to idle, then grow deficits from idle — so
        /// unrelated clicks don't churn worker identities (avatars stay put). _projecting guards the
        /// re-entrant OnWorkersChanged that AssignJob fires.
        /// </summary>
        private void ReconcileJobPool()
        {
            var wm = WorkerManager.Instance;
            if (wm == null || _projecting) return;
            _projecting = true;
            try
            {
                var desired = new Dictionary<string, int>();
                foreach (var kv in _assigned)
                {
                    var job = JobForCell(kv.Key) ?? WorkerJobs.Build;
                    desired.TryGetValue(job, out int c);
                    desired[job] = c + kv.Value;
                }

                // shrink first — frees surplus workers back to idle so the grow pass can reuse them
                foreach (var job in ProjectedJobs)
                {
                    int want = desired.TryGetValue(job, out int d) ? d : 0;
                    var have = wm.GetWorkers(job);
                    for (int i = have.Count - 1; i >= want; i--)
                        wm.AssignJob(have[i], WorkerJobs.Idle);
                }
                // grow — pull assignable idle workers into remaining deficits
                foreach (var kv in desired)
                {
                    int deficit = kv.Value - wm.GetWorkers(kv.Key).Count;
                    if (deficit <= 0) continue;
                    foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
                    {
                        if (deficit <= 0) break;
                        if (w.strikeDaysLeft <= 0 && !w.resting) { wm.AssignJob(w, kv.Key); deficit--; }
                    }
                }
            }
            finally { _projecting = false; }
        }

        // Keep the per-cell plan honest when WorkerManager's population changes (death / exodus). Do NOT
        // re-project here — the daily tick owns strike/death job moves, and re-growing jobs would fight a
        // strike. The plan re-syncs to the job pool on the next explicit assignment / construction event.
        private WorkerManager _wmHooked;
        private void EnsureWmHook()
        {
            var wm = WorkerManager.Instance;
            if (wm == _wmHooked) return;
            if (_wmHooked != null) _wmHooked.OnWorkersChanged -= HandleWorkersChanged;
            _wmHooked = wm;
            if (wm != null) wm.OnWorkersChanged += HandleWorkersChanged;
        }

        // ประชากรลด (ตาย/อพยพ) → ตัดแผนจัดคนส่วนเกินออกให้ไม่เกินคนที่ยังมีชีวิต
        private void HandleWorkersChanged()
        {
            if (_projecting) return;
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            int overflow = TotalAssigned - wm.AliveCount;
            if (overflow > 0) { TrimAssigned(overflow); RaisePool(); }
        }

        // ตัดคนออกจากแผนจัดคนทีละอาคารจนครบจำนวนที่ต้องลด
        private void TrimAssigned(int count)
        {
            var cells = new List<Vector2Int>(_assigned.Keys);
            foreach (var cell in cells)
            {
                if (count <= 0) break;
                int have = _assigned[cell];
                int take = Mathf.Min(have, count);
                int rem = have - take;
                count -= take;
                if (rem <= 0) _assigned.Remove(cell); else _assigned[cell] = rem;
                EventManager.Instance.RaiseWorkerAssignmentChanged(cell, rem);
            }
        }

        // สร้างเสร็จ → คืน "ผู้สร้างส่วนเกิน" ที่เกิน workerRequired กลับเป็น idle · แล้ว re-project (Build → job จริง)
        // (เช่น Habitat: ระหว่างสร้างจัดคนได้ 1 · เสร็จแล้ว workerRequired=0 → ปล่อยคนกลับ)
        private void HandleConstructionComplete(Vector2Int cell, BuildingData data)
        {
            int cap = data != null ? Mathf.Max(0, data.workerRequired) : 0;
            if (GetAssigned(cell) > cap) SetAssigned(cell, cap);
            // Always re-project on completion: JobForCell flips from Build → the real job, so the worker
            // moves out of the Build holding job into production (or idle for a job-less Habitat).
            if (WorkerManager.Instance != null) ReconcileJobPool();
            RaisePool();
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            bool had = _assigned.Remove(position);
            if (had) EventManager.Instance.RaiseWorkerAssignmentChanged(position, 0); // คนคืน idle อัตโนมัติ
            if (WorkerManager.Instance != null) ReconcileJobPool();
            if (had) RaisePool();
        }

        // ── reconcile: จำนวนคนลดลง (legacy PopulationData path only) ──
        private void HandlePopulationChanged(PopulationData pop)
        {
            // v6.3: WorkerManager owns population — death/exodus handled by HandleWorkersChanged. The legacy
            // PopulationData counters are stale here (could be 0), so eviction against them would wrongly
            // clear the plan.
            if (WorkerManager.Instance != null) return;

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

        private void RaisePool()
        {
            var wm = WorkerManager.Instance;
            if (wm != null)
            {
                int total = wm.AliveCount;
                EventManager.Instance.RaiseWorkerPoolChanged(Mathf.Max(0, total - TotalAssigned), total);
            }
            else EventManager.Instance.RaiseWorkerPoolChanged(IdleWorkers, TotalWorkers);
        }

        // ── save/load ────────────────────────────────────────────
        private void HandleSaveLoaded(SaveData save)
        {
            _assigned.Clear();
            var registry = BuildingRegistry.Instance;
            bool wmLive = WorkerManager.Instance != null;

            if (save.workerAssignmentCells != null && save.workerAssignmentCounts != null)
            {
                // clamp remaining "ต่อคลาส" (legacy) — กันเซฟที่ assign เกินจำนวนคนของคลาสนั้น
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

                    count = Mathf.Clamp(count, 0, EffectiveCap(cell, data)); // เพดานตามเลเวล/หล่อเย็น
                    if (!wmLive)
                    {
                        var cls = data.requiredClass;
                        usedByClass.TryGetValue(cls, out int used);
                        int remaining = Mathf.Max(0, ClassCount(cls) - used);
                        count = Mathf.Min(count, remaining);
                        if (count <= 0) continue;
                        usedByClass[cls] = used + count;
                    }
                    else if (count <= 0) continue;

                    _assigned[cell] = count;
                }
            }

            // v6.3: clamp the restored plan to the live workforce, then project onto the job pool
            // (WorkerManager reloads all workers idle — reconcile re-applies their jobs from _assigned).
            if (wmLive)
            {
                int overflow = TotalAssigned - WorkerManager.Instance.AliveCount;
                if (overflow > 0) TrimAssigned(overflow);
                ReconcileJobPool();
            }

            // แจ้งภาพ/HUD ให้ respawn + refresh
            foreach (var kvp in _assigned)
                EventManager.Instance.RaiseWorkerAssignmentChanged(kvp.Key, kvp.Value);
            RaisePool();
            EnsureWmHook();
        }
    }
}
