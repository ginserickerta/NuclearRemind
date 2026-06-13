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
            EventManager.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.OnPlacementCancelled += HandlePlacementCancelled;
            EventManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            EventManager.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.OnPlacementCancelled -= HandlePlacementCancelled;
            EventManager.OnGameStateChanged -= HandleGameStateChanged;
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
