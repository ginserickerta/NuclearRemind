using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// STORY.md §④ "สรุปการเล่นของคุณ" + Achievements. RunStats latches the three facts the live
    /// systems throw away — the run's lowest Hope and when it happened, which Decree option was taken,
    /// and whether a Triage card was ever shown — and the achievements are scored from exactly those.
    /// </summary>
    public class RunStatsTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EventManager events;
        private WorkerManager wm;
        private RunStats stats;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            Achievements.ResetForTest();

            events = NewComponent<EventManager>("EventManager");
            wm = NewComponent<WorkerManager>("WorkerManager");
            wm.Initialize(cfg);
            stats = NewComponent<RunStats>("RunStats");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameConfigSO.OverrideForTest(null);
            Achievements.ResetForTest();
        }

        // ── Hope floor ────────────────────────────────────────────

        [Test]
        public void LowestHope_RemembersTheWorstDay_NotTheLast()
        {
            Day(3, hope: 62f);
            Day(4, hope: 41f);   // ★ the floor
            Day(5, hope: 55f);   // recovery must not overwrite it

            Assert.AreEqual(41f, stats.LowestHope, 1e-3f, "ต้องจำวันที่แย่ที่สุด ไม่ใช่ค่าล่าสุด");
            Assert.AreEqual(4, stats.LowestHopeDay, "ต้องจำว่าตกต่ำสุดวันไหน");
        }

        [Test]
        public void LowestHope_BeforeAnyDay_ReportsNoData_NotZero()
        {
            // A run that ends on day 1 has no committed Hope yet. Reporting "0" would read as
            // "the city lost all hope", which is the opposite of what happened.
            StringAssert.Contains("Hope ต่ำสุด", stats.BuildSummary());
            StringAssert.Contains("—", stats.BuildSummary(), "ยังไม่มีข้อมูล → ขีด ไม่ใช่เลข 0");
            Assert.AreEqual(0, stats.LowestHopeDay);
        }

        // ── Decree ────────────────────────────────────────────────

        [Test]
        public void Decree_RecordsWhichOptionWasTaken()
        {
            Assert.AreEqual(RunStats.NoDecree, stats.DecreeOption, "ยังไม่เจอการ์ด → ไม่มีประกาศ");
            StringAssert.Contains("ไม่มี", stats.BuildSummary());

            events.RaiseCrisisCardResolved(CardIds.Decree, 2);
            Assert.AreEqual(2, stats.DecreeOption);
            StringAssert.Contains("C · ดึงแรงงานเด็ก", stats.BuildSummary());
        }

        [Test]
        public void Decree_OptionA_CountsAsNoDecreeIssued()
        {
            // A = "ไม่ออกประกาศ". Resolving the card is not the same as issuing a decree, and the
            // "ศักดิ์ศรีของเมือง" achievement turns on that distinction.
            events.RaiseCrisisCardResolved(CardIds.Decree, 0);
            Assert.AreEqual(RunStats.NoDecree, stats.DecreeOption);
            Assert.Contains(Achievements.NoDecree, Achievements.Evaluate(stats, GameEndType.TrueEnding));
        }

        [Test]
        public void OtherCards_DoNotTouchTheDecreeRow()
        {
            events.RaiseCrisisCardResolved(CardIds.Hunger, 2);
            Assert.AreEqual(RunStats.NoDecree, stats.DecreeOption, "การ์ดใบอื่นไม่เกี่ยวกับ Decree");
        }

        // ── Triage ────────────────────────────────────────────────

        [Test]
        public void Triage_CountsWhenShown_EvenIfNeverResolved()
        {
            Assert.IsFalse(stats.TriageEncountered);
            events.RaiseCrisisCardShown(MakeCard(CardIds.Triage));

            Assert.IsTrue(stats.TriageEncountered,
                "★ เจอ = ถูกบังคับให้เลือก — นับตั้งแต่การ์ดเด้ง ไม่ต้องรอกดตอบ");
        }

        // ── Achievements ──────────────────────────────────────────

        [Test]
        public void CleanRun_EarnsAllThree_AndPersists()
        {
            var earned = Achievements.Evaluate(stats, GameEndType.TrueEnding);

            Assert.AreEqual(3, earned.Count, "ไม่เจอ Triage · ไม่ออก Decree · ไม่มีใครตาย → ครบ 3");
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.NoTriage));
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.NoDecree));
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.EveryoneHome));
        }

        [Test]
        public void Triage_LosesItsAchievement_ButNotTheOthers()
        {
            events.RaiseCrisisCardShown(MakeCard(CardIds.Triage));
            var earned = Achievements.Evaluate(stats, GameEndType.NormalEnding);

            CollectionAssert.DoesNotContain(earned, Achievements.NoTriage);
            CollectionAssert.Contains(earned, Achievements.NoDecree);
            CollectionAssert.Contains(earned, Achievements.EveryoneHome);
        }

        [Test]
        public void ADeath_LosesEveryoneHome()
        {
            wm.Workers[0].alive = false;
            Assert.AreEqual(1, stats.Deaths);

            CollectionAssert.DoesNotContain(
                Achievements.Evaluate(stats, GameEndType.TrueEnding), Achievements.EveryoneHome);
        }

        [Test]
        public void LostRun_EarnsNothing_EvenWithACleanSheet()
        {
            // ★ Otherwise a city that collapsed on day 9 would collect all three for having had no
            //   time to face a hard choice — rewarding failing early.
            foreach (var end in new[] { GameEndType.HopeZero, GameEndType.Meltdown, GameEndType.TimeoutLowQ })
                Assert.IsEmpty(Achievements.Evaluate(stats, end), $"{end} → ไม่ได้ achievement");
        }

        // ── Summary rows ──────────────────────────────────────────

        [Test]
        public void Summary_ShowsAllSevenSpecRows()
        {
            string s = stats.BuildSummary();

            StringAssert.Contains("ความรู้ที่ยืนยันแล้ว", s);
            StringAssert.Contains("คนที่รอด", s);
            StringAssert.Contains("คนที่เสียไป", s);
            StringAssert.Contains("ALARA compliance", s);
            StringAssert.Contains("Decree ที่ออก", s);
            StringAssert.Contains("Hope ต่ำสุด", s);
            StringAssert.Contains("Record ที่กู้คืน", s);
        }

        // ── ALARA — วัน-คนใน Zone B ที่ใส่ชุด ─────────────────────

        [Test]
        public void Alara_CountsSuitedWorkerDays_NotHeadcount()
        {
            var a = wm.Workers[0];
            var b = wm.Workers[1];
            wm.AssignJob(a, WorkerJobs.ZoneB);
            wm.AssignJob(b, WorkerJobs.ZoneB);

            a.hasRadSuit = true;                      // วันที่ 2: 1 ใน 2 คนมีชุด
            events.RaiseDayEnded(2);

            b.hasRadSuit = true;                      // วันที่ 3: ครบทั้งคู่
            events.RaiseDayEnded(3);

            Assert.AreEqual(4, stats.ZoneBWorkerDays, "2 คน × 2 วัน = 4 วัน-คน");
            Assert.AreEqual(3, stats.ZoneBSuitedDays, "ใส่ชุด 1 + 2 = 3 วัน-คน");
            Assert.AreEqual(0.75f, stats.AlaraCompliance.Value, 1e-3f);
            StringAssert.Contains("75%", stats.BuildSummary());
        }

        [Test]
        public void Alara_NeverSentAnyone_ReportsNoData_NotZeroPercent()
        {
            // ★ 0% would accuse the player of sending crews in unprotected; 100% would credit them for
            //   caution they never had to show. Neither happened — there is nothing to report.
            events.RaiseDayEnded(2);

            Assert.IsFalse(stats.AlaraCompliance.HasValue);
            StringAssert.Contains("ไม่เคยส่งคนเข้า Zone B", stats.BuildSummary());
        }

        [Test]
        public void Alara_DeadWorkersDoNotCount()
        {
            var w = wm.Workers[0];
            wm.AssignJob(w, WorkerJobs.ZoneB);
            w.alive = false;
            events.RaiseDayEnded(2);

            Assert.AreEqual(0, stats.ZoneBWorkerDays);
        }

        [Test]
        public void Summary_CountsMasteryOutOfEleven_NotAHardcodedNumber()
        {
            StringAssert.Contains($"/{QuizIds.All.Length}", stats.BuildSummary());
            Assert.AreEqual(11, QuizIds.All.Length, "สเปกกำหนด 11 ควิซ = 11 Codex entry");
        }

        [Test]
        public void Summary_SurvivorsStartAtFullPopulation()
        {
            StringAssert.Contains($"{wm.AliveCount}/{wm.Workers.Count}", stats.BuildSummary());
            Assert.AreEqual(0, stats.Deaths, "เริ่มเกม → ยังไม่มีใครตาย");
        }

        // ── Save / load ───────────────────────────────────────────

        [Test]
        public void SaveLoad_KeepsTheStatsThatNothingElseRemembers()
        {
            var a = wm.Workers[0];
            var b = wm.Workers[1];
            wm.AssignJob(a, WorkerJobs.ZoneB);
            wm.AssignJob(b, WorkerJobs.ZoneB);
            a.hasRadSuit = true;

            Day(6, hope: 33f);
            events.RaiseDayEnded(6);
            events.RaiseCrisisCardResolved(CardIds.Decree, 1);
            events.RaiseCrisisCardShown(MakeCard(CardIds.Triage));

            var save = new SaveData();
            stats.WriteTo(save);
            stats.ResetForNewRun();
            stats.RestoreFromSave(save);

            Assert.AreEqual(33f, stats.LowestHope, 1e-3f);
            Assert.AreEqual(6, stats.LowestHopeDay);
            Assert.AreEqual(1, stats.DecreeOption);
            Assert.IsTrue(stats.TriageEncountered);
            Assert.AreEqual(2, stats.ZoneBWorkerDays, "วัน-คนใน Zone B ต้องรอดข้ามเซฟ");
            Assert.AreEqual(1, stats.ZoneBSuitedDays);
        }

        [Test]
        public void OldSave_WithoutTheseFields_DoesNotClaimHopeHitZero()
        {
            // JsonUtility leaves absent fields at their C# initialiser, so statLowestHope defaults to
            // -1 (the "never sampled" sentinel) rather than 0, which would be a real Hope value.
            stats.RestoreFromSave(new SaveData());

            Assert.IsTrue(float.IsPositiveInfinity(stats.LowestHope), "เซฟเก่า → ถือว่ายังไม่เคยเก็บค่า");
            StringAssert.Contains("—", stats.BuildSummary());
        }

        // ── helpers ───────────────────────────────────────────────

        // One settled day: RunStats samples HopeLedger.OnCommitted, not the live running total.
        private void Day(int day, float hope)
        {
            events.RaiseDayStarted(day, true);
            wm.Hope.Report("test", "test", hope - wm.Hope.Current, HopeCategory.Story);
            wm.Hope.OnDayEnd();
        }

        private CrisisCardSO MakeCard(string id)
        {
            var c = ScriptableObject.CreateInstance<CrisisCardSO>();
            c.cardId = id; c.title = id; c.description = "d";
            c.dialogueLines = new string[0];
            c.options = new CardOption[0];
            _spawned.Add(c);
            return c;
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            // EditMode never calls Awake/OnEnable for AddComponent — invoke them so the singletons exist.
            TryInvokePrivate(c, "Awake");
            TryInvokePrivate(c, "OnEnable");
            return c;
        }

        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { method?.Invoke(target, null); }
            catch (TargetInvocationException) { }
        }
    }
}
