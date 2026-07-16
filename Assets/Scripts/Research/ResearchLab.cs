using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Research Lab (GDD §19) — a production building that turns people + time + resources
    /// into knowledge. ★ Starts the game as a RUIN (isRuined = true): repair costs Iron 80,
    /// 2 days, 2 workers before anything can be researched.
    ///
    /// LOCKED rules baked in here:
    /// - bug #2:  research cost is paid ONCE at TryStartResearch — never per day
    /// - bug #7:  active jobs re-staff themselves every day (idle first, then donors)
    /// - bug #9:  donor floors — farm/water never below 2, mine never below 1
    /// - S7/S8:   emergency preempt drops the active job (progress lost = the price)
    /// - staffRatio &lt; 0.5 → progress does not advance
    ///
    /// Day-order note: subscribes OnDayEnded like WorkerManager; WorkerManager must exist
    /// first (playtest panel spawns it first) so worker statuses are fresh when we read them.
    /// </summary>
    // Execution order -40: runs AFTER WorkerManager (-50) so its OnDayEnded handler sees today's
    // refreshed worker status (EmergencyPreempt / SoftTrigger snapshot). See WorkerManager header.
    [DefaultExecutionOrder(-40)]
    public class ResearchLab : MonoBehaviour
    {
        public static ResearchLab Instance { get; private set; }

        // ★ v6.3 cutover (slice 1): auto-spawn into the live game (was F9-playtest-only before). AfterSceneLoad
        //   fires once at the app's first scene; the instance there is destroyed on LoadScene(Gamescene) →
        //   re-spawn on every sceneLoaded (same pattern as LabPanelUI/ResearchManager).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        private static void AutoSpawn()
        {
            try
            {
                if (EventManager.Instance == null) return; // no core yet (MainMenu) — a later sceneLoaded retries
                if (FindFirstObjectByType<ResearchLab>() != null) return;
                new GameObject("ResearchLab (auto)").AddComponent<ResearchLab>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResearchLab] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>Active research job — progress in "effective days" (GDD §19 OnDayEnd).</summary>
        public class ResearchJob
        {
            public ResearchNoteSO note;
            public float progress;
        }

        private GameConfigSO _cfg;
        private readonly List<string> _queue = new List<string>(); // paid, waiting noteIds

        /// <summary>★ เริ่มเกมเป็นซาก — ห้ามวิจัยจนกว่าจะซ่อม</summary>
        public bool IsRuined { get; private set; } = true;
        public int Level { get; private set; } = 1;
        public bool RepairPaid { get; private set; }
        public float RepairProgress { get; private set; }
        public ResearchJob ActiveJob { get; private set; }
        public IReadOnlyList<string> QueuedNoteIds => _queue;

        public SoftTriggerWatcher Triggers { get; private set; }

        public int ResearcherSlots => Level >= 3 ? _cfg.researcherSlotsLv3
            : Level == 2 ? _cfg.researcherSlotsLv2 : _cfg.researcherSlotsLv1;
        public int QueueCapacity => Level >= 3 ? _cfg.queueCapacityLv3
            : Level == 2 ? _cfg.queueCapacityLv2 : _cfg.queueCapacityLv1;

        /// <summary>labBusy for ShiftSystem's deadlock guard (bug #7).</summary>
        public bool LabBusy => !IsRuined && ActiveJob != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>Bootstrap — also the EditMode-test entry point.</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            Triggers = new SoftTriggerWatcher(cfg);
            IsRuined = true;
            RepairPaid = false;
            RepairProgress = 0f;
            Level = 1;
            ActiveJob = null;
            _queue.Clear();
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
        }

        // ─────────────────────────────────────────
        //  Repair (ซาก → ห้องวิจัย)
        // ─────────────────────────────────────────

        /// <summary>Pay Iron 80 once to start the repair. Crew = workers on job "lab".</summary>
        public bool StartRepair()
        {
            if (!IsRuined || RepairPaid) return false;

            var rm = ResourceManager.Instance;
            if (rm != null && rm.Current.iron < _cfg.repairIron)
            {
                EventManager.Instance?.RaiseNotice($"เหล็กไม่พอซ่อมห้องวิจัย (ต้องใช้ {_cfg.repairIron})");
                return false;
            }

            EventManager.Instance?.RaiseResourceDelta(ResourceType.Iron, -_cfg.repairIron);
            RepairPaid = true;
            return true;
        }

        private void TickRepair()
        {
            if (!IsRuined || !RepairPaid) return;
            var wm = WorkerManager.Instance;
            int crew = wm != null ? wm.GetWorkers(WorkerJobs.Lab).Count : 0;
            if (crew < _cfg.repairWorkers) return; // ต้องมีคนซ่อมครบ 2 — วันนี้ไม่คืบ

            RepairProgress += 1f;
            if (RepairProgress >= _cfg.repairDays)
            {
                IsRuined = false;
                EventManager.Instance?.RaiseResearchLabRepaired();
            }
        }

        // ─────────────────────────────────────────
        //  Start research — จ่ายครั้งเดียว (bug #2)
        // ─────────────────────────────────────────

        public bool CanAfford(ResearchNoteSO note)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true; // test harness — ไม่บล็อก (idiom เดียวกับ InventoryManager)
            var c = rm.Current;
            return c.energy >= note.costPower && c.iron >= note.costIron && c.labMat >= note.costLabMat;
        }

        /// <summary>
        /// Start (or enqueue) a note. Pays the FULL cost here, exactly once — bug #2:
        /// deducting per day made tritium eat the iron stock and block itself.
        /// </summary>
        public bool TryStartResearch(string noteId)
        {
            if (IsRuined)
            {
                EventManager.Instance?.RaiseNotice("ห้องวิจัยยังเป็นซาก — ซ่อมก่อน (เหล็ก 80 · 2 วัน · 2 คน)");
                return false;
            }

            var db = KnowledgeDB.Instance;
            var note = db.GetNote(noteId);
            if (note == null || !db.IsResearchable(note)) return false;
            if (ActiveJob?.note.noteId == noteId || _queue.Contains(noteId)) return false;

            int slotsUsed = (ActiveJob != null ? 1 : 0) + _queue.Count;
            if (slotsUsed >= QueueCapacity)
            {
                EventManager.Instance?.RaiseNotice($"คิววิจัยเต็ม ({QueueCapacity})");
                return false;
            }

            if (!CanAfford(note))
            {
                EventManager.Instance?.RaiseNotice($"ของไม่พอวิจัย {note.title} (P{note.costPower}/Fe{note.costIron}/Lab{note.costLabMat})");
                return false;
            }

            // ★ จ่ายครั้งเดียว ตรงนี้ที่เดียว (bug #2)
            if (note.costPower > 0) EventManager.Instance?.RaiseResourceDelta(ResourceType.Energy, -note.costPower);
            if (note.costIron > 0) EventManager.Instance?.RaiseResourceDelta(ResourceType.Iron, -note.costIron);
            if (note.costLabMat > 0) EventManager.Instance?.RaiseResourceDelta(ResourceType.LabMat, -note.costLabMat);

            if (ActiveJob == null)
                ActiveJob = new ResearchJob { note = note, progress = 0f };
            else
                _queue.Add(noteId);

            EventManager.Instance?.RaiseResearchNoteStarted(noteId);
            return true;
        }

        // ─────────────────────────────────────────
        //  Daily tick
        // ─────────────────────────────────────────

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 tutorial
            TickDay();
        }

        /// <summary>One lab day. Public so tests drive it headless (same pattern as WorkerManager).</summary>
        public void TickDay()
        {
            TickRepair();
            if (IsRuined) return;

            var wm = WorkerManager.Instance;
            EmergencyPreempt(wm);
            ReStaffForResearch(wm);
            TickProgress(wm);

            Triggers.Evaluate(SoftTriggerWatcher.Snapshot(), KnowledgeDB.Instance);
        }

        /// <summary>
        /// S7/S8 (GDD §19): a severe people-crisis dumps the current job — progress หาย = ราคาที่จ่าย.
        /// Verbatim per spec: only drops; the player must choose to research the emergency note.
        /// </summary>
        private void EmergencyPreempt(WorkerManager wm)
        {
            if (wm == null || ActiveJob == null) return;
            var db = KnowledgeDB.Instance;
            string current = ActiveJob.note.noteId;

            if (wm.HungryCount >= _cfg.softHungryCount &&
                !db.HasNote("food_logistics") && current != "food_logistics")
            {
                DropActiveJob("วิกฤตคนหิว — งานวิจัยถูกทิ้งกลางคัน");
            }
            else if (wm.ExhaustedCount >= _cfg.softExhaustedCount &&
                     !db.HasNote("shift_management") && current != "shift_management")
            {
                DropActiveJob("คนหมดแรงทั้งเมือง — งานวิจัยถูกทิ้งกลางคัน");
            }
        }

        private void DropActiveJob(string reason)
        {
            ActiveJob = null; // ★ progress หายจริง — ไม่คืนของ (ไม่มีทางเลือกฟรี)
            EventManager.Instance?.RaiseNotice(reason);
            PromoteQueue();
        }

        /// <summary>
        /// bug #7 (research deadlock): the job re-staffs itself every day — idle workers first,
        /// then donors mine → water → farm. bug #9 floors: farm/water ≥ 2, mine ≥ 1 —
        /// ดึงชาวนาเกลี้ยง = อาหารหมด D22 = Hope 0.
        /// </summary>
        public void ReStaffForResearch(WorkerManager wm)
        {
            if (wm == null || ActiveJob == null) return;

            int need = ActiveJob.note.researcherSlots - wm.GetWorkers(WorkerJobs.Lab).Count;
            if (need <= 0) return;

            // 1) idle ก่อนเสมอ
            foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
            {
                if (need <= 0) break;
                if (w.resting || w.strikeDaysLeft > 0) continue;
                if (wm.AssignJob(w, WorkerJobs.Lab)) need--;
            }

            // 2) donors ตามลำดับ GDD — floor ห้ามต่ำกว่า (bug #9)
            foreach (var donor in new[] { WorkerJobs.Mine, WorkerJobs.Water, WorkerJobs.Farm })
            {
                int floor = donor == WorkerJobs.Farm ? _cfg.restaffFloorFarm
                    : donor == WorkerJobs.Water ? _cfg.restaffFloorWater
                    : _cfg.restaffFloorMine;

                while (need > 0 && wm.GetWorkers(donor).Count > floor)
                {
                    var w = wm.GetWorkers(donor)[0];
                    if (!wm.AssignJob(w, WorkerJobs.Lab)) break;
                    need--;
                }
            }
        }

        /// <summary>GDD §19 OnDayEnd verbatim: progress += 1 × avgEff × staffRatio (min 0.5 gate).</summary>
        private void TickProgress(WorkerManager wm)
        {
            if (ActiveJob == null || wm == null) return;
            var labW = wm.GetWorkers(WorkerJobs.Lab);
            if (labW.Count == 0) return;

            float staffRatio = Mathf.Min(1f, (float)labW.Count / ActiveJob.note.researcherSlots);
            if (staffRatio < _cfg.staffRatioMin) return;

            ActiveJob.progress += 1f * labW.Average(w => wm.GetEfficiency(w)) * staffRatio;
            if (ActiveJob.progress >= ActiveJob.note.daysRequired)
                CompleteResearch();
        }

        private void CompleteResearch()
        {
            var note = ActiveJob.note;
            ActiveJob = null;

            KnowledgeDB.Instance.CompleteNote(note.noteId);
            WorkerManager.Instance?.Hope.Report("research.complete", $"วิจัยสำเร็จ: {note.title}",
                GameConfigSO.Instance.hopeResearchComplete, HopeCategory.Research);

            // ★ NoteCardPopup รับ event นี้ → เด้ง knowledgeBody + บท NPC (ห้ามผ่าน BarkManager)
            EventManager.Instance?.RaiseResearchNoteCompleted(note.noteId);

            PromoteQueue();
        }

        private void PromoteQueue()
        {
            while (ActiveJob == null && _queue.Count > 0)
            {
                var nextId = _queue[0];
                _queue.RemoveAt(0);
                var next = KnowledgeDB.Instance.GetNote(nextId);
                if (next != null && !KnowledgeDB.Instance.HasNote(nextId))
                    ActiveJob = new ResearchJob { note = next, progress = 0f }; // จ่ายไปแล้วตอนเข้าคิว
            }
        }

        /// <summary>Lab upgrade (Phase 2/3 building gate ทำงานฝั่ง placement — ที่นี่แค่ระดับ)</summary>
        public void SetLevel(int level) => Level = Mathf.Clamp(level, 1, 3);
    }
}
