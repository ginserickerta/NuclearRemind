using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// เฟส 5 — วิกฤตซ้อน (deferredCrisis §4) + ปลุก decree_emergency:
    /// - เลือกทางที่มีคีย์ deferred → StoryDirector จดไว้ แล้วยิง beat OnDeferredCrisis หลังหน่วง 2 วัน
    /// - คีย์รอด save/load (deferredCrisisKeys/Days ใน SaveData)
    /// - "coolingWorkerShortage" = จบวันระหว่างพายุ (Day 25–30) ที่ HEAT ≥ 70 เท่านั้น
    /// รันแบบไม่มี Card UI (การ์ด auto-advance) เหมือน StoryDirectorTests
    /// </summary>
    public class StoryDeferredCrisisTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private StoryDirector director;

        [SetUp]
        public void SetUp()
        {
            StoryDirector.CardUIAvailable = false;
            eventManager = NewComponent<EventManager>("EventManager");
            NewComponent<TimeManager>("TimeManager");
            director = NewComponent<StoryDirector>("StoryDirector");
        }

        [TearDown]
        public void TearDown()
        {
            StoryDirector.CardUIAvailable = false;
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        // ── helpers ────────────────────────────────────────────

        private StoryBeatSO NewDeferredBeat(string id, string key)
        {
            var beat = ScriptableObject.CreateInstance<StoryBeatSO>();
            beat.beatId = id;
            beat.triggerType = StoryTriggerType.OnDeferredCrisis;
            beat.triggerParam = key;
            var record = ScriptableObject.CreateInstance<RecordCardSO>();
            record.recordId = $"r_{id}";
            record.archiveTitle = id;
            beat.record = record; // ใช้ record แทน crisis — เล่นจบได้โดยไม่ต้องมี DilemmaManager
            _spawned.Add(beat);
            _spawned.Add(record);
            return beat;
        }

        private DilemmaData NewDilemmaWithDeferredC(string key)
        {
            var d = ScriptableObject.CreateInstance<DilemmaData>();
            d.dilemmaId = "parent_crisis";
            d.choiceC_DeferredCrisis = key;
            _spawned.Add(d);
            return d;
        }

        // ── DilemmaData.GetDeferredCrisis ─────────────────────

        [Test]
        public void GetDeferredCrisis_ReturnsPerChoiceKey_EmptyOtherwise()
        {
            var d = NewDilemmaWithDeferredC("water");

            Assert.IsTrue(string.IsNullOrEmpty(d.GetDeferredCrisis(0)), "A ไม่มีคีย์");
            Assert.IsTrue(string.IsNullOrEmpty(d.GetDeferredCrisis(1)), "B ไม่มีคีย์");
            Assert.AreEqual("water", d.GetDeferredCrisis(2), "C → water");
            Assert.IsNull(d.GetDeferredCrisis(99), "นอกช่วง → null");
        }

        // ── ยิงหลังหน่วง 2 วัน ─────────────────────────────────

        [Test]
        public void DeferredCrisis_FiresBeat_TwoDaysAfterChoice()
        {
            director.beats = new[] { NewDeferredBeat("deferred_water_crisis", "water") };
            var parent = NewDilemmaWithDeferredC("water");

            eventManager.RaiseDayStarted(10, true);              // _currentDay = 10
            eventManager.RaiseDilemmaResolved(parent, 2);        // เลือก C → จดคีย์ ยิงวัน 12

            CollectionAssert.AreEqual(new[] { "water" }, director.DeferredCrisisKeys);
            CollectionAssert.AreEqual(new[] { 12 }, director.DeferredCrisisFireDays);

            eventManager.RaiseDayStarted(11, true);
            Assert.IsEmpty(director.FiredBeatIds, "ยังไม่ครบ 2 วัน → ไม่ยิง");

            eventManager.RaiseDayStarted(12, true);
            CollectionAssert.AreEqual(new[] { "deferred_water_crisis" }, director.FiredBeatIds,
                "ครบกำหนด → beat วิกฤตซ้อนยิง");
            Assert.IsEmpty(director.DeferredCrisisKeys, "คีย์ใช้ครั้งเดียวแล้วทิ้ง");

            eventManager.RaiseDayStarted(13, true);
            Assert.AreEqual(1, director.FiredBeatIds.Count, "ไม่ยิงซ้ำ");
        }

        [Test]
        public void ChoiceWithoutDeferredKey_DoesNotRegister()
        {
            director.beats = new[] { NewDeferredBeat("deferred_water_crisis", "water") };
            var parent = NewDilemmaWithDeferredC("water");

            eventManager.RaiseDayStarted(10, true);
            eventManager.RaiseDilemmaResolved(parent, 0); // เลือก A — ไม่มีคีย์

            Assert.IsEmpty(director.DeferredCrisisKeys);
            eventManager.RaiseDayStarted(12, true);
            Assert.IsEmpty(director.FiredBeatIds, "ไม่ได้เลือกทางที่มีคีย์ → วิกฤตซ้อนไม่มา");
        }

        // ── save / load ───────────────────────────────────────

        [Test]
        public void DeferredCrisis_RestoredFromSave_FiresOnDueDay()
        {
            director.beats = new[] { NewDeferredBeat("deferred_food_crisis", "food") };

            var save = new SaveData
            {
                deferredCrisisKeys = new List<string> { "food" },
                deferredCrisisDays = new List<int> { 22 },
            };
            eventManager.RaiseSaveLoaded(save);

            eventManager.RaiseDayStarted(21, true);
            Assert.IsEmpty(director.FiredBeatIds);

            eventManager.RaiseDayStarted(22, true);
            CollectionAssert.AreEqual(new[] { "deferred_food_crisis" }, director.FiredBeatIds,
                "คีย์จากเซฟต้องยิงเมื่อถึงวันกำหนด");
        }

        [Test]
        public void OldSave_WithoutDeferredFields_IsSafe()
        {
            director.beats = new[] { NewDeferredBeat("deferred_food_crisis", "food") };

            var oldSave = new SaveData { deferredCrisisKeys = null, deferredCrisisDays = null };
            Assert.DoesNotThrow(() => eventManager.RaiseSaveLoaded(oldSave));
            Assert.IsEmpty(director.DeferredCrisisKeys);
        }

        // ── coolingWorkerShortage (decree_emergency) ──────────

        [Test]
        public void CoolingShortage_FiresOnlyDuringStorm_WhenHeatHigh()
        {
            var beat = NewDeferredBeat("decree_emergency", "coolingWorkerShortage");
            beat.triggerType = StoryTriggerType.OnStormActive;
            director.beats = new[] { beat };

            int stormDay = CoreTowerManager.StormStartDay; // 25

            // ก่อนพายุ — ต่อให้ร้อนก็ไม่ยิง
            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 75f });
            eventManager.RaiseDayEnded(stormDay - 1);
            Assert.IsEmpty(director.FiredBeatIds, "ก่อนพายุ → decree ไม่เด้ง");

            // ระหว่างพายุแต่หล่อเย็นเอาอยู่ (HEAT ต่ำ) — ไม่ยิง
            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 40f });
            eventManager.RaiseDayEnded(stormDay + 1);
            Assert.IsEmpty(director.FiredBeatIds, "HEAT < 70 = แรงงานหล่อเย็นยังพอ → ไม่เด้ง");

            // ระหว่างพายุ + HEAT ≥ 70 → ยิง
            eventManager.RaiseTowerProgressChanged(new TowerData { coreHeat = 75f });
            eventManager.RaiseDayEnded(stormDay + 2);
            CollectionAssert.AreEqual(new[] { "decree_emergency" }, director.FiredBeatIds,
                "พายุ + หล่อเย็นไม่ทัน (HEAT ≥ 70) → ประกาศฉุกเฉินเด้ง");
        }

        // ── infrastructure ────────────────────────────────────

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
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
