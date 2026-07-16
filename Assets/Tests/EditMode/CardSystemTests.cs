using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// GDD v6.3 §25 — Crisis Cards. The Sprint 4 acceptance (§32): a "good" option is LOCKED 🔒 and
    /// visible until its note is researched — you can see the better path but can't take it. Plus:
    /// triggers bound to STATE not day (rule #1), cooldown / once-only, one card at a time, and
    /// option effects reaching the live Hope ledger. In-memory catalog (no Resources dependency).
    /// </summary>
    public class CardSystemTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private GameConfigSO cfg;
        private EventManager events;
        private WorkerManager wm;
        private CardManager cm;

        [SetUp]
        public void SetUp()
        {
            cfg = ScriptableObject.CreateInstance<GameConfigSO>();
            _spawned.Add(cfg);
            GameConfigSO.OverrideForTest(cfg);
            KnowledgeDB.ResetForTest();

            var evGo = new GameObject("EventManager");
            _spawned.Add(evGo);
            events = evGo.AddComponent<EventManager>();

            var wmGo = new GameObject("WorkerManager");
            _spawned.Add(wmGo);
            wm = wmGo.AddComponent<WorkerManager>();
            wm.Initialize(cfg);

            var cmGo = new GameObject("CardManager");
            _spawned.Add(cmGo);
            cm = cmGo.AddComponent<CardManager>();
            cm.Initialize(cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) Object.DestroyImmediate(o);
            GameConfigSO.OverrideForTest(null);
            KnowledgeDB.ResetForTest();
        }

        // ── in-memory card builders ────────────────────────────────
        private CrisisCardSO MakeCard(string id, int cooldown, bool once, params CardOption[] opts)
        {
            var c = ScriptableObject.CreateInstance<CrisisCardSO>();
            c.cardId = id; c.title = id; c.description = "d"; c.dialogueLines = new string[0];
            c.cooldownDays = cooldown; c.onceOnly = once; c.options = opts;
            _spawned.Add(c);
            return c;
        }

        private static CardOption Opt(string label, string reqNote, float hope) => new CardOption
        {
            label = label, effectSummary = "", afterText = "",
            requiredNoteId = reqNote ?? "", effect = new CardEffect { hopeDelta = hope },
        };

        private CardWorldState Hungry() => new CardWorldState { hungryWorkers = cfg.cardHungryCount };

        // ─────────────────────────────────────────
        //  ★ Acceptance — locked option until researched
        // ─────────────────────────────────────────
        [Test]
        public void Option_LockedUntilNoteResearched_Bug6Rule()
        {
            var card = MakeCard(CardIds.Hunger, 4, false,
                Opt("A ดึงคนกลับฟาร์ม", "", -6f),
                Opt("B ปันส่วนครึ่ง", "", -6f),
                Opt("C เปิดคลังสำรอง", "food_logistics", 0f)); // ★ locked
            cm.RegisterCatalog(new[] { card });

            cm.EvaluateDay(2, Hungry());
            Assert.IsTrue(cm.HasPending, "การ์ดโผล่");
            Assert.IsTrue(cm.CanChoose(0), "A เลือกได้");
            Assert.IsFalse(cm.CanChoose(2), "ยังไม่วิจัย food_logistics → C ล็อก");

            var bad = cm.ResolveOption(2);
            Assert.IsFalse(bad.valid, "เลือกตัวเลือกที่ล็อกไม่ได้");
            Assert.IsTrue(cm.HasPending, "ยังค้างอยู่ (ยังไม่ได้เลือกจริง)");

            KnowledgeDB.Instance.CompleteNote("food_logistics");
            Assert.IsTrue(cm.CanChoose(2), "★ วิจัยเสร็จ → ปลดล็อก C");
            var ok = cm.ResolveOption(2);
            Assert.IsTrue(ok.valid, "เลือก C ได้แล้ว");
            Assert.IsFalse(cm.HasPending, "resolve → เคลียร์การ์ด");
        }

        [Test]
        public void LockedRow_IsVisibleWithLockAndNote_NeverHidden()
        {
            var opt = Opt("ฉีดสารหล่อเย็นฉุกเฉิน", "confinement", 0f);
            string row = CrisisCardPanel.OptionRow(2, opt, KnowledgeDB.Instance);
            StringAssert.Contains("🔒", row, "ต้องโชว์แม่กุญแจ");
            StringAssert.Contains("confinement", row, "ต้องบอกว่าต้องวิจัยอะไร (ห้ามซ่อน)");
            Assert.IsTrue(CrisisCardPanel.IsRowLocked(opt, KnowledgeDB.Instance));

            KnowledgeDB.Instance.CompleteNote("confinement");
            Assert.IsFalse(CrisisCardPanel.IsRowLocked(opt, KnowledgeDB.Instance), "วิจัยแล้ว → ไม่ล็อก");
        }

        // ─────────────────────────────────────────
        //  Trigger — bound to STATE, never the day
        // ─────────────────────────────────────────
        [Test]
        public void Trigger_IsStateBound_NotDay()
        {
            Assert.IsTrue(CardTriggers.IsTriggered(CardIds.Hunger, new CardWorldState { hungryWorkers = 3 }));
            Assert.IsFalse(CardTriggers.IsTriggered(CardIds.Hunger, new CardWorldState { hungryWorkers = 2 }));

            Assert.IsTrue(CardTriggers.IsTriggered(CardIds.Heat, new CardWorldState { heat = 63f }));
            Assert.IsFalse(CardTriggers.IsTriggered(CardIds.Heat, new CardWorldState { heat = 62f }), "heat > 62 (strict)");

            Assert.IsTrue(CardTriggers.IsTriggered(CardIds.Spoil, new CardWorldState { food = 31f, avgRadiation = 7f }));
            Assert.IsFalse(CardTriggers.IsTriggered(CardIds.Spoil, new CardWorldState { food = 31f, avgRadiation = 5f }), "ต้องมีทั้ง food และ rad");

            Assert.IsTrue(CardTriggers.IsTriggered(CardIds.Triage, new CardWorldState { sickWorkers = 6, medBayCapacity = 4 }));
            Assert.IsFalse(CardTriggers.IsTriggered(CardIds.Triage, new CardWorldState { sickWorkers = 6, medBayCapacity = 6 }), "ต้อง sick > เตียง");
        }

        // ─────────────────────────────────────────
        //  Cooldown / once / one-at-a-time
        // ─────────────────────────────────────────
        [Test]
        public void Cooldown_BlocksRefireUntilElapsed()
        {
            cm.RegisterCatalog(new[] { MakeCard(CardIds.Hunger, 4, false, Opt("A", "", 0f)) });

            Assert.IsNotNull(cm.EvaluateDay(2, Hungry()), "วันที่ 2 → โผล่");
            cm.ResolveOption(0);

            Assert.IsNull(cm.EvaluateDay(3, Hungry()), "ยังไม่พ้น cooldown 4 (3−2=1)");
            Assert.IsNull(cm.EvaluateDay(5, Hungry()), "5−2=3 < 4 ยังไม่ได้");
            Assert.IsNotNull(cm.EvaluateDay(6, Hungry()), "6−2=4 → เกิดซ้ำได้");
        }

        [Test]
        public void OnceOnly_FiresAtMostOncePerGame()
        {
            cm.RegisterCatalog(new[] { MakeCard(CardIds.Decree, 99, true, Opt("A", "", 0f)) });
            var storm = new CardWorldState { stormActive = true, coolingWorkers = 0 };

            Assert.IsNotNull(cm.EvaluateDay(2, storm), "ครั้งแรกโผล่");
            cm.ResolveOption(0);
            Assert.IsNull(cm.EvaluateDay(200, storm), "★ onceOnly → ไม่เกิดอีกแม้พ้น cooldown");
        }

        [Test]
        public void OnlyOneCardPresentedAtATime()
        {
            cm.RegisterCatalog(new[] { MakeCard(CardIds.Hunger, 4, false, Opt("A", "", 0f)) });
            var first = cm.EvaluateDay(2, Hungry());
            var again = cm.EvaluateDay(2, Hungry());
            Assert.AreSame(first, again, "มีการ์ดค้างอยู่ → ไม่ยัดใบใหม่");
        }

        [Test]
        public void Priority_LowerNumberedCardWins_WhenBothTrigger()
        {
            cm.RegisterCatalog(new[]
            {
                MakeCard(CardIds.Heat, 3, false, Opt("A", "", 0f)),
                MakeCard(CardIds.Hunger, 4, false, Opt("A", "", 0f)),
            });
            var both = new CardWorldState { heat = 70f, hungryWorkers = 3 };
            var shown = cm.EvaluateDay(2, both);
            Assert.AreEqual(CardIds.Heat, shown.cardId, "การ์ด 1 (heat) มาก่อนการ์ด 4 (hunger)");
        }

        // ─────────────────────────────────────────
        //  Effect reaches the live Hope ledger
        // ─────────────────────────────────────────
        [Test]
        public void ResolvingOption_AppliesHopeToLedger()
        {
            cm.RegisterCatalog(new[] { MakeCard(CardIds.Hunger, 4, false, Opt("B ปันส่วน", "", -6f)) });
            cm.EvaluateDay(2, Hungry());
            var res = cm.ResolveOption(0);
            Assert.IsTrue(res.valid);

            var entry = wm.Hope.GetTodayBreakdown().FirstOrDefault(e => e.sourceKey == "card.hunger");
            Assert.AreEqual(-6f, entry.value, 1e-3f, "ตัวเลือกส่ง Hope −6 เข้า HopeLedger จริง");
        }

        [Test]
        public void NoPendingCard_ResolveIsNoOp()
        {
            var res = cm.ResolveOption(0);
            Assert.IsFalse(res.valid, "ไม่มีการ์ดค้าง → resolve ไม่ทำอะไร");
        }
    }
}
