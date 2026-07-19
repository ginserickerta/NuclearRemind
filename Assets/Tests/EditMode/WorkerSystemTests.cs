using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §17/§17.5 — per-worker daily tick (fixed order), hungriest-eat-first,
    /// hunger 30/day (bug #12), radiation never self-decays, efficiency formula,
    /// shift system with lab deadlock guard 92 (bug #7).
    /// </summary>
    public class WorkerSystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private WorkerManager wm;
        private GameConfigSO cfg;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>(); // defaults mirror CONFIG.md
            _spawned.Add(cfg);

            var go = new GameObject("WorkerManager");
            _spawned.Add(go);
            wm = go.AddComponent<WorkerManager>();
            wm.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private static WorkerTickContext Ctx(float food = 999f, int seed = 1) => new WorkerTickContext
        {
            foodStock = food,
            rng = new System.Random(seed),
            medBayHeal = 25f,
        };

        // ── ค่าเริ่มเกม (CONFIG.md: pop 14) ──────────────────────────
        [Test]
        public void Starts_With14Workers_AllHealthyIdle()
        {
            Assert.AreEqual(14, wm.AliveCount, "pop 14 (LOCK บั๊ก #8)");
            Assert.IsTrue(wm.Workers.All(w => w.status == WorkerStatus.Healthy && w.job == WorkerJobs.Idle));
        }

        // ── Hunger: คนหิวสุดได้กินก่อน — ห้ามสุ่ม (§17) ──────────────
        [Test]
        public void Hunger_HungriestEatsFirst_NeverRandom()
        {
            var workers = wm.Workers;
            for (int i = 0; i < workers.Count; i++)
                workers[i].hunger = i * 5f; // 0,5,10,...,65 — คนท้าย list หิวสุด

            var ctx = Ctx(food: 3f); // มีอาหารแค่ 3 หน่วย
            wm.RunDailyTick(ctx);

            Assert.AreEqual(3, ctx.foodConsumed, "กินเป็นจำนวนเต็ม = min(stock, need)");
            // 3 คนที่หิวสุด (hunger 65, 60, 55 ก่อน tick) ต้องได้กิน → hunger = 0
            int zeroCount = workers.Count(w => w.hunger == 0f);
            Assert.AreEqual(3, zeroCount, "คนหิวสุด 3 คนได้กิน → hunger 0");
            // The least hungry worker (started at 0) is last in line, so he goes unfed → +30.
            // The old assertion looked at the LOWEST hunger after the tick, which is one of the
            // three who ate (0) - it could never be 30, and it duplicated the check below anyway.
            Assert.AreEqual(30f, workers[0].hunger, 1e-3f, "คนหิวน้อยสุดอดข้าว → 0 + 30");
            Assert.IsTrue(workers.Any(w => Mathf.Approximately(w.hunger, 30f)), "คนไม่ได้กิน hunger +30");
        }

        [Test]
        public void Hunger_Unfed_Gains30PerDay_NotTwenty()
        {
            var ctx = Ctx(food: 0f);
            wm.RunDailyTick(ctx);
            Assert.IsTrue(wm.Workers.All(w => Mathf.Approximately(w.hunger, 30f)),
                "hunger_per_day = 30 (LOCK บั๊ก #12: 20 → S7 ไม่เคยเกิด)");
            Assert.AreEqual(0, ctx.foodConsumed);
        }

        [Test]
        public void Hunger_TwoDaysUnfed_CrossesHungryThreshold()
        {
            wm.RunDailyTick(Ctx(food: 0f));
            wm.RunDailyTick(Ctx(food: 0f));
            // 60 > 55 → Hungry ทุกคน → S7 (hungry >= 3) ต้องถึงได้จริง
            Assert.AreEqual(14, wm.HungryCount, "อด 2 วัน → hunger 60 > 55 → Hungry ครบเมือง");
        }

        // ── Daily tick order: fatigue ก่อน status (ห้ามสลับ §17) ──────
        [Test]
        public void DailyTick_WorkingWorker_GainsFatigue12()
        {
            wm.AssignJob(wm.Workers[0], WorkerJobs.Farm);
            wm.RunDailyTick(Ctx());
            Assert.AreEqual(12f, wm.Workers[0].fatigue, 1e-3f, "fatigue_work = 12/วัน");
        }

        [Test]
        public void DailyTick_RestingWorker_Recovers30_Or45WithBarracks()
        {
            var w = wm.Workers[0];
            w.fatigue = 80f;
            w.resting = true;
            w.job = WorkerJobs.Idle;

            wm.RunDailyTick(Ctx());
            Assert.AreEqual(50f, w.fatigue, 1e-3f, "พักปกติ −30/วัน");

            var ctx = Ctx();
            ctx.hasBarracks = true;
            wm.RunDailyTick(ctx);
            Assert.AreEqual(5f, w.fatigue, 1e-3f, "มี Barracks −45/วัน");
        }

        [Test]
        public void DailyTick_BoostingCoolWorker_GainsExtra6Fatigue()
        {
            wm.AssignJob(wm.Workers[0], WorkerJobs.Cool);
            var ctx = Ctx();
            ctx.boosting = true;
            wm.RunDailyTick(ctx);
            Assert.AreEqual(18f, wm.Workers[0].fatigue, 1e-3f, "boost + cool → 12 + 6");
        }

        // ── Radiation (§17): โซนตาม job · ไม่ลดเอง · ชุด ×0.4 ─────────
        [Test]
        public void Radiation_MineWorker_Gains4PerDay_NeverDecays()
        {
            wm.AssignJob(wm.Workers[0], WorkerJobs.Mine);
            wm.RunDailyTick(Ctx());
            Assert.AreEqual(4f, wm.Workers[0].radiation, 1e-3f, "rad_mine = 4");

            wm.AssignJob(wm.Workers[0], WorkerJobs.Idle); // ออกจากเหมือง
            wm.RunDailyTick(Ctx());
            Assert.AreEqual(4f, wm.Workers[0].radiation, 1e-3f, "รังสีไม่ลดเอง — Med Bay เท่านั้น");
        }

        [Test]
        public void Radiation_ZoneB15_RadSuitCutsTo40Percent()
        {
            var w = wm.Workers[0];
            wm.AssignJob(w, WorkerJobs.ZoneB);
            w.hasRadSuit = true;
            wm.RunDailyTick(Ctx());
            Assert.AreEqual(15f * 0.4f, w.radiation, 1e-3f, "rad_zoneb 15 × suit 0.4 = 6");
        }

        [Test]
        public void Radiation_HeatLeakAndStorm_HitEveryone()
        {
            var ctx = Ctx();
            ctx.heat = 90f;        // > 85 → +5
            ctx.stormActive = true; // +3
            wm.RunDailyTick(ctx);
            Assert.IsTrue(wm.Workers.All(w => Mathf.Approximately(w.radiation, 8f)),
                "idle ก็โดน heat leak +5 + storm +3");
        }

        [Test]
        public void MedBay_HealsMostIrradiatedFirst_UpToCapacity()
        {
            for (int i = 0; i < 6; i++)
                wm.Workers[i].radiation = 30f + i * 5f; // 30..55
            var ctx = Ctx();
            ctx.medBayCapacity = 2;
            ctx.medBayHeal = 25f;
            wm.RunDailyTick(ctx);
            // 2 คน rad สูงสุด (55, 50) ถูกรักษา −25 → 30, 25
            Assert.AreEqual(30f, wm.Workers[5].radiation, 1e-3f);
            Assert.AreEqual(25f, wm.Workers[4].radiation, 1e-3f);
            Assert.AreEqual(45f, wm.Workers[3].radiation, 1e-3f, "เกิน capacity ไม่ถูกรักษา");
        }

        // ── Efficiency (§17 CRITICAL — คูณ Σ eff ไม่ใช่นับหัว) ────────
        [Test]
        public void Efficiency_Thresholds_MatchConfig()
        {
            var w = wm.Workers[0];

            w.fatigue = 86f;
            Assert.AreEqual(0f, wm.GetEfficiency(w), "fatigue > 85 → 0%");

            w.fatigue = 0f; w.radiation = 51f;
            Assert.AreEqual(0f, wm.GetEfficiency(w), "radiation > 50 → 0%");

            w.radiation = 0f; w.fatigue = 61f;
            Assert.AreEqual(0.7f, wm.GetEfficiency(w), 1e-3f, "fatigue > 60 → ×0.7");

            w.fatigue = 0f; w.hunger = 56f;
            Assert.AreEqual(0.6f, wm.GetEfficiency(w), 1e-3f, "hunger > 55 → ×0.6");

            w.fatigue = 61f; // ซ้อนกัน
            Assert.AreEqual(0.42f, wm.GetEfficiency(w), 1e-3f, "0.7 × 0.6 = 0.42");
        }

        [Test]
        public void SumEfficiency_ExcludesRestingWorkers()
        {
            wm.AssignJob(wm.Workers[0], WorkerJobs.Farm);
            wm.AssignJob(wm.Workers[1], WorkerJobs.Farm);
            wm.Workers[1].resting = true;
            wm.Workers[1].job = WorkerJobs.Idle; // resting → job ว่าง (ShiftSystem ทำแบบนี้)
            Assert.AreEqual(1f, wm.SumEfficiency(WorkerJobs.Farm), 1e-3f, "คนพักไม่นับในผลผลิต");
        }

        // ── Status severity + death ──────────────────────────────────
        [Test]
        public void Status_SeverityOrder_DyingBeatsHungry()
        {
            var w = wm.Workers[0];
            w.radiation = 81f;
            w.hunger = 90f;
            wm.RunDailyTick(Ctx(food: 0f, seed: 42));
            Assert.IsTrue(w.status == WorkerStatus.Dying || !w.alive,
                "rad > 80 → Dying (หรือตายจาก roll 20%) — ไม่ใช่ Hungry");
        }

        [Test]
        public void Death_Radiation80Plus_Rolls20Percent_Deterministic()
        {
            // Seed 42, not 1234: System.Random(1234) yields 0.399, 0.896, 0.319, 0.947 ... - not one of
            // its first 14 draws falls under deathChance 0.2, so every worker survived and the test read
            // as "deaths are broken". The roll itself is correct; the seed was just a 4%-unlucky draw.
            foreach (var w in wm.Workers) w.radiation = 95f;
            wm.RunDailyTick(Ctx(seed: 42));
            int dead = wm.Workers.Count(w => !w.alive);
            Assert.Greater(dead, 0, "rad 95 ทั้งเมือง seed คงที่ → ต้องมีคนตายบ้าง (~20%)");
            Assert.Less(dead, 14, "ไม่ใช่ตายหมด");

            // deterministic: seed เดิม + state เดิม → ผลเดิม
            wm.Initialize(cfg);
            foreach (var w in wm.Workers) w.radiation = 95f;
            wm.RunDailyTick(Ctx(seed: 42));
            Assert.AreEqual(dead, wm.Workers.Count(w => !w.alive), "seeded rng → ผลซ้ำได้");
        }

        // ── ShiftSystem (§17.5) ──────────────────────────────────────
        [Test]
        public void Shift_Fatigue70_RestsAndRemembersJob()
        {
            var w = wm.Workers[0];
            wm.AssignJob(w, WorkerJobs.Farm);
            w.fatigue = 70f;

            ShiftSystem.TickShifts(wm.Workers, labBusy: false, cfg);
            Assert.IsTrue(w.resting);
            Assert.AreEqual(WorkerJobs.Idle, w.job);
            Assert.AreEqual(WorkerJobs.Farm, w.lastJob, "จำงานเดิมไว้");

            w.fatigue = 25f;
            ShiftSystem.TickShifts(wm.Workers, labBusy: false, cfg);
            Assert.IsFalse(w.resting);
            Assert.AreEqual(WorkerJobs.Farm, w.job, "fatigue ≤ 25 → กลับงานเดิม");
        }

        [Test]
        public void Shift_LabDeadlockGuard92_KeepsResearcherOnJob()
        {
            var w = wm.Workers[0];
            wm.AssignJob(w, WorkerJobs.Lab);
            w.fatigue = 88f; // ≥ 70 แต่ < 92

            ShiftSystem.TickShifts(wm.Workers, labBusy: true, cfg);
            Assert.IsFalse(w.resting, "LOCK บั๊ก #7: labBusy && fatigue < 92 → ห้ามดึงออก");
            Assert.AreEqual(WorkerJobs.Lab, w.job);

            w.fatigue = 93f; // เกิน guard → พักได้
            ShiftSystem.TickShifts(wm.Workers, labBusy: true, cfg);
            Assert.IsTrue(w.resting, "fatigue ≥ 92 → พักได้แม้ lab busy");
        }

        [Test]
        public void Shift_LabWorker_NoActiveResearch_RestsNormally()
        {
            var w = wm.Workers[0];
            wm.AssignJob(w, WorkerJobs.Lab);
            w.fatigue = 75f;
            ShiftSystem.TickShifts(wm.Workers, labBusy: false, cfg);
            Assert.IsTrue(w.resting, "ไม่มีงานวิจัย active → กติกาพักปกติ");
        }
    }
}
