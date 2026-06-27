using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// B1 — CORE TOWER v2.1: CORE% + HEAT, 3 เฟส, overclock, meltdown/win
    /// ก้าวหน้าต่อวัน (OnDayEnded), ปลดล็อก Day 11
    /// </summary>
    public class CoreTowerManagerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private ResourceManager resources;
        private CoreTowerManager tower;
        private BuildingData coreData;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            resources = NewComponent<ResourceManager>("ResourceManager");
            tower = NewComponent<CoreTowerManager>("CoreTowerManager");

            coreData = ScriptableObject.CreateInstance<BuildingData>();
            coreData.buildingName = "CoreTower";
            coreData.size = new Vector2Int(1, 1);
            coreData.buildingType = BuildingType.CoreTower;
            coreData.isCoreTowerPart = true;
            coreData.towerPhaseRequired = 0;
            coreData.energyCost = 0;
            _spawned.Add(coreData);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        /// <summary>ฉีดสถานะ tower + resource ผ่าน path เดียวกับ load เกม</summary>
        private void Inject(float corePercent, int phase, int mode, bool unlocked,
            float energy, float water, float heatCap = 100f, float coreHeat = 0f, int scramCooldown = 0)
        {
            var save = new SaveData
            {
                resources = new ResourceData { energy = energy, water = water, food = 0, workers = 10 },
                tower = new TowerData
                {
                    corePercent = corePercent,
                    coreHeat = coreHeat,
                    currentPhase = phase,
                    overclockMode = mode,
                    heatCap = heatCap,
                    isUnlocked = unlocked,
                    scramCooldown = scramCooldown,
                },
            };
            eventManager.RaiseSaveLoaded(save); // ResourceManager + CoreTowerManager + Registry(clear)
        }

        private void PlaceCore(int col, int row) =>
            eventManager.RaiseBuildingPlaced(new Cell(col, row), coreData);

        // ───────────────────────── Unlock ─────────────────────────

        [Test]
        public void Unlock_OnDay11_SetsCore30Phase1()
        {
            eventManager.RaiseDayStarted(11, true);

            Assert.IsTrue(tower.Current.isUnlocked);
            Assert.AreEqual(30f, tower.Current.corePercent, 1e-4f);
            Assert.AreEqual(1, tower.Current.currentPhase);
        }

        [Test]
        public void NoUnlock_BeforeDay11()
        {
            eventManager.RaiseDayStarted(10, true);
            Assert.IsFalse(tower.Current.isUnlocked);
        }

        // ───────────────────────── Turn progression ─────────────────────────

        [Test]
        public void Phase1_DayEnd_AdvancesCoreByBaseGain()
        {
            Inject(corePercent: 30f, phase: 1, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(11);

            // Phase 1 ล็อก Normal (mult 1), fuelEff 1 → dCore = 3
            Assert.AreEqual(33f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Phase1_OverclockLocked_ModeUnchanged()
        {
            Inject(corePercent: 30f, phase: 1, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f);

            tower.SetOverclockMode(CoreTowerManager.ModeOverdrive);

            Assert.AreEqual(CoreTowerManager.ModeNormal, tower.Current.overclockMode,
                "Phase 1 ต้องล็อกโหมด — เปลี่ยนไม่ได้");
        }

        [Test]
        public void NoCoreTowerPart_NoProgress()
        {
            Inject(corePercent: 30f, phase: 1, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f);
            // ไม่วาง core part

            eventManager.RaiseDayEnded(11);

            Assert.AreEqual(30f, tower.Current.corePercent, 1e-4f, "ไม่มี CORE TOWER → ไม่เดินเครื่อง");
        }

        [Test]
        public void PhaseTransition_Cross50_RaisesPhaseComplete()
        {
            int completed = -1;
            eventManager.OnTowerPhaseComplete += p => completed = p;

            Inject(corePercent: 49f, phase: 1, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(17); // 49 + 3 = 52 → ข้าม 50%

            Assert.AreEqual(2, tower.Current.currentPhase, "ต้องเข้า Phase 2 Plasma");
            Assert.AreEqual(1, completed, "ต้อง raise OnTowerPhaseComplete(1)");
        }

        // ───────────────────────── Win / Meltdown ─────────────────────────

        [Test]
        public void Win_CoreReaches100_RaisesTowerComplete()
        {
            bool won = false;
            eventManager.OnTowerComplete += () => won = true;

            Inject(corePercent: 98f, phase: 3, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(30); // 98 + 3 → clamp 100

            Assert.IsTrue(won, "CORE% ถึง 100 → OnTowerComplete");
            Assert.AreEqual(100f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Meltdown_HeatExceedsCap_RaisesGameOver()
        {
            GameEndType end = GameEndType.Win;
            bool over = false;
            eventManager.OnGameOver += e => { over = true; end = e; };

            // Phase 2 Boost (heat +20), น้ำ 0 → cooling 15 → dHeat +5 → coreHeat 95→100 = heatCap
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 1000f, water: 0f, heatCap: 100f, coreHeat: 95f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.IsTrue(over, "coreHeat ทะลุ heatCap → GameOver");
            Assert.AreEqual(GameEndType.TowerDestroyed, end);
        }

        // ───────────────────────── SCRAM (B3) ─────────────────────────

        [Test]
        public void Scram_HeatHigh_ReducesHeatAndCore_CostsWater()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 1000f, water: 100f, heatCap: 100f, coreHeat: 95f);

            tower.Scram();

            Assert.AreEqual(55f, tower.Current.coreHeat, 1e-3f, "HEAT −40");
            Assert.AreEqual(50f, tower.Current.corePercent, 1e-3f, "CORE% −10");
            Assert.AreEqual(2, tower.Current.scramCooldown, "ตั้ง cooldown");
            Assert.AreEqual(50f, resources.Current.water, 1e-3f, "เสีย Water 50");
        }

        [Test]
        public void Scram_HeatLow_NoEffect()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 100f, coreHeat: 50f);

            tower.Scram();

            Assert.AreEqual(50f, tower.Current.coreHeat, 1e-3f, "HEAT < 90 → กดไม่ได้");
            Assert.AreEqual(60f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Scram_OnCooldown_Blocked()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 100f, coreHeat: 95f, scramCooldown: 1);

            tower.Scram();

            Assert.AreEqual(95f, tower.Current.coreHeat, 1e-3f, "cooldown ยังไม่หมด → กดไม่ได้");
        }

        [Test]
        public void ScramCooldown_DecrementsOnTurn()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 1000f, scramCooldown: 2);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(1, tower.Current.scramCooldown, "cooldown ลดลง 1 ต่อเทิร์น");
        }

        // ---- reflection helpers (เหมือน IntegrationFlowTests) ----

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
