using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ตั้งขนาดกริดเป็น 43×43 ในซีน + re-init (Zone A สี่เหลี่ยมกลาง · Zone B กรอบรอบนอก)
    /// รันผ่านเมนู NuclearReMind / Setup Grid 43x43
    /// หมายเหตุ: regenerate tile art (Fill Grids) + จัดกล้อง/zoom ให้เห็นกริดเต็ม = งานมือใน Unity
    /// </summary>
    public static class GridSizeSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Grid 43x43")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null)
            {
                Debug.LogWarning("[GridSizeSetup] ไม่พบ GridManager ในซีน — เปิด Gamescene แล้วรันใหม่");
                return;
            }

            grid.columns = 43;
            grid.rows = 43;
            grid.InitializeGrid();

            EditorUtility.SetDirty(grid);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[GridSizeSetup] ตั้งกริด 43×43 + re-init สำเร็จ");
            EditorUtility.DisplayDialog("Grid 43×43",
                "ตั้งกริดเป็น 43×43 แล้ว\n\nงานที่เหลือใน Unity:\n  • Fill Grids (regenerate tile art)\n  • จัดกล้อง/zoom ให้เห็นกริดเต็ม", "OK");
        }
    }
}
