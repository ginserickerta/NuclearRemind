using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [DEV] เพิ่ม StoryTestPanel เข้า Gamescene + assign วิกฤต 4 ตัว (ปุ่ม F1-F4)
    /// ★ ไม่รวมใน Run All Setups (เป็น dev tool) — รันเองตอนอยากทดสอบ · ลบ GameObject "StoryTestPanel" ออกได้เมื่อเสร็จ
    /// </summary>
    public static class StoryTestPanelSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        // ลำดับตรงกับปุ่ม F1-F4 ใน StoryTestPanel
        private static readonly string[] Order =
        {
            "Crisis_PlasmaInstability",  // F1
            "Crisis_MalignantOutbreak",  // F2
            "Crisis_FoodShortage",       // F3
            "Crisis_DecreeEmergency",    // F4
        };

        [MenuItem("NuclearReMind/Setup Story Test Panel (dev)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("StoryTestPanel");
            if (go == null)
            {
                go = new GameObject("StoryTestPanel");
                Debug.Log("[StoryTestPanel] สร้าง GameObject ใน scene");
            }
            var panel = go.GetComponent<StoryTestPanel>() ?? go.AddComponent<StoryTestPanel>();

            // รวม DilemmaData ทั้งหมดในโปรเจกต์แล้วเรียงตาม Order (ปุ่ม 1-4)
            var byId = new Dictionary<string, DilemmaData>();
            foreach (var guid in AssetDatabase.FindAssets("t:DilemmaData"))
            {
                var d = AssetDatabase.LoadAssetAtPath<DilemmaData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && !string.IsNullOrEmpty(d.dilemmaId)) byId[d.dilemmaId] = d;
            }

            var list = new List<DilemmaData>();
            foreach (var id in Order)
            {
                byId.TryGetValue(id, out var d);
                list.Add(d);
                if (d == null)
                    Debug.LogWarning($"[StoryTestPanel] ไม่พบ DilemmaData: {id} — รัน Setup Crisis Dilemmas + Setup Story Content ก่อน");
            }
            panel.crises = list.ToArray();

            EditorUtility.SetDirty(panel);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[StoryTestPanel] พร้อมใช้ — กด Play แล้วกด F1-F4 เพื่อ force วิกฤต, N เพื่อจบวัน (ดูค่า effect มุมซ้ายบน)");
        }
    }
}
