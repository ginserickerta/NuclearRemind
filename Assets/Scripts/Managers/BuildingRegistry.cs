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

        private readonly Dictionary<Vector2Int, BuildingData> _placedBuildings = new Dictionary<Vector2Int, BuildingData>();
        public IReadOnlyDictionary<Vector2Int, BuildingData> PlacedBuildings => _placedBuildings;

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
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            _placedBuildings[new Vector2Int(cell.col, cell.row)] = data;
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            _placedBuildings.Remove(position);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _placedBuildings.Clear();
            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = GetBuildingDataByName(save.buildingTypes[i]);
                if (data != null)
                    _placedBuildings[save.placedBuildings[i]] = data;
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
