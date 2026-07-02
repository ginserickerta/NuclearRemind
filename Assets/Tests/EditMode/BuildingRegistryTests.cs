using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §7 (เฟส 6) — Building level/upgrade: อาคารเริ่ม L1, อัปด้วยแร่เหล็ก (×ระดับ),
    /// cap ที่ maxBuildingLevel, แร่เหล็กไม่พอ/ไม่มีอาคาร → ไม่อัป
    /// </summary>
    public class BuildingRegistryTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private BuildingRegistry registry;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager"); // คลังแร่เหล็ก (default iron=100)
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private Vector2Int Place(int col, int row, int upgradeCost = 40)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "B";
            b.size = new Vector2Int(1, 1);
            b.upgradeIronCost = upgradeCost;
            _spawned.Add(b);
            eventManager.RaiseBuildingPlaced(new Cell(col, row), b);
            return new Vector2Int(col, row);
        }

        [Test]
        public void NewBuilding_StartsAtLevel1()
        {
            var cell = Place(1, 1);
            Assert.AreEqual(1, registry.GetLevel(cell));
        }

        [Test]
        public void Upgrade_IncrementsLevel_AndDeductsIron()
        {
            var cell = Place(1, 1, upgradeCost: 40); // iron เริ่ม 100
            eventManager.RaiseUpgradeBuildingRequested(cell);

            Assert.AreEqual(2, registry.GetLevel(cell));
            Assert.AreEqual(60f, resources.Current.iron, 1e-3f, "อัป L1→L2 หัก 40 (×ระดับ 1)");
        }

        [Test]
        public void Upgrade_CapsAtMaxLevel()
        {
            eventManager.RaiseResourceDelta(ResourceType.Iron, 200); // เติมให้พออัปหลายรอบ (iron 300)
            var cell = Place(1, 1, upgradeCost: 40);

            eventManager.RaiseUpgradeBuildingRequested(cell); // L2 (cost 40)
            eventManager.RaiseUpgradeBuildingRequested(cell); // L3 (cost 80)
            eventManager.RaiseUpgradeBuildingRequested(cell); // เต็มแล้ว → ไม่ทำ

            Assert.AreEqual(3, registry.GetLevel(cell), "cap ที่ maxBuildingLevel (3)");
        }

        [Test]
        public void Upgrade_InsufficientIron_Blocked()
        {
            var cell = Place(1, 1, upgradeCost: 200); // iron 100 < 200
            eventManager.RaiseUpgradeBuildingRequested(cell);

            Assert.AreEqual(1, registry.GetLevel(cell), "แร่เหล็กไม่พอ → ไม่อัป");
            Assert.AreEqual(100f, resources.Current.iron, 1e-3f, "ไม่หักแร่เหล็ก");
        }

        [Test]
        public void Upgrade_NonexistentCell_NoOp()
        {
            eventManager.RaiseUpgradeBuildingRequested(new Vector2Int(5, 5)); // ไม่มีอาคาร
            Assert.AreEqual(100f, resources.Current.iron, 1e-3f, "ไม่มีอาคาร → ไม่หักอะไร");
        }

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
