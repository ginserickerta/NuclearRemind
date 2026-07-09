using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// ใบความรู้จากห้องวิจัย (GDD §6): วาง Lab → ปลด entry ที่ unlockedByEvent = "lab_built"
    /// อัป Lab เป็น L2/L3 → ปลด "lab_l2"/"lab_l3" · อาคารอื่นวาง/อัปไม่ปลด
    /// </summary>
    public class CodexLabUnlockTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private EventManager eventManager;
        private BuildingRegistry registry;
        private CodexManager codex;

        [SetUp]
        public void SetUp()
        {
            eventManager = NewComponent<EventManager>("EventManager");
            registry = NewComponent<BuildingRegistry>("BuildingRegistry");
            registry.allBuildingData = new BuildingData[0];

            var go = new GameObject("CodexManager");
            _spawned.Add(go);
            codex = go.AddComponent<CodexManager>();
            codex.allCodexEntries = new[]
            {
                NewEntry("lab_isotope_basics", "lab_built"),
                NewEntry("lab_neutron_activation", "lab_l2"),
                NewEntry("lab_breeding_tritium", "lab_l3"),
            };
            TryInvokePrivate(codex, "Awake");
            TryInvokePrivate(codex, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
        }

        private CodexEntry NewEntry(string id, string unlockedBy)
        {
            var e = ScriptableObject.CreateInstance<CodexEntry>();
            e.entryId = id;
            e.title = id;
            e.branch = "Core";
            e.unlockedByEvent = unlockedBy;
            _spawned.Add(e);
            return e;
        }

        private BuildingData NewBuilding(string name, BuildingType type)
        {
            var b = ScriptableObject.CreateInstance<BuildingData>();
            b.buildingName = name;
            b.size = new Vector2Int(1, 1);
            b.buildingType = type;
            b.upgradeIronCost = 0;
            b.upgradeEnergyCost = 0;
            _spawned.Add(b);
            return b;
        }

        [Test]
        public void PlaceLab_UnlocksLabBuiltEntry()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewBuilding("Lab", BuildingType.Laboratory));

            Assert.IsTrue(codex.IsUnlocked("lab_isotope_basics"), "วาง Lab → ปลดใบความรู้ชุดแรก");
            Assert.IsFalse(codex.IsUnlocked("lab_neutron_activation"), "ชุด L2 ยังไม่ปลด");
        }

        [Test]
        public void PlaceOtherBuilding_DoesNotUnlock()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewBuilding("Farm", BuildingType.Farm));
            Assert.IsFalse(codex.IsUnlocked("lab_isotope_basics"));
        }

        [Test]
        public void UpgradeLab_UnlocksTieredEntries()
        {
            eventManager.RaiseBuildingPlaced(new Cell(1, 1), NewBuilding("Lab", BuildingType.Laboratory));
            var cell = new Vector2Int(1, 1);

            eventManager.RaiseUpgradeBuildingRequested(cell); // → L2
            Assert.IsTrue(codex.IsUnlocked("lab_neutron_activation"), "อัป L2 → ปลดชุด L2");
            Assert.IsFalse(codex.IsUnlocked("lab_breeding_tritium"));

            eventManager.RaiseUpgradeBuildingRequested(cell); // → L3
            Assert.IsTrue(codex.IsUnlocked("lab_breeding_tritium"), "อัป L3 → ปลดชุด L3");
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
