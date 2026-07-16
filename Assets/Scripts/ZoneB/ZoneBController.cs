using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Zone B — the tritium breeder (GDD §22 / CONFIG.md 🔒 METHOD B). The heart of the Death Spiral:
    /// workers inside take rad_zoneb every day (applied by WorkerManager via Worker.Zone — we do NOT
    /// re-apply it here), and once a worker's radiation passes zoneb_rotate_rad we pull them OUT before
    /// they sicken. Pull enough out and the zone drops below min-staff → tritium stops → CORE stalls →
    /// card #6 fires → hope falls. Rad suits (RadSuitManager) cut the dose ×0.4 and slow the spiral.
    ///
    /// Production is the ★ game-decider: MasteryRegistry.ZoneBTritiumPerDay() returns 3.0 without the
    /// quiz (deficit → lose) or 8.0 with it (enough → win). Each producing day latches the
    /// tritiumEverProduced flag for the q_tritium_breeding quiz (bug #3 — see CodexQuizManager).
    /// </summary>
    // -35: AFTER WorkerManager (-50) so rotation reads today's freshly-applied radiation, and BEFORE
    // CardManager (-30) so card #6 sees the post-rotation staff count.
    [DefaultExecutionOrder(-35)]
    public class ZoneBController : MonoBehaviour
    {
        public static ZoneBController Instance { get; private set; }

        private GameConfigSO _cfg;

        public bool IsOpen { get; private set; }
        public float TritiumStock { get; private set; }
        public float TritiumProducedTotal { get; private set; }
        public int ProducedDays { get; private set; }
        public float LastProduced { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Initialize(GameConfigSO.Instance);
        }

        /// <summary>Bootstrap — also the EditMode-test entry point.</summary>
        public void Initialize(GameConfigSO cfg)
        {
            _cfg = cfg;
            IsOpen = false;
            TritiumStock = 0f;
            TritiumProducedTotal = 0f;
            ProducedDays = 0;
            LastProduced = 0f;
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

        private void HandleDayEnded(int day)
        {
            if (day <= 1) return; // Day 1 = tutorial
            TickDay();
        }

        // ─────────────────────────────────────────
        //  Open / close (research gates it; player opens it)
        // ─────────────────────────────────────────

        /// <summary>Open Zone B (needs the tritium note; the player commits to the risk).</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            EventManager.Instance?.RaiseZoneBOpened();
        }

        public void Close() => IsOpen = false;

        /// <summary>Reactor (Sprint 6) draws tritium from here. Never below 0.</summary>
        public float ConsumeTritium(float amount)
        {
            float taken = Mathf.Min(TritiumStock, Mathf.Max(0f, amount));
            TritiumStock -= taken;
            return taken;
        }

        // ─────────────────────────────────────────
        //  Daily tick — rotation THEN production (public for tests)
        // ─────────────────────────────────────────

        public void TickDay()
        {
            TickRotation();
            TickProduction();
        }

        /// <summary>§22: pull anyone past zoneb_rotate_rad out to idle BEFORE they sicken.</summary>
        private void TickRotation()
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;
            var staff = wm.GetWorkers(WorkerJobs.ZoneB);
            for (int i = staff.Count - 1; i >= 0; i--)
            {
                var w = staff[i];
                if (w.radiation > _cfg.zoneBRotateRad)
                {
                    wm.AssignJob(w, WorkerJobs.Idle);
                    w.lastJob = WorkerJobs.Idle; // don't auto-send them back into the zone
                }
            }
        }

        /// <summary>Produce tritium if open and staffed — the 3.0 vs 8.0 game-decider.</summary>
        private void TickProduction()
        {
            LastProduced = 0f;
            if (!IsOpen) return;

            var wm = WorkerManager.Instance;
            int staff = wm != null ? wm.GetWorkers(WorkerJobs.ZoneB).Count : 0;
            if (staff < _cfg.zoneBMinStaff) return; // understaffed → nothing bred (the spiral bites here)

            float rate = MasteryRegistry.Instance.ZoneBTritiumPerDay(); // 3.0 base · 8.0 with quiz
            TritiumStock += rate;
            TritiumProducedTotal += rate;
            LastProduced = rate;
            ProducedDays++;

            // ★ latch for q_tritium_breeding (bug #3: flag, never current stock)
            CodexQuizManager.Instance.MarkTritiumProduced();
            EventManager.Instance?.RaiseZoneBTritiumProduced(rate);
        }
    }
}
