using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// เก็บรายการอาคารที่วางแล้วทั้งหมดแบบรวมศูนย์ (ตำแหน่ง -> BuildingData)
    /// Manager อื่น (ResourceManager, CoreTowerManager, SaveManager) อ่าน PlacedBuildings ได้โดยตรง
    /// เนื่องจากเป็น read-only query ของ registry กลาง (เทียบเท่า .Current ของ manager อื่น)
    /// ไม่ใช่การเรียก method ที่เปลี่ยนสถานะข้าม Manager ซึ่งเป็นสิ่งที่ห้าม
    /// </summary>
    public class BuildingRegistry : MonoBehaviour
    {
        public static BuildingRegistry Instance { get; private set; }

        [Header("Lookup table สำหรับ restore จาก save (ชื่อ -> BuildingData asset)")]
        public BuildingData[] allBuildingData;

        [Header("Upgrade (V4 §7 — เฟส 6)")]
        public int maxBuildingLevel = 3; // L1–L3

        private readonly Dictionary<Vector2Int, BuildingData> _placedBuildings = new Dictionary<Vector2Int, BuildingData>();
        public IReadOnlyDictionary<Vector2Int, BuildingData> PlacedBuildings => _placedBuildings;

        // ระดับอาคารต่อ cell (L1..L3) — ผลิตสเกลตามระดับ + ปลด fuel ที่ระดับสูงสุด
        private readonly Dictionary<Vector2Int, int> _levels = new Dictionary<Vector2Int, int>();

        /// <summary>ระดับปัจจุบันของอาคารที่ cell นี้ (1 ถ้าไม่พบ)</summary>
        public int GetLevel(Vector2Int cell) => _levels.TryGetValue(cell, out int lvl) ? lvl : 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            EventManager.Instance.OnUpgradeBuildingRequested += HandleUpgradeRequested;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            EventManager.Instance.OnUpgradeBuildingRequested -= HandleUpgradeRequested;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            var pos = new Vector2Int(cell.col, cell.row);
            _placedBuildings[pos] = data;
            _levels[pos] = 1; // เริ่มที่ L1
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            _placedBuildings.Remove(position);
            _levels.Remove(position);
        }

        // อัประดับอาคาร (V4 §7): จ่ายแร่เหล็ก (×ระดับปัจจุบัน) → level++ จนถึง maxBuildingLevel
        private void HandleUpgradeRequested(Vector2Int cell)
        {
            if (!_placedBuildings.TryGetValue(cell, out var data) || data == null) return;

            int level = GetLevel(cell);
            if (level >= maxBuildingLevel) return; // เต็มแล้ว

            int cost = data.upgradeIronCost * level;
            var rm = ResourceManager.Instance;
            if (rm != null && rm.Current.iron < cost) return; // แร่เหล็กไม่พอ

            if (rm != null)
                EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -cost);

            _levels[cell] = level + 1;
            EventManager.Instance.RaiseBuildingUpgraded(cell, level + 1);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _placedBuildings.Clear();
            _levels.Clear(); // 🔸 ระดับอาคารยังไม่ persist ในเซฟ (เฟส 6) — โหลดแล้วรีเซ็ตเป็น L1
            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = GetBuildingDataByName(save.buildingTypes[i]);
                if (data != null)
                {
                    _placedBuildings[save.placedBuildings[i]] = data;
                    _levels[save.placedBuildings[i]] = 1;
                }
            }
        }

        /// <summary>
        /// ค้นหา BuildingData จาก allBuildingData ตามชื่อ — ใช้สำหรับ restore สถานะจาก save
        /// (GridManager / BuildingVisualSpawner เรียกใช้ตอน OnSaveLoaded เพื่อ re-occupy cell และ respawn visual)
        /// </summary>
        public BuildingData GetBuildingDataByName(string buildingName)
        {
            foreach (var data in allBuildingData)
            {
                if (data != null && data.buildingName == buildingName)
                    return data;
            }
            return null;
        }
    }
}
