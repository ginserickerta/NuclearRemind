using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เพิ่ม GameObject "BuildingVisualSpawner" เข้า scene และผูก buildingsParent
    /// เข้ากับ GameObject "Buildings" (Tilemap, sorting layer Buildings) — รันครั้งเดียวจาก Editor
    /// </summary>
    public static class BuildingVisualSpawnerSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Building Visual Spawner")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var buildingsGO = GameObject.Find("Buildings");
            if (buildingsGO == null)
            {
                Debug.LogError("[BuildingVisualSpawnerSetup] ไม่พบ GameObject 'Buildings' ใน scene");
                return;
            }

            var existing = GameObject.Find("BuildingVisualSpawner");
            BuildingVisualSpawner spawner;

            if (existing != null)
            {
                spawner = existing.GetComponent<BuildingVisualSpawner>();
                if (spawner == null)
                    spawner = existing.AddComponent<BuildingVisualSpawner>();
            }
            else
            {
                var go = new GameObject("BuildingVisualSpawner");
                spawner = go.AddComponent<BuildingVisualSpawner>();
            }

            spawner.buildingsParent = buildingsGO.transform;

            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[BuildingVisualSpawnerSetup] เพิ่ม BuildingVisualSpawner และผูก buildingsParent สำเร็จ");
        }
    }
}
