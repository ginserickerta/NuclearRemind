using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แสดง overlay สีบน cell ที่ได้รับพลังงานจาก PowerGridManager
    /// กด P เพื่อเปิด/ปิด overlay
    /// </summary>
    public class PowerGridVisual : MonoBehaviour
    {
        [Header("สีและ opacity ของ overlay")]
        public Color poweredCellColor   = new Color(0.15f, 0.75f, 1f, 0.22f);
        public Color unpoweredCellColor = new Color(0.8f,  0.2f,  0.1f, 0.18f);

        [Header("Toggle key")]
        public KeyCode toggleKey = KeyCode.P;

        [Header("แสดง cell ที่มีอาคารแต่ไม่ได้รับพลังงาน")]
        public bool showUnpoweredBuildings = true;

        private bool    _visible = true;
        private Sprite  _tileSprite;
        private Transform _container;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _poweredOverlays   = new();
        private readonly Dictionary<Vector2Int, SpriteRenderer> _unpoweredOverlays = new();

        private void Awake()
        {
            _tileSprite = CreateDiamondSprite(100, 50);
            _container  = new GameObject("PowerGridOverlayContainer").transform;
            _container.SetParent(transform, false);
        }

        private void OnEnable()
        {
            EventManager.Instance.OnPowerGridChanged += HandlePowerGridChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnPowerGridChanged -= HandlePowerGridChanged;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _visible = !_visible;
                _container.gameObject.SetActive(_visible);
            }
        }

        private void HandlePowerGridChanged(HashSet<Vector2Int> poweredCells)
        {
            ClearOverlays();
            if (!_visible) return;

            var grid = GridManager.Instance;
            if (grid == null) return;

            // สร้าง overlay สำหรับ powered cells
            foreach (var cell in poweredCells)
                CreateOverlay(_poweredOverlays, cell, grid.IsoToWorld(cell.x, cell.y), poweredCellColor, 1);

            // สร้าง overlay สำหรับ occupied + unpowered
            if (showUnpoweredBuildings && BuildingRegistry.Instance != null)
            {
                foreach (var kvp in BuildingRegistry.Instance.PlacedBuildings)
                {
                    if (poweredCells.Contains(kvp.Key)) continue;
                    CreateOverlay(_unpoweredOverlays, kvp.Key,
                        grid.IsoToWorld(kvp.Key.x, kvp.Key.y), unpoweredCellColor, 2);
                }
            }
        }

        private void CreateOverlay(Dictionary<Vector2Int, SpriteRenderer> dict,
                                   Vector2Int cell, Vector3 worldPos, Color color, int sortOrder)
        {
            var go = new GameObject($"Overlay_{cell.x}_{cell.y}");
            go.transform.SetParent(_container, false);
            go.transform.position = worldPos;

            // scale sprite ให้ตรงกับ tile ปัจจุบัน (sprite base = 1×0.5 units)
            if (GridManager.Instance != null)
            {
                float s = GridManager.Instance.tileWidth;
                go.transform.localScale = new Vector3(s, s, 1f);
            }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _tileSprite;
            sr.color        = color;
            sr.sortingOrder = sortOrder;

            dict[cell] = sr;
        }

        private void ClearOverlays()
        {
            foreach (var sr in _poweredOverlays.Values)
                if (sr != null) Destroy(sr.gameObject);
            foreach (var sr in _unpoweredOverlays.Values)
                if (sr != null) Destroy(sr.gameObject);

            _poweredOverlays.Clear();
            _unpoweredOverlays.Clear();
        }

        /// <summary>สร้าง diamond sprite แบบ procedural ที่ตรงกับรูปร่าง isometric tile</summary>
        private static Sprite CreateDiamondSprite(int w, int h)
        {
            var tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w - 0.5f;
                    float ny = (float)y / h - 0.5f;
                    bool inside = Mathf.Abs(nx) + Mathf.Abs(ny) < 0.49f; // 0.49 เว้น border เล็กน้อย
                    pixels[y * w + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }

            tex.SetPixels32(pixels);
            tex.Apply();
            // pixels per unit = 100 → 100px = 1 unit (tileWidth), 50px = 0.5 unit (tileHeight)
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
