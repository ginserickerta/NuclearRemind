using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: ตั้งค่าโทนภาพ "Ashfall Dawn" (เฟส A) — เปลี่ยน URP เป็น HDR, สร้างโปรไฟล์เกรดสี,
    /// ตั้ง Volume/กล้อง/ไฟ 2D และพื้น ให้ทั้งซีนเป็นโทนรุ่งอรุณหลังหายนะ (งานภาพล้วน ไม่แตะเกมเพลย์)
    /// ASHFALL DAWN — Phase A of docs/RENDER_PLAN.md (post-apocalyptic dawn look).
    /// Pure visual layer: no gameplay, no SaveData, no sorting math touched.
    ///
    /// What it does (idempotent — safe to re-run):
    ///   1. URP asset  : Color Grading Mode -> HDR (required for split-toning + tight bloom threshold)
    ///   2. Profile    : creates Assets/Settings/Ashfall_Dawn.asset with ACES + white balance +
    ///                   color adjustments + split toning (teal/orange) + SMH + bloom + vignette + grain
    ///   3. Scene      : global Volume "__ASHFALL_Volume" (priority 10) — overrides DefaultVolumeProfile
    ///                   values set by ColorGradeSetup/RenderingSetup without destroying them
    ///   4. Camera     : post ON + FXAA + dark background (#0B0E12)
    ///   5. Lights     : "Global Light 2D" white 1.15 -> cool teal-grey 0.72 · new warm "Sun Key Light 2D"
    ///                   global 0.38 · "Fill Light 2D" back down to subtle cool fill
    ///   6. Ground     : Tilemap "Ground" switches to Sprite-Lit-Default (was unlit -> would ignore the
    ///                   darker lighting = RENDER_PLAN §14 pitfall #1) + olive-grey tile tint (#7C7A5E)
    ///
    /// ★ Must run AFTER "Setup Rendering (Bloom + Reactor Glow)" — ColorGradeSetup/LightingMoodSetup
    ///   earlier in the chain stomp global light intensity/color; this setup has the final word.
    /// ★ Revert: delete "__ASHFALL_Volume" + re-run "Lighting Mood" + "Fix Colors" menus.
    /// Run: NuclearReMind/Setup Ashfall Render (Phase A)
    /// </summary>
    public static class AshfallRenderSetup
    {
        private const string ScenePath   = "Assets/Scenes/Gamescene.unity";
        private const string ProfilePath = "Assets/Settings/Ashfall_Dawn.asset";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string VolumeName  = "__ASHFALL_Volume";
        private const string SunLightName = "Sun Key Light 2D";
        private const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // ── Grading (RENDER_PLAN §4 — tweak here, re-run menu) ──
        private const float WbTemperature = 8f;   // was 18: whole frame went yellow — keep warmth in highlights only
        private const float WbTint        = 4f;
        private const float PostExposure  = 0.4f;   // ACES pulls midtones down — compensate to match reference brightness
        private const float Contrast      = 10f;
        private static readonly Color ColorFilter = new Color32(0xEE, 0xF1, 0xEC, 0xFF); // near neutral, hint cool (was warm #FFE9CE)
        private const float Saturation    = -35f;
        private static readonly Color SplitShadows    = new Color32(0x2E, 0x5A, 0x66, 0xFF);
        private static readonly Color SplitHighlights = new Color32(0xE9, 0xA2, 0x4B, 0xFF);
        private const float SplitBalance  = 0f;   // was +12: pushed too much of the frame into the warm side
        private static readonly Vector4 SmhShadows    = new Vector4(0.85f, 0.95f, 1.05f, 0f);
        private static readonly Vector4 SmhHighlights = new Vector4(1.08f, 1.02f, 0.92f, 0f);
        private const float BloomThreshold = 0.9f;
        private const float BloomIntensity = 1.0f;
        private const float BloomScatter   = 0.62f;
        private static readonly Color BloomTint = new Color32(0xFF, 0xDF, 0xAF, 0xFF);
        private static readonly Color VignetteColor = new Color32(0x07, 0x0A, 0x0D, 0xFF);
        private const float VignetteIntensity  = 0.30f;
        private const float VignetteSmoothness = 0.42f;
        private const float GrainIntensity = 0.20f;
        private const float GrainResponse  = 0.8f;

        // ── Lights (RENDER_PLAN §5) ──
        private static readonly Color KeyColor = new Color32(0x8F, 0xA3, 0xB4, 0xFF); // cool blue-grey base (more teal than before)
        private const float KeyIntensity = 1.05f;
        private static readonly Color SunColor = new Color32(0xFF, 0xB7, 0x65, 0xFF); // warm dawn key
        private const float SunIntensity = 0.35f; // was 0.55: warm ambient washed the whole frame yellow
        private static readonly Color FillColor = new Color(0.85f, 0.92f, 1.00f);     // subtle cool fill
        private const float FillIntensity = 0.18f;

        private const string FillLightName = "Fill Light 2D";

        // ★ Blend styles as declared in Assets/Settings/Renderer2D.asset: 0 Multiply · 1 Additive
        //   (2/3 are the "with Mask" variants — a plain global light there does nothing useful).
        //   ONE global light per style per sorting layer, hence key→0 and sun→1.
        private const int StyleMultiply = 0;
        private const int StyleAdditive = 1;

        // key + fill composited into a single style-0 light (URP 2D won't render both).
        // Premultiply each by its intensity, add, then renormalise so the strongest channel is 1.
        private static readonly Color KeyPlusFillColor = CompositeColor(KeyColor, KeyIntensity, FillColor, FillIntensity);
        private static readonly float KeyPlusFillIntensity = CompositeIntensity(KeyColor, KeyIntensity, FillColor, FillIntensity);

        private static float CompositeIntensity(Color a, float ai, Color b, float bi)
            => Mathf.Max(a.r * ai + b.r * bi, Mathf.Max(a.g * ai + b.g * bi, a.b * ai + b.b * bi));

        private static Color CompositeColor(Color a, float ai, Color b, float bi)
        {
            float i = CompositeIntensity(a, ai, b, bi);
            if (i <= 0.0001f) return Color.white;
            return new Color((a.r * ai + b.r * bi) / i, (a.g * ai + b.g * bi) / i, (a.b * ai + b.b * bi) / i);
        }

        // ── Ground (RENDER_PLAN §6A) ──
        private static readonly Color GroundTint = new Color32(0xA6, 0xAC, 0xA0, 0xFF); // grey-green, less yellow (reference is cool stone)
        private static readonly Color CameraBg   = new Color32(0x0B, 0x0E, 0x12, 0xFF);

        // เมนูนี้: ปรับโทนสี + แสงทั้งซีนเป็นลุค "รุ่งอรุณเถ้าถ่าน" (เกรดสี HDR + ไฟ 2D + พื้นติดแสง) — รันซ้ำได้
        [MenuItem("NuclearReMind/Setup Ashfall Render (Phase A)")]
        public static void Apply()
        {
            ConfigurePipelineAsset();
            var profile = BuildProfile();
            ConfigureScene(profile);
            Debug.Log("[AshfallRenderSetup] ✅ Phase A applied — dawn grade + twilight lights + lit ground. " +
                      "If any sprite stays bright, its material is Unlit (see RENDER_PLAN §14 #1).");
        }

        // 1) URP asset: LDR -> HDR grading (split-tone needs it; bloom threshold behaves correctly)
        private static void ConfigurePipelineAsset()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (rp == null)
            {
                Debug.LogError($"[AshfallRenderSetup] missing {PipelinePath} — skipped HDR grading mode");
                return;
            }
            rp.colorGradingMode = ColorGradingMode.HighDynamicRange;
            rp.colorGradingLutSize = 32;
            EditorUtility.SetDirty(rp);
            Debug.Log("[AshfallRenderSetup] URP grading mode = HDR, LUT 32");
        }

        // 2) Ashfall_Dawn profile — create once, then always re-stamp values (idempotent)
        private static VolumeProfile BuildProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tone = GetOrAdd<Tonemapping>(profile);
            tone.active = true;
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES;

            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.active = true;
            wb.temperature.overrideState = true; wb.temperature.value = WbTemperature;
            wb.tint.overrideState        = true; wb.tint.value        = WbTint;

            var ca = GetOrAdd<ColorAdjustments>(profile);
            ca.active = true;
            ca.postExposure.overrideState = true; ca.postExposure.value = PostExposure;
            ca.contrast.overrideState     = true; ca.contrast.value     = Contrast;
            ca.colorFilter.overrideState  = true; ca.colorFilter.value  = ColorFilter;
            ca.hueShift.overrideState     = true; ca.hueShift.value     = 0f;
            ca.saturation.overrideState   = true; ca.saturation.value   = Saturation;

            var split = GetOrAdd<SplitToning>(profile);
            split.active = true;
            split.shadows.overrideState    = true; split.shadows.value    = SplitShadows;
            split.highlights.overrideState = true; split.highlights.value = SplitHighlights;
            split.balance.overrideState    = true; split.balance.value    = SplitBalance;

            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.active = true;
            smh.shadows.overrideState    = true; smh.shadows.value    = SmhShadows;
            smh.highlights.overrideState = true; smh.highlights.value = SmhHighlights;

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.overrideState = true; bloom.threshold.value = BloomThreshold;
            bloom.intensity.overrideState = true; bloom.intensity.value = BloomIntensity;
            bloom.scatter.overrideState   = true; bloom.scatter.value   = BloomScatter;
            bloom.tint.overrideState      = true; bloom.tint.value      = BloomTint;
            bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = true;

            var vig = GetOrAdd<Vignette>(profile);
            vig.active = true;
            vig.color.overrideState      = true; vig.color.value      = VignetteColor;
            vig.intensity.overrideState  = true; vig.intensity.value  = VignetteIntensity;
            vig.smoothness.overrideState = true; vig.smoothness.value = VignetteSmoothness;

            var grain = GetOrAdd<FilmGrain>(profile);
            grain.active = true;
            grain.type.overrideState      = true; grain.type.value      = FilmGrainLookup.Medium3;
            grain.intensity.overrideState = true; grain.intensity.value = GrainIntensity;
            grain.response.overrideState  = true; grain.response.value  = GrainResponse;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[AshfallRenderSetup] Ashfall_Dawn profile stamped (ACES · split-tone · bloom · vignette · grain)");
            return profile;
        }

        // 3-6) Scene: volume + camera + lights + lit/tinted ground
        private static void ConfigureScene(VolumeProfile profile)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3) Global volume — priority 10 wins over the pipeline DefaultVolumeProfile
            var volGo = GameObject.Find(VolumeName) ?? new GameObject(VolumeName);
            var vol = volGo.GetComponent<Volume>();
            if (vol == null) vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.weight = 1f;
            vol.sharedProfile = profile;
            EditorUtility.SetDirty(vol);

            // 4) Camera: post ON, FXAA, dark background (edge of world should not glow brown)
            int cams = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType != CameraRenderType.Base) continue;
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = CameraBg;
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(data);
                cams++;
            }

            // 5) Lights — ★ URP 2D allows exactly ONE global light per blend style per sorting layer.
            //    Extra globals are silently dropped and log "More than one global light on layer …"
            //    on every OnEnable. So: key (cool base, with the fill folded in) owns blend style 0
            //    "Multiply"; the warm sun owns blend style 1 "Additive" — the slot it always belonged
            //    in. Stray "Fill Light 2D" objects are removed here (they were dead weight + error
            //    spam). Blend style names come from Assets/Settings/Renderer2D.asset.
            int keys = 0, removed = 0;
            Light2D sun = null;
            var strays = new System.Collections.Generic.List<Light2D>();
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                if (light.name == SunLightName)
                {
                    if (sun == null) sun = light; else strays.Add(light);
                }
                else if (light.name == FillLightName)
                {
                    strays.Add(light); // folded into the key below
                }
                else if (keys == 0) // "Global Light 2D" (or any other global key)
                {
                    // key + fill composited additively into one light (see KeyPlusFill*)
                    light.color = KeyPlusFillColor;
                    light.intensity = KeyPlusFillIntensity;
                    light.blendStyleIndex = StyleMultiply;
                    ApplyToAllSortingLayers(light);
                    EditorUtility.SetDirty(light);
                    keys++;
                }
                else strays.Add(light); // duplicate key — only one can win
            }

            if (sun == null)
            {
                var go = new GameObject(SunLightName);
                sun = go.AddComponent<Light2D>();
                sun.lightType = Light2D.LightType.Global;
            }
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.blendStyleIndex = StyleAdditive; // ★ was 0 — collided with the key and never rendered
            ApplyToAllSortingLayers(sun);
            EditorUtility.SetDirty(sun);

            foreach (var s in strays)
                if (s != null) { Object.DestroyImmediate(s.gameObject); removed++; }

            // 6) Ground: switch to lit material (unlit ground would ignore the darker lights —
            //    RENDER_PLAN §14 pitfall #1) + olive tint per tile (tiles usually LockColor)
            TintGround();

            // 7) Mood controller — Inspector-tunable component on the volume GO.
            //    First run: seeded with this file's consts. Re-runs: keeps user-tuned values.
            WireMoodController(volGo, profile);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AshfallRenderSetup] scene done — volume ok · cams {cams} · key {keys} (style {StyleMultiply}) · " +
                      $"sun 1 (style {StyleAdditive}) · removed {removed} conflicting global light(s)");
        }

        private static void TintGround()
        {
            var groundGo = GameObject.Find("Ground");
            var tilemap = groundGo != null ? groundGo.GetComponent<Tilemap>() : null;
            if (tilemap == null)
            {
                Debug.LogWarning("[AshfallRenderSetup] Tilemap 'Ground' not found — skipped ground lit/tint");
                return;
            }

            var renderer = groundGo.GetComponent<TilemapRenderer>();
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (renderer != null && lit != null)
            {
                renderer.sharedMaterial = lit;
                EditorUtility.SetDirty(renderer);
            }
            else if (lit == null)
            {
                Debug.LogWarning("[AshfallRenderSetup] Sprite-Lit-Default.mat not found — ground stays unlit (tint only)");
            }

            // Per-tile tint: default tiles have LockColor, so Tilemap.color alone is ignored.
            int tinted = 0;
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(pos)) continue;
                tilemap.SetTileFlags(pos, TileFlags.None);
                tilemap.SetColor(pos, GroundTint);
                tinted++;
            }
            EditorUtility.SetDirty(tilemap);
            Debug.Log($"[AshfallRenderSetup] ground lit + tinted {tinted} tiles -> #7C7A5E");
        }

        // Wire AshfallMoodController refs (existing tuned values are preserved — only refs update)
        private static void WireMoodController(GameObject volGo, VolumeProfile profile)
        {
            var ctrl = volGo.GetComponent<AshfallMoodController>();
            bool fresh = ctrl == null;
            if (fresh) ctrl = volGo.AddComponent<AshfallMoodController>();

            ctrl.profile = profile;

            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.lightType != Light2D.LightType.Global) continue;
                if (light.name == SunLightName) ctrl.sunLight = light;
                else ctrl.keyLight = light; // "Global Light 2D" — the only style-0 global left
            }
            // No separate fill light any more: it is composited into the key (one global per blend
            // style). The controller's fillColor/fillIntensity sliders still work — they feed that mix.
            ctrl.fillLights = new Light2D[0];

            var groundGo = GameObject.Find("Ground");
            ctrl.groundTilemap = groundGo != null ? groundGo.GetComponent<Tilemap>() : null;
            var stoneGo = GameObject.Find("StonePave"); // may not exist yet (GROUND_PLAN setup not run)
            ctrl.stoneTilemap = stoneGo != null ? stoneGo.GetComponent<Tilemap>() : null;

            if (fresh)
            {
                // Seed with the consts above so first run matches the menu-applied look
                ctrl.keyColor = KeyColor;       ctrl.keyIntensity = KeyIntensity;
                ctrl.sunColor = SunColor;       ctrl.sunIntensity = SunIntensity;
                ctrl.fillColor = FillColor;     ctrl.fillIntensity = FillIntensity;
                ctrl.postExposure = PostExposure; ctrl.contrast = Contrast;
                ctrl.saturation = Saturation;   ctrl.colorFilter = ColorFilter;
                ctrl.wbTemperature = WbTemperature; ctrl.wbTint = WbTint;
                ctrl.splitShadows = SplitShadows; ctrl.splitHighlights = SplitHighlights;
                ctrl.splitBalance = SplitBalance;
                ctrl.bloomThreshold = BloomThreshold; ctrl.bloomIntensity = BloomIntensity;
                ctrl.bloomScatter = BloomScatter; ctrl.bloomTint = BloomTint;
                ctrl.vignetteColor = VignetteColor; ctrl.vignetteIntensity = VignetteIntensity;
                ctrl.vignetteSmoothness = VignetteSmoothness; ctrl.grainIntensity = GrainIntensity;
                ctrl.groundTint = GroundTint;
            }

            ctrl.Apply(); // controller is the source of truth from here on
            EditorUtility.SetDirty(ctrl);
            Debug.Log($"[AshfallRenderSetup] mood controller {(fresh ? "created (seeded)" : "re-wired (values kept)")} on {volGo.name}");
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp)) return comp;
            comp = profile.Add<T>(false);
            comp.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(comp, profile); // persist as sub-asset
            return comp;
        }

        // m_ApplyToSortingLayers has no public setter — SerializedObject like LightingSetup
        private static void ApplyToAllSortingLayers(Light2D light)
        {
            var layers = SortingLayer.layers;
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ApplyToSortingLayers");
            if (prop == null || !prop.isArray) return;
            prop.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
