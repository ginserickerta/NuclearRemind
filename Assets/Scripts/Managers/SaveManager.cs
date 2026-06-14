using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Save/Load สถานะเกมเป็น JSON (SaveData) ที่ Application.persistentDataPath
    /// แคชสถานะล่าสุดของ Resource/Population/Tower ผ่าน OnXxxChanged event
    /// เรียก Save()/Load() ผ่าน EventManager.OnSaveRequested/OnLoadRequested (คีย์ลัด F5/F9 ใน InputManager)
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SaveFileName = "savegame.json";

        private ResourceData _resources;
        private PopulationData _population;
        private TowerData _tower;
        private int _aethonRelationship;
        private int _keranRelationship;

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

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
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgressChanged;
            EventManager.Instance.OnSaveRequested += Save;
            EventManager.Instance.OnLoadRequested += Load;
            EventManager.Instance.OnRelationshipChanged += HandleRelationshipChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
            EventManager.Instance.OnSaveRequested -= Save;
            EventManager.Instance.OnLoadRequested -= Load;
            EventManager.Instance.OnRelationshipChanged -= HandleRelationshipChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        private void HandleResourceChanged(ResourceData data) => _resources = data;
        private void HandlePopulationChanged(PopulationData data) => _population = data;
        private void HandleTowerProgressChanged(TowerData data) => _tower = data;

        private void HandleRelationshipChanged(int aethon, int keran)
        {
            _aethonRelationship = aethon;
            _keranRelationship = keran;
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _aethonRelationship = save.aethonRelationship;
            _keranRelationship = save.keranRelationship;
        }

        public void Save()
        {
            var save = new SaveData
            {
                resources = _resources,
                population = _population,
                tower = _tower,
                placedBuildings = new List<Vector2Int>(),
                buildingTypes = new List<string>(),
                unlockedCodexEntries = new List<string>(),
                gameTime = Time.time,
                aethonRelationship = _aethonRelationship,
                keranRelationship = _keranRelationship
            };

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                save.placedBuildings.Add(kvp.Key);
                save.buildingTypes.Add(kvp.Value.buildingName);
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
            Debug.Log($"[SaveManager] Saved to {SavePath}");
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[SaveManager] No save file found.");
                return;
            }

            var save = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            EventManager.Instance.RaiseSaveLoaded(save);
            Debug.Log($"[SaveManager] Loaded from {SavePath}");
        }
    }
}
