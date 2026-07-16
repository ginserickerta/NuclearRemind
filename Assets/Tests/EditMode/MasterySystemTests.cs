using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §21 — Mastery + Codex quiz. The Sprint 3 acceptance (§32):
    ///   • answer vs no-answer → CLEARLY different (ZoneB 3→8, radiation ×0.8, heal 25→35, ...)
    ///   • Codex counts x/11 correctly
    /// plus bug #3 (requiresApplied uses the tritiumEverProduced LATCH, never current stock),
    /// wrong-answer = no penalty + retry, and meta-persistence of mastery.
    /// In-memory catalog (no Resources dependency); Application.isPlaying is false in EditMode, so
    /// Grant / codex unlock stay in-memory and never touch PlayerPrefs.
    /// </summary>
    public class MasterySystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EventManager events;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);

            var evGo = new GameObject("EventManager");
            _spawned.Add(evGo);
            events = evGo.AddComponent<EventManager>();

            MetaProgress.ClearForTest();          // empty MasteryBank/Codex BEFORE registry seeds
            MasteryRegistry.ResetForTest();
            CodexQuizManager.ResetForTest();
            CodexQuizManager.Instance.RegisterCatalog(BuildQuizzes(), BuildCodex());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            MetaProgress.ClearForTest();
            MasteryRegistry.ResetForTest();
            CodexQuizManager.ResetForTest();
        }

        // ── in-memory catalog (mirror QuizCodexSetup — 11 quiz + 11 codex 1:1) ──
        private static QuizQuestionSO MakeQuiz(string quizId, string entryId, QuizCategory cat)
        {
            var q = ScriptableObject.CreateInstance<QuizQuestionSO>();
            q.id = quizId; q.quizId = quizId; q.category = cat;
            q.topicTitle = quizId; q.question = "q?";
            q.options = new[] { "A", "B", "C" };
            q.correctIndex = 1;                    // B correct for every test quiz
            q.explainText = "explain-" + quizId;   // shown always (right or wrong)
            q.codexUnlockId = entryId;
            q.requiresApplied = true;
            return q;
        }

        private static CodexEntrySO MakeCodex(string entryId, string quizId, QuizCategory cat)
        {
            var c = ScriptableObject.CreateInstance<CodexEntrySO>();
            c.entryId = entryId; c.unlockedFromQuiz = quizId; c.category = cat;
            c.titleTh = entryId; c.titleEn = entryId; c.bodyText = "body-" + entryId;
            return c;
        }

        // quizId → (entryId, category), covering all 11
        private static readonly (string quiz, string entry, QuizCategory cat)[] Map =
        {
            (QuizIds.Deuterium,       "codex_deuterium",             QuizCategory.Reactor),
            (QuizIds.Plasma,          "codex_plasma_confinement",    QuizCategory.Reactor),
            (QuizIds.MagneticPair,    "codex_magnetic_confinement",  QuizCategory.Reactor),
            (QuizIds.NuclearMedicine, "codex_nuclear_medicine",      QuizCategory.Medical),
            (QuizIds.Alara,           "codex_alara",                 QuizCategory.Ethics),
            (QuizIds.Mutation,        "codex_mutation_breeding",     QuizCategory.Agriculture),
            (QuizIds.FoodIrradiation, "codex_food_irradiation",      QuizCategory.Agriculture),
            (QuizIds.DtFuel,          "codex_dt_fusion_fuel",        QuizCategory.Reactor),
            (QuizIds.TritiumBreeding, "codex_tritium_breeding",      QuizCategory.Reactor),
            (QuizIds.Fusion,          "codex_nuclear_fusion",        QuizCategory.Reactor),
            (QuizIds.CleanEnergy,     "codex_clean_energy",          QuizCategory.Reactor),
        };

        private List<QuizQuestionSO> BuildQuizzes()
        {
            var list = Map.Select(m => MakeQuiz(m.quiz, m.entry, m.cat)).ToList();
            _spawned.AddRange(list);
            return list;
        }

        private List<CodexEntrySO> BuildCodex()
        {
            var list = Map.Select(m => MakeCodex(m.entry, m.quiz, m.cat)).ToList();
            _spawned.AddRange(list);
            return list;
        }

        // fully-applied snapshot (every requiresApplied satisfied)
        private static MasteryAppliedState AllApplied() => new MasteryAppliedState
        {
            extractorRanDays = 1, coilTypesInstalled = 2, medBayHealedCount = 1,
            riskZoneEntered = true, mutationLabRan = true, co60Ran = true, tritiumFed = true,
            zoneBProducedDays = 2, tritiumEverProduced = true, coreProgress = 100f, reachedEnding = true,
        };

        // ─────────────────────────────────────────
        //  ★ bug #3 — the latch, never current stock
        // ─────────────────────────────────────────
        [Test]
        public void Bug3_TritiumBreeding_GatedByLatch_NotCurrentStock()
        {
            var s = new MasteryAppliedState();
            // Zone B never produced → locked (this is correct)
            Assert.IsFalse(QuizAvailability.IsApplied(QuizIds.TritiumBreeding, s), "ยังไม่ผลิต → ล็อก");

            // Produced 1 day: latch set but < 2 days → still locked
            s.tritiumEverProduced = true; s.zoneBProducedDays = 1;
            Assert.IsFalse(QuizAvailability.IsApplied(QuizIds.TritiumBreeding, s), "ผลิต 1 วัน → ยังไม่ครบ");

            // Produced ≥2 days: answerable — and stays so FOREVER even though tritium stock is burned
            // to 0 every turn (the struct has no stock field ON PURPOSE — that's the bug #3 fix).
            s.zoneBProducedDays = 2;
            Assert.IsTrue(QuizAvailability.IsApplied(QuizIds.TritiumBreeding, s),
                "★ บั๊ก #3: ผูก latch (tritiumEverProduced) ไม่ใช่ stock ปัจจุบัน — ตอบได้แม้ tritium ถูกเผาเป็น 0");
        }

        [Test]
        public void Bug3_QuizStaysAnswerable_AcrossManyBurnTurns()
        {
            var cq = CodexQuizManager.Instance;
            cq.MarkTritiumProduced();  // day 1
            cq.MarkTritiumProduced();  // day 2 → latched + 2 days
            // Simulate 40 turns where stock would read 0 (we never feed stock — only the latch matters)
            for (int i = 0; i < 40; i++)
                Assert.IsTrue(cq.IsAnswerable(QuizIds.TritiumBreeding),
                    "ควิซหัวใจต้องตอบได้ทุกเทิร์น ไม่ใช่ 1/40 (บั๊ก #3)");
        }

        // ─────────────────────────────────────────
        //  ★ answer vs no-answer → clearly different
        // ─────────────────────────────────────────
        [Test]
        public void AnswerVsNoAnswer_ZoneBTritium_3to8_TheWinGate()
        {
            var mr = MasteryRegistry.Instance;
            Assert.AreEqual(cfg.zoneBTritiumBase, mr.ZoneBTritiumPerDay(), 1e-4f, "ไม่ตอบ → 3.0/วัน (แพ้)");
            Assert.IsTrue(mr.Grant(QuizIds.TritiumBreeding));
            Assert.AreEqual(cfg.zoneBTritiumMastery, mr.ZoneBTritiumPerDay(), 1e-4f, "★ ตอบถูก → 8.0/วัน (ชนะ)");
            Assert.AreNotEqual(mr.ZoneBTritiumPerDay(), cfg.zoneBTritiumBase, "ต่างกันชัด");
        }

        [Test]
        public void AnswerVsNoAnswer_AllTypedBonusesFlip()
        {
            var mr = MasteryRegistry.Instance;

            // baseline (no mastery)
            Assert.AreEqual(1f, mr.RadiationMult(), 1e-4f);
            Assert.AreEqual(cfg.medBayHeal, mr.MedBayHeal(), 1e-4f);
            Assert.AreEqual(0f, mr.FuelEfficiencyBonus(), 1e-4f);
            Assert.AreEqual(0f, mr.CoolingBonus(), 1e-4f);
            Assert.AreEqual(1f, mr.PoloidalDampMult(), 1e-4f);
            Assert.AreEqual(1f, mr.FarmYieldMult(), 1e-4f);
            Assert.AreEqual(1f, mr.SpoilMult(), 1e-4f);
            Assert.AreEqual(1f, mr.BoostHeatMult(), 1e-4f);

            mr.Grant(QuizIds.NuclearMedicine);
            mr.Grant(QuizIds.Deuterium);
            mr.Grant(QuizIds.DtFuel);
            mr.Grant(QuizIds.Plasma);
            mr.Grant(QuizIds.MagneticPair);
            mr.Grant(QuizIds.Mutation);
            mr.Grant(QuizIds.FoodIrradiation);
            mr.Grant(QuizIds.Fusion);

            Assert.AreEqual(cfg.radAlaraMult, mr.RadiationMult(), 1e-4f, "ALARA rad ×0.8");
            Assert.AreEqual(cfg.medBayHealMastery, mr.MedBayHeal(), 1e-4f, "heal 25→35");
            Assert.AreEqual(cfg.fuelEffBonus * 2f, mr.FuelEfficiencyBonus(), 1e-4f, "deuterium + D–T = +0.16");
            Assert.AreEqual(cfg.coolingMasteryBonus, mr.CoolingBonus(), 1e-4f, "cooling +5");
            Assert.AreEqual(cfg.poloidalMasteryMult, mr.PoloidalDampMult(), 1e-4f, "poloidalDamp ×1.15");
            Assert.AreEqual(cfg.farmYieldMasteryMult, mr.FarmYieldMult(), 1e-4f, "yield +15%");
            Assert.AreEqual(cfg.spoilMasteryMult, mr.SpoilMult(), 1e-4f, "spoil −50%");
            Assert.AreEqual(cfg.boostHeatMasteryMult, mr.BoostHeatMult(), 1e-4f, "boost heat −10%");
        }

        // ─────────────────────────────────────────
        //  Submit flow — correct / wrong / locked
        // ─────────────────────────────────────────
        [Test]
        public void Submit_Correct_GrantsMastery_UnlocksCodex()
        {
            var cq = CodexQuizManager.Instance;
            cq.SetApplied(AllApplied());

            var res = cq.Submit(QuizIds.Deuterium, 1);       // B correct
            Assert.IsTrue(res.valid && res.correct);
            Assert.IsTrue(res.masteryEarned, "ตอบถูกครั้งแรก → ได้ mastery");
            Assert.AreEqual("codex_deuterium", res.codexUnlockedId);
            Assert.IsTrue(MasteryRegistry.Instance.Has(QuizIds.Deuterium));
            Assert.IsTrue(cq.IsCodexUnlocked("codex_deuterium"));
            Assert.IsFalse(cq.IsAnswerable(QuizIds.Deuterium), "ปลดแล้ว → ไม่ต้องตอบซ้ำ");
            StringAssert.Contains("q_deuterium", res.explanation, "แสดง explanation ทุกกรณี");
        }

        [Test]
        public void Submit_Wrong_NoPenalty_ShowsExplanation_StaysAnswerable()
        {
            var cq = CodexQuizManager.Instance;
            cq.SetApplied(AllApplied());

            var res = cq.Submit(QuizIds.Deuterium, 0);       // A wrong
            Assert.IsTrue(res.valid);
            Assert.IsFalse(res.correct);
            Assert.IsFalse(res.masteryEarned, "ตอบผิด → ไม่ปลด (ไม่มีโทษ)");
            Assert.IsFalse(MasteryRegistry.Instance.Has(QuizIds.Deuterium));
            Assert.IsFalse(string.IsNullOrEmpty(res.explanation), "แสดง explanation แม้ตอบผิด");
            Assert.IsTrue(cq.IsAnswerable(QuizIds.Deuterium), "ลองใหม่วันถัดไปได้");
        }

        [Test]
        public void Submit_WhenLocked_IsInvalid()
        {
            var cq = CodexQuizManager.Instance;   // no applied state set → everything locked
            var res = cq.Submit(QuizIds.Deuterium, 1);
            Assert.IsFalse(res.valid, "ยังไม่ใช้ความรู้ (requiresApplied) → ตอบไม่ได้");
            Assert.IsFalse(MasteryRegistry.Instance.Has(QuizIds.Deuterium));
        }

        // ─────────────────────────────────────────
        //  Codex counts x/11
        // ─────────────────────────────────────────
        [Test]
        public void Codex_CountsXof11()
        {
            var cq = CodexQuizManager.Instance;
            Assert.AreEqual(11, cq.TotalCodex, "11 entry");
            Assert.AreEqual(0, cq.UnlockedCodexCount, "เริ่มต้น 0");

            cq.SetApplied(AllApplied());
            cq.Submit(QuizIds.Deuterium, 1);
            cq.Submit(QuizIds.TritiumBreeding, 1);
            cq.Submit(QuizIds.Alara, 0);           // Alara correct = A (index 0)? our test quiz correct=1

            // Alara test-quiz correctIndex is 1 (B) like all others → index 0 is wrong, so it should NOT count
            Assert.AreEqual(2, cq.UnlockedCodexCount, "ปลด 2 (deuterium + tritium) — alara ตอบผิดไม่นับ");
        }

        // ─────────────────────────────────────────
        //  Availability / red dot
        // ─────────────────────────────────────────
        [Test]
        public void Availability_GatedByRequiresApplied()
        {
            var cq = CodexQuizManager.Instance;
            foreach (var id in QuizIds.All)
                Assert.IsFalse(cq.IsAnswerable(id), $"{id}: ยังไม่ใช้ความรู้ → ล็อก");
            Assert.IsFalse(cq.HasNewQuiz, "ไม่มีควิซใหม่ตอนเริ่ม");

            cq.SetApplied(AllApplied());
            foreach (var id in QuizIds.All)
                Assert.IsTrue(cq.IsAnswerable(id), $"{id}: ใช้ครบ → ตอบได้");
            Assert.IsTrue(cq.HasNewQuiz, "🔴 มีควิซใหม่");
        }

        [Test]
        public void Availability_MagneticPairNeedsBothCoils()
        {
            var cq = CodexQuizManager.Instance;
            cq.SetCoilTypes(1);
            Assert.IsTrue(cq.IsAnswerable(QuizIds.Plasma), "1 คอยล์ → q_plasma ตอบได้");
            Assert.IsFalse(cq.IsAnswerable(QuizIds.MagneticPair), "ต้องครบ 2 ชนิด");
            cq.SetCoilTypes(2);
            Assert.IsTrue(cq.IsAnswerable(QuizIds.MagneticPair), "ครบ 2 → ตอบได้");
        }

        [Test]
        public void HasNewQuiz_ClearsAfterAllAnswered()
        {
            var cq = CodexQuizManager.Instance;
            cq.SetApplied(AllApplied());
            foreach (var id in QuizIds.All) cq.Submit(id, 1);   // all B correct
            Assert.IsFalse(cq.HasNewQuiz, "ตอบครบแล้ว → จุดแดงหาย");
            Assert.AreEqual(11, cq.UnlockedCodexCount, "ปลดครบ 11");
        }

        // ─────────────────────────────────────────
        //  Persistence + events
        // ─────────────────────────────────────────
        [Test]
        public void Mastery_SeedsFromMetaBank_AcrossRestart()
        {
            MetaProgress.MasteryBank.Add(QuizIds.TritiumBreeding);  // as if persisted from a prior run
            MasteryRegistry.ResetForTest();                         // "restart"
            Assert.IsTrue(MasteryRegistry.Instance.Has(QuizIds.TritiumBreeding),
                "★ Mastery ถาวรข้ามรอบ — แพ้แล้วเริ่มใหม่ยังเก่งขึ้นจริง");
            Assert.AreEqual(cfg.zoneBTritiumMastery, MasteryRegistry.Instance.ZoneBTritiumPerDay(), 1e-4f);
        }

        [Test]
        public void Grant_IsIdempotent_FiresEventOnce()
        {
            int fired = 0;
            EventManager.Instance.OnMasteryEarned += _ => fired++;
            Assert.IsTrue(MasteryRegistry.Instance.Grant(QuizIds.Deuterium));
            Assert.IsFalse(MasteryRegistry.Instance.Grant(QuizIds.Deuterium), "ครั้งสอง false");
            Assert.AreEqual(1, fired, "ยิง event ครั้งเดียว");
            Assert.AreEqual(1, MasteryRegistry.Instance.EarnedCount);
        }
    }
}
