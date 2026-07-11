using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// มู้ดแสงบรรยากาศ (neutral key + cool fill จางๆ) — สีบนจอตรงกับ asset ดิบ ไม่ติดฟิลเตอร์อุ่น
    ///   • Key  = Global Light 2D เดิม → ขาวกลาง (1,1,1) สีจริงของ sprite
    ///   • Fill = Global Light 2D ดวงใหม่ โทนเย็นจางๆ intensity ต่ำ → ยกเงาให้ไม่แบน (ไม่กลบสี)
    /// ไฟ Global ชนิดเดียวกัน (blend style 0) จะ "บวกกัน" → รวมเป็นแสงกลางวันเกือบขาวสนิท
    /// ทั้งสองดวงส่องทุก sorting layer (เหมือน LightingSetup) ครอบ sprite ทุกตัว
    ///
    /// ปรับโทน/ความสว่างที่ const ด้านล่าง · idempotent (รันซ้ำได้) · รัน: NuclearReMind/Lighting Mood (Warm Key + Cool Fill)
    /// </summary>
    public static class LightingMoodSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        // ── ปรับมู้ดตรงนี้ ──
        // key = ขาวกลาง (1,1,1) → สีบนจอตรงกับ asset ดิบ ไม่ติดโทนอุ่น (เดิม 1,0.87,0.66 = ทำหญ้าเขียวอมเหลือง)
        private static readonly Color KeyColor  = new Color(1.00f, 1.00f, 1.00f); // ขาวกลาง (ตรง asset)
        private const float KeyIntensity = 1.0f;
        private static readonly Color FillColor = new Color(0.85f, 0.92f, 1.00f); // เย็นจางๆ ยกเงาไม่ให้แบน (ไม่ติดสีชัด)
        private const float FillIntensity = 0.12f; // ต่ำ ไม่ให้กลบสีจริง

        [MenuItem("NuclearReMind/Lighting Mood (Warm Key + Cool Fill)")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // key = global light เดิม (ถ้าไม่พบ สร้างใหม่)
            Light2D key = null;
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global && l.name != "Fill Light 2D") { key = l; break; }

            if (key == null)
                key = CreateGlobalLight("Global Light 2D");
            key.color = KeyColor;
            key.intensity = KeyIntensity;
            key.blendStyleIndex = 0;
            ApplyToAllSortingLayers(key);
            EditorUtility.SetDirty(key);

            // fill = global light ดวงที่สอง (find-or-create ตามชื่อ)
            var fillGo = GameObject.Find("Fill Light 2D");
            Light2D fill = fillGo != null ? fillGo.GetComponent<Light2D>() : CreateGlobalLight("Fill Light 2D");
            fill.lightType = Light2D.LightType.Global;
            fill.color = FillColor;
            fill.intensity = FillIntensity;
            fill.blendStyleIndex = 0;
            ApplyToAllSortingLayers(fill);
            EditorUtility.SetDirty(fill);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LightingMoodSetup] เสร็จ — key อุ่น {KeyIntensity} + fill เย็น {FillIntensity} · ส่องทุก sorting layer");
        }

        private static Light2D CreateGlobalLight(string name)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Global;
            return l;
        }

        // m_ApplyToSortingLayers ไม่มี public setter — เขียนผ่าน SerializedObject (เหมือน LightingSetup)
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
