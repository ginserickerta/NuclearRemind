using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ปรับขนาด "Zone C" = apron (แถบพื้นตกแต่งรอบนอกกริดจริง ที่มีสไปรต์แต่วางสิ่งปลูกสร้างไม่ได้
    /// เพราะช่องนอก [0,columns)×[0,rows) ไม่ใช่ Cell ของ GridManager → GetCell คืน null)
    ///
    /// apronMargin คุมความกว้าง apron ทุกด้าน (แหล่งความจริง = OreDepositManager.apronMargin · เดิม 35)
    /// เมนูนี้ตั้งค่าใหม่ + ซิงค์ DecorSpawner + ★ ระบายพื้นใหม่ (Fill Grids) ให้เห็นผลทันที
    ///   (ถ้าไม่ระบายใหม่ ไทล์ apron เดิมที่ระบายค้างไว้จะไม่หด)
    ///
    /// รัน: NuclearReMind → Zone C (Apron): ลดครึ่ง (50%)
    /// </summary>
    public static class ApronSizeSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const int HalfMargin = 18; // ~50% ของ 35 (ปัดขึ้นจาก 17.5)

        [MenuItem("NuclearReMind/Zone C (Apron): ลดครึ่ง (50%)")]
        public static void HalveApron() => SetApron(HalfMargin);

        private static void SetApron(int margin)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var ore = Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();
            if (ore == null)
            {
                EditorUtility.DisplayDialog("Zone C (Apron)",
                    "ไม่พบ OreDepositManager ในซีน — เปิด Gamescene ก่อน", "OK");
                return;
            }

            int before = ore.apronMargin;
            Undo.RecordObject(ore, "Set Apron Margin");
            ore.apronMargin = margin;
            EditorUtility.SetDirty(ore);

            // ซิงค์ DecorSpawner ให้ decor รันไทม์กระจายในช่วง apron ใหม่ (ไม่โผล่นอกพื้น)
            var decor = Object.FindFirstObjectByType<NuclearReMind.DecorSpawner>();
            if (decor != null)
            {
                Undo.RecordObject(decor, "Set Apron Margin");
                decor.apronMargin = margin;
                EditorUtility.SetDirty(decor);
            }

            // ★ ระบายพื้นใหม่ — GridSpriteFiller อ่าน apronMargin สดจาก OreDepositManager ที่เพิ่งตั้ง
            NuclearReMind.Editor.GridSpriteFiller.FillFromMenu();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[ApronSizeSetup] ✅ Zone C (apron) {before} → {margin} ทุกด้าน + ระบายพื้นใหม่ · อย่าลืม Save Scene (Ctrl+S)");
        }
    }
}
