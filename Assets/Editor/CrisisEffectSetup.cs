using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เพิ่ม CrisisEffectManager เข้า Gamescene (Story Guide §4 — ผลกระทบวิกฤตเต็มระบบ)
    /// รันผ่านเมนู NuclearReMind / Setup Crisis Effects (หรือรวมใน Run All Setups)
    ///
    /// CrisisEffectManager subscribe OnDilemmaResolved เอง (แบบ DecreeManager) แล้วเก็บ state
    /// (yield/spoil/efficiency/busy/hopePerDay) ให้ ResourceManager/CoreTower/Radiation/Population pull ไปใช้
    /// ไม่ต้อง wire field — ใช้ค่า tuning default ในโค้ด (riot/patient/minVulnerable ปรับใน Inspector ได้)
    /// </summary>
    public static class CrisisEffectSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Crisis Effects")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("CrisisEffectManager");
            if (go == null)
            {
                go = new GameObject("CrisisEffectManager");
                Debug.Log("[CrisisEffectSetup] สร้าง CrisisEffectManager ใน scene");
            }

            var mgr = go.GetComponent<CrisisEffectManager>() ?? go.AddComponent<CrisisEffectManager>();
            EditorUtility.SetDirty(mgr);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CrisisEffectSetup] wire CrisisEffectManager เข้า Gamescene สำเร็จ (ค่า tuning ใช้ default — ปรับใน Inspector ได้)");
        }
    }
}
