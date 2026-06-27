using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// Day 9 — Integration milestone: ทดสอบ flow รวมทั้งหมดผ่าน EventManager
    /// placement -> resource -> population -> tower -> dilemma -> HUD/tooltip -> save/load
    /// </summary>
    public class IntegrationFlowTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private GridManager grid;
        private BuildingRegistry registry;
        private ResourceManager resources;
        private PopulationManager population;
        private CoreTowerManager tower;
        private SaveManager saveManager;
        private CodexManager codexManager;
        private BuildingVisualSpawner visualSpawner;
        private DilemmaManager dilemmaManager;
        private GameManager gameManager;
        private UIManagerHUD hud;
        private TooltipController tooltip;

        private Transform visualParent;
        private BuildingData habitatData;
        private BuildingData coreTowerData;
        private DilemmaData dilemmaData;

        private static readonly string SaveFilePath =
            Path.Combine(Application.persistentDataPath, "savegame.json");

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            grid = NewComponent<GridManager>("GridManager");
            grid.columns = 20;
            grid.rows = 12;
            grid.tileWidth = 1f;
            grid.tileHeight = 0.5f;
            grid.originOffset = Vector3.zero;
            grid.InitializeGrid();

            habitatData = ScriptableObject.CreateInstance<BuildingData>();
            habitatData.buildingName = "Habitat";
            habitatData.size = new Vector2Int(1, 1);
            habitatData.energyCost = 10;
            habitatData.workerRequired = 2;
            habitatData.buildingType = BuildingType.Habitat;
            _spawned.Add(habitatData);

            coreTowerData = ScriptableObject.CreateInstance<BuildingData>();
            coreTowerData.buildingName = "CoreTower";
            coreTowerData.size = new Vector2Int(1, 1);
            coreTowerData.buildingType = BuildingType.CoreTower;
            coreTowerData.isCoreTowerPart = true;
            coreTowerData.towerPhaseRequired = 0;
            _spawned.Add(coreTowerData);

            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new[] { habitatData, coreTowerData };

            resources = NewComponent<ResourceManager>("ResourceManager");
            population = NewComponent<PopulationManager>("PopulationManager");

            tower = NewComponent<CoreTowerManager>("CoreTowerManager");

            saveManager = NewComponent<SaveManager>("SaveManager");

            // SaveManager.Save() อ่าน CodexManager.Instance.UnlockedIds — ต้องมี instance ในซีนทดสอบ
            // สร้างแบบ manual (ไม่ผ่าน NewComponent) เพราะ Awake() วน allCodexEntries ต้องตั้งค่า field ก่อน Awake
            var codexGo = new GameObject("CodexManager");
            _spawned.Add(codexGo);
            codexManager = codexGo.AddComponent<CodexManager>();
            codexManager.allCodexEntries = new CodexEntry[0];
            TryInvokePrivate(codexManager, "Awake");
            TryInvokePrivate(codexManager, "OnEnable");

            var parentGo = new GameObject("BuildingsParent");
            _spawned.Add(parentGo);
            visualParent = parentGo.transform;

            visualSpawner = NewComponent<BuildingVisualSpawner>("BuildingVisualSpawner");
            visualSpawner.buildingsParent = visualParent;

            dilemmaData = ScriptableObject.CreateInstance<DilemmaData>();
            dilemmaData.dilemmaId = "phase1_test";
            dilemmaData.triggerCondition = "phase_1_complete";
            dilemmaData.choiceA_FoodChange = 20f;
            dilemmaData.choiceA_TrustChange = 5f;
            dilemmaData.choiceA_AethonRelationChange = 1;
            dilemmaData.choiceA_KeranRelationChange = -1;
            _spawned.Add(dilemmaData);

            dilemmaManager = NewComponent<DilemmaManager>("DilemmaManager");
            dilemmaManager.dilemmaPool = new[] { dilemmaData };

            gameManager = NewComponent<GameManager>("GameManager");
            hud = NewComponent<UIManagerHUD>("UIManagerHUD");
            tooltip = NewComponent<TooltipController>("TooltipController");

            // จำลอง Start() ของ manager ที่ต้องอ่าน config จาก Instance อื่นตอนเริ่มเกม
            // hud.Start() ต้องรันก่อน เพราะ resources/tower.Start() raise event ที่ hud subscribe ไว้แล้ว
            // (ถ้า hud._towerPhaseTargets/_maxFood ยังไม่ถูกตั้งค่า handler จะ NRE)
            InvokePrivate(hud, "Start");
            InvokePrivate(resources, "Start");
            InvokePrivate(population, "Start");
            InvokePrivate(tower, "Start");
            InvokePrivate(tooltip, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);

            if (File.Exists(SaveFilePath))
                File.Delete(SaveFilePath);
        }

        [Test]
        public void BuildingPlaced_PropagatesToResourcesRegistryGridAndVisual()
        {
            var tooltipPanel = new GameObject("TooltipPanel");
            _spawned.Add(tooltipPanel);
            tooltip.tooltipPanel = tooltipPanel;

            eventManager.RaiseBuildingSelected(habitatData);
            Assert.IsTrue(tooltipPanel.activeSelf, "เลือกอาคารแล้ว tooltip panel ต้องเปิด");

            float energyBefore = resources.Current.energy;
            int workersBefore = resources.Current.workers;

            Cell cell = grid.GetCell(2, 3);
            cell.isOccupied = true;
            cell.buildingType = habitatData.buildingType;
            eventManager.RaiseBuildingPlaced(cell, habitatData);

            Assert.AreEqual(energyBefore - habitatData.energyCost, resources.Current.energy, 1e-4f,
                "ResourceManager ต้องหัก energyCost ตอนวางอาคาร");
            Assert.AreEqual(workersBefore, resources.Current.workers,
                "workers เป็น reserve pool — วางอาคารต้องไม่หัก workers (จองตอน Tick แทน)");

            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(2, 3)),
                "BuildingRegistry ต้องบันทึกอาคารที่วางแล้ว");

            Assert.AreEqual(1, visualParent.childCount,
                "BuildingVisualSpawner ต้อง spawn visual ของอาคารที่วาง");

            eventManager.RaiseBuildingSelected(null);
            Assert.IsFalse(tooltipPanel.activeSelf, "ยกเลิกเลือกอาคารแล้ว tooltip panel ต้องปิด");
        }

        [Test]
        public void TowerPhaseComplete_TriggersDilemma_AndResolutionAppliesEffects()
        {
            DilemmaData triggered = null;
            eventManager.OnDilemmaTriggered += d => triggered = d;

            // phase 1 เสร็จ (CORE% ข้าม 50%) → CoreTowerManager raise OnTowerPhaseComplete(1)
            // ทดสอบ wiring: DilemmaManager ต้องเปิด dilemma ที่ trigger "phase_1_complete"
            eventManager.RaiseTowerPhaseComplete(1);

            Assert.IsNotNull(triggered, "DilemmaManager ต้อง trigger dilemma เมื่อ phase 1 เสร็จ");
            Assert.AreEqual("phase1_test", triggered.dilemmaId);

            float foodBefore = resources.Current.food;
            float trustBefore = population.Current.trust;

            eventManager.RaiseDilemmaResolved(triggered, true);

            Assert.AreEqual(foodBefore + dilemmaData.choiceA_FoodChange, resources.Current.food, 1e-4f,
                "เลือก choice A ต้องบวก food ตาม choiceA_FoodChange");
            Assert.AreEqual(Mathf.Clamp(trustBefore + dilemmaData.choiceA_TrustChange, 0f, 100f),
                population.Current.trust, 1e-4f,
                "เลือก choice A ต้องปรับ trust ตาม choiceA_TrustChange");
            Assert.AreEqual(1, dilemmaManager.AethonRelationship);
            Assert.AreEqual(-1, dilemmaManager.KeranRelationship);
        }

        [Test]
        public void TowerCompletion_TriggersVictoryAndGameOverPanel()
        {
            var panel = new GameObject("GameOverPanel");
            _spawned.Add(panel);
            panel.SetActive(false);
            hud.gameOverPanel = panel;

            // CORE% ถึง 100% → CoreTowerManager raise OnTowerComplete
            eventManager.RaiseTowerComplete();

            Assert.AreEqual(GameManager.GameState.Victory, gameManager.CurrentState,
                "GameManager ต้องเปลี่ยนเป็น Victory เมื่อ CORE TOWER เสร็จ");
            Assert.IsTrue(panel.activeSelf, "UIManagerHUD ต้องเปิด gameOverPanel ตอน OnGameOver");
        }

        [Test]
        public void SaveLoad_RoundTrip_RestoresResourcePopulationGridAndVisual()
        {
            Cell cell = grid.GetCell(4, 5);
            cell.isOccupied = true;
            cell.buildingType = habitatData.buildingType;
            eventManager.RaiseBuildingPlaced(cell, habitatData);

            eventManager.RaiseResourceDelta(ResourceType.Food, 50f);
            eventManager.RaiseTrustDelta(-10f);

            ResourceData savedResources = resources.Current;
            PopulationData savedPopulation = population.Current;
            TowerData savedTower = tower.Current;

            saveManager.Save();
            Assert.IsTrue(File.Exists(SaveFilePath), "Save() ต้องเขียนไฟล์ savegame.json");

            // เปลี่ยนสถานะทั้งหมดให้ต่างจากตอน save เพื่อพิสูจน์ว่า Load() คืนค่าเดิมจริง
            eventManager.RaiseResourceDelta(ResourceType.Food, -1000f);
            eventManager.RaiseTrustDelta(1000f);
            grid.InitializeGrid();

            var stale = new List<GameObject>();
            foreach (Transform child in visualParent) stale.Add(child.gameObject);
            foreach (var go in stale) Object.DestroyImmediate(go);

            saveManager.Load();

            Assert.AreEqual(savedResources.food, resources.Current.food, 1e-4f,
                "Load() ต้องคืนค่า food ตามที่ save ไว้");
            Assert.AreEqual(savedPopulation.trust, population.Current.trust, 1e-4f,
                "Load() ต้องคืนค่า trust ตามที่ save ไว้");
            Assert.AreEqual(savedTower.currentPhase, tower.Current.currentPhase,
                "Load() ต้องคืนค่า tower phase ตามที่ save ไว้");

            Cell restored = grid.GetCell(4, 5);
            Assert.IsTrue(restored.isOccupied, "Load() ต้อง mark cell ที่มีอาคารว่า occupied อีกครั้ง");
            Assert.AreEqual(BuildingType.Habitat, restored.buildingType);

            Assert.AreEqual(1, visualParent.childCount,
                "Load() ต้อง respawn visual ของอาคารที่ถูกบันทึกไว้");
        }

        // Edit Mode test ([Test] แบบ synchronous) ไม่มี editor update loop มา trigger
        // Awake()/OnEnable() ให้อัตโนมัติเหมือน Play Mode — ต้องเรียกเองผ่าน reflection
        // ทันทีหลัง AddComponent เพื่อให้ Instance ของแต่ละ singleton ถูก set
        // และ subscription ผ่าน EventManager.Instance ใน OnEnable ทำงานก่อนใช้งานจริง
        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            TryInvokePrivate(component, "Awake");
            TryInvokePrivate(component, "OnEnable");
            return component;
        }

        // บางคลาส (EventManager, GameManager) เรียก DontDestroyOnLoad() ใน Awake()
        // ซึ่งโยน InvalidOperationException เมื่อรันใน Edit Mode — ไม่กระทบ Instance
        // ที่ถูก set ไปแล้วก่อนหน้า จึง catch แล้วปล่อยผ่านได้
        private static void TryInvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try
            {
                method?.Invoke(target, null);
            }
            catch (TargetInvocationException) { }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"ไม่พบ method '{methodName}' บน {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
