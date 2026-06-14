using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Subscriber ตัวอย่างสำหรับทดสอบว่า EventManager ส่ง event ถูกต้องตาม Observer Pattern
    /// </summary>
    public class EventTestSubscriber : MonoBehaviour
    {
        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnPlacementCancelled += HandlePlacementCancelled;
            EventManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return; // scene teardown safety
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnPlacementCancelled -= HandlePlacementCancelled;
            EventManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            Debug.Log($"[EventTestSubscriber] OnBuildingPlaced → {data.buildingName} at ({cell.col}, {cell.row})");
        }

        private void HandlePlacementCancelled()
        {
            Debug.Log("[EventTestSubscriber] OnPlacementCancelled");
        }

        private void HandleGameStateChanged(GameManager.GameState newState)
        {
            Debug.Log($"[EventTestSubscriber] OnGameStateChanged → {newState}");
        }
    }
}
