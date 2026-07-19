using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 / BARKS.md — Bark pool. State-bound conditions (never day), ≤ 2 barks/day, highest
    /// priority wins, cooldown gates repeats, onceOnly speaks once. Uses real barkIds so the
    /// BarkConditions switch is exercised against crafted snapshots.
    /// </summary>
    public class BarkTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private BarkManager bm;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();

            var evGo = new GameObject("EventManager"); _spawned.Add(evGo);
            evGo.AddComponent<EventManager>();

            var bmGo = new GameObject("BarkManager"); _spawned.Add(bmGo);
            bm = bmGo.AddComponent<BarkManager>(); bm.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            KnowledgeDB.ResetForTest();
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
        }

        private BarkSO B(string id, BarkSpeaker sp, int prio, int cd, bool once)
        {
            var b = ScriptableObject.CreateInstance<BarkSO>();
            b.barkId = id; b.speaker = sp; b.text = id; b.priority = prio; b.cooldownDays = cd; b.onceOnly = once;
            _spawned.Add(b);
            return b;
        }

        // ─────────────────────────────────────────
        //  Conditions — bound to state, not day
        // ─────────────────────────────────────────
        // Snapshot() never wrote these 5 fields, so BarkConditions read a permanent `false` and the barks
        // could not fire at all. Guarding them here because the scheduler tests all pass regardless.
        [Test]
        public void RevivedConditions_FireFromTheirSnapshotFields()
        {
            Assert.IsTrue(BarkConditions.IsMet("K11", new BarkWorldState { researchStalled = true }));
            Assert.IsTrue(BarkConditions.IsMet("K12", new BarkWorldState { blackout = true }));
            Assert.IsTrue(BarkConditions.IsMet("M13", new BarkWorldState { decreeActive = true }));
            Assert.IsTrue(BarkConditions.IsMet("C04", new BarkWorldState { decreeChildren = true }));
            Assert.IsTrue(BarkConditions.IsMet("C05", new BarkWorldState { decreeNone = true, stormActive = true }));
        }

        [Test]
        public void M03_StopsNaggingOnceTheMedBayExists()
        {
            KnowledgeDB.Instance.CompleteNote("nuclear_medicine");
            Assert.IsTrue(BarkConditions.IsMet("M03", new BarkWorldState { hasMedBay = false }));
            Assert.IsFalse(BarkConditions.IsMet("M03", new BarkWorldState { hasMedBay = true }),
                "สร้างที่พยาบาลแล้ว ห้ามบอกให้สร้างอีก");
        }

        [Test]
        public void M04_NeedsAMedBayBeforeItCanBeFull()
        {
            Assert.IsFalse(BarkConditions.IsMet("M04",
                new BarkWorldState { hasMedBay = false, medBayCapacity = 0, sickWorkers = 9 }),
                "ยังไม่มีที่พยาบาล จะ 'เต็ม' ไม่ได้");
            Assert.IsTrue(BarkConditions.IsMet("M04",
                new BarkWorldState { hasMedBay = true, medBayCapacity = 4, sickWorkers = 5 }));
        }

        [Test]
        public void Conditions_AreStateBound()
        {
            Assert.IsTrue(BarkConditions.IsMet("M11", new BarkWorldState { hope = 39f }));
            Assert.IsFalse(BarkConditions.IsMet("M11", new BarkWorldState { hope = 40f }));
            Assert.IsTrue(BarkConditions.IsMet("M10", new BarkWorldState { hungryWorkers = 3 }));
            Assert.IsFalse(BarkConditions.IsMet("M10", new BarkWorldState { hungryWorkers = 2 }));
            Assert.IsTrue(BarkConditions.IsMet("M05", new BarkWorldState { dyingWorkers = 1 }));
            Assert.IsTrue(BarkConditions.IsMet("C03", new BarkWorldState { hope = 24f }));
        }

        [Test]
        public void Conditions_HasNoteAndMastery()
        {
            Assert.IsFalse(BarkConditions.IsMet("D03", default), "ยังไม่วิจัย irradiation");
            KnowledgeDB.Instance.CompleteNote("irradiation");
            Assert.IsTrue(BarkConditions.IsMet("D03", default), "วิจัยแล้ว → Dorn พูด");

            Assert.IsFalse(BarkConditions.IsMet("M09", default), "ยังไม่ได้ mastery");
            MasteryRegistry.Instance.Grant(QuizIds.NuclearMedicine);
            Assert.IsTrue(BarkConditions.IsMet("M09", default), "ได้ mastery → Mira พูด ALARA");
        }

        // ─────────────────────────────────────────
        //  Selection — ≤ 2/day, priority, cooldown, once
        // ─────────────────────────────────────────
        [Test]
        public void AtMostTwoPerDay_HighestPriorityFirst()
        {
            bm.RegisterCatalog(new[]
            {
                B("M05", BarkSpeaker.Mira, 95, 1, false),  // dying
                B("M11", BarkSpeaker.Mira, 85, 3, false),  // hope<40
                B("C02", BarkSpeaker.Citizen, 75, 3, false),// hope<40
                B("C01", BarkSpeaker.Citizen, 60, 99, true),// hope<60
            });
            var s = new BarkWorldState { hope = 20f, dyingWorkers = 1 }; // all four met

            var fired = bm.EvaluateDay(2, s);
            Assert.AreEqual(2, fired.Count, "★ สูงสุด 2 bark/วัน");
            Assert.AreEqual("M05", fired[0].barkId, "prio 95 มาก่อน");
            Assert.AreEqual("M11", fired[1].barkId, "prio 85 รองลงมา");
        }

        [Test]
        public void Cooldown_BlocksRepeatUntilElapsed()
        {
            bm.RegisterCatalog(new[] { B("M05", BarkSpeaker.Mira, 95, 3, false) });
            var s = new BarkWorldState { dyingWorkers = 1 };

            Assert.AreEqual(1, bm.EvaluateDay(2, s).Count, "วัน 2 พูด");
            Assert.AreEqual(0, bm.EvaluateDay(3, s).Count, "cooldown 3 → วัน 3 เงียบ");
            Assert.AreEqual(1, bm.EvaluateDay(5, s).Count, "5−2=3 → พูดได้อีก");
        }

        [Test]
        public void OnceOnly_SpeaksOncePerGame()
        {
            bm.RegisterCatalog(new[] { B("M01", BarkSpeaker.Mira, 95, 99, true) });
            var s = new BarkWorldState { sickWorkers = 2 };
            Assert.AreEqual(1, bm.EvaluateDay(2, s).Count, "ครั้งแรกพูด");
            Assert.AreEqual(0, bm.EvaluateDay(50, s).Count, "onceOnly → ไม่พูดอีก");
        }

        [Test]
        public void SilentWhenNoConditionMet()
        {
            bm.RegisterCatalog(new[] { B("M05", BarkSpeaker.Mira, 95, 1, false) });
            var fired = bm.EvaluateDay(2, new BarkWorldState { dyingWorkers = 0 });
            Assert.IsEmpty(fired, "ไม่มีเงื่อนไขเข้า → เงียบ");
        }

        [Test]
        public void FiringRaisesEvent()
        {
            bm.RegisterCatalog(new[] { B("M10", BarkSpeaker.Mira, 80, 2, false) });
            var heard = new List<string>();
            EventManager.Instance.OnBarkFired += b => heard.Add(b.barkId);
            bm.EvaluateDay(2, new BarkWorldState { hungryWorkers = 3 });
            Assert.Contains("M10", heard, "ยิง event ให้ HUD");
        }
    }
}
