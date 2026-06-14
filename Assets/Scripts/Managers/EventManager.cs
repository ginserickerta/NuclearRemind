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

        // ===== Resources =====
        public event Action<ResourceData> OnResourceChanged;
        public event Action<ResourceType> OnResourceCritical;
        public event Action<ResourceType> OnResourceDepleted;

        // ===== Population =====
        public event Action<float> OnTrustChanged;
        public event Action OnWorkerStrike;
        public event Action OnRiotStarted;

        // ===== CORE TOWER =====
        public event Action<int> OnTowerPhaseComplete;
        public event Action OnTowerComplete;

        // ===== Game State =====
        public event Action<GameManager.GameState> OnGameStateChanged;
        public event Action<GameEndType> OnGameOver;

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

        // ===== Resources =====
        public void RaiseResourceChanged(ResourceData data) => OnResourceChanged?.Invoke(data);
        public void RaiseResourceCritical(ResourceType type) => OnResourceCritical?.Invoke(type);
        public void RaiseResourceDepleted(ResourceType type) => OnResourceDepleted?.Invoke(type);

        // ===== Population =====
        public void RaiseTrustChanged(float newTrust) => OnTrustChanged?.Invoke(newTrust);
        public void RaiseWorkerStrike() => OnWorkerStrike?.Invoke();
        public void RaiseRiotStarted() => OnRiotStarted?.Invoke();

        // ===== CORE TOWER =====
        public void RaiseTowerPhaseComplete(int phase) => OnTowerPhaseComplete?.Invoke(phase);
        public void RaiseTowerComplete() => OnTowerComplete?.Invoke();

        // ===== Game State =====
        public void RaiseGameStateChanged(GameManager.GameState newState) => OnGameStateChanged?.Invoke(newState);
        public void RaiseGameOver(GameEndType endType) => OnGameOver?.Invoke(endType);
    }
}
