using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// เอฟเฟกต์ "เล่นแสง" ให้ไอคอน/โลโก้ UI — หายใจเรืองแสงเป็นจังหวะ (breathing glow)
    /// วิธี: pulse ค่า alpha ของ Outline (รัศมีเรืองแสงรอบสไปรต์) ด้วยคลื่นไซน์ · ไม่แตะ Image.color
    /// จึงไม่ชนกับ logic ที่เปลี่ยนสีไอคอน (เช่นโหมดที่เลือก=ขาว/ไม่เลือก=หรี่ ใน CoreTowerPanelUI.Refresh)
    ///
    /// ใช้ Time.unscaledTime → เรืองแสงต่อแม้เกม pause (แผง CORE TOWER เปิดตอน timeScale 0)
    /// phase เหลื่อมกันต่อ instance → ไอคอนหลายตัวไม่กะพริบพร้อมกันเป๊ะ (ดูมีชีวิต)
    /// เปิด brightenImage ได้ถ้าอยากให้สไปรต์สว่างวูบตามด้วย (สำหรับไอคอนที่ Refresh ไม่ทับสี)
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIGlowPulse : MonoBehaviour
    {
        public float speed = 2.0f;                       // ความถี่การเต้น (เร็ว = กระพริบไว)
        public Color glowColor = new Color(1f, 0.85f, 0.45f, 1f);
        public float haloMin = 0.12f;                    // alpha รัศมีต่ำสุด
        public float haloMax = 0.65f;                    // alpha รัศมีสูงสุด
        public float haloDistance = 5f;                  // ขนาดรัศมี (px)

        public bool brightenImage = false;               // true → pulse ความสว่างสไปรต์ด้วย (ไอคอนสี ไม่ใช่ขาวล้วน)
        public float imgMin = 0.92f, imgMax = 1.25f;

        private Image _img;
        private Color _imgBase;
        private Outline _halo;
        private float _phase;

        private void Awake()
        {
            _img = GetComponent<Image>();
            if (_img != null) _imgBase = _img.color;

            _halo = GetComponent<Outline>();
            if (_halo == null) _halo = gameObject.AddComponent<Outline>();
            _halo.useGraphicAlpha = false;

            _phase = (GetInstanceID() & 0x3ff) / 1023f * (Mathf.PI * 2f); // เหลื่อมเฟสตาม instance
        }

        private void OnDisable()
        {
            if (_halo != null) { var c = _halo.effectColor; c.a = 0f; _halo.effectColor = c; }
            if (brightenImage && _img != null) _img.color = _imgBase;
        }

        private void Update()
        {
            float s = (Mathf.Sin(Time.unscaledTime * speed + _phase) + 1f) * 0.5f; // 0..1

            if (_halo != null)
            {
                _halo.effectDistance = new Vector2(haloDistance, haloDistance); // setter มี equality-guard → ตั้งจริงครั้งแรก
                var c = glowColor;
                c.a = Mathf.Lerp(haloMin, haloMax, s);
                _halo.effectColor = c;
            }

            if (brightenImage && _img != null)
            {
                float m = Mathf.Lerp(imgMin, imgMax, s);
                _img.color = new Color(
                    Mathf.Min(_imgBase.r * m, 1f),
                    Mathf.Min(_imgBase.g * m, 1f),
                    Mathf.Min(_imgBase.b * m, 1f),
                    _imgBase.a);
            }
        }

        /// <summary>ติด glow ให้ไอคอน (สร้าง/คืน component เดิม) พร้อมตั้งสี+ความแรง</summary>
        public static UIGlowPulse Attach(GameObject go, Color color, float min = 0.12f, float max = 0.65f,
                                         float dist = 5f, float spd = 2.0f, bool brighten = false)
        {
            var g = go.GetComponent<UIGlowPulse>();
            if (g == null) g = go.AddComponent<UIGlowPulse>();
            g.glowColor = color; g.haloMin = min; g.haloMax = max; g.haloDistance = dist;
            g.speed = spd; g.brightenImage = brighten;
            return g;
        }
    }
}
