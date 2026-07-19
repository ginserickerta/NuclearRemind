using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ตัวช่วยปรับ "เงาอาคาร" ด้วยการลาก handle ใน Scene view (edit-time เท่านั้น — คู่แฝดของ BaseColliderTuner)
    ///   • assign BuildingData → โชว์ตัวอาคาร + เงาจริง (พรีวิวสด) ให้ลากปรับเห็นภาพ
    ///   • ค่าเขียนลง BuildingData.shadowOffset/Squash/Lean/Alpha + overrideShadow = true (กด Ctrl+S เซฟ asset)
    ///   • ค่าอยู่บน asset ไม่ใช่ scene → เข้าเกมแล้วเงาทุกหลังของอาคารชนิดนี้ใช้ค่าที่ตั้งไว้ทันที
    ///   • ไม่มีผลตอน Play (renderer ปิด) · ลบ GameObject นี้ทิ้งได้เมื่อปรับเสร็จ
    /// เปิดใช้ผ่านเมนู NuclearReMind → Tools → Shadow Editor
    ///
    /// ★ ทำไมไม่ใช้ prefab: อาคารทุกหลังถูกประกอบตอนรันไทม์โดย BuildingVisualSpawner (สไปรต์ตามเลเวล +
    ///   collider ตามรูป + BuildingDepthSort ที่หั่นสไปรต์เป็นส่วนบน/ฐาน + ไฟ Light2D ที่ clone จาก
    ///   ออบเจกต์ในซีน ซึ่ง prefab อ้างไม่ได้) — prefab จะเก็บได้จริงแค่ transform ของเงาชิ้นเดียว
    ///   แต่ต้องรื้อ pipeline ทั้งเส้น ค่าเงาบน BuildingData จึงตรงจุดกว่าและพังยากกว่า
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShadowTuner : MonoBehaviour
    {
        public BuildingData data;

        [Tooltip("ดูเงาของสไปรต์ระดับไหน (1/2/3) — อาคารที่มี levelSprites")]
        [Range(1, 3)] public int previewLevel = 1;

        private Transform _shadow;

        private void OnEnable() => Sync();
        private void Update() { if (!Application.isPlaying) Sync(); }

        /// <summary>สไปรต์ที่ใช้พรีวิว (ตาม previewLevel)</summary>
        public Sprite PreviewSprite =>
            data == null ? null : (data.SpriteForLevel(previewLevel) ?? data.sprite);

        // ★ ต้องอยู่ sorting layer บนสุด (WorldUI) ไม่ใช่ order สูง ๆ ใน "Default"
        //   layer Default = ล่างสุดของลิสต์ → พื้น/อาคารทับมิด ต่อให้ order 32000 ก็ไม่โผล่
        private static string TopSortingLayerName()
        {
            var layers = SortingLayer.layers;
            return layers.Length > 0 ? layers[layers.Length - 1].name : "Default";
        }

        // ★ วัสดุแบบไม่รับแสง — ฉากเกรด Ashfall มืด ถ้าใช้ Sprite-Lit-Default พรีวิวจะจมไปกับพื้นดำ
        //   (เหตุผลเดียวกับที่ Tilemap พื้นเคยใช้ Sprites-Default แล้วรอดตอนไฟยังไม่ครบ)
        private static Material _unlitMat;
        private static Material UnlitMat
        {
            get
            {
                if (_unlitMat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    if (sh != null) _unlitMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                }
                return _unlitMat;
            }
        }

        private void Sync()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;

            sr.enabled = !Application.isPlaying;   // ให้เห็นเฉพาะตอน edit
            var sprite = PreviewSprite;
            if (sprite != null) sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingLayerName = TopSortingLayerName();
            sr.sortingOrder = 32000;               // เหนือของอื่นใน layer บนสุด
            if (UnlitMat != null && sr.sharedMaterial != UnlitMat) sr.sharedMaterial = UnlitMat;

            SyncShadowPreview(sprite);
        }

        // เงาพรีวิว = ลูกชื่อ "Shadow" — ใช้สูตรเดียวกับ BuildingVisualSpawner.ConfigureShadow เป๊ะ
        // (ค่าที่เห็นในนี้ = ค่าที่จะได้ตอนเล่นจริง)
        private void SyncShadowPreview(Sprite sprite)
        {
            if (sprite == null || data == null) return;

            if (_shadow == null)
            {
                var found = transform.Find("Shadow");
                if (found == null)
                {
                    var go = new GameObject("Shadow");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<SpriteRenderer>();
                    found = go.transform;
                }
                _shadow = found;
            }

            var sr = _shadow.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            sr.enabled = !Application.isPlaying;
            sr.sprite = sprite;
            sr.sortingLayerName = TopSortingLayerName();
            sr.sortingOrder = 31999;               // ใต้ตัวอาคารพรีวิว แต่ยังเหนือฉาก
            sr.color = new Color(0f, 0f, 0f, data.shadowAlpha);
            if (UnlitMat != null && sr.sharedMaterial != UnlitMat) sr.sharedMaterial = UnlitMat;

            _shadow.localPosition = new Vector3(data.shadowOffset.x, data.shadowOffset.y, 0f);
            _shadow.localRotation = Quaternion.Euler(0f, 0f, data.shadowLeanDegrees);
            _shadow.localScale    = new Vector3(1f, -data.shadowSquash, 1f);
        }
    }
}
