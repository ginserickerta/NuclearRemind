using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Health emoji badge floating above a worker sprite (v6.3 §17 status).
    /// Child of a WorkerView GO — follows the sprite automatically.
    ///
    /// Renders an emoji glyph (monochrome, via the OS "Segoe UI Emoji" dynamic font) tinted by
    /// status, over a soft dark circle so the status stays readable even if a glyph can't render.
    /// Color alone conveys health; the emoji is the nicety.
    /// </summary>
    public class WorkerHealthBadge : MonoBehaviour
    {
        // Sorting so badges draw above every worker/building. (Unit sorting orders are far below this.)
        private const int BadgeOrder = 30000;
        private const float TextScale = 0.045f;   // TextMesh world size tuner — bump if the glyph is tiny
        private const float BgScale = 0.34f;

        private static Font _emojiFont;
        private static Sprite _circle;

        private TextMesh _text;
        private MeshRenderer _textMr;
        private SpriteRenderer _bg;
        private WorkerStatus _last = (WorkerStatus)(-1);
        private bool _built;

        /// <summary>Build the badge above the given body sprite. Call once right after AddComponent.</summary>
        public void Init(SpriteRenderer body)
        {
            if (_built) return;
            _built = true;

            float headY = (body != null && body.sprite != null) ? body.sprite.bounds.max.y + 0.12f : 0.5f;
            transform.localPosition = new Vector3(0f, headY, 0f);
            string layer = body != null ? body.sortingLayerName : "Default";

            // dark backing circle
            var bgGo = new GameObject("BadgeBg");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localScale = Vector3.one * BgScale;
            _bg = bgGo.AddComponent<SpriteRenderer>();
            _bg.sprite = Circle();
            _bg.color = new Color(0f, 0f, 0f, 0.55f);
            _bg.sortingLayerName = layer;
            _bg.sortingOrder = BadgeOrder;

            // emoji glyph
            var tGo = new GameObject("BadgeText");
            tGo.transform.SetParent(transform, false);
            tGo.transform.localScale = Vector3.one * TextScale;
            _text = tGo.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontSize = 40;
            _text.characterSize = 1f;
            var font = EmojiFont();
            if (font != null)
            {
                _text.font = font;
                _textMr = tGo.GetComponent<MeshRenderer>();
                _textMr.sharedMaterial = font.material; // dynamic fonts need the font's own atlas material
            }
            else
            {
                _textMr = tGo.GetComponent<MeshRenderer>();
            }
            if (_textMr != null)
            {
                _textMr.sortingLayerName = layer;
                _textMr.sortingOrder = BadgeOrder + 1;
            }
        }

        /// <summary>Set the shown status (no-op if unchanged — cheap to call every refresh).</summary>
        public void SetStatus(WorkerStatus status)
        {
            if (!_built || status == _last) return;
            _last = status;

            var c = StatusColor(status);
            if (_text != null)
            {
                _text.text = StatusEmoji(status);
                _text.color = c;
            }
            // tint the circle a touch toward the status color so color reads even without the glyph
            if (_bg != null)
                _bg.color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.6f);
        }

        // ── status → glyph / color (GDD §17 table) ─────────────────
        private static string StatusEmoji(WorkerStatus s)
        {
            switch (s)
            {
                case WorkerStatus.Healthy:   return "🙂";
                case WorkerStatus.Tired:     return "😪";
                case WorkerStatus.Exhausted: return "😵";
                case WorkerStatus.Hungry:    return "🍚";
                case WorkerStatus.Sick:      return "🤢";
                case WorkerStatus.Dying:     return "💀";
                case WorkerStatus.Dead:      return "⚰";
                default:                     return "?";
            }
        }

        private static Color StatusColor(WorkerStatus s)
        {
            switch (s)
            {
                case WorkerStatus.Healthy:   return new Color(0.50f, 0.90f, 0.50f);
                case WorkerStatus.Tired:     return new Color(0.95f, 0.85f, 0.35f);
                case WorkerStatus.Exhausted: return new Color(0.95f, 0.60f, 0.25f);
                case WorkerStatus.Hungry:    return new Color(0.98f, 0.78f, 0.30f);
                case WorkerStatus.Sick:      return new Color(0.78f, 0.50f, 0.95f);
                case WorkerStatus.Dying:     return new Color(0.97f, 0.35f, 0.35f);
                default:                     return new Color(0.55f, 0.55f, 0.55f);
            }
        }

        // ── shared assets ──────────────────────────────────────────
        private static Font EmojiFont()
        {
            if (_emojiFont != null) return _emojiFont;
            // Windows 11 ships Segoe UI Emoji; dynamic font renders the glyph shapes (monochrome).
            _emojiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI Emoji", "Segoe UI Symbol", "Segoe UI", "Arial" }, 40);
            return _emojiFont;
        }

        private static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int d = 64;
            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[d * d];
            float c = (d - 1) * 0.5f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01((1f - dist) * 4f); // solid center, soft edge
                    px[y * d + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), d);
            return _circle;
        }
    }
}
