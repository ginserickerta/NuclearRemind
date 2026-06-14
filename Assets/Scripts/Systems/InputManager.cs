using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ input จากเมาส์ และแปลงตำแหน่งคลิกเป็นพิกัด grid (col, row)
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        private Camera mainCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                HandleLeftClick();

            if (Input.GetMouseButtonDown(1))
                HandleRightClick();

            if (Input.GetKeyDown(KeyCode.F5))
                EventManager.Instance.RaiseSaveRequested();

            if (Input.GetKeyDown(KeyCode.F9))
                EventManager.Instance.RaiseLoadRequested();
        }

        /// <summary>
        /// คลิกซ้าย: แปลงตำแหน่งเมาส์เป็นพิกัด grid และ log
        /// </summary>
        private void HandleLeftClick()
        {
            Vector2Int gridPos = GetMouseGridPosition();
            Debug.Log($"[InputManager] Left click → Grid ({gridPos.x}, {gridPos.y})");
        }

        /// <summary>
        /// คลิกขวา: ยกเลิกคำสั่ง
        /// </summary>
        private void HandleRightClick()
        {
            Debug.Log("[InputManager] Right click → Cancel");
        }

        /// <summary>
        /// แปลงตำแหน่งเมาส์ปัจจุบันใน world space เป็นพิกัด grid (col, row)
        /// </summary>
        public Vector2Int GetMouseGridPosition()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            return GridManager.Instance.WorldToIso(worldPos);
        }
    }
}
