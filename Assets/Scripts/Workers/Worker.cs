using System;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: สถานะสุขภาพของคนงาน 1 ป้ายต่อคน เรียงตามความรุนแรง (ปกติ→เหนื่อย→หมดแรง→หิว→ป่วย→ใกล้ตาย→ตาย)
    /// Worker status per GDD §17 status table — one label per worker, severity-ordered.
    /// </summary>
    public enum WorkerStatus { Healthy, Tired, Exhausted, Hungry, Sick, Dying, Dead }

    /// <summary>
    /// [TH] หน้าที่: โซนรังสีที่คนงานยืนทำงานอยู่ คำนวณจากชื่องาน (mine/zoneb/cool) — ใช้คิดปริมาณรังสีที่รับต่อวัน
    /// Radiation zone a worker stands in — derived from job string (GDD §17 radiation formula).
    /// </summary>
    public enum WorkerZone { None, Mine, ZoneB, Core }

    /// <summary>
    /// [TH] หน้าที่: เก็บสถานะรายคนของคนงาน 1 คน (งานที่ทำ, ความเหนื่อย, ความหิว, รังสีสะสม, เป็น/ตาย)
    /// [TH] คนงานทุกคนเหมือนกันหมด ไม่มีอาชีพ/สกิล — ต่างกันแค่ string งานปัจจุบันเท่านั้น
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

        /// <summary>
        /// [TH] ถือว่า "กำลังทำงาน" เมื่อยังมีชีวิต ไม่ได้พัก ไม่ได้สไตรค์ และมีงานจริง (ไม่ใช่ idle)
        /// Working = alive, not resting/striking, and has an actual job.
        /// </summary>
        public bool IsWorking => alive && !resting && strikeDaysLeft <= 0 && job != WorkerJobs.Idle;

        /// <summary>
        /// [TH] แปลงงานปัจจุบันเป็นโซนรังสี: เหมือง 4.0/วัน · Zone B 15.0/วัน · หล่อเย็นเตา 6.0/วัน (งานอื่นไม่โดนรังสี)
        /// Zone from current job (GDD §17): mine → Mine 4.0 · zoneb → B 15.0 · cool → Core 6.0.
        /// </summary>
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

    /// <summary>
    /// [TH] หน้าที่: รวมค่าคงที่ชื่องานทั้งหมดไว้ที่เดียว กันการพิมพ์ string ผิดกระจายทั่วโค้ด
    /// Canonical job string constants (GDD §17) — avoid scattered literals.
    /// </summary>
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

        // Not a GDD §17 production job — a holding job for workers sent to build a site (per-cell
        // construction). Kept out of the idle pool so they can't be double-booked, but SumEfficiency
        // (production) ignores it and no radiation Zone applies. Cleared to the real job (or idle)
        // when construction completes. See WorkerAssignmentManager.ReconcileJobPool.
        public const string Build = "build";
    }
}
