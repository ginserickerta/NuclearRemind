using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: สร้าง sprite มุมโค้งสีขาวแบบ 9-slice ขึ้นตอนรัน (ไม่ต้องมีไฟล์ .png / shader)
    /// ใช้ทำ border-radius ให้ panel ที่สร้างด้วยโค้ด — cache หนึ่งชิ้นต่อรัศมี ย้อมสีผ่าน Image.color ได้เลย
    ///
    /// Runtime-generated rounded-corner sprites for code-built uGUI (no .png assets, no shaders).
    /// Each radius gets one tiny white 9-sliced texture, cached for the app's lifetime; tint it
    /// through Image.color exactly like a flat panel. This is how the CSS-mockup border-radius
    /// values (3/4/6/8/12 px) are reproduced — see ResearchLabPanelUI.
    ///
    /// Usage:  img.sprite = RoundedSprite.Get(6); img.type = Image.Type.Sliced;
    /// </summary>
    public static class RoundedSprite
    {
        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        /// <summary>
        /// [TH] คืน sprite ขาว 9-slice มุมโค้งรัศมี radius px — สร้างครั้งแรกครั้งเดียวแล้ว cache ตลอดอายุเกม
        /// White 9-sliced sprite whose corners are circles of the given pixel radius.</summary>
        public static Sprite Get(int radius)
        {
            radius = Mathf.Max(1, radius);
            if (_cache.TryGetValue(radius, out var s) && s != null) return s;

            // Texture just big enough for the four corner circles + a 2px stretchable center band.
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"RoundedSprite_{radius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color32[size * size];
            float half = 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the pixel center to the rounded-rect edge (corner circles only —
                    // pixels inside the cross-shaped middle are always fully opaque).
                    float cx = Mathf.Clamp(x + half, radius, size - radius);
                    float cy = Mathf.Clamp(y + half, radius, size - radius);
                    float dist = Vector2.Distance(new Vector2(x + half, y + half), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - dist + half); // 1px antialiased rim
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true); // no mips, non-readable after upload

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1)); // 9-slice border
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _cache[radius] = sprite;
            return sprite;
        }
    }
}
