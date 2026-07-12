using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// integration ของ hook ฝั่งเกม (Story Guide §4) — ยืนยันว่าผลกระทบวิกฤต "เกิดจริง" กับ manager ปลายทาง:
    ///   • CoreTowerManager.ReduceHeat/ReduceCore (พลาสมา — HEAT ปลอดภัยจริง, ปิดบั๊กแก้แล้วหลอม)
    ///   • ResourceManager.ApplyDailySpoilage (อาหารเน่าตามอัตราของ CrisisEffectManager)
    /// </summary>
    public class CrisisEffectIntegrationTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private EventManager eventManager;

        [SetUp]
        public void SetUp() => eventManager = NewComponent<EventManager>("EventManager");

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned) Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        // ── CoreTowerManager: ลด HEAT/CORE ตอนแก้วิกฤตพลาสมา ──
        [Test]
        public void CoreTower_ReduceHeatAndCore_ClampAndNoPhaseReversal()
        {
            var core = NewComponent<CoreTowerManager>("CoreTowerManager");
            // ปลดล็อกเตา + HEAT สูง (โซนอันตราย) ผ่านการโหลดเซฟ
            eventManager.RaiseSaveLoaded(new SaveData
            {
                tower = new TowerData { isUnlocked = true, coreHeat = 90f, corePercent = 60f, currentPhase = 2, heatCap = 100f }
            });

            core.ReduceHeat(60f);
            Assert.AreEqual(30f, core.Current.coreHeat, 1e-3f, "HEAT 90 − 60 = 30 (ปลอดภัย)");

            core.ReduceHeat(999f);
            Assert.AreEqual(0f, core.Current.coreHeat, 1e-3f, "clamp ไม่ต่ำกว่า 0");

            core.ReduceCore(20f);
            Assert.AreEqual(40f, core.Current.corePercent, 1e-3f, "CORE% 60 − 20 = 40 (guide q:-0.2)");
            Assert.AreEqual(2, core.Current.currentPhase, "ไม่ถอยเฟส (invariant เดียวกับ Scram)");
        }

        // ── ResourceManager: อาหารเน่าตามอัตราจาก CrisisEffectManager ──
        [Test]
        public void ResourceManager_Spoilage_ReducesStoredFood()
        {
            var rm = NewComponent<ResourceManager>("ResourceManager");
            var crisis = NewComponent<CrisisEffectManager>("CrisisEffectManager");

            // inject food = 150 ผ่าน load (ไม่พึ่ง startFood default — GDD v4.1 ปรับเป็น 120 แล้ว เทสต์ควรทนต่อการจูน)
            eventManager.RaiseSaveLoaded(new SaveData { resources = new ResourceData { food = 150f } });

            // วิกฤตอาหารเหนี่ยวนำการเน่า 20%/วัน (ทางเลือกที่ไม่ได้หยุดเน่า)
            var d = ScriptableObject.CreateInstance<DilemmaData>();
            d.dilemmaId = "Crisis_FoodShortage";
            d.inducedSpoilRatePerDay = 0.2f;
            _spawned.Add(d);
            eventManager.RaiseDilemmaResolved(d, 0);
            Assert.AreEqual(0.2f, crisis.FoodSpoilRatePerDay, 1e-4f);

            rm.ApplyDailySpoilage();
            Assert.AreEqual(120f, rm.Current.food, 1e-2f, "150 × (1 − 0.2) = 120");

            // ทางเลือก B ฉายรังสี → หยุดเน่า → ApplyDailySpoilage ไม่ทำอะไร
            d.choiceB_Effects = new CrisisChoiceEffects { stopSpoilage = true };
            eventManager.RaiseDilemmaResolved(d, 1);
            rm.ApplyDailySpoilage();
            Assert.AreEqual(120f, rm.Current.food, 1e-2f, "หยุดเน่าแล้ว — อาหารคงเดิม");
        }

        // ── harness เหมือน DecreeManagerTests (AddComponent + Awake/OnEnable ผ่าน reflection) ──
        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var c = go.AddComponent<T>();
            Invoke(c, "Awake"); Invoke(c, "OnEnable");
            return c;
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try { m?.Invoke(target, null); } catch (TargetInvocationException) { }
        }
    }
}
