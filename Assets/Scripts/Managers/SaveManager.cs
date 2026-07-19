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

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: persistentDataPath = IndexedDB (ผ่าน MEMFS) — ต้อง flush หลังเขียนไฟล์ ไม่งั้นเซฟหายเมื่อรีเฟรช (jslib NuclearSave)
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void NuclearSyncFs();
#endif

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
                unlockedCodexEntries = new List<string>(CodexManager.Instance.UnlockedIds),
                gameTime = Time.time,
                aethonRelationship = _aethonRelationship,
                keranRelationship = _keranRelationship
            };

            if (ConstructionController.Instance != null)
            {
                var (cells, progress) = ConstructionController.Instance.GetSaveState();
                save.underConstructionCells = cells;
                save.constructionProgress   = progress;
            }

            // Story (read-only query แบบเดียวกับ CodexManager.UnlockedIds) — ไม่มี StoryDirector = list ว่าง (default)
            if (StoryDirector.Instance != null)
            {
                save.firedStoryBeats = new List<string>(StoryDirector.Instance.FiredBeatIds);
                save.archivedRecords = StoryDirector.Instance.ArchivedRecordIds;
                save.archivedRecordDays = new List<int>(StoryDirector.Instance.ArchivedRecordDays);
                save.deferredCrisisKeys = new List<string>(StoryDirector.Instance.DeferredCrisisKeys);
                save.deferredCrisisDays = new List<int>(StoryDirector.Instance.DeferredCrisisFireDays);
            }

            // ค่าเสี่ยงรังสีสะสม (§4) — read-only query แบบเดียวกับ Story/Codex · ไม่มี manager = 0
            save.radiationExposure = RadiationManager.Instance != null ? RadiationManager.Instance.CurrentExposure : 0f;

            // ผลกระทบวิกฤต (§4) — read-only query · ไม่มี manager = คง default (multiplier=1) ใน SaveData
            if (CrisisEffectManager.Instance != null)
            {
                var ce = CrisisEffectManager.Instance;
                save.foodYieldMultiplier = ce.FoodYieldMultiplier;
                save.foodSpoilRatePerDay = ce.FoodSpoilRatePerDay;
                save.workerEfficiencyMultiplier = ce.WorkerEfficiencyMultiplier;
                save.workerEfficiencyDaysRemaining = ce.WorkerEfficiencyDaysRemaining;
                save.busyWorkerCounts = new List<int>(ce.BusyWorkerCounts);
                save.busyWorkerDays = new List<int>(ce.BusyWorkerDays);
                save.hopeDrainPerDay = new List<float>(ce.HopeDrainPerDay);
                save.hopeDrainDays = new List<int>(ce.HopeDrainDays);
                save.spoilageActive = ce.SpoilageActive;
                save.spoilMultiplier = ce.SpoilMultiplier;
                save.co60Active = ce.Co60Active;
            }

            foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
            {
                save.placedBuildings.Add(kvp.Key);
                save.buildingTypes.Add(kvp.Value.buildingName);
            }

            // การจัดสรร Worker ประจำอาคาร (§5) — read-only query · ไม่มี manager = list ว่าง (ทุกคน idle)
            save.workerAssignmentCells = new List<Vector2Int>();
            save.workerAssignmentCounts = new List<int>();
            if (WorkerAssignmentManager.Instance != null)
            {
                foreach (var kvp in WorkerAssignmentManager.Instance.Assignments)
                {
                    save.workerAssignmentCells.Add(kvp.Key);
                    save.workerAssignmentCounts.Add(kvp.Value);
                }
            }

            // ไอเทมคราฟต์ (GDD §13) — read-only query · ไม่มี manager = คง default (คลังว่าง)
            if (InventoryManager.Instance != null)
                save.inventory = InventoryManager.Instance.GetSaveState();

            // โครงการวิจัยห้องวิจัย (ResearchLab_Spec) — read-only query · ไม่มี manager = default false
            if (ResearchManager.Instance != null)
            {
                var rs = ResearchManager.Instance;
                save.researchSeedsDone = rs.SeedsDone;
                save.researchIsotopeDone = rs.IsotopeDone;
                save.researchCoreUnlockDone = rs.CoreUnlockDone;
                save.researchIsotopePending = rs.IsotopePending;
            }

            // บันทึก Elara (STORY.md §3) — ไม่เก็บ = โหลดเซฟแล้ว Record + Lead ที่ปลดไว้หายหมด
            if (DataRecovery.Instance != null)
            {
                save.dataRecoveryProgress = DataRecovery.Instance.Progress;
                save.dataRecoveryRecords = DataRecovery.Instance.RecordsRecovered;
            }

            // สรุปการเล่น (STORY.md §④) — Hope ต่ำสุด/Decree/Triage หายทันทีถ้าไม่จด (ไม่มีระบบไหนเก็บไว้)
            if (RunStats.Instance != null) RunStats.Instance.WriteTo(save);

            File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
#if UNITY_WEBGL && !UNITY_EDITOR
            NuclearSyncFs();   // flush ลง IndexedDB (WebGL) ให้เซฟติดเบราว์เซอร์ · เดสก์ท็อป/เอดิเตอร์ข้ามบล็อกนี้
#endif
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
