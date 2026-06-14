using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// สร้าง/ลบ SpriteRenderer ของอาคารที่วางแล้วใน scene ตาม OnBuildingPlaced/OnBuildingRemoved
    /// เพื่อให้ตัวอาคารยังคงปรากฏอยู่บน grid หลังจากวางเสร็จ (ghost จะถูกซ่อนไปแล้ว)
    /// </summary>
    public class BuildingVisualSpawner : MonoBehaviour
    {
        private const string BuildingsSortingLayer = "Buildings";

        [Header("Parent transform สำหรับอาคารที่วางแล้ว (Buildings sorting layer)")]
        public Transform buildingsParent;

        private readonly Dictionary<Vector2Int, GameObject> _spawnedVisuals = new Dictionary<Vector2Int, GameObject>();

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            var position = new Vector2Int(cell.col, cell.row);

            var go = new GameObject($"Building_{data.buildingName}_{position.x}_{position.y}");
            go.transform.SetParent(buildingsParent, false);
            go.transform.position = GridManager.Instance.IsoToWorld(cell.col, cell.row);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.sprite;
            spriteRenderer.sortingLayerName = BuildingsSortingLayer;
            spriteRenderer.sortingOrder = cell.col + cell.row;

            _spawnedVisuals[position] = go;
        }

        private void HandleBuildingRemoved(Vector2Int position)
        {
            if (!_spawnedVisuals.TryGetValue(position, out var go))
                return;

            Destroy(go);
            _spawnedVisuals.Remove(position);
        }
    }
}
