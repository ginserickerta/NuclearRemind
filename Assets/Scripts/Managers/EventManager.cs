using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ event กลางของเกมทั้งหมด (Observer pattern, Singleton)
    /// ระบบอื่นต้อง subscribe/raise ผ่าน EventManager.Instance เท่านั้น ห้าม direct reference ข้าม Manager
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        // ===== Building =====
        public event Action<Cell, BuildingData> OnBuildingPlaced;
        public event Action<Vector2Int> OnBuildingRemoved;
        public event Action OnPlacementCancelled;
        public event Action<BuildingData> OnBuildingSelected;

        // ===== Resources =====
        public event Action<ResourceData> OnResourceChanged;
        public event Action<ResourceType> OnResourceCritical;
        public event Action<ResourceType> OnResourceDepleted;
        public event Action<ResourceType, float> OnResourceDelta;

        // ===== Population =====
        public event Action<float> OnTrustChanged;
        public event Action<PopulationData> OnPopulationChanged;
        public event Action OnWorkerStrike;
        public event Action OnRiotStarted;
        public event Action<float> OnTrustDelta;

        // ===== CORE TOWER =====
        public event Action<TowerData> OnTowerProgressChanged;
        public event Action<int> OnTowerPhaseComplete;
        public event Action OnTowerComplete;

        // ===== Game State =====
        public event Action<GameManager.GameState> OnGameStateChanged;
        public event Action<GameEndType> OnGameOver;

        // ===== Save/Load =====
        public event Action<SaveData> OnSaveLoaded;
        public event Action OnSaveRequested;
        public event Action OnLoadRequested;

        // ===== Narrative / Dilemma =====
        public event Action<DilemmaData> OnDilemmaTriggered;
        public event Action<DilemmaData, bool> OnDilemmaResolved;
        public event Action<int, int> OnRelationshipChanged; // (aethon, keran)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ===== Building =====
        public void RaiseBuildingPlaced(Cell cell, BuildingData data) => OnBuildingPlaced?.Invoke(cell, data);
        public void RaiseBuildingRemoved(Vector2Int position) => OnBuildingRemoved?.Invoke(position);
        public void RaisePlacementCancelled() => OnPlacementCancelled?.Invoke();
        public void RaiseBuildingSelected(BuildingData data) => OnBuildingSelected?.Invoke(data);

        // ===== Resources =====
        public void RaiseResourceChanged(ResourceData data) => OnResourceChanged?.Invoke(data);
        public void RaiseResourceCritical(ResourceType type) => OnResourceCritical?.Invoke(type);
        public void RaiseResourceDepleted(ResourceType type) => OnResourceDepleted?.Invoke(type);
        public void RaiseResourceDelta(ResourceType type, float amount) => OnResourceDelta?.Invoke(type, amount);

        // ===== Population =====
        public void RaiseTrustChanged(float newTrust) => OnTrustChanged?.Invoke(newTrust);
        public void RaisePopulationChanged(PopulationData data) => OnPopulationChanged?.Invoke(data);
        public void RaiseWorkerStrike() => OnWorkerStrike?.Invoke();
        public void RaiseRiotStarted() => OnRiotStarted?.Invoke();
        public void RaiseTrustDelta(float amount) => OnTrustDelta?.Invoke(amount);

        // ===== CORE TOWER =====
        public void RaiseTowerProgressChanged(TowerData data) => OnTowerProgressChanged?.Invoke(data);
        public void RaiseTowerPhaseComplete(int phase) => OnTowerPhaseComplete?.Invoke(phase);
        public void RaiseTowerComplete() => OnTowerComplete?.Invoke();

        // ===== Game State =====
        public void RaiseGameStateChanged(GameManager.GameState newState) => OnGameStateChanged?.Invoke(newState);
        public void RaiseGameOver(GameEndType endType) => OnGameOver?.Invoke(endType);

        // ===== Save/Load =====
        public void RaiseSaveLoaded(SaveData data) => OnSaveLoaded?.Invoke(data);
        public void RaiseSaveRequested() => OnSaveRequested?.Invoke();
        public void RaiseLoadRequested() => OnLoadRequested?.Invoke();

        // ===== Narrative / Dilemma =====
        public void RaiseDilemmaTriggered(DilemmaData data) => OnDilemmaTriggered?.Invoke(data);
        public void RaiseDilemmaResolved(DilemmaData data, bool choiceA) => OnDilemmaResolved?.Invoke(data, choiceA);
        public void RaiseRelationshipChanged(int aethon, int keran) => OnRelationshipChanged?.Invoke(aethon, keran);
    }
}
