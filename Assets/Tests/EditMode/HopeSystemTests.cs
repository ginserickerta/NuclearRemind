using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §18 — Hope Ledger (hope is never written directly), source values from
    /// CONFIG.md, threshold events with hysteresis (+8), strike/exodus effects.
    /// </summary>
    public class HopeSystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private WorkerManager wm;
        private GameConfigSO cfg;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
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

        // ── Ledger core (§18) ───────────────────────────────────────
        [Test]
        public void Ledger_StartsAt70_NotHundred()
        {
            Assert.AreEqual(70f, wm.Hope.Current, 1e-3f, "hope_start = 70 (v5.2 — ไม่ใช่ 100)");
        }

        [Test]
        public void Ledger_SumsEntries_AppliesOnDayEnd_ThenClears()
        {
            wm.Hope.Report("worker.hungry", "คนหิว 4 คน", -8f, HopeCategory.Worker);
            wm.Hope.Report("research.complete", "วิจัยสำเร็จ", +6f, HopeCategory.Research);

            Assert.AreEqual(70f, wm.Hope.Current, 1e-3f, "ยังไม่ apply จนกว่าจะจบวัน");
            Assert.AreEqual(2, wm.Hope.GetTodayBreakdown().Count);

            float delta = wm.Hope.OnDayEnd();
            Assert.AreEqual(-2f, delta, 1e-3f);
            Assert.AreEqual(68f, wm.Hope.Current, 1e-3f);
            Assert.AreEqual(0, wm.Hope.GetTodayBreakdown().Count, "เคลียร์หลัง commit");
            Assert.AreEqual(2, wm.Hope.GetCommittedBreakdown().Count, "breakdown เก็บไว้ให้ UI");
        }

        [Test]
        public void Ledger_ClampsToZeroAndHundred()
        {
            wm.Hope.Report("x", "หนักมาก", -500f, HopeCategory.Card);
            wm.Hope.OnDayEnd();
            Assert.AreEqual(0f, wm.Hope.Current, "clamp ต่ำสุด 0");

            wm.Hope.Report("y", "ปาฏิหาริย์", +500f, HopeCategory.Card);
            wm.Hope.OnDayEnd();
            Assert.AreEqual(100f, wm.Hope.Current, "clamp เพดาน 100");
        }

        [Test]
        public void Ledger_ZeroValueEntries_AreIgnored()
        {
            wm.Hope.Report("noop", "ไม่มีผล", 0f, HopeCategory.Worker);
            Assert.AreEqual(0, wm.Hope.GetTodayBreakdown().Count, "ค่า 0 ไม่เข้าบัญชี — กัน breakdown รก");
        }

        // ── Worker status → hope (CONFIG.md source table) ───────────
        [Test]
        public void DailyTick_HungryWorkers_ReportMinus2PerHead()
        {
            // อด 2 วัน → ทุกคน Hungry (hunger 60) → −2 × 14 = −28
            wm.RunDailyTick(new WorkerTickContext { foodStock = 0f, rng = new System.Random(1) });
            wm.Hope.OnDayEnd(); // วันแรก: hunger 30 ยังไม่เกิน 55 → ไม่มี entry
            float before = wm.Hope.Current;

            wm.RunDailyTick(new WorkerTickContext { foodStock = 0f, rng = new System.Random(2) });
            var entries = wm.Hope.GetTodayBreakdown();
            var hungry = entries.FirstOrDefault(e => e.sourceKey == "worker.hungry");
            Assert.AreEqual(-28f, hungry.value, 1e-3f, "hungry 14 คน × −2");

            wm.Hope.OnDayEnd();
            Assert.AreEqual(before - 28f, wm.Hope.Current, 1e-3f);
        }

        [Test]
        public void DailyTick_Death_ReportsMinus8Once()
        {
            foreach (var w in wm.Workers) w.radiation = 100f;
            wm.RunDailyTick(new WorkerTickContext { foodStock = 999f, rng = new System.Random(7) });

            int deaths = wm.Workers.Count(w => !w.alive);
            Assert.Greater(deaths, 0);
            var deathEntry = wm.Hope.GetTodayBreakdown().FirstOrDefault(e => e.sourceKey == "worker.death");
            Assert.AreEqual(-8f * deaths, deathEntry.value, 1e-3f, "worker.death = −8/คน ครั้งเดียว");
        }

        // ── Threshold watcher + hysteresis (§18) ────────────────────
        [Test]
        public void Watcher_Strike_FiresOnceBelow40_RearmsAboveThresholdPlus8()
        {
            int strikes = 0;
            var watcher = new HopeThresholdWatcher(cfg);
            watcher.OnStrike += _ => strikes++;

            watcher.Evaluate(39f);
            Assert.AreEqual(1, strikes, "ต่ำกว่า 40 → strike");
            watcher.Evaluate(35f);
            watcher.Evaluate(39f);
            Assert.AreEqual(1, strikes, "ยังไม่ re-arm — ห้ามยิงซ้ำทุกวัน");

            watcher.Evaluate(47f); // 40 + 8 = 48 → ยังไม่พ้น
            watcher.Evaluate(39f);
            Assert.AreEqual(1, strikes, "47 < 48 → ยังไม่ re-arm (hysteresis margin 8)");

            watcher.Evaluate(49f); // > 48 → re-arm
            watcher.Evaluate(39f);
            Assert.AreEqual(2, strikes, "พ้น threshold+8 แล้วตกใหม่ → ยิงอีกครั้ง");
        }

        [Test]
        public void Watcher_GameOver_AtZero()
        {
            bool over = false;
            var watcher = new HopeThresholdWatcher(cfg);
            watcher.OnGameOver += () => over = true;
            watcher.Evaluate(0f);
            Assert.IsTrue(over, "hope <= 0 → Game Over (HopeZero)");
        }

        // ── Strike / Exodus effects ──────────────────────────────────
        [Test]
        public void Strike_10Percent_StopFor2Days_ThenReturn()
        {
            foreach (var w in wm.Workers) wm.AssignJob(w, WorkerJobs.Farm);

            wm.Hope.Report("crisis", "วิกฤตหนัก", -35f, HopeCategory.Card); // 70 → 35 < 40
            wm.CommitDay();

            var strikers = wm.Workers.Where(w => w.strikeDaysLeft > 0).ToList();
            Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(14 * 0.10f)), strikers.Count, "10% หยุดงาน");
            Assert.IsTrue(strikers.All(w => w.job == WorkerJobs.Idle && w.lastJob == WorkerJobs.Farm));

            wm.TickStrikes(); // วัน 1
            wm.TickStrikes(); // วัน 2 — ครบกำหนด
            Assert.IsTrue(strikers.All(w => w.strikeDaysLeft == 0 && w.job == WorkerJobs.Farm),
                "ครบ 2 วัน → กลับงานเดิม");
        }

        [Test]
        public void Exodus_15Percent_LeavePermanently_NoDeathPenalty()
        {
            wm.Hope.Report("collapse", "เมืองใกล้ล่ม", -46f, HopeCategory.Card); // 70 → 24 < 25
            wm.CommitDay();

            int expectedLeft = 14 - Mathf.Max(1, Mathf.FloorToInt(14 * 0.15f));
            Assert.AreEqual(expectedLeft, wm.AliveCount, "ประชากร −15% ถาวร");
            Assert.IsFalse(wm.Hope.GetTodayBreakdown().Any(e => e.sourceKey == "worker.death"),
                "คนทิ้งเมือง ≠ คนตาย — ไม่มีโทษ worker.death");
        }

        // ── Trend helper (HopeBreakdownPanel) ───────────────────────
        [Test]
        public void TrendGlyphs_MapHopeToBlocks()
        {
            var trend = HopeBreakdownPanel.BuildTrend(new List<float> { 0f, 50f, 99f });
            Assert.AreEqual(3, trend.Length);
            Assert.AreEqual('▁', trend[0]);
            Assert.AreEqual('█', trend[2]);
        }
    }
}
