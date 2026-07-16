using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §26 — Reactor. The formula that ties the game together: capped cooling (bug #1), the
    /// ★ Method B tritium gate (CORE stalls at 80 without tritium — the Death-Spiral payoff / win
    /// gate), mastery bonuses, boost, SCRAM, coils, win, meltdown. DailyTick takes its environmental
    /// couplings as params, so no ResourceManager/scene is needed.
    /// </summary>
    public class ReactorTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private ReactorController r;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
            KnowledgeDB.ResetForTest();

            var evGo = new GameObject("EventManager"); _spawned.Add(evGo);
            evGo.AddComponent<EventManager>();

            var rGo = new GameObject("ReactorController"); _spawned.Add(rGo);
            r = rGo.AddComponent<ReactorController>(); r.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
            KnowledgeDB.ResetForTest();
        }

        // ─────────────────────────────────────────
        //  ★ Method B gate — the win condition
        // ─────────────────────────────────────────
        [Test]
        public void MethodBGate_CoreStallsAt80_WithoutTritium()
        {
            r.Core = 85f; r.Tritium = 3f; // < methodBTritiumMin (5)
            r.DailyTick(90f, 3, false, false);
            Assert.AreEqual(0f, r.LastGain, 1e-4f, "★ core≥80 && tritium<5 → gain 0 (CORE ค้าง = แพ้)");
            Assert.AreEqual(85f, r.Core, 1e-4f, "CORE ไม่ขยับ");
        }

        [Test]
        public void MethodBGate_CoreProgresses_WithTritium()
        {
            r.Core = 85f; r.Tritium = 10f;
            r.DailyTick(90f, 3, false, false);
            Assert.Greater(r.LastGain, 0f, "★ มีทริเทียม → CORE เดินต่อ (ตอบควิซ = ชนะ)");
            Assert.Greater(r.Core, 85f);
            Assert.AreEqual(10f - cfg.idleTritiumCost, r.Tritium, 1e-4f, "หักทริเทียม 6/วัน (idle)");
        }

        [Test]
        public void ReachesWin_AtCore100()
        {
            r.Core = 99f; r.Tritium = 20f; r.SetBoosting(true);
            r.DailyTick(90f, 5, false, false);
            Assert.AreEqual(100f, r.Core, 1e-4f, "clamp ที่ 100");
            Assert.IsTrue(r.IsWin);
        }

        [Test]
        public void Meltdown_WhenHeatReaches100()
        {
            r.Heat = 95f; r.SetBoosting(true);
            r.DailyTick(0f, 0, false, false); // no cooling water/workers → heat runs away
            Assert.GreaterOrEqual(r.Heat, cfg.heatMeltdown);
            Assert.IsTrue(r.IsMeltdown);
        }

        // ─────────────────────────────────────────
        //  Cooling (capped) + mastery
        // ─────────────────────────────────────────
        [Test]
        public void Cooling_IsCapped_Bug1()
        {
            r.DailyTick(1200f, 2, false, false); // huge water — must NOT blow cooling up
            // 6 + min(1200/12, 7)=7 + 2*3=6 + 0 = 19
            Assert.AreEqual(19f, r.LastCooling, 1e-3f, "★ water เพดาน 7 (บั๊ก #1: เดิมไม่มีเพดาน HEAT=0 ตลอด)");
        }

        [Test]
        public void Mastery_Confinement_AddsCooling5()
        {
            r.DailyTick(120f, 2, false, false);
            float baseCool = r.LastCooling;
            MasteryRegistry.Instance.Grant(QuizIds.Plasma); // cooling +5
            r.DailyTick(120f, 2, false, false);
            Assert.AreEqual(baseCool + cfg.coolingMasteryBonus, r.LastCooling, 1e-3f, "Mastery cooling +5");
        }

        [Test]
        public void Mastery_Deuterium_RaisesFuelEfficiency()
        {
            r.Core = 50f;
            r.DailyTick(90f, 3, false, false);
            float g0 = r.LastGain;                              // 1.05 × 1.0
            MasteryRegistry.Instance.Grant(QuizIds.Deuterium);  // fe += 0.08
            r.Core = 50f;
            r.DailyTick(90f, 3, false, false);
            Assert.AreEqual(g0 * (1f + cfg.fuelEffBonus), r.LastGain, 1e-3f, "fuelEfficiency +0.08");
        }

        // ─────────────────────────────────────────
        //  Boost / storm / SCRAM / coils
        // ─────────────────────────────────────────
        [Test]
        public void Boost_HigherGainAndHeat_ThanIdle()
        {
            r.Core = 50f; r.Heat = 20f; r.SetBoosting(false);
            r.DailyTick(120f, 3, false, false);
            float idleGain = r.LastGain;

            r.Core = 50f; r.Heat = 20f; r.SetBoosting(true);
            r.DailyTick(120f, 3, false, false);
            Assert.Greater(r.LastGain, idleGain, "boost gain > idle");
        }

        [Test]
        public void StormHeat_PushesHeatUp()
        {
            r.Heat = 20f;
            r.DailyTick(200f, 5, false, false);
            float calm = r.Heat;
            r.Heat = 20f;
            r.DailyTick(200f, 5, true, false); // storm +40/day
            Assert.Greater(r.Heat, calm, "★ storm_heat 40/วัน (เดิม 12 = ไร้ผล)");
        }

        [Test]
        public void SensorHeadroom_LowersHeat()
        {
            r.Heat = 40f;
            r.DailyTick(90f, 1, false, false);
            float noSensor = r.Heat;
            r.Heat = 40f;
            r.DailyTick(90f, 1, false, true); // sensor +4 cooling
            Assert.Less(r.Heat, noSensor, "Sensor ให้ห้องหายใจ +4");
        }

        [Test]
        public void Scram_Gated_And_ReducesHeat()
        {
            r.Heat = 50f;
            Assert.IsFalse(r.CanScram, "HEAT < 90 → กด SCRAM ไม่ได้");
            r.Heat = 95f;
            Assert.IsTrue(r.CanScram);
            Assert.IsTrue(r.Scram());
            Assert.AreEqual(55f, r.Heat, 1e-3f, "HEAT −40");
            Assert.IsFalse(r.IsBoosting, "บังคับ idle");
            Assert.AreEqual(cfg.scramCooldownDays, r.ScramCooldown);
            Assert.IsFalse(r.CanScram, "cooldown → กดซ้ำไม่ได้");
        }

        [Test]
        public void Coils_GatedByConfinementNote()
        {
            Assert.IsFalse(r.InstallToroidal(), "ยังไม่วิจัย confinement → ติดคอยล์ไม่ได้");
            KnowledgeDB.Instance.CompleteNote("confinement");
            Assert.IsTrue(r.InstallToroidal());
            Assert.AreEqual(1, r.ToroidalLv);
        }
    }
}
