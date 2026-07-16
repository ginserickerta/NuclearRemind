using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Hope Ledger (GDD §18) — the ONLY writer of Hope. Plain class per GDD spec;
    /// WorkerManager owns the runtime instance and commits it at end of day.
    ///
    /// Flow per day: systems Report() entries → OnDayEnd() sums → applies → clamps [0,100]
    /// → stores the committed breakdown for UI → clears for the next day.
    /// </summary>
    public class HopeLedger
    {
        private readonly float _min;
        private readonly float _max;

        private readonly List<HopeEntry> _today = new List<HopeEntry>();       // reported, not yet applied
        private readonly List<HopeEntry> _committed = new List<HopeEntry>();   // last committed day (UI breakdown)
        private readonly List<float> _history = new List<float>();             // hope after each committed day (trend graph)

        /// <summary>Current hope value. Starts at hope_start (70) — never written from outside.</summary>
        public float Current { get; private set; }

        /// <summary>Signed total of the last committed day.</summary>
        public float LastDelta { get; private set; }

        /// <summary>(newHope, dayDelta) — fired after each OnDayEnd commit.</summary>
        public event Action<float, float> OnCommitted;

        public HopeLedger(GameConfigSO cfg)
        {
            _min = cfg.hopeMin;
            _max = cfg.hopeMax;
            Current = cfg.hopeStart;   // ★ 70, not 100 (v5.2)
        }

        /// <summary>
        /// Submit one hope delta (GDD §18). Aggregate per source before reporting
        /// (e.g. 4 hungry workers = one entry, value −8) so the breakdown reads clean.
        /// </summary>
        public void Report(string sourceKey, string text, float value, HopeCategory category)
        {
            if (Mathf.Approximately(value, 0f)) return; // zero rows add noise to the breakdown
            _today.Add(new HopeEntry(sourceKey, text, value, category));
        }

        /// <summary>
        /// End-of-day commit (GDD §18): sum → apply → clamp(0,100) → keep breakdown → clear.
        /// Threshold events are the watcher's job — caller runs HopeThresholdWatcher after this.
        /// </summary>
        public float OnDayEnd()
        {
            float sum = 0f;
            for (int i = 0; i < _today.Count; i++)
                sum += _today[i].value;

            Current = Mathf.Clamp(Current + sum, _min, _max);
            LastDelta = sum;

            _committed.Clear();
            _committed.AddRange(_today);
            _today.Clear();

            _history.Add(Current);

            OnCommitted?.Invoke(Current, sum);
            return sum;
        }

        /// <summary>Entries reported today that have not been applied yet (live view during the day).</summary>
        public IReadOnlyList<HopeEntry> GetTodayBreakdown() => _today;

        /// <summary>Breakdown of the last committed day — what HopeBreakdownPanel shows (GDD §18 UI).</summary>
        public IReadOnlyList<HopeEntry> GetCommittedBreakdown() => _committed;

        /// <summary>Hope value after each committed day — panel draws the last 7 as the trend graph.</summary>
        public IReadOnlyList<float> History => _history;
    }
}
