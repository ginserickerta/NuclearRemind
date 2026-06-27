using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แสดง progress bar (world-space) เหนืออาคารที่กำลังสร้าง — แทนตัวเลข "x/10" เดิม
    /// สร้าง bar เอง (background + fill) ด้วย SpriteRenderer จึงไม่ต้องพึ่ง sprite asset
    /// และซ่อน TextMesh เดิมให้อัตโนมัติ (รองรับ prefab เวอร์ชันตัวเลขที่ยังค้างอยู่)
    /// สร้างโดย ConstructionController ตอน OnBuildingPlaced — ทำลายตัวเองเมื่อเสร็จหรือถูก cancel
    /// </summary>
    public class ConstructionProgressUI : MonoBehaviour
    {
        // ขนาด bar (หน่วย world)
        private const float BarWidth  = 1.0f;
        private const float BarHeight = 0.16f;
        private const int   SortBg    = 10;
        private const int   SortFill  = 11;

        private static readonly Color BgColor   = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        private static readonly Color FillColor = new Color(0.30f, 0.85f, 1.00f, 1f);

        private static Sprite _whiteSprite;

        private Vector2Int _cell;
        private Transform _fill;
        private bool _initialized;

        public void Init(Vector2Int cell, int initialProgress)
        {
            _cell = cell;
            BuildBar();
            _initialized = true;
            UpdateBar(initialProgress);
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
            UpdateBar(progress);
        }

        private void HandleComplete(Vector2Int cell, BuildingData _)
        {
            if (_initialized && cell == _cell) Destroy(gameObject);
        }

        private void HandleRemoved(Vector2Int cell)
        {
            if (_initialized && cell == _cell) Destroy(gameObject);
        }

        // ───────────────────────── bar construction ─────────────────────────

        private void BuildBar()
        {
            // ซ่อนการแสดงผลแบบตัวเลขเดิม (ถ้า prefab ยังมี TextMesh จากเวอร์ชันก่อน)
            var oldText = GetComponent<TextMesh>();
            if (oldText != null) oldText.text = string.Empty;
            var rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;

            var sprite = GetWhiteSprite();

            var bg = CreateQuad("Bar_BG", sprite, BgColor, SortBg);
            bg.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);

            var fillGO = CreateQuad("Bar_Fill", sprite, FillColor, SortFill);
            _fill = fillGO.transform;
            _fill.localScale = new Vector3(0f, BarHeight, 1f);
        }

        private GameObject CreateQuad(string childName, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private void UpdateBar(int progress)
        {
            if (_fill == null) return;
            float t = Mathf.Clamp01((float)progress / ConstructionController.TotalConstructionTicks);

            // โตจากซ้ายไปขวา: ย่อ scale.x แล้วเลื่อน fill ไปทางซ้ายให้ขอบซ้ายตรงกับ background
            _fill.localScale    = new Vector3(BarWidth * t, BarHeight, 1f);
            _fill.localPosition = new Vector3(-BarWidth * 0.5f + BarWidth * t * 0.5f, 0f, 0f);
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSprite;
        }
    }
}
