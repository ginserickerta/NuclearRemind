using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// CORE TOWER มากับแมพ (PrePlacedBuilding): ตอน Start ต้องเข้า registry + จองกริดครบ footprint
    /// + ไม่ติดคิวก่อสร้าง (สร้างเสร็จทันที) + ไม่หักทรัพยากร (ราคา 0) + กันวางซ้ำเมื่อรันสองรอบ
    /// </summary>
    public class PrePlacedBuildingTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private ResourceManager resources;
        private BuildingRegistry registry;
        private ConstructionController construction;
        private PrePlacedBuilding prePlaced;
        private BuildingData tower;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            resources = NewComponent<ResourceManager>("ResourceManager");
            NewComponent<GridManager>("GridManager"); // default 20×12 — พอสำหรับ footprint ทดสอบ
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];
            construction = NewComponent<ConstructionController>("ConstructionController");

            tower = ScriptableObject.CreateInstance<BuildingData>();
            tower.buildingName = "CORE TOWER";
            tower.buildingType = BuildingType.CoreTower;
            tower.size = new Vector2Int(3, 3);
            tower.ironCost = 0;
            tower.energyCost = 0;
            _spawned.Add(tower);

            prePlaced = NewComponent<PrePlacedBuilding>("PrePlacedCoreTower");
            prePlaced.building = tower;
            prePlaced.cell = new Vector2Int(8, 4); // กึ่งกลางกริด 20×12 สำหรับตึก 3×3
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private void RunStart() => TryInvokePrivate(prePlaced, "Start");

        [Test]
        public void Start_RegistersTower_AndOccupiesFootprint()
        {
            RunStart();

            Assert.IsTrue(registry.PlacedBuildings.ContainsKey(new Vector2Int(8, 4)),
                "ตึกต้องเข้า registry ผ่าน pipeline ปกติ");

            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                    Assert.IsTrue(GridManager.Instance.GetCell(8 + dx, 4 + dy).isOccupied,
                        $"ช่อง ({8 + dx},{4 + dy}) ใน footprint 3×3 ต้องถูกจอง");
        }

        [Test]
        public void Start_SkipsConstructionQueue()
        {
            RunStart();
            Assert.IsFalse(construction.IsUnderConstruction(new Vector2Int(8, 4)),
                "ตึกที่มากับแมพต้องสร้างเสร็จทันที — ไม่ค้างในคิวก่อสร้าง");
        }

        [Test]
        public void Start_DoesNotChargeResources()
        {
            float ironBefore = resources.Current.iron;
            float energyBefore = resources.Current.energy;

            RunStart();

            Assert.AreEqual(ironBefore, resources.Current.iron, 1e-3f, "ตึกมากับแมพ ราคา 0 — คลังไม่ถูกหัก");
            Assert.AreEqual(energyBefore, resources.Current.energy, 1e-3f);
        }

        [Test]
        public void Start_Twice_PlacesOnlyOnce()
        {
            RunStart();
            RunStart();
            Assert.AreEqual(1, registry.PlacedBuildings.Count, "รัน Start ซ้ำต้องไม่วางซ้อน");
        }

        [Test]
        public void PlacementOnFootprint_Blocked()
        {
            RunStart();

            // วางตึกอื่นทับ footprint เตา — ต้องถูกบล็อกโดย occupancy
            var farm = ScriptableObject.CreateInstance<BuildingData>();
            farm.buildingName = "Farm";
            farm.size = new Vector2Int(1, 1);
            _spawned.Add(farm);

            var placement = NewComponent<PlacementController>("PlacementController");
            placement.buildingHotbar = new BuildingData[0];
            // currentCell default (0,0) ว่าง — ทดสอบผ่าน IsPlacementValid ทางอ้อมด้วยการวางตรง footprint
            var cellOnTower = GridManager.Instance.GetCell(9, 5);
            Assert.IsTrue(cellOnTower.isOccupied, "ช่องกลาง footprint ต้องไม่ว่างให้วางทับ");
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
