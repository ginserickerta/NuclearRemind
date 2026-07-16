using System.Collections.Generic;

namespace NuclearReMind
{
    /// <summary>
    /// Shift/rest system (GDD §17.5) — workers pull themselves off duty at fatigue >= 70
    /// and return to their last job at fatigue <= 25. Pure logic, no Unity dependency.
    ///
    /// CRITICAL (bug #7 — research deadlock): while a research job is active, lab workers
    /// are NOT pulled to rest unless fatigue >= labDeadlockGuard (92). Otherwise the shift
    /// system drains the lab and research progress stalls forever.
    ///
    /// Tutorial Day 1 must teach: "คนเหนื่อยเกิน 70 จะพักเอง — เผื่อคนสำรองไว้"
    /// </summary>
    public static class ShiftSystem
    {
        /// <summary>
        /// One shift pass over all alive workers (GDD §17.5 TickShifts — logic verbatim).
        /// labBusy = (researchJob != null); Sprint 2 wires the real ResearchLab query.
        /// </summary>
        public static void TickShifts(IReadOnlyList<Worker> aliveWorkers, bool labBusy, GameConfigSO cfg)
        {
            for (int i = 0; i < aliveWorkers.Count; i++)
            {
                var w = aliveWorkers[i];
                if (!w.alive) continue;

                // bug #7 guard: never pull a lab worker off an active research job below the guard line
                if (labBusy && w.job == WorkerJobs.Lab && w.fatigue < cfg.labDeadlockGuard) continue;

                if (!w.resting && w.fatigue >= cfg.restThreshold)
                {
                    w.lastJob = w.job;
                    w.job = WorkerJobs.Idle;
                    w.resting = true;
                }
                else if (w.resting && w.fatigue <= cfg.restReturn)
                {
                    w.resting = false;
                    w.job = (w.lastJob != WorkerJobs.Idle) ? w.lastJob : WorkerJobs.Idle;
                    w.lastJob = WorkerJobs.Idle;
                }
            }
        }
    }
}
