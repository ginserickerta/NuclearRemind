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

        // หมายเหตุ: คืน state ของ registry ตาม save เท่านั้น ยังไม่ spawn GameObject อาคารใหม่ในฉาก
        // (การ respawn ภาพ placement จาก save เป็นงาน integration ของ Day 9)
        private void HandleSaveLoaded(SaveData save)
        {
            _placedBuildings.Clear();
            if (save.placedBuildings == null || save.buildingTypes == null)
                return;

            for (int i = 0; i < save.placedBuildings.Count; i++)
            {
                BuildingData data = FindBuildingDataByName(save.buildingTypes[i]);
                if (data != null)
                    _placedBuildings[save.placedBuildings[i]] = data;
            }
        }

        private BuildingData FindBuildingDataByName(string buildingName)
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
