using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// กติกาการวางอาคาร (V4 §6/§8):
    /// - ทรัพยากรไม่พอ → เข้าโหมดวาง/ยืนยันวางไม่ได้ (เดิมวางได้แล้วคลัง clamp 0 เงียบๆ)
    /// - CORE TOWER สร้างได้แค่หลังเดียว
    /// - เข้าโหมดวาง → นาฬิกาวันหยุด, วาง/ยกเลิก → เดินต่อ (V4 §15)
    /// </summary>
    public class PlacementRulesTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;   // เริ่มต้น energy 200 / iron 100
        private BuildingRegistry registry;
        private PlacementController placement;
        private TimeManager time;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            NewComponent<GridManager>("GridManager"); // ให้ GetCell/footprint ใช้งานได้ (default 20×12)
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            time = NewComponent<TimeManager>("TimeManager");
            placement = NewComponent<PlacementController>("PlacementController");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private BuildingData NewBuilding(string name, int ironCost, int energyCost,
            BuildingType type = BuildingType.Farm)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = name;
            b.size = new Vector2Int(1, 1);
            b.ironCost = ironCost;
            b.energyCost = energyCost;
            b.buildingType = type;
            _spawned.Add(b);
            return b;
        }

        [Test]
        public void Place_Affordable_Succeeds_AndDeductsCost()
        {
            var farm = NewBuilding("Farm", ironCost: 30, energyCost: 10);

            placement.BeginPlacement(farm);
            placement.ConfirmPlace(); // currentCell default (0,0) — ว่าง

            Assert.AreEqual(1, registry.PlacedBuildings.Count, "ทรัพยากรพอ → วางได้");
            Assert.AreEqual(70f, resources.Current.iron, 1e-3f, "หักแร่เหล็ก 30");
            Assert.AreEqual(190f, resources.Current.energy, 1e-3f, "หักพลังงาน 10");
        }

        [Test]
        public void Place_InsufficientIron_Blocked()
        {
            var pricey = NewBuilding("Pricey", ironCost: 999, energyCost: 0);

            placement.BeginPlacement(pricey); // ไม่เข้าโหมดวาง
            placement.ConfirmPlace();

            Assert.AreEqual(0, registry.PlacedBuildings.Count, "แร่เหล็กไม่พอ → วางไม่ได้");
            Assert.AreEqual(100f, resources.Current.iron, 1e-3f, "คลังไม่ถูกหัก");
        }

        [Test]
        public void Place_InsufficientEnergy_Blocked()
        {
            var pricey = NewBuilding("Pricey", ironCost: 0, energyCost: 999);

            placement.BeginPlacement(pricey);
            placement.ConfirmPlace();

            Assert.AreEqual(0, registry.PlacedBuildings.Count, "พลังงานไม่พอ → วางไม่ได้");
            Assert.AreEqual(200f, resources.Current.energy, 1e-3f, "คลังไม่ถูกหัก");
        }

        [Test]
        public void CoreTower_SecondPlacement_Blocked()
        {
            var tower = NewBuilding("CORE TOWER", ironCost: 0, energyCost: 0, BuildingType.CoreTower);
            eventManager.RaiseBuildingPlaced(new Cell(5, 5), tower); // หลังแรกอยู่ในเมืองแล้ว

            var tower2 = NewBuilding("CORE TOWER", ironCost: 0, energyCost: 0, BuildingType.CoreTower);
            placement.BeginPlacement(tower2); // ถูกบล็อก — สร้างได้หลังเดียว (V4 §8)
            placement.ConfirmPlace();

            int towers = 0;
            foreach (var kvp in registry.PlacedBuildings)
                if (kvp.Value.buildingType == BuildingType.CoreTower) towers++;
            Assert.AreEqual(1, towers, "CORE TOWER มีได้หลังเดียว");
        }

        // ── V4 §15: หยุดเวลาตอนวางอาคาร ────────────────────────────

        [Test]
        public void BeginPlacement_PausesDayClock()
        {
            placement.BeginPlacement(NewBuilding("Farm", 0, 0));

            Assert.IsTrue(time.IsPaused(PauseReason.Placement), "เข้าโหมดวาง → มีเหตุหยุด Placement");
            Assert.IsFalse(time.IsRunning, "นาฬิกาวันต้องหยุดระหว่างวาง (V4 §15)");
        }

        [Test]
        public void ConfirmPlace_ResumesDayClock()
        {
            placement.BeginPlacement(NewBuilding("Farm", 0, 0));
            placement.ConfirmPlace(); // วางสำเร็จที่ (0,0)

            Assert.IsTrue(time.IsRunning, "วางเสร็จ → นาฬิกาเดินต่อ (V4 §15)");
        }

        [Test]
        public void CancelPlacement_ResumesDayClock()
        {
            placement.BeginPlacement(NewBuilding("Farm", 0, 0));
            placement.CancelPlacement();

            Assert.IsTrue(time.IsRunning, "ยกเลิกการวาง → นาฬิกาเดินต่อ (V4 §15)");
        }

        [Test]
        public void BlockedPlacement_DoesNotPauseClock()
        {
            placement.BeginPlacement(NewBuilding("Pricey", ironCost: 999, energyCost: 0)); // ไม่เข้าโหมดวาง

            Assert.IsTrue(time.IsRunning, "เข้าโหมดวางไม่สำเร็จ (ของไม่พอ) → นาฬิกาต้องไม่ถูกหยุดค้าง");
        }

        [Test]
        public void NonUniqueBuilding_CanPlaceMultiple()
        {
            eventManager.RaiseBuildingPlaced(new Cell(5, 5), NewBuilding("Farm", 0, 0));

            placement.BeginPlacement(NewBuilding("Farm", 0, 0));
            placement.ConfirmPlace(); // วางที่ (0,0)

            Assert.AreEqual(2, registry.PlacedBuildings.Count, "อาคารทั่วไปวางซ้ำชนิดได้");
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
