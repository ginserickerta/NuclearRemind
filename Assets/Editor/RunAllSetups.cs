using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// รัน setup menu ทั้งหมดตามลำดับใน UNITY_SETUP_AND_PLAYTEST.md §2 แล้วเซฟซีน
    /// ลำดับสำคัญ — manager/HUD ต้องมาก่อน content ที่ wire เข้า manager (Quiz ต้องหลัง Crisis + HUD)
    ///
    /// รัน 2 ทาง:
    ///   • เมนู: NuclearReMind/Run All Setups (in order)
    ///   • batch: -executeMethod NuclearReMind.EditorTools.RunAllSetups.Execute
    /// </summary>
    public static class RunAllSetups
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        private static readonly string[] MenuOrder =
        {
            "NuclearReMind/Setup HUD Canvas",
            "NuclearReMind/Setup Tooltip and Dilemma UI",
            "NuclearReMind/Apply Building Balance (V4 §6)",
            "NuclearReMind/Setup Phase 3 Population",
            "NuclearReMind/Setup Phase 6 Buildings",
            "NuclearReMind/Setup Codex System",
            "NuclearReMind/Setup Crisis Dilemmas",
            "NuclearReMind/Setup Quiz System",
            "NuclearReMind/Setup Decrees",
            // Power Grid ถอดจาก chain แล้ว — GDD V4 ไม่มีกลไกรัศมีไฟ (ต้นทุนพลังงาน = upkeep §6)
            // และ setup เดิมจะยัด PowerConduit (no-op) กลับเข้า hotbar — เมนูยังอยู่ ถ้าอยากรันเอง
            "NuclearReMind/Setup Day 11 Systems",
            "NuclearReMind/Setup Demolition System",
            "NuclearReMind/Setup Grid 43x43",
            "NuclearReMind/Fill Grids (Ground + Fog)",
            "NuclearReMind/Apply Kanit Font (Scene + Prefabs)",
        };

        [MenuItem("NuclearReMind/Run All Setups (in order)")]
        public static void Execute()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            for (int i = 0; i < MenuOrder.Length; i++)
            {
                string item = MenuOrder[i];
                Debug.Log($"[RunAllSetups] ▶ ({i + 1}/{MenuOrder.Length}) {item}");
                if (!EditorApplication.ExecuteMenuItem(item))
                    throw new Exception($"[RunAllSetups] เมนูล้มเหลว/ไม่พบ: {item}");
            }

            // บางขั้นเซฟเองแล้ว แต่ขั้นที่แค่ MarkSceneDirty ต้องเซฟปิดท้าย (= ขั้น 13 Ctrl+S)
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RunAllSetups] ✅ รันครบ {MenuOrder.Length} เมนู + เซฟซีนแล้ว");
        }
    }
}
