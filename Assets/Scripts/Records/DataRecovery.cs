using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Data Recovery (GDD §24 / STORY.md) — Dr. Elara Vane's 4 logs, decrypted passively as the lab
    /// runs. Each recovered record pre-unlocks a research LEAD (record_01→water_analysis,
    /// record_02→magnetic_theory, record_03→storm_detection→Sensor Array, record_final→lithium_breeding),
    /// so a player who keeps researchers idle gets leads 4–6 days early — a real power-up, not just lore.
    ///
    /// Rate = passive 14 + 10 per idle researcher, ×1.3 at lab L2 (CONFIG.md). Records are a shortcut,
    /// never a requirement (STORY.md §3): skip them and you still win, just later.
    /// </summary>
    // -25: after ResearchLab (-40) / WorkerManager (-50) so it reads today's lab staffing.
    [DefaultExecutionOrder(-25)]
    public class DataRecovery : MonoBehaviour
    {
        public static DataRecovery Instance { get; private set; }

        // Recovery order (STORY.md) — record_final is 4th despite the id sorting oddly.
        private static readonly string[] Order = { "record_01", "record_02", "record_03", "record_final" };

        private GameConfigSO _cfg;
        private readonly Dictionary<string, RecordCardSO> _catalog = new Dictionary<string, RecordCardSO>();
        private bool _catalogLoaded;

        public float Progress { get; private set; }
        public int RecordsRecovered { get; private set; }   // 0..4
        public int TotalRecords => Order.Length;

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
            Progress = 0f;
            RecordsRecovered = 0;
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
        //  Catalog (tests register directly; play mode auto-loads from Resources)
        // ─────────────────────────────────────────

        public void RegisterCatalog(IEnumerable<RecordCardSO> records)
        {
            if (records != null)
                foreach (var r in records)
                    if (r != null && !string.IsNullOrEmpty(r.recordId)) _catalog[r.recordId] = r;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            RegisterCatalog(Resources.LoadAll<RecordCardSO>("Records")); // Assets/Resources/Records
        }

        // ─────────────────────────────────────────
        //  Daily tick (public for tests)
        // ─────────────────────────────────────────

        public void TickDay()
        {
            EnsureCatalog();
            if (RecordsRecovered >= Order.Length) return;

            var lab = ResearchLab.Instance;
            if (lab == null || lab.IsRuined) return; // a ruined lab can't decrypt (GDD §24)

            int labWorkers = WorkerManager.Instance != null ? WorkerManager.Instance.GetWorkers(WorkerJobs.Lab).Count : 0;
            int busy = lab.ActiveJob != null && lab.ActiveJob.note != null
                ? Mathf.Min(labWorkers, lab.ActiveJob.note.researcherSlots)
                : 0;
            int idleResearchers = Mathf.Max(0, labWorkers - busy);

            float rate = _cfg.dataRecoveryPassive + idleResearchers * _cfg.dataRecoveryRate;
            if (lab.Level >= 2) rate *= _cfg.dataRecoveryLv2Mult;

            Progress += rate;
            while (Progress >= _cfg.dataRecoveryTarget && RecordsRecovered < Order.Length)
            {
                Progress -= _cfg.dataRecoveryTarget;
                UnlockNextRecord();
            }
        }

        private void UnlockNextRecord()
        {
            var rec = GetRecord(Order[RecordsRecovered]);
            RecordsRecovered++;
            if (rec == null) return;

            if (!string.IsNullOrEmpty(rec.unlocksLead))
                KnowledgeDB.Instance.UnlockLead(rec.unlocksLead); // ★ pre-unlock the lead (power-up)

            EventManager.Instance?.RaiseRecordRecovered(rec); // pops the card + archives it
        }

        public RecordCardSO GetRecord(string recordId)
        {
            EnsureCatalog();
            return _catalog.TryGetValue(recordId, out var r) ? r : null;
        }

        /// <summary>Records recovered so far, in order — for the Records panel.</summary>
        public IEnumerable<RecordCardSO> Recovered
        {
            get
            {
                EnsureCatalog();
                for (int i = 0; i < RecordsRecovered && i < Order.Length; i++)
                {
                    var r = GetRecord(Order[i]);
                    if (r != null) yield return r;
                }
            }
        }
    }
}
