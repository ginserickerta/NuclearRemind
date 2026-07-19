using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §22 — Zone B & Rad Suits. The Sprint 5 acceptance ("Death Spiral เกิดจริง"):
    /// unsuited workers take rad_zoneb daily, cross zoneb_rotate_rad, get pulled out, the zone drops
    /// below min-staff and tritium stops. Suits (×0.4) slow it. Production is the 3.0-vs-8.0 game
    /// decider and latches the q_tritium_breeding quiz (bug #3).
    /// </summary>
    public class ZoneBSystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private WorkerManager wm;
        private ZoneBController zb;
        private RadSuitManager suits;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
            CodexQuizManager.ResetForTest();
            KnowledgeDB.ResetForTest();

            NewComponent<EventManager>("EventManager");
            wm = NewComponent<WorkerManager>("WorkerManager"); wm.Initialize(cfg);
            zb = NewComponent<ZoneBController>("ZoneBController"); zb.Initialize(cfg);
            suits = NewComponent<RadSuitManager>("RadSuitManager"); suits.Initialize(cfg);
        }

        /// <summary>
        /// EditMode never calls Awake/OnEnable for AddComponent, so the `Instance` singletons stay null
        /// and the systems cannot find each other. Same idiom as ResearchManagerTests / DataRecoveryTests.
        /// </summary>
        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
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

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
            CodexQuizManager.ResetForTest();
            KnowledgeDB.ResetForTest();
        }

        private void AssignZoneB(int count)
        {
            for (int i = 0; i < count && i < wm.Workers.Count; i++)
                wm.AssignJob(wm.Workers[i], WorkerJobs.ZoneB);
        }

        // one full day: WorkerManager applies radiation, then ZoneB rotates + produces
        private void Tick(int seed = 1) =>
            wm.RunDailyTick(new WorkerTickContext { foodStock = 999f, rng = new System.Random(seed) });

        // ─────────────────────────────────────────
        //  ★ Death Spiral — unsuited workers rotate out and tritium stops
        // ─────────────────────────────────────────
        [Test]
        public void DeathSpiral_NoSuits_WorkersRotatedOutAndTritiumStops()
        {
            zb.Open();
            AssignZoneB(2);

            Tick(); zb.TickDay();  // rad 15 (<32) — staffed
            Assert.AreEqual(2, wm.GetWorkers(WorkerJobs.ZoneB).Count, "วัน 1: ยังอยู่");
            Assert.Greater(zb.LastProduced, 0f, "ยังผลิต");

            Tick(); zb.TickDay();  // rad 30 (<32) — staffed
            Assert.AreEqual(2, wm.GetWorkers(WorkerJobs.ZoneB).Count, "วัน 2: ยังอยู่");

            Tick(); zb.TickDay();  // rad 45 (>32) — BOTH rotated out
            Assert.AreEqual(0, wm.GetWorkers(WorkerJobs.ZoneB).Count, "★ วัน 3: รังสีเกิน 32 → ดึงออกหมด");
            Assert.AreEqual(0f, zb.LastProduced, 1e-4f, "★ ไม่มีคน → tritium หยุด (Death Spiral)");
        }

        [Test]
        public void Suits_SlowTheSpiral()
        {
            KnowledgeDB.Instance.CompleteNote("nuclear_medicine"); // unlock suit recipe
            Assert.IsTrue(suits.CraftSuit());
            Assert.IsTrue(suits.CraftSuit());                       // 2 suits (no ResourceManager → free in test)

            zb.Open();
            AssignZoneB(2);
            suits.EquipZoneBWorkers();                              // both wear a suit

            for (int i = 0; i < 3; i++) { Tick(); zb.TickDay(); }   // rad ×0.4 → 6/day → 18 after 3 days
            Assert.AreEqual(2, wm.GetWorkers(WorkerJobs.ZoneB).Count,
                "★ มีชุด → 3 วันยังไม่ถึง 32 (เทียบไม่มีชุดโดนดึงออกหมดแล้ว)");
            foreach (var w in wm.GetWorkers(WorkerJobs.ZoneB))
                Assert.Less(w.radiation, cfg.zoneBRotateRad, "รังสียังต่ำกว่าเกณฑ์ดึงออก");
        }

        // ─────────────────────────────────────────
        //  Production — the 3.0 vs 8.0 game decider
        // ─────────────────────────────────────────
        [Test]
        public void Production_3WithoutQuiz_8WithQuiz()
        {
            zb.Open();
            AssignZoneB(2);

            Tick(); zb.TickDay();
            Assert.AreEqual(cfg.zoneBTritiumBase, zb.LastProduced, 1e-4f, "ไม่ตอบควิซ → 3.0/วัน");

            MasteryRegistry.Instance.Grant(QuizIds.TritiumBreeding);
            Tick(); zb.TickDay();
            Assert.AreEqual(cfg.zoneBTritiumMastery, zb.LastProduced, 1e-4f, "★ ตอบควิซ → 8.0/วัน");
        }

        [Test]
        public void Production_LatchesTritiumQuizApplied_Bug3Link()
        {
            zb.Open();
            AssignZoneB(2);
            Tick(); zb.TickDay();   // produced day 1
            Tick(); zb.TickDay();   // produced day 2

            var applied = CodexQuizManager.Instance.Applied;
            Assert.IsTrue(applied.tritiumEverProduced, "★ ผลิตแล้ว → latch (บั๊ก #3) ติดตลอด");
            Assert.GreaterOrEqual(applied.zoneBProducedDays, 2, "ผลิต ≥ 2 วัน → q_tritium_breeding พร้อมตอบ");
        }

        [Test]
        public void Understaffed_NoProduction()
        {
            zb.Open();
            AssignZoneB(1);           // < min-staff 2
            Tick(); zb.TickDay();
            Assert.AreEqual(0f, zb.LastProduced, 1e-4f, "คน < 2 → ไม่ผลิต");
        }

        [Test]
        public void Closed_NoProduction()
        {
            AssignZoneB(2);           // staffed but zone not opened
            Tick(); zb.TickDay();
            Assert.AreEqual(0f, zb.LastProduced, 1e-4f, "ยังไม่เปิด Zone B → ไม่ผลิต");
        }

        [Test]
        public void CraftSuit_GatedByNuclearMedicineNote()
        {
            Assert.IsFalse(suits.CraftSuit(), "ยังไม่วิจัย nuclear_medicine → คราฟต์ชุดไม่ได้");
            KnowledgeDB.Instance.CompleteNote("nuclear_medicine");
            Assert.IsTrue(suits.CraftSuit(), "วิจัยแล้ว → คราฟต์ได้");
        }
    }
}
