using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เฟส 7 (V4 §11) — สร้าง 2 ประกาศฉุกเฉิน (DecreeSO) + wire DecreeManager ในซีน
    /// รันผ่านเมนู NuclearReMind / Setup Decrees
    /// </summary>
    public static class DecreeSetup
    {
        private const string Folder = "Assets/ScriptableObjects/Decrees";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Decrees")]
        public static void SetupAll()
        {
            System.IO.Directory.CreateDirectory(Folder);

            var decrees = new[]
            {
                Create("Decree1_SickLabor", "เกณฑ์ผู้ป่วยร่วมงาน", coolingLabor: 6, hopeNow: -8f, hopePerDay: -3f),
                Create("Decree2_ChildLabor", "ดึงแรงงานเด็ก",       coolingLabor: 12, hopeNow: -15f, hopePerDay: -2f),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Wire(decrees);

            Debug.Log("[DecreeSetup] สร้าง 2 decree + wire DecreeManager สำเร็จ");
        }

        private static DecreeSO Create(string id, string title, int coolingLabor, float hopeNow, float hopePerDay)
        {
            string path = $"{Folder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DecreeSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DecreeSO>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.id = id;
            asset.title = title;
            asset.coolingLaborGain = coolingLabor;
            asset.hopeImmediate = hopeNow;
            asset.hopePerDay = hopePerDay;
            asset.linkedQuizIds = new[] { "Q10" };
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void Wire(DecreeSO[] decrees)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("DecreeManager") ?? new GameObject("DecreeManager");
            var mgr = go.GetComponent<DecreeManager>() ?? go.AddComponent<DecreeManager>();
            mgr.decrees = decrees;
            EditorUtility.SetDirty(mgr);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[DecreeSetup] wire DecreeManager.decrees = {decrees.Length}");
        }
    }
}
