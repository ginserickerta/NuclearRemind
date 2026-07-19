using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ASHFALL DAWN — Phase B of docs/RENDER_PLAN.md: atmosphere.
    /// Pure visual layer (no gameplay). Idempotent — deletes and rebuilds its own root.
    ///
    ///   1. Generates soft VFX textures procedurally (no art dependency):
    ///        Assets/Sprites/VFX/soft_puff.png  — radial gradient (smoke / haze)
    ///        Assets/Sprites/VFX/ash_dot.png    — small dot (floating ash)
    ///      + unlit materials (Sprites/Default — 2D lights must NOT relight fog/smoke)
    ///   2. Scene root "__ASHFALL_Atmosphere":
    ///        CoreSmoke   — chimney smoke ParticleSystem above the CORE TOWER
    ///        AshDust     — floating ash/dust particles across the playfield
    ///        GroundHaze  — 4 big soft fog sprites drifting slowly (AshfallDrifter)
    ///   3. Adds LightFlicker to "Reactor Glow Light 2D" (subtle breathing)
    ///
    /// ★ Run after "Setup Ashfall Render (Phase A)".
    /// Run: NuclearReMind/Setup Ashfall Atmosphere (Phase B)
    /// </summary>
    public static class AshfallAtmosphereSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string RootName  = "__ASHFALL_Atmosphere";
        private const string VfxDir    = "Assets/Sprites/VFX";
        private const string PuffTexPath = VfxDir + "/soft_puff.png";
        private const string AshTexPath  = VfxDir + "/ash_dot.png";
        private const string PuffMatPath = VfxDir + "/VFX_Puff.mat";
        private const string AshMatPath  = VfxDir + "/VFX_Ash.mat";
        private const string SortLayer   = "Buildings";
        // Orders far above any building sort (max ≈ (43+43)*16 + tier ≈ 1400)
        private const int HazeOrder  = 4700;
        private const int SmokeOrder = 4800;
        private const int AshOrder   = 5000;

        private static readonly Color SmokeColor = new Color(0.23f, 0.23f, 0.25f, 0.55f);
        private static readonly Color AshColor   = new Color(0.72f, 0.76f, 0.80f, 0.45f);
        private static readonly Color HazeColor  = new Color(0.54f, 0.58f, 0.63f, 0.22f); // #8A94A0 @ low alpha
        private const float ChimneyYOffset = 2.3f; // above the core tower sprite

        [MenuItem("NuclearReMind/Setup Ashfall Atmosphere (Phase B)")]
        public static void Apply()
        {
            EnsureTextures();
            var puffMat = EnsureMaterial(PuffMatPath, PuffTexPath);
            var ashMat  = EnsureMaterial(AshMatPath, AshTexPath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // idempotent: rebuild our root from scratch
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            Vector3 center = GridCenter(out Vector3 corePos);
            BuildCoreSmoke(root.transform, corePos + new Vector3(0f, ChimneyYOffset, 0f), puffMat);
            BuildAshDust(root.transform, center, ashMat);
            BuildGroundHaze(root.transform, center, puffMat);
            AddReactorFlicker();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AshfallAtmosphere] ✅ Phase B — core smoke + ash dust + ground haze + reactor flicker");
        }

        // ── positions ─────────────────────────────────────────────────────────

        private static Vector3 GridCenter(out Vector3 corePos)
        {
            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null)
            {
                Debug.LogWarning("[AshfallAtmosphere] no GridManager — using origin (run Setup Grid first)");
                corePos = Vector3.zero;
                return Vector3.zero;
            }
            // same approach as RenderingSetup: core tower sits centered on the grid
            var size = new Vector2Int(3, 3);
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.isCoreTowerPart) { size = data.size; break; }
            }
            var origin = new Vector2Int((grid.columns - size.x) / 2, (grid.rows - size.y) / 2);
            corePos = grid.FootprintCenterWorld(origin, size);
            return corePos;
        }

        // ── scene builders ────────────────────────────────────────────────────

        // Chimney smoke column above the CORE TOWER (RENDER_PLAN §7.2)
        private static void BuildCoreSmoke(Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject("CoreSmoke");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startColor = SmokeColor;
            main.gravityModifier = -0.04f; // negative = rises
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // cone points up (+Y)

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.23f, 0.23f, 0.25f), 0f),
                        new GradientColorKey(new Color(0.42f, 0.43f, 0.46f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.4f)); // puffs grow

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.1f;

            ConfigureRenderer(go, mat, SmokeOrder);
        }

        // Floating ash / dust across the playfield (RENDER_PLAN §7.3-7.4)
        private static void BuildAshDust(Transform parent, Vector3 center, Material mat)
        {
            var go = new GameObject("AshDust");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = AshColor;
            main.gravityModifier = 0.003f; // barely settles
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            var em = ps.emission; em.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 20f, 0f); // covers view around the city

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            ConfigureRenderer(go, mat, AshOrder);
        }

        // 4 big soft fog sprites drifting around the city (RENDER_PLAN §7.3)
        private static void BuildGroundHaze(Transform parent, Vector3 center, Material mat)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PuffTexPath);
            if (sprite == null)
            {
                Debug.LogWarning("[AshfallAtmosphere] soft_puff sprite missing — haze skipped");
                return;
            }

            Vector3[] offsets = { new Vector3(-9f, 4f), new Vector3(10f, 2.5f),
                                  new Vector3(0f, -6f), new Vector3(-4f, -2f) };
            Vector3[] scales  = { new Vector3(13f, 4.5f, 1f), new Vector3(11f, 4f, 1f),
                                  new Vector3(14f, 5f, 1f), new Vector3(9f, 3.5f, 1f) };

            for (int i = 0; i < offsets.Length; i++)
            {
                var go = new GameObject($"GroundHaze_{i}");
                go.transform.SetParent(parent, false);
                go.transform.position = center + offsets[i];
                go.transform.localScale = scales[i];

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = mat;   // unlit — fog must not be relit by 2D lights
                sr.color = HazeColor;
                sr.sortingLayerName = SortLayer;
                sr.sortingOrder = HazeOrder;

                var drift = go.AddComponent<AshfallDrifter>();
                drift.amplitude = new Vector2(0.9f, 0.25f);
                drift.speed = 0.03f + i * 0.008f; // slightly different pace each
            }
        }

        private static void AddReactorFlicker()
        {
            var go = GameObject.Find("Reactor Glow Light 2D");
            var light = go != null ? go.GetComponent<Light2D>() : null;
            if (light == null)
            {
                Debug.LogWarning("[AshfallAtmosphere] Reactor Glow Light 2D not found — flicker skipped");
                return;
            }
            var flicker = go.GetComponent<LightFlicker>();
            if (flicker == null) flicker = go.AddComponent<LightFlicker>();
            flicker.baseIntensity = light.intensity;
            flicker.amplitude = 0.15f;
            flicker.speed = 5f;
            EditorUtility.SetDirty(go);
        }

        private static void ConfigureRenderer(GameObject go, Material mat, int order)
        {
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = mat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingLayerName = SortLayer;
            psr.sortingOrder = order;
        }

        // ── procedural assets ─────────────────────────────────────────────────

        private static void EnsureTextures()
        {
            if (!Directory.Exists(VfxDir)) Directory.CreateDirectory(VfxDir);
            EnsureRadialTexture(PuffTexPath, 128, 1.9f); // soft wide falloff
            EnsureRadialTexture(AshTexPath, 16, 1.1f);   // small firmer dot
        }

        // White radial gradient, alpha = (1 - dist)^power — soft glow/puff base
        private static void EnsureRadialTexture(string path, int size, float power)
        {
            if (File.Exists(path)) { EnsureImporter(path, size); return; }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            EnsureImporter(path, size);
        }

        private static void EnsureImporter(string path, int size)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear; // soft VFX — Point would band the gradient
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = size / 2f;  // sprite ≈ 2 world units wide
            imp.SaveAndReimport();
        }

        // Unlit material (Sprites/Default) — particles/haze must ignore 2D lights
        private static Material EnsureMaterial(string matPath, string texPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null) mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
