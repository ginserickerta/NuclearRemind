using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// แก้ sprite ดำทั้งแมพ — Global Light 2D ส่องแค่ sorting layer "Default" (m_ApplyToSortingLayers = [0])
    ///
    /// สาเหตุ: SpriteRenderer ที่สร้างตอนรันไทม์ (BuildingVisualSpawner/WorkerVisualSpawner/ZoneBarrierRenderer/ghost)
    /// ได้วัสดุ default ของ URP 2D = Sprite-Lit-Default (รับแสง) แต่เลเยอร์ Buildings/Units/FogOfWar/WorldUI
    /// ไม่อยู่ในรายชื่อที่ไฟส่อง → ไม่มีแสงตกกระทบ → เรนเดอร์ดำสนิท
    /// (พื้น Ground รอดเพราะ Tilemap ใช้ Sprites-Default แบบไม่รับแสง)
    ///
    /// แก้: ให้ Global Light ส่องทุก sorting layer — ครอบคลุม sprite ทุกตัวในเกม ไม่ต้องไล่แก้ทีละสคริปต์
    /// รัน: เมนู NuclearReMind/Fix 2D Lighting (Light All Sorting Layers) — หรือรวมใน Run All Setups
    /// </summary>
    public static class LightingSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Fix 2D Lighting (Light All Sorting Layers)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (lights.Length == 0)
            {
                Debug.LogWarning("[LightingSetup] ไม่พบ Light2D ในซีน — ข้าม (sprite จะดำถ้าใช้วัสดุ Sprite-Lit-Default)");
                return;
            }

            // sorting layer ทั้งหมดที่โปรเจกต์ประกาศไว้ (Default/Ground/Buildings/Units/FogOfWar/WorldUI)
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
                ids[i] = layers[i].id;

            int patched = 0;
            foreach (var light in lights)
            {
                // m_ApplyToSortingLayers ไม่มี public setter — เขียนผ่าน SerializedObject
                var so = new SerializedObject(light);
                var prop = so.FindProperty("m_ApplyToSortingLayers");
                if (prop == null || !prop.isArray)
                {
                    Debug.LogWarning($"[LightingSetup] {light.name}: ไม่พบ m_ApplyToSortingLayers (URP เปลี่ยนชื่อฟิลด์?)");
                    continue;
                }

                prop.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                    prop.GetArrayElementAtIndex(i).intValue = ids[i];

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(light);
                patched++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var names = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++) names[i] = layers[i].name;
            Debug.Log($"[LightingSetup] ✅ ตั้ง Light2D {patched} ดวงให้ส่องทุก sorting layer: {string.Join(", ", names)} " +
                      "— sprite อาคาร/คนงาน/รั้ว จะแสดงสีจริงแล้ว");
        }
    }
}
