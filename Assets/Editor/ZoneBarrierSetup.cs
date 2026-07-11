using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// จัดการ GameObject "ZoneBarrier" (ZoneBarrierRenderer) ในซีน
    ///   • โมเดลปัจจุบัน Zone B เป็นกรอบรอบนอก → ปิดรั้วเส้นตั้ง (active = false)
    ///   • เก็บคอมโพเนนต์ไว้เผื่ออยากได้รั้วกลับ (ตั้ง active = true เอง)
    ///
    /// รัน: เมนู NuclearReMind/Setup Zone Barrier (Fence + Gate) — หรือรวมใน Run All Setups
    /// </summary>
    public static class ZoneBarrierSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Zone Barrier (Fence + Gate)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("ZoneBarrier");
            if (go == null)
            {
                go = new GameObject("ZoneBarrier");
                Debug.Log("[ZoneBarrierSetup] สร้าง ZoneBarrier ใน scene");
            }

            var barrier = go.GetComponent<ZoneBarrierRenderer>() ?? go.AddComponent<ZoneBarrierRenderer>();

            // โมเดลปัจจุบัน Zone B เป็นกรอบรอบนอก — ไม่ใช้รั้วเส้นตั้งคั่น A|B จึงปิดไว้
            barrier.active = false;
            EditorUtility.SetDirty(barrier);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[ZoneBarrierSetup] ✅ ปิดรั้วเส้นตั้ง (active = false) — Zone B เป็นกรอบรอบนอกแล้ว " +
                      "· ตั้ง active = true ในคอมโพเนนต์ถ้าอยากได้รั้วกลับ");
        }
    }
}
