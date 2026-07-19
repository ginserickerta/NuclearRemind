using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Health face floating above a worker sprite (v6.3 §17 status).
    /// Child of a WorkerView GO — follows the sprite automatically.
    ///
    /// The face is DRAWN, not typed. This used to be a TextMesh printing emoji, which never rendered:
    /// 🙂 is U+1F642 and six of the seven glyphs sit outside the Basic Multilingual Plane, so in C# they
    /// are surrogate PAIRS. Unity's legacy Text/TextMesh walk a string one UTF-16 char at a time and see
    /// two broken halves instead of one character — no font can fix that, because the limit is in the
    /// renderer. (⚰ U+26B0 is the lone BMP glyph, so "dead" was the only status that could ever appear.)
    ///
    /// Generating the faces as sprites also drops the dependency on the machine's emoji font, which
    /// matters for a build that has to run on somebody else's PC.
    ///
    /// Colour still carries the meaning on its own; the face is the nicety.
    /// </summary>
    public class WorkerHealthBadge : MonoBehaviour
    {
        // Sorting so badges draw above every worker/building. (Unit sorting orders are far below this.)
        private const int BadgeOrder = 30000;
        private const float FaceScale = 0.30f;
        private const float BgScale = 0.34f;

        private static Sprite _circle;
        private static readonly Dictionary<WorkerStatus, Sprite> _faces = new Dictionary<WorkerStatus, Sprite>();

        private SpriteRenderer _face;
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

            // face
            var fGo = new GameObject("BadgeFace");
            fGo.transform.SetParent(transform, false);
            fGo.transform.localScale = Vector3.one * FaceScale;
            _face = fGo.AddComponent<SpriteRenderer>();
            _face.sortingLayerName = layer;
            _face.sortingOrder = BadgeOrder + 1;
        }

        /// <summary>Set the shown status (no-op if unchanged — cheap to call every refresh).</summary>
        public void SetStatus(WorkerStatus status)
        {
            if (!_built || status == _last) return;
            _last = status;

            var c = StatusColor(status);
            if (_face != null)
            {
                _face.sprite = Face(status);
                _face.color = c;
            }
            // tint the circle a touch toward the status color so color reads even at a glance
            if (_bg != null)
                _bg.color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.6f);
        }

        // ── status → colour (GDD §17 table) ────────────────────────
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

        // ═══════════════ drawn faces ═══════════════
        // Drawn white and tinted by SpriteRenderer.color, so one texture per status is all that's needed
        // and the palette above stays the single source of truth for what a status looks like.

        private const int D = 64;                 // texture size
        private const float EyeY = 40f, EyeLX = 22f, EyeRX = 42f;

        private static Sprite Face(WorkerStatus s)
        {
            if (_faces.TryGetValue(s, out var cached) && cached != null) return cached;

            var px = new Color[D * D];            // starts fully transparent
            switch (s)
            {
                case WorkerStatus.Healthy:        // dot eyes + smile
                    Dot(px, EyeLX, EyeY, 4.5f); Dot(px, EyeRX, EyeY, 4.5f);
                    Arc(px, 32f, 34f, 14f, 4f, 200f, 340f);
                    break;

                case WorkerStatus.Tired:          // half-closed eyes + flat mouth
                    Line(px, 16f, EyeY, 28f, EyeY, 4f); Line(px, 36f, EyeY, 48f, EyeY, 4f);
                    Line(px, 24f, 22f, 40f, 22f, 4f);
                    break;

                case WorkerStatus.Exhausted:      // X eyes + open mouth
                    Cross(px, EyeLX, EyeY, 6f, 3.5f); Cross(px, EyeRX, EyeY, 6f, 3.5f);
                    Arc(px, 32f, 21f, 6f, 3.5f, 0f, 360f);
                    break;

                case WorkerStatus.Hungry:         // dot eyes + wide open mouth
                    Dot(px, EyeLX, EyeY, 4.5f); Dot(px, EyeRX, EyeY, 4.5f);
                    Dot(px, 32f, 20f, 7.5f);
                    break;

                case WorkerStatus.Sick:           // dot eyes + wavy mouth
                    Dot(px, EyeLX, EyeY, 4.5f); Dot(px, EyeRX, EyeY, 4.5f);
                    Arc(px, 26f, 22f, 5f, 3f, 0f, 180f);
                    Arc(px, 38f, 22f, 5f, 3f, 180f, 360f);
                    break;

                case WorkerStatus.Dying:          // X eyes + frown
                    Cross(px, EyeLX, EyeY, 6f, 3.5f); Cross(px, EyeRX, EyeY, 6f, 3.5f);
                    Arc(px, 32f, 12f, 12f, 4f, 25f, 155f);
                    break;

                default:                          // Dead — X eyes, flat mouth
                    Cross(px, EyeLX, EyeY, 6f, 3.5f); Cross(px, EyeRX, EyeY, 6f, 3.5f);
                    Line(px, 24f, 20f, 40f, 20f, 3.5f);
                    break;
            }

            var tex = new Texture2D(D, D, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f), D);
            _faces[s] = sprite;
            return sprite;
        }

        // ── pixel helpers. Each writes white with a soft edge, keeping the strongest alpha so
        //    overlapping strokes join instead of cutting holes in each other. ──

        private static void Plot(Color[] px, int x, int y, float a)
        {
            if (a <= 0f || x < 0 || y < 0 || x >= D || y >= D) return;
            int i = y * D + x;
            if (a > px[i].a) px[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        private static void Dot(Color[] px, float cx, float cy, float r)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= cx + r + 1; x++)
                    Plot(px, x, y, r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)));
        }

        private static void Line(Color[] px, float x0, float y0, float x1, float y1, float thick)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = dx * dx + dy * dy;
            float h = thick * 0.5f;
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - h - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + h + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - h - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + h + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float t = len2 > 0f ? Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2) : 0f;
                    float px0 = x0 + t * dx, py0 = y0 + t * dy;
                    float dist = Mathf.Sqrt((x - px0) * (x - px0) + (y - py0) * (y - py0));
                    Plot(px, x, y, h - dist);
                }
        }

        private static void Cross(Color[] px, float cx, float cy, float size, float thick)
        {
            float h = size * 0.5f;
            Line(px, cx - h, cy - h, cx + h, cy + h, thick);
            Line(px, cx - h, cy + h, cx + h, cy - h, thick);
        }

        /// <summary>Stroke of a circle between two angles (degrees, counter-clockwise, y up).</summary>
        private static void Arc(Color[] px, float cx, float cy, float r, float thick, float from, float to)
        {
            float h = thick * 0.5f;
            int minX = Mathf.FloorToInt(cx - r - h - 1), maxX = Mathf.CeilToInt(cx + r + h + 1);
            int minY = Mathf.FloorToInt(cy - r - h - 1), maxY = Mathf.CeilToInt(cy + r + h + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dist - r) > h) continue;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    if (to - from < 360f && (ang < from || ang > to)) continue;
                    Plot(px, x, y, h - Mathf.Abs(dist - r));
                }
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
