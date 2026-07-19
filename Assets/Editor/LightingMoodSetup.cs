using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// มู้ดแสงบรรยากาศ (neutral key + cool fill จางๆ) — สีบนจอตรงกับ asset ดิบ ไม่ติดฟิลเตอร์อุ่น
    ///   • Key = Global Light 2D ดวงเดียว (blend style 0 "Multiply") → ขาวกลาง + fill เย็นจางๆ รวมอยู่ในดวงเดียวกัน
    ///
    /// ⚠ แก้ 2026-07-18: เดิมสร้าง "Fill Light 2D" เป็น global ดวงที่สองบน blend style 0 ด้วยความเข้าใจผิดว่า
    ///   "ไฟ global ชนิดเดียวกันจะบวกกัน" — ความจริง URP 2D รับ global light ได้ **ดวงเดียวต่อ blend style
    ///   ต่อ sorting layer** ดวงที่เกินถูกทิ้งทั้งดวง (ไม่ส่องอะไรเลย) แถมยิง error
    ///   "More than one global light on layer …" ทุกครั้งที่ OnEnable · ซ้ำร้าย GameObject.Find ไม่เห็นออบเจกต์
    ///   ที่ inactive → รันซ้ำแล้วได้ Fill ซ้อนอีกดวง
    ///   ตอนนี้จึงรวม fill เข้า key ดวงเดียว แล้วลบ Fill Light 2D ที่ค้างอยู่ทิ้ง (ดู GlobalLight2DFixSetup)
    ///   ★ ต้องการแสงอุ่นซ้อนจริงๆ ให้ใช้ blend style 1 "Additive" (AshfallRenderSetup ทำแบบนั้นกับ Sun Key Light)
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

        private const string FillLightName = "Fill Light 2D";
        private const int StyleMultiply = 0; // blend style ตาม Assets/Settings/Renderer2D.asset (1 = Additive)

        // รวมไฟสองดวงเป็นดวงเดียว: คูณสีด้วยความสว่าง → บวกกัน → normalize ให้ช่องแรงสุด = 1
        private static float CompositeIntensity(Color a, float ai, Color b, float bi)
            => Mathf.Max(a.r * ai + b.r * bi, Mathf.Max(a.g * ai + b.g * bi, a.b * ai + b.b * bi));

        private static Color CompositeColor(Color a, float ai, Color b, float bi)
        {
            float i = CompositeIntensity(a, ai, b, bi);
            if (i <= 0.0001f) return Color.white;
            return new Color((a.r * ai + b.r * bi) / i, (a.g * ai + b.g * bi) / i, (a.b * ai + b.b * bi) / i);
        }

        [MenuItem("NuclearReMind/Lighting Mood (Warm Key + Cool Fill)")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // key = global light เดิมบน blend style 0 (ถ้าไม่พบ สร้างใหม่) · global ดวงอื่นบน style 0 = ส่วนเกิน
            Light2D key = null;
            var strays = new System.Collections.Generic.List<Light2D>();
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l == null || l.lightType != Light2D.LightType.Global) continue;
                if (l.blendStyleIndex != StyleMultiply) continue; // เช่น Sun Key Light บน style 1 (Additive) — ไม่ชน ปล่อยไว้
                if (key == null && l.name != FillLightName) key = l;
                else strays.Add(l);
            }

            if (key == null)
                key = CreateGlobalLight("Global Light 2D");

            // ★ fill รวมอยู่ในดวงเดียวกับ key (บวกสีแบบพรีมัลติพลายแล้ว normalize) — ห้ามสร้าง global ดวงที่สอง
            key.color = CompositeColor(KeyColor, KeyIntensity, FillColor, FillIntensity);
            key.intensity = CompositeIntensity(KeyColor, KeyIntensity, FillColor, FillIntensity);
            key.blendStyleIndex = StyleMultiply;
            ApplyToAllSortingLayers(key);
            EditorUtility.SetDirty(key);

            // ลบ global ส่วนเกินบน style 0 (Fill Light 2D เดิม/ตัวซ้ำ) — ไม่งั้น error กลับมาทุกครั้งที่โหลดซีน
            int removed = 0;
            foreach (var s in strays)
                if (s != null) { Object.DestroyImmediate(s.gameObject); removed++; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LightingMoodSetup] เสร็จ — global light ดวงเดียวบน blend style {StyleMultiply} " +
                      $"(key {KeyIntensity} + fill {FillIntensity} รวมกัน) · ลบดวงเกิน {removed} ดวง · ส่องทุก sorting layer");
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
