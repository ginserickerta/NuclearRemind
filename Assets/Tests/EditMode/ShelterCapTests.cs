using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// V4 §5 — เพดานประชากรจากตึก Shelter:
    /// ฐานเริ่มเกม 10 · วางตึก Shelter (shelterCapacity 10) → 20 · อัป L2/L3 → 40/80 · ทุบ → กลับ 10
    /// ครอบทั้งสองลำดับ subscriber ของ OnBuildingPlaced (registry ก่อน/หลัง population)
    /// </summary>
    public class ShelterCapTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private BuildingRegistry registry;
        private PopulationManager population;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            population = NewComponent<PopulationManager>("PopulationManager");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private BuildingData NewShelter(int capacity = 10)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "Shelter";
            b.size = new Vector2Int(1, 1);
            b.shelterCapacity = capacity;
            b.upgradeIronCost = 0;   // แยกทดสอบ cap logic ออกจากระบบต้นทุน (มีเทสต์ต้นทุนใน BuildingRegistryTests)
            b.upgradeEnergyCost = 0;
            _spawned.Add(b);
            return b;
        }

        [Test]
        public void StartsAt_BaseCap10()
        {
            Assert.AreEqual(10, population.Current.shelterCap);
        }

        [Test]
        public void PlaceShelter_RaisesCapTo20()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewShelter());
            Assert.AreEqual(20, population.Current.shelterCap, "ฐาน 10 + Shelter L1 (+10) = 20 (§5 L2)");
        }

        [Test]
        public void UpgradeShelter_RaisesCapTo40Then80()
        {
            var shelter = NewShelter();
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), shelter);
            var cell = new Vector2Int(1, 1);

            eventManager.RaiseUpgradeBuildingRequested(cell); // L2
            Assert.AreEqual(40, population.Current.shelterCap, "ฐาน 10 + L2 (+30) = 40 (§5 L3)");

            eventManager.RaiseUpgradeBuildingRequested(cell); // L3
            Assert.AreEqual(80, population.Current.shelterCap, "ฐาน 10 + L3 (+70) = 80 (§5 L4)");
        }

        [Test]
        public void RemoveShelter_RestoresBaseCap()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewShelter());
            eventManager.RaiseBuildingRemoved(new Vector2Int(1, 1));
            Assert.AreEqual(10, population.Current.shelterCap);
        }

        [Test]
        public void TwoShelters_Stack()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewShelter());
            eventManager.RaiseBuildingPlaced(new Cell(2, 2), NewShelter());
            Assert.AreEqual(30, population.Current.shelterCap, "ฐาน 10 + L1 สองหลัง (+10+10) = 30");
        }

        [Test]
        public void NonShelterBuilding_DoesNotChangeCap()
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "Farm";
            b.size = new Vector2Int(1, 1);
            _spawned.Add(b);

            eventManager.RaiseBuildingPlaced(new Cell(1, 1), b);
            Assert.AreEqual(10, population.Current.shelterCap);
        }

        [Test]
        public void Growth_UnblockedAfterShelterPlaced()
        {
            // เต็มเพดานฐาน (10/10) → ไม่โต · วาง Shelter → เพดาน 20 → โตได้
            eventManager.RaiseDayEnded(2);
            Assert.AreEqual(10, population.Current.total, "เต็มเพดาน 10 → ไม่โต");

            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewShelter());
            eventManager.RaiseDayEnded(3);
            Assert.AreEqual(11, population.Current.total, "เพดานขยายเป็น 20 → +1 คน/วัน");
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

    /// <summary>
    /// ลำดับ subscriber กลับด้าน: PopulationManager สมัคร OnBuildingPlaced ก่อน BuildingRegistry
    /// → ตอน recalc ตึกยังไม่อยู่ใน registry ต้องบวกเองที่ L1 (fallback path ใน RecalcShelterCap)
    /// </summary>
    public class ShelterCapSubscriberOrderTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private PopulationManager population;
        private BuildingRegistry registry;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            population = NewComponent<PopulationManager>("PopulationManager"); // สมัครก่อน registry
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        [Test]
        public void PlaceShelter_PopulationSubscribedFirst_StillCountsOnce()
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = "Shelter";
            b.size = new Vector2Int(1, 1);
            b.shelterCapacity = 10;
            _spawned.Add(b);

            eventManager.RaiseBuildingPlaced(new Cell(1, 1), b);
            Assert.AreEqual(20, population.Current.shelterCap, "นับตึกที่เพิ่งวางครั้งเดียว ไม่ว่าลำดับ subscriber");
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
