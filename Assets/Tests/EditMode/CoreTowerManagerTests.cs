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
            float energy, float water, float heatCap = 100f, float coreHeat = 0f, int scramCooldown = 0,
            float deuterium = 0f, float tritium = 0f, float knowledge = 0f)
        {
            var save = new SaveData
            {
                resources = new ResourceData
                {
                    energy = energy, water = water, food = 0,
                    deuterium = deuterium, tritium = tritium, knowledge = knowledge,
                },
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

            // Phase 3 ใช้ Tritium — ต้องมีเชื้อเพลิงถึงจะดัน CORE% ได้
            Inject(corePercent: 98f, phase: 3, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 1000f, water: 0f, tritium: 100f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(30); // 98 + 3 → clamp 100

            Assert.IsTrue(won, "CORE% ถึง 100 → OnTowerComplete");
            Assert.AreEqual(100f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Meltdown_HeatExceedsCap_RaisesGameOver()
        {
            GameEndType end = GameEndType.TrueEnding;
            bool over = false;
            eventManager.OnGameOver += e => { over = true; end = e; };

            // Phase 2 Boost (heat +20), น้ำ 0 → cooling 15 → dHeat +5 → coreHeat 95→100 = heatCap
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 1000f, water: 0f, heatCap: 100f, coreHeat: 95f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.IsTrue(over, "coreHeat ทะลุ heatCap → GameOver");
            Assert.AreEqual(GameEndType.Meltdown, end);
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

        // ───────────────────────── เฟส 2: เชื้อเพลิงจริง (§8) ─────────────────────────

        [Test]
        public void Phase2_WithDeuterium_AdvancesFull()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 0f, water: 0f, deuterium: 100f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            // Deuterium พอ (100 ≥ need 10) → fuelEff 1 → dCore = 3×1×1 = 3
            Assert.AreEqual(63f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Phase2_LowDeuterium_ScalesDownCoreGain()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 0f, water: 0f, deuterium: 5f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            // Deuterium 5 / need 10 → fuelEff 0.5 → dCore = 3×1×0.5 = 1.5
            Assert.AreEqual(61.5f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void Phase3_UsesTritium_NotDeuterium()
        {
            // Phase 3 มี Deuterium เต็มแต่ไม่มี Tritium → ต้องไม่ดัน CORE% (พิสูจน์ว่าใช้ Tritium)
            Inject(corePercent: 85f, phase: 3, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 0f, water: 0f, deuterium: 100f, tritium: 0f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(85f, tower.Current.corePercent, 1e-3f, "Phase 3 ใช้ Tritium เท่านั้น — Deuterium ไม่นับ");
        }

        [Test]
        public void KnowBonus_Expert_BoostsCoreGain()
        {
            // Knowledge 80 → KnowBonus +0.10 → fuelEff = 1 + 0.10 = 1.10 → dCore = 3×1×1.10 = 3.3
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 0f, water: 0f, deuterium: 100f, knowledge: 80f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(63.3f, tower.Current.corePercent, 1e-3f);
        }

        // ───────────────────────── เฟส 2: HEAT / พายุ / micro-damage ─────────────────────────

        [Test]
        public void Storm_AddsHeat_OnlyFromDay25()
        {
            // Phase 3 Boost, น้ำ 0 → cooling 15 · heat Boost +20
            Inject(corePercent: 85f, phase: 3, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 0f, coreHeat: 0f, tritium: 100f);
            PlaceCore(1, 1);
            eventManager.RaiseDayEnded(24); // ก่อนพายุ: dHeat = 20 − 15 = +5
            Assert.AreEqual(5f, tower.Current.coreHeat, 1e-3f, "Day 24 ยังไม่มีพายุ");

            Inject(corePercent: 85f, phase: 3, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 0f, coreHeat: 0f, tritium: 100f);
            PlaceCore(1, 1);
            eventManager.RaiseDayEnded(25); // พายุ +12: dHeat = 20 + 12 − 15 = +17
            Assert.AreEqual(17f, tower.Current.coreHeat, 1e-3f, "Day 25 พายุ +12");
        }

        [Test]
        public void MicroDamage_WarnZone_NoPoloidal_ReducesCore()
        {
            // Phase 2 Boost, coreHeat 85 → +5 = 90 (โซนเตือน 80–99), ไม่มี Poloidal → CORE% −2
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 0f, coreHeat: 85f, deuterium: 100f);
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(90f, tower.Current.coreHeat, 1e-3f);
            // dCore = 3×2×1 = 6 → 66 · micro-damage −2 → 64
            Assert.AreEqual(64f, tower.Current.corePercent, 1e-3f);
        }

        [Test]
        public void MicroDamage_WithPoloidal_NoReduction()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 0f, coreHeat: 85f, deuterium: 100f);
            tower.hasPoloidalCoils = true; // Poloidal กัน micro-damage
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(90f, tower.Current.coreHeat, 1e-3f);
            Assert.AreEqual(66f, tower.Current.corePercent, 1e-3f, "มี Poloidal → ไม่เสีย CORE%");
        }

        [Test]
        public void CoolingTowerLevel_ReducesHeat()
        {
            // Toroidal L3 → cooling = 15 + 0 + 0 + 30 = 45 · Boost heat 20 → dHeat = −25
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 0f, coreHeat: 50f, deuterium: 100f);
            tower.coolingTowerLevel = 3;
            PlaceCore(1, 1);

            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(25f, tower.Current.coreHeat, 1e-3f);
        }

        // ───────────────────────── เฟส 2: ForceIdle (วิกฤต 2·B) + Q ─────────────────────────

        [Test]
        public void ForceIdle_ForcesIdleMode_NoCoreGain_NoFuelUse()
        {
            Inject(corePercent: 60f, phase: 2, mode: CoreTowerManager.ModeBoost,
                unlocked: true, energy: 0f, water: 1000f, deuterium: 100f);
            PlaceCore(1, 1);

            tower.ForceIdle(1);
            eventManager.RaiseDayEnded(20);

            Assert.AreEqual(60f, tower.Current.corePercent, 1e-3f, "Idle → CORE% ไม่ขึ้น");
            Assert.AreEqual(100f, resources.Current.deuterium, 1e-3f, "Idle → ไม่กินเชื้อเพลิง");
        }

        [Test]
        public void Q_ReflectsCorePercentOver100()
        {
            Inject(corePercent: 82f, phase: 3, mode: CoreTowerManager.ModeNormal,
                unlocked: true, energy: 0f, water: 0f);

            Assert.AreEqual(0.82f, tower.Q, 1e-4f);
        }

        // ───────────────────────── เฟส 6: Coils (§6) ─────────────────────────

        [Test]
        public void UpgradeToroidal_IncrementsCoolingTowerLevel()
        {
            Assert.AreEqual(0, tower.coolingTowerLevel);
            tower.UpgradeToroidal(); // iron default 100 ≥ cost 50
            Assert.AreEqual(1, tower.coolingTowerLevel);
        }

        [Test]
        public void UpgradeToroidal_CapsAt3()
        {
            eventManager.RaiseResourceDelta(ResourceType.Iron, 500f);
            tower.UpgradeToroidal();
            tower.UpgradeToroidal();
            tower.UpgradeToroidal();
            tower.UpgradeToroidal(); // เกิน cap → ไม่ทำ
            Assert.AreEqual(3, tower.coolingTowerLevel, "Toroidal cap 3 ระดับ");
        }

        [Test]
        public void InstallPoloidal_SetsFlag()
        {
            Assert.IsFalse(tower.hasPoloidalCoils);
            tower.InstallPoloidal(); // iron ≥ cost 60
            Assert.IsTrue(tower.hasPoloidalCoils);
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
