using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// แก้ error "More than one global light on layer X for light blend style index 0"
    ///
    /// สาเหตุ: URP 2D อนุญาต global light ได้ **ดวงเดียวต่อ 1 blend style ต่อ 1 sorting layer**
    /// ดวงที่เกินจะถูกทิ้ง (ไม่ส่องอะไรเลย) + ยิง error ทุกครั้งที่ OnEnable
    /// ซีนนี้เคยมี 4 ดวงบน blend style 0 พร้อมกัน: Global Light 2D (key) · Sun Key Light 2D ·
    /// Fill Light 2D ×2 (ซ้ำเพราะ GameObject.Find ไม่เห็นตัวที่ inactive) → 3 ดวงตายเงียบ + error 18 บรรทัด
    ///
    /// แก้ตามที่ Renderer2D.asset ประกาศ blend style ไว้จริง:
    ///   • style 0 "Multiply" ← key เย็น (แสงฐาน) · fill ถูกรวมเข้า key แบบบวกสี แล้วลบทิ้ง
    ///   • style 1 "Additive" ← Sun Key Light 2D (แสงอุ่นตอนเช้า) — ที่ที่ควรอยู่ตั้งแต่แรก
    ///   (style 2/3 เป็นเวอร์ชัน "with Mask" ใช้กับ global light ทั่วไปไม่ได้ผล)
    /// ผลพลอยได้: sun/fill ที่เคยตายเงียบ กลับมาทำงานจริงเป็นครั้งแรก → มู้ดตรงกับที่ RENDER_PLAN ออกแบบไว้
    /// ปรับความสว่าง/สีต่อได้สดๆ ที่ AshfallMoodController (__ASHFALL_Volume)
    ///
    /// idempotent — รันซ้ำได้ · รัน: NuclearReMind/Fix 2D Global Lights
    /// </summary>
    public static class GlobalLight2DFixSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string KeyName   = "Global Light 2D";
        private const string SunName   = "Sun Key Light 2D";
        private const string FillName  = "Fill Light 2D";

        /// <summary>blend style ตาม Assets/Settings/Renderer2D.asset — 0 Multiply · 1 Additive</summary>
        private const int StyleMultiply = 0;
        private const int StyleAdditive = 1;

        [MenuItem("NuclearReMind/Fix 2D Global Lights")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Light2D key = null, sun = null;
            var fills = new List<Light2D>();
            var extraKeys = new List<Light2D>();

            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light == null || light.lightType != Light2D.LightType.Global) continue; // point/spot ไม่ชนกัน

                if (light.name == SunName)
                {
                    if (sun == null) sun = light; else extraKeys.Add(light);
                }
                else if (light.name == FillName)
                {
                    fills.Add(light);
                }
                else
                {
                    if (key == null) key = light; else extraKeys.Add(light); // global ที่ไม่รู้จัก = key เกิน
                }
            }

            if (key == null && sun == null && fills.Count == 0)
            {
                Debug.LogWarning("[GlobalLight2DFix] ไม่พบ global Light2D ในซีน — ไม่มีอะไรต้องแก้");
                return;
            }

            // ── key (style 0 Multiply) — รวมสี fill ทุกดวงเข้ามาแบบบวก แล้ว fill จะถูกลบ ──
            if (key == null && fills.Count > 0)
            {
                key = fills[0];                 // ไม่มี key เลย → เลื่อน fill ดวงแรกขึ้นเป็น key
                key.name = KeyName;
                fills.RemoveAt(0);
            }

            if (key != null)
            {
                Vector3 sum = Weighted(key.color, key.intensity);
                foreach (var f in fills) sum += Weighted(f.color, f.intensity);
                Normalize(sum, out var keyColor, out float keyIntensity);

                key.color = keyColor;
                key.intensity = keyIntensity;
                key.blendStyleIndex = StyleMultiply;
                ApplyToAllSortingLayers(key);
                EditorUtility.SetDirty(key);
            }

            // ── sun (style 1 Additive) — ย้ายออกจาก style 0 เพื่อเลิกชนกับ key ──
            if (sun != null)
            {
                sun.blendStyleIndex = StyleAdditive;
                ApplyToAllSortingLayers(sun);
                EditorUtility.SetDirty(sun);
            }

            // ── ลบดวงที่เกิน (fill ที่รวมเข้า key แล้ว + global ซ้ำชื่อ) ──
            int removed = 0;
            foreach (var f in fills)    { if (f != null) { Object.DestroyImmediate(f.gameObject); removed++; } }
            foreach (var e in extraKeys) { if (e != null) { Object.DestroyImmediate(e.gameObject); removed++; } }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[GlobalLight2DFix] ✅ global light เหลือดวงเดียวต่อ blend style — " +
                      $"key '{(key != null ? key.name : "-")}' style {StyleMultiply} (Multiply) · " +
                      $"sun '{(sun != null ? sun.name : "-")}' style {StyleAdditive} (Additive) · ลบดวงเกิน {removed} ดวง " +
                      "— error \"More than one global light\" หายแล้ว");
        }

        // สีคูณความสว่าง (พรีมัลติพลาย) — ใช้บวกไฟหลายดวงเข้าด้วยกันก่อน normalize กลับ
        private static Vector3 Weighted(Color c, float intensity)
            => new Vector3(c.r * intensity, c.g * intensity, c.b * intensity);

        // แยกกลับเป็น (สี, ความสว่าง) โดยให้ช่องที่แรงสุด = 1 → ไม่เกินช่วงสีที่ Light2D รับได้
        private static void Normalize(Vector3 sum, out Color color, out float intensity)
        {
            intensity = Mathf.Max(sum.x, Mathf.Max(sum.y, sum.z));
            color = intensity > 0.0001f
                ? new Color(sum.x / intensity, sum.y / intensity, sum.z / intensity)
                : Color.white;
        }

        // m_ApplyToSortingLayers ไม่มี public setter — เขียนผ่าน SerializedObject (เหมือน LightingSetup)
        private static void ApplyToAllSortingLayers(Light2D light)
        {
            var layers = SortingLayer.layers;
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ApplyToSortingLayers");
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[GlobalLight2DFix] {light.name}: ไม่พบ m_ApplyToSortingLayers (URP เปลี่ยนชื่อฟิลด์?)");
                return;
            }

            prop.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = layers[i].id;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
