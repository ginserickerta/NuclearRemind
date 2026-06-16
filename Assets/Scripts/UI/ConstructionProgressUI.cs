using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แสดง progress text "x/10" ในโลก (world-space) เหนืออาคารที่กำลังสร้าง
    /// สร้างโดย ConstructionController ตอน OnBuildingPlaced
    /// ทำลายตัวเองเมื่อ construction เสร็จหรือถูก cancel
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class ConstructionProgressUI : MonoBehaviour
    {
        private Vector2Int _cell;
        private TextMesh _text;
        private bool _initialized;

        public void Init(Vector2Int cell, int initialProgress)
        {
            _cell = cell;
            _text = GetComponent<TextMesh>();
            _initialized = true;
            UpdateText(initialProgress);
        }

        private void OnEnable()
        {
            EventManager.Instance.OnConstructionProgressChanged += HandleProgressChanged;
            EventManager.Instance.OnConstructionComplete        += HandleComplete;
            EventManager.Instance.OnBuildingRemoved             += HandleRemoved;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnConstructionProgressChanged -= HandleProgressChanged;
            EventManager.Instance.OnConstructionComplete        -= HandleComplete;
            EventManager.Instance.OnBuildingRemoved             -= HandleRemoved;
        }

        private void HandleProgressChanged(Vector2Int cell, int progress)
        {
            if (!_initialized || cell != _cell) return;
            UpdateText(progress);
        }

        private void HandleComplete(Vector2Int cell, BuildingData _)
        {
            if (_initialized && cell == _cell) Destroy(gameObject);
        }

        private void HandleRemoved(Vector2Int cell)
        {
            if (_initialized && cell == _cell) Destroy(gameObject);
        }

        private void UpdateText(int progress)
        {
            if (_text != null)
                _text.text = $"{progress}/{ConstructionController.TotalConstructionTicks}";
        }
    }
}
