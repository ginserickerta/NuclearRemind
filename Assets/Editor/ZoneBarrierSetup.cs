using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// วางแนวรั้ว + ประตู GATE บนเส้นแบ่งโซน A/B (V4 §5) เข้า Gamescene
    ///   • สร้าง GameObject "ZoneBarrier" + ZoneBarrierRenderer (idiom RadiationSetup — root GO เดี่ยว)
    ///   • sync barrierColumn กับ OreDepositManager.zoneAColumns เสมอ (แหล่งความจริงเดียวของเส้นแบ่ง)
    ///
    /// รั้วเป็น visual ล้วน (อยู่บนขอบระหว่างช่อง ไม่จอง Cell) — ไม่กระทบการวางอาคาร/เซฟ
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

            // เส้นแบ่งต้องตรงกับที่ OreDepositManager ใช้ scatter โหนด (ไม่งั้นรั้วกับแหล่งแร่จะคนละแนว)
            var ore = Object.FindFirstObjectByType<OreDepositManager>();
            if (ore != null)
                barrier.barrierColumn = ore.zoneAColumns;
            else
                Debug.LogWarning("[ZoneBarrierSetup] ไม่พบ OreDepositManager — ใช้ barrierColumn เดิมของ barrier " +
                                 "(รัน Setup Ore Deposits ก่อนถ้าอยากให้ sync)");

            EditorUtility.SetDirty(barrier);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var grid = Object.FindFirstObjectByType<GridManager>();
            string size = grid != null ? $"{grid.columns}×{grid.rows}" : "ไม่พบ GridManager";
            Debug.Log($"[ZoneBarrierSetup] ✅ วางรั้วบนขอบคอลัมน์ {barrier.barrierColumn} " +
                      $"(กริด {size}) — ประตูกว้าง {barrier.gateWidthRows} แถว " +
                      "· รั้วสร้างตอน Start (กด Play เพื่อดู) หรือกด Rebuild Barrier ใน context menu ของคอมโพเนนต์");
        }
    }
}
