using System.Collections.Generic;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ระบบกะพักงาน — คนงานเหนื่อยเกิน 70 จะถอนตัวไปพักเอง และกลับมาทำงานเดิมเมื่อเหนื่อยลดต่ำกว่า 25
    /// [TH] มี guard กันบั๊ก #7: ห้ามดึงคนแล็บออกไปพักระหว่างวิจัยค้าง ไม่งั้น progress วิจัยจะค้างตลอดกาล
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
        /// [TH] วนเช็คคนงานที่ยังมีชีวิตทุกคนวันละครั้ง: ใครเหนื่อยถึงเกณฑ์ให้ไปพัก ใครหายเหนื่อยแล้วให้กลับงานเดิม
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
                // [TH] กันบั๊ก #7: ถ้าแล็บกำลังวิจัยอยู่ อย่าดึงคนแล็บไปพัก จนกว่าเหนื่อยจะทะลุเส้น guard (92)
                if (labBusy && w.job == WorkerJobs.Lab && w.fatigue < cfg.labDeadlockGuard) continue;

                // [TH] เหนื่อยถึงเกณฑ์ (>=70) → จำงานเดิมไว้แล้วออกไปพัก
                if (!w.resting && w.fatigue >= cfg.restThreshold)
                {
                    w.lastJob = w.job;
                    w.job = WorkerJobs.Idle;
                    w.resting = true;
                }
                // [TH] พักจนเหนื่อยลดต่ำพอ (<=25) → กลับไปทำงานเดิมอัตโนมัติ
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
