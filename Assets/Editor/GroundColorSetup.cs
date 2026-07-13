using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ตั้งสี Zone A (หญ้า) เป็นคู่ #7A8C4E / #6E7A47 — สลับหมากรุก isometric ผ่าน (col+row)&1
    /// (base = อ่อน · alt = เข้ม · ทาโดย IsoGroundPainter.ColorForTile) แล้วระบายพื้นใหม่ให้เห็นผลทันที
    ///
    /// เขียนลง OreDepositManager.groundPalette (แหล่งความจริงที่ GridSpriteFiller อ่าน) — คงค่า Zone B/
    /// transition/outside เดิม เปลี่ยนแค่ 2 สี Zone A · ถ้าพาเลตต์ยังไม่ตั้ง (α<0.5) เริ่มจาก Default ก่อน
    ///
    /// รัน: NuclearReMind → Zone A สี: #7A8C4E / #6E7A47 (หมากรุก)
    /// </summary>
    public static class GroundColorSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        [MenuItem("NuclearReMind/Zone A สี: #7A8C4E / #6E7A47 (หมากรุก)")]
        public static void ApplyZoneAColors()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var ore = Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();
            if (ore == null)
            {
                EditorUtility.DisplayDialog("Zone A Color",
                    "ไม่พบ OreDepositManager ในซีน — เปิด Gamescene ก่อน", "OK");
                return;
            }

            // เริ่มจากพาเลตต์ปัจจุบัน · ถ้ายังไม่ตั้ง (struct ศูนย์ → α<0.5) ใช้ Default เป็นฐาน คงค่าอื่นครบ
            var pal = ore.groundPalette;
            if (pal.zoneA_base.a < 0.5f) pal = NuclearReMind.GroundPalette.Default;

            pal.zoneA_base = Hex(0x7A8C4E); // อ่อน
            pal.zoneA_alt  = Hex(0x6E7A47); // เข้ม

            Undo.RecordObject(ore, "Set Zone A Colors");
            ore.groundPalette = pal;
            EditorUtility.SetDirty(ore);

            // ระบายพื้นใหม่ — อ่าน groundPalette สดจาก OreDepositManager ที่เพิ่งตั้ง
            NuclearReMind.Editor.GridSpriteFiller.FillFromMenu();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[GroundColorSetup] ✅ Zone A = #7A8C4E/#6E7A47 (หมากรุก) + ระบายพื้นใหม่ · อย่าลืม Save Scene (Ctrl+S)");
        }
    }
}
