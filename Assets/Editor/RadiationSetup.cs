using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor เพิ่มระบบรังสี (RadiationManager) เข้าซีนเกม
    /// เพิ่ม RadiationManager เข้า Gamescene (Story Guide §4 — ระบบ Zone A / วิกฤตโรครังสี)
    /// รันผ่านเมนู NuclearReMind / Setup Radiation System (หรือรวมใน Run All Setups)
    ///
    /// RadiationManager สะสม exposure ทุกสิ้นวันหลังเตาเดินเครื่อง → beat crisis_radiation_disease
    /// ยิงเมื่อ exposure_above_60 (StatCondition) · ไม่ต้อง wire field — ใช้ค่า default ในโค้ด
    /// </summary>
    public static class RadiationSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        // เมนูนี้: สร้าง GameObject RadiationManager ในซีนเกม (ถ้ายังไม่มี)
        [MenuItem("NuclearReMind/Setup Radiation System")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("RadiationManager");
            if (go == null)
            {
                go = new GameObject("RadiationManager");
                Debug.Log("[RadiationSetup] สร้าง RadiationManager ใน scene");
            }

            var mgr = go.GetComponent<RadiationManager>() ?? go.AddComponent<RadiationManager>();
            EditorUtility.SetDirty(mgr);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RadiationSetup] wire RadiationManager เข้า Gamescene สำเร็จ (ค่า tuning ใช้ default — ปรับใน Inspector ได้)");
        }
    }
}
