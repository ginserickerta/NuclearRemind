using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ตั้งค่าความสวยของภาพ (Bloom + Vignette + แสงเตาปฏิกรณ์) ให้ซีนเกม
    /// "Full rendering" ชั้นสุดท้าย — เพิ่มมิติ/ความ "สวย" ให้ฉากด้วย 3 ส่วน (idempotent · รันซ้ำได้):
    ///   1. Bloom (DefaultVolumeProfile) — เดิม intensity 0 (ปิด) → เปิดให้ของเรืองแสง (เตาปฏิกรณ์) ฟุ้ง
    ///   2. Vignette — ขอบจอมืดลงเล็กน้อย จัดสายตาเข้ากลางเมือง (subtle framing)
    ///   3. Point Light2D ที่ CORE TOWER — แสงอุ่นเรืองจากเตาฟิวชัน (additive) ดันพิกเซลสว่างพอให้ Bloom จับ
    ///
    /// ★ ต้องรัน "หลัง" Fix Colors (ColorGradeSetup) — ใช้ profile/กล้อง/global light ที่ตั้งไว้แล้ว
    /// ★ เงา silhouette อาคาร + เงาใต้เท้าคนงานทำใน runtime (BuildingVisualSpawner/WorkerVisualSpawner) — ไม่ต้อง setup
    /// ปรับค่ามู้ดที่ const ด้านล่าง แล้วรันเมนูซ้ำ: NuclearReMind/Setup Rendering (Bloom + Reactor Glow)
    /// </summary>
    public static class RenderingSetup
    {
        private const string ScenePath   = "Assets/Scenes/Gamescene.unity";
        private const string ProfilePath = "Assets/DefaultVolumeProfile.asset";

        // ── Bloom (ฟุ้งเฉพาะพิกเซลสว่างเกิน threshold — pixel-art ปกติไม่ฟุ้ง มีแต่แสงเตา) ──
        private const float BloomThreshold = 1.02f;                       // สูงกว่า 1 เล็กน้อย = เฉพาะที่โดน additive light ดันเกินขาว
        private const float BloomIntensity = 0.85f;                       // ความแรงฟุ้ง
        private const float BloomScatter   = 0.75f;                       // การกระจายรัศมีฟุ้ง
        private static readonly Color BloomTint = new Color(1f, 0.92f, 0.78f); // อุ่นนิด (โทนเตา)

        // ── Vignette (ขอบมืด framing) ──
        private const float VignetteIntensity  = 0.24f;
        private const float VignetteSmoothness = 0.50f;

        // ── Reactor glow (Point Light2D ที่ CORE TOWER) ──
        private static readonly Color ReactorColor = new Color(1f, 0.68f, 0.32f); // อุ่นส้ม (ความร้อนเตา)
        private const float ReactorIntensity  = 1.35f;
        private const float ReactorOuterRadius = 4.5f;
        private const float ReactorInnerRadius = 0.6f;
        private const float ReactorYOffset     = 1.4f;  // ยกจากฐานขึ้นไปตรงตัวเตา (ไม่ใช่ที่พื้น)
        private const int   AdditiveBlendStyle = 1;      // Renderer2D: index 1 = Additive (index 0 = Multiply ที่ global light ใช้)
        private const string ReactorLightName  = "Reactor Glow Light 2D";

        // เมนูนี้: เปิด Bloom + Vignette ใน Volume Profile และเพิ่มแสง Point Light2D ที่ CORE TOWER
        [MenuItem("NuclearReMind/Setup Rendering (Bloom + Reactor Glow)")]
        public static void Apply()
        {
            ConfigurePostFx();
            ConfigureReactorLight();
            Debug.Log("[RenderingSetup] ✅ เปิด Bloom + Vignette + แสงเตาปฏิกรณ์ — ฉากมีมิติ/เรืองแสงแล้ว");
        }

        // 1+2) Bloom + Vignette ใน DefaultVolumeProfile (global volume)
        private static void ConfigurePostFx()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[RenderingSetup] ไม่พบ {ProfilePath} — ข้าม post FX");
                return;
            }

            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.active = true;
                bloom.threshold.overrideState = true; bloom.threshold.value = BloomThreshold;
                bloom.intensity.overrideState = true; bloom.intensity.value = BloomIntensity;
                bloom.scatter.overrideState   = true; bloom.scatter.value   = BloomScatter;
                bloom.tint.overrideState      = true; bloom.tint.value      = BloomTint;
            }
            else Debug.LogWarning("[RenderingSetup] ไม่พบ Bloom ใน profile");

            if (profile.TryGet<Vignette>(out var vig))
            {
                vig.active = true;
                vig.intensity.overrideState  = true; vig.intensity.value  = VignetteIntensity;
                vig.smoothness.overrideState = true; vig.smoothness.value = VignetteSmoothness;
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RenderingSetup] Bloom: threshold={BloomThreshold} intensity={BloomIntensity} · Vignette={VignetteIntensity}");
        }

        // 3) Point Light2D อุ่น ๆ ที่ CORE TOWER (find-or-create ตามชื่อ · ตำแหน่งคำนวณจากกลางกริดเหมือน BuildingBalanceSetup)
        private static void ConfigureReactorLight()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null)
            {
                Debug.LogWarning("[RenderingSetup] ไม่พบ GridManager — ข้ามแสงเตา (รัน Setup Grid ก่อน)");
                return;
            }

            var core = FindCoreTowerData();
            var size = core != null ? core.size : new Vector2Int(3, 3);
            var origin = new Vector2Int((grid.columns - size.x) / 2, (grid.rows - size.y) / 2);
            Vector3 center = grid.FootprintCenterWorld(origin, size);
            if (core != null) center += (Vector3)core.spriteOffset;
            center += new Vector3(0f, ReactorYOffset, 0f);

            var go = GameObject.Find(ReactorLightName) ?? new GameObject(ReactorLightName);
            go.transform.position = center;

            var light = go.GetComponent<Light2D>() ?? go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = ReactorColor;
            light.intensity = ReactorIntensity;
            light.pointLightInnerRadius = ReactorInnerRadius;
            light.pointLightOuterRadius = ReactorOuterRadius;
            light.blendStyleIndex = AdditiveBlendStyle; // additive = "บวก" แสงเข้าฉาก → พิกเซลสว่างเกินขาว → Bloom จับ
            ApplyToAllSortingLayers(light);
            EditorUtility.SetDirty(light);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RenderingSetup] แสงเตาที่ ({center.x:0.0},{center.y:0.0}) รัศมี {ReactorOuterRadius} · additive intensity {ReactorIntensity}");
        }

        private static BuildingData FindCoreTowerData()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.isCoreTowerPart) return data;
            }
            return null;
        }

        // m_ApplyToSortingLayers ไม่มี public setter — เขียนผ่าน SerializedObject (เหมือน LightingSetup/LightingMoodSetup)
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
