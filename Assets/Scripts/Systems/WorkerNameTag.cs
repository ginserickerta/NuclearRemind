using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// The worker's name, printed under their feet. Child of a WorkerView GO, so it follows the sprite
    /// for free — same arrangement as WorkerHealthBadge, which sits above the head.
    ///
    /// Drawn as two TextMesh copies: a near-black one offset a hair down-right, and the real one on top.
    /// A single flat label is unreadable over the isometric ground, whose brightness changes tile to
    /// tile, and TextMesh has no outline of its own; the offset copy is the cheapest thing that reads on
    /// both light sand and dark rock.
    /// </summary>
    public class WorkerNameTag : MonoBehaviour
    {
        // Just under the health badge (30000) so a crowded cluster never hides a status behind a name.
        private const int TagOrder = 29900;
        private const float TextScale = 0.028f; // world size tuner — TextMesh renders at font pixel size
        private const float FeetOffset = -0.14f; // below the sprite pivot, which sits at the feet
        private const float ShadowOffset = 0.035f;

        private static readonly Color CName   = new Color(0.96f, 0.95f, 0.90f, 1f);
        private static readonly Color CShadow = new Color(0f, 0f, 0f, 0.85f);

        private static Font _font;

        private TextMesh _text, _shadow;
        private string _last;
        private bool _built;

        /// <summary>Build the tag under the given body sprite. Call once right after AddComponent.</summary>
        public void Init(SpriteRenderer body)
        {
            if (_built) return;
            _built = true;

            transform.localPosition = new Vector3(0f, FeetOffset, 0f);
            string layer = body != null ? body.sortingLayerName : "Default";

            _shadow = MakeText("NameShadow", layer, TagOrder, CShadow,
                               new Vector3(ShadowOffset, -ShadowOffset, 0f));
            _text = MakeText("NameText", layer, TagOrder + 1, CName, Vector3.zero);

            // Ride the worker's depth (same reason as the health badge) so a name never shows through a
            // building the worker is hidden behind.
            var view = GetComponentInParent<WorkerView>();
            if (view != null)
            {
                view.AddSortFollower(_shadow.GetComponent<MeshRenderer>(), 1);
                view.AddSortFollower(_text.GetComponent<MeshRenderer>(), 2);
            }
        }

        private TextMesh MakeText(string name, string layer, int order, Color color, Vector3 localOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = Vector3.one * TextScale;

            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.UpperCenter; // hangs down from the feet
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 36;
            tm.characterSize = 1f;
            tm.color = color;

            var font = NameFont();
            var mr = go.GetComponent<MeshRenderer>();
            if (font != null)
            {
                tm.font = font;
                mr.sharedMaterial = font.material; // dynamic fonts need their own atlas material
            }
            mr.sortingLayerName = layer;
            mr.sortingOrder = order;
            return tm;
        }

        /// <summary>Set the shown name (no-op if unchanged — cheap to call every sync).</summary>
        public void SetName(string displayName)
        {
            if (!_built || displayName == _last) return;
            _last = displayName;
            if (_text != null) _text.text = displayName ?? "";
            if (_shadow != null) _shadow.text = displayName ?? "";
        }

        /// <summary>Names are Latin now, so an OS UI font is enough — no project font asset required.</summary>
        private static Font NameFont()
        {
            if (_font != null) return _font;
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI Semibold", "Segoe UI", "Tahoma", "Arial" }, 36);
            return _font;
        }
    }
}
