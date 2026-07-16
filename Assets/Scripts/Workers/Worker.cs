using System;

namespace NuclearReMind
{
    /// <summary>Worker status per GDD §17 status table — one label per worker, severity-ordered.</summary>
    public enum WorkerStatus { Healthy, Tired, Exhausted, Hungry, Sick, Dying, Dead }

    /// <summary>Radiation zone a worker stands in — derived from job string (GDD §17 radiation formula).</summary>
    public enum WorkerZone { None, Mine, ZoneB, Core }

    /// <summary>
    /// Per-worker state (GDD §17). Plain class — no classes/training (v5.2 killed them),
    /// everyone is identical, only the current job string differs.
    /// job strings: farm / power / water / mine / lab / cool / zoneb / extract / idle
    /// </summary>
    [Serializable]
    public class Worker
    {
        public int id;
        public string displayName;
        public string job = WorkerJobs.Idle;
        public float fatigue;              // 0-100
        public float hunger;               // 0-100
        public float radiation;            // 0-100 · never decays on its own — Med Bay only
        public WorkerStatus status = WorkerStatus.Healthy;
        public bool alive = true;
        public bool hasRadSuit;
        public bool resting;
        public string lastJob = WorkerJobs.Idle;

        // Implementation detail for §18 StrikeEvent (10% stop working 2 days) — not in §17 spec,
        // but strike semantics differ from rest (no fatigue-based auto-return).
        public int strikeDaysLeft;

        /// <summary>Working = alive, not resting/striking, and has an actual job.</summary>
        public bool IsWorking => alive && !resting && strikeDaysLeft <= 0 && job != WorkerJobs.Idle;

        /// <summary>Zone from current job (GDD §17): mine → Mine 4.0 · zoneb → B 15.0 · cool → Core 6.0.</summary>
        public WorkerZone Zone
        {
            get
            {
                if (!IsWorking) return WorkerZone.None;
                switch (job)
                {
                    case WorkerJobs.Mine: return WorkerZone.Mine;
                    case WorkerJobs.ZoneB: return WorkerZone.ZoneB;
                    case WorkerJobs.Cool: return WorkerZone.Core;
                    default: return WorkerZone.None;
                }
            }
        }
    }

    /// <summary>Canonical job string constants (GDD §17) — avoid scattered literals.</summary>
    public static class WorkerJobs
    {
        public const string Farm = "farm";
        public const string Power = "power";
        public const string Water = "water";
        public const string Mine = "mine";
        public const string Lab = "lab";
        public const string Cool = "cool";
        public const string ZoneB = "zoneb";
        public const string Extract = "extract";
        public const string Idle = "idle";
    }
}
