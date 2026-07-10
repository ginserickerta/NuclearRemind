using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// แก้ "sprite หมอง/ทึม" — ไม่ใช่บั๊ก แต่สีดิบของ pixel-art ดูจืดเพราะ:
    ///   • กล้องปิด Post-processing → color grade ไม่ทำงาน
    ///   • Global Light 2D = ขาว intensity 1 (สีจริง แต่ flat)
    /// วิธีแก้ (ปรับค่าได้ที่ const ด้านล่าง — รันซ้ำได้):
    ///   1. เปิด Post-processing ที่กล้องหลัก
    ///   2. ColorAdjustments: saturation/contrast/postExposure ให้สีสดขึ้น (DefaultVolumeProfile — global)
    ///   3. ดัน Global Light 2D intensity เล็กน้อยให้ sprite สว่างเด้ง
    /// </summary>
    public static class ColorGradeSetup
    {
        private const string ScenePath   = "Assets/Scenes/Gamescene.unity";
        private const string ProfilePath = "Assets/DefaultVolumeProfile.asset";

        // ── ปรับความสดตรงนี้ (มาก = สดขึ้น) ──
        private const float Saturation   = 22f;   // -100..100  (สีสด)
        private const float Contrast     = 10f;   // -100..100  (คอนทราสต์/ความเด้ง)
        private const float PostExposure = 0.12f; // EV         (สว่างรวม)
        private const float LightIntensity = 1.15f; // Global Light 2D (1 = เดิม)

        [MenuItem("NuclearReMind/Fix Colors (สี sprite สดขึ้น)")]
        public static void Apply()
        {
            // 1) DefaultVolumeProfile → ColorAdjustments (global grade)
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[ColorGradeSetup] ไม่พบ {ProfilePath}");
            }
            else if (profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.active = true;
                ca.saturation.overrideState = true;   ca.saturation.value = Saturation;
                ca.contrast.overrideState = true;     ca.contrast.value = Contrast;
                ca.postExposure.overrideState = true; ca.postExposure.value = PostExposure;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ColorGradeSetup] ColorAdjustments: sat={Saturation} contrast={Contrast} exposure={PostExposure}");
            }

            // 2+3) scene: เปิด post ที่กล้อง + ดัน global light
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int cams = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && data.renderType == CameraRenderType.Base)
                {
                    data.renderPostProcessing = true;
                    EditorUtility.SetDirty(data);
                    cams++;
                }
            }

            int lights = 0;
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.lightType == Light2D.LightType.Global)
                {
                    light.intensity = LightIntensity;
                    EditorUtility.SetDirty(light);
                    lights++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ColorGradeSetup] เสร็จ — เปิด post ที่กล้อง {cams} ตัว · ดัน global light {lights} ดวง (intensity {LightIntensity})");
        }
    }
}
