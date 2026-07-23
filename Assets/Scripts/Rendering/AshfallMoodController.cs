using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: แผงจูน "บรรยากาศ ASHFALL DAWN" ของทั้งฉากผ่าน Inspector — แสงหลัก/แสงอาทิตย์,
    /// การเกรดสี (exposure/contrast/split toning), bloom, vignette, grain และสีย้อมพื้น
    /// ปรับค่าแล้วเห็นผลทันทีทั้ง edit/play mode · เป็นชั้นภาพล้วน ไม่แตะ gameplay และไม่ลง SaveData
    ///
    /// ASHFALL DAWN mood tuner — live Inspector control over the Phase A look
    /// (docs/RENDER_PLAN.md). Sits on "__ASHFALL_Volume"; references are wired by
    /// the "Setup Ashfall Render (Phase A)" editor menu.
    ///
    /// Tweak any field in the Inspector and the scene updates immediately
    /// (edit mode and play mode). Values live on this component and are saved
    /// with the scene — tuning done in PLAY mode is lost on exit like any other
    /// scene change, so tune in edit mode (or copy component values before stopping).
    ///
    /// Visual layer only: writes to the Volume profile + Light2D + ground Tilemap.
    /// No gameplay state, nothing persisted in SaveData.
    /// </summary>
    [ExecuteAlways]
    public class AshfallMoodController : MonoBehaviour
    {
        [Header("Scene references (wired by setup menu)")]
        public VolumeProfile profile;          // Ashfall_Dawn.asset
        public Light2D keyLight;               // "Global Light 2D" — cool base + fill, blend style 0 (Multiply)
        public Light2D sunLight;               // "Sun Key Light 2D" — warm dawn, blend style 1 (Additive)
        public Light2D[] fillLights;           // legacy strays — switched off on Apply (folded into keyLight)
        public Tilemap groundTilemap;          // "Ground"
        public Tilemap stoneTilemap;           // "StonePave" — stone plazas/paths (GROUND_PLAN)

        [Header("Lights")]
        public Color keyColor = new Color32(0x8F, 0xA3, 0xB4, 0xFF);
        [Range(0f, 2f)] public float keyIntensity = 1.05f;
        public Color sunColor = new Color32(0xFF, 0xB7, 0x65, 0xFF);
        [Range(0f, 2f)] public float sunIntensity = 0.35f;
        public Color fillColor = new Color(0.85f, 0.92f, 1.00f);
        [Range(0f, 1f)] public float fillIntensity = 0.18f;

        [Header("Grading — exposure / tone")]
        [Range(-2f, 2f)] public float postExposure = 0.4f;
        [Range(-100f, 100f)] public float contrast = 10f;
        [Range(-100f, 100f)] public float saturation = -35f;
        public Color colorFilter = new Color32(0xEE, 0xF1, 0xEC, 0xFF);
        [Range(-100f, 100f)] public float wbTemperature = 8f;
        [Range(-100f, 100f)] public float wbTint = 4f;

        [Header("Grading — split toning (teal/orange)")]
        public Color splitShadows = new Color32(0x2E, 0x5A, 0x66, 0xFF);
        public Color splitHighlights = new Color32(0xE9, 0xA2, 0x4B, 0xFF);
        [Range(-100f, 100f)] public float splitBalance = 0f;

        [Header("Bloom")]
        [Range(0f, 2f)] public float bloomThreshold = 0.9f;
        [Range(0f, 4f)] public float bloomIntensity = 1.0f;
        [Range(0f, 1f)] public float bloomScatter = 0.62f;
        public Color bloomTint = new Color32(0xFF, 0xDF, 0xAF, 0xFF);

        [Header("Vignette / Grain")]
        public Color vignetteColor = new Color32(0x07, 0x0A, 0x0D, 0xFF);
        [Range(0f, 1f)] public float vignetteIntensity = 0.30f;
        [Range(0f, 1f)] public float vignetteSmoothness = 0.42f;
        [Range(0f, 1f)] public float grainIntensity = 0.20f;

        [Header("Ground")]
        // ⚠ Tile Color Painter owns per-cell grass colour (stored in OreDepositManager.tileColorOverrides).
        //    A blanket tint here overwrites every cell with one flat colour and destroys that work —
        //    which is exactly what used to happen on entering play mode. Off by default; only turn it on
        //    if you are NOT hand-painting the ground.
        [Tooltip("⚠ ทับสีพื้นทุกช่องด้วยสีเดียว — จะลบสีที่ทาเองด้วย Tile Color Painter ทิ้ง " +
                 "เปิดเฉพาะตอนที่ไม่ได้ทาสีเอง")]
        public bool applyGroundTint = false;
        public Color groundTint = new Color32(0xA6, 0xAC, 0xA0, 0xFF);
        [Tooltip("ทับสีหินทุกช่องด้วยสีเดียว (StonePave) — ปิดถ้าจะทาสีหินเองทีละช่อง")]
        public bool applyStoneTint = true;
        [Tooltip("สีหินปูพื้น (StonePave) — แยกจากหญ้า ปกติอ่อนกว่าเล็กน้อย")]
        public Color stoneTint = new Color32(0xB8, 0xBC, 0xB4, 0xFF);

        private bool _dirty = true;             // apply once on load, then on any change
        private Color _appliedGroundTint = default;
        private Color _appliedStoneTint = default;

        private void OnEnable() { _dirty = true; }
        private void OnValidate() { _dirty = true; } // any Inspector change

        private void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            Apply();
        }

        // [TH] อัดค่าทุก field ลง Volume profile + ไฟ + พื้น — เรียกซ้ำได้ปลอดภัย
        /// <summary>Push all fields into profile + lights + ground. Safe to call any time.</summary>
        [ContextMenu("Apply Now")]
        public void Apply()
        {
            ApplyLights();
            ApplyGrading();
            ApplyGround();
#if UNITY_EDITOR
            // Persist profile edits when tuning in edit mode (no-op in play mode/build)
            if (!Application.isPlaying && profile != null)
                UnityEditor.EditorUtility.SetDirty(profile);
#endif
        }

        // [TH] จัดไฟ: URP 2D ยอมให้มี global light ได้แค่ 1 ดวงต่อ blend style — เลยรวมแสงหลัก+fill
        //      เป็นดวงเดียว (Multiply) ส่วนแสงอาทิตย์อยู่อีก style (Additive) · ไฟเกินถูกปิดกันคอนโซลสแปม
        /// <summary>
        /// ★ URP 2D renders exactly ONE global light per blend style per sorting layer — any extra is
        /// dropped and logs "More than one global light on layer …" on every OnEnable. So the key and
        /// the fill share a single style-0 (Multiply) light: their colours are composited here, and
        /// the warm sun lives on style 1 (Additive). Any leftover fill object is switched off rather
        /// than left to spam the console (the scene fix is NuclearReMind/Fix 2D Global Lights).
        /// </summary>
        private void ApplyLights()
        {
            if (keyLight != null)
            {
                keyLight.color = CompositeColor(keyColor, keyIntensity, fillColor, fillIntensity);
                keyLight.intensity = CompositeIntensity(keyColor, keyIntensity, fillColor, fillIntensity);
                keyLight.blendStyleIndex = StyleMultiply;
            }
            if (sunLight != null)
            {
                sunLight.color = sunColor;
                sunLight.intensity = sunIntensity;
                sunLight.blendStyleIndex = StyleAdditive; // never style 0 — it would collide with the key
            }

            if (fillLights == null) return;
            foreach (var f in fillLights)
            {
                if (f == null || f == keyLight || f == sunLight || !f.gameObject.activeSelf) continue;
                f.gameObject.SetActive(false); // folded into the key above — leaving it on = error spam
                Debug.LogWarning($"[AshfallMood] ปิด '{f.name}' — global light เกิน 1 ดวงบน blend style เดียวกัน " +
                                 "(รัน NuclearReMind/Fix 2D Global Lights เพื่อลบออกจากซีนถาวร)");
            }
        }

        private const int StyleMultiply = 0; // Assets/Settings/Renderer2D.asset: 0 Multiply · 1 Additive
        private const int StyleAdditive = 1;

        private static float CompositeIntensity(Color a, float ai, Color b, float bi)
            => Mathf.Max(a.r * ai + b.r * bi, Mathf.Max(a.g * ai + b.g * bi, a.b * ai + b.b * bi));

        private static Color CompositeColor(Color a, float ai, Color b, float bi)
        {
            float i = CompositeIntensity(a, ai, b, bi);
            if (i <= 0.0001f) return Color.white;
            return new Color((a.r * ai + b.r * bi) / i, (a.g * ai + b.g * bi) / i, (a.b * ai + b.b * bi) / i);
        }

        // [TH] อัดค่าการเกรดสีทั้งหมดลง Volume profile (มี override ไหนก็เซ็ตอันนั้น)
        private void ApplyGrading()
        {
            if (profile == null) return;

            if (profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.value = postExposure;
                ca.contrast.value = contrast;
                ca.saturation.value = saturation;
                ca.colorFilter.value = colorFilter;
            }
            if (profile.TryGet<WhiteBalance>(out var wb))
            {
                wb.temperature.value = wbTemperature;
                wb.tint.value = wbTint;
            }
            if (profile.TryGet<SplitToning>(out var split))
            {
                split.shadows.value = splitShadows;
                split.highlights.value = splitHighlights;
                split.balance.value = splitBalance;
            }
            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.threshold.value = bloomThreshold;
                bloom.intensity.value = bloomIntensity;
                bloom.scatter.value = bloomScatter;
                bloom.tint.value = bloomTint;
            }
            if (profile.TryGet<Vignette>(out var vig))
            {
                vig.color.value = vignetteColor;
                vig.intensity.value = vignetteIntensity;
                vig.smoothness.value = vignetteSmoothness;
            }
            if (profile.TryGet<FilmGrain>(out var grain))
            {
                grain.intensity.value = grainIntensity;
            }
        }

        // [TH] ย้อมสีพื้นหญ้า/หิน — เฉพาะเมื่อเปิดสวิตช์ (กันไปทับสีที่ทามือด้วย Tile Color Painter)
        private void ApplyGround()
        {
            // Grass colour belongs to Tile Color Painter / GridSpriteFiller — never stomp it unasked
            if (applyGroundTint && groundTint != _appliedGroundTint)
            {
                _appliedGroundTint = groundTint;
                TintTilemap(groundTilemap, groundTint);
            }
            if (applyStoneTint && stoneTint != _appliedStoneTint)
            {
                _appliedStoneTint = stoneTint;
                TintTilemap(stoneTilemap, stoneTint);
            }
        }

        // Per-tile tint (tiles default to LockColor, so Tilemap.color alone is ignored)
        private static void TintTilemap(Tilemap map, Color tint)
        {
            if (map == null) return;
            foreach (var pos in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(pos)) continue;
                map.SetTileFlags(pos, TileFlags.None);
                map.SetColor(pos, tint);
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(map);
#endif
        }
    }
}
