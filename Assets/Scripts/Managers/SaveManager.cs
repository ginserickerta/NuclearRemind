using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Save/Load สถานะเกมเป็น JSON (SaveData) ที่ Application.persistentDataPath
    /// แคชสถานะล่าสุดของ Resource/Population/Tower ผ่าน OnXxxChanged event
    /// ปุ่ม Save/Load บน HUD จะต่อเข้ากับ Save()/Load() ใน Day 7
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SaveFileName = "savegame.json";

        private ResourceData _resources;
        private PopulationData _population;
        private TowerData _tower;

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
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgressChanged;
        }

        private void HandleResourceChanged(ResourceData data) => _resources = data;
        private void HandlePopulationChanged(PopulationData data) => _population = data;
        private void HandleTowerProgressChanged(TowerData data) => _tower = data;

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
                gameTime = Time.time
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
