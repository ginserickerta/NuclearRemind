using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor แก้บั๊กสีพื้นหายตอนกด Play — ปลดล็อก LockColor ใน Tile asset ทุกใบ
    /// แก้บั๊ก "สี tile หายตอนกด Play" — Tile asset ตั้ง flags = LockColor (m_Flags:1) ตั้งแต่สร้าง
    /// (ค่าเริ่มต้นของ ScriptableObject Tile) → SetColor ต่อช่องใช้ได้ตอน edit แต่พอ Play/refresh
    /// Tilemap ดึง flag จาก Tile asset กลับมา ล็อกสีเป็นขาว = tint (paint/gradient) รีเซ็ตหมด
    ///
    /// เมนูนี้เคลียร์ LockColor (flags → None) ทุก Tile asset ใต้ Assets/Sprites แล้วระบายพื้นใหม่
    /// (ทำครั้งเดียวพอ — flag เป็นค่าใน asset เก็บถาวร · รันซ้ำได้ ปลอดภัย)
    ///
    /// รัน: NuclearReMind → Fix: ปลดล็อกสี Tile (แก้สีหายตอน Play)
    /// </summary>
    public static class TileColorUnlock
    {
        // เมนูนี้: เคลียร์ LockColor ทุก Tile asset ใต้ Assets/Sprites แล้วระบายพื้นใหม่
        [MenuItem("NuclearReMind/Fix: ปลดล็อกสี Tile (แก้สีหายตอน Play)")]
        public static void UnlockAndRefill()
        {
            int changed = UnlockAll();
            AssetDatabase.SaveAssets();

            // ระบายพื้นใหม่ให้ tilemap ดึงสี (gradient + override) กลับมา โดยไม่โดนล็อกแล้ว
            NuclearReMind.Editor.GridSpriteFiller.FillFromMenu();

            Debug.Log($"[TileColorUnlock] ✅ ปลดล็อกสี {changed} Tile asset + ระบายพื้นใหม่ · " +
                      "ทีนี้กด Play สีจะไม่หายแล้ว (อย่าลืม Ctrl+S)");
        }

        // เคลียร์ LockColor ทุก Tile asset ใต้ Assets/Sprites — คืนจำนวนใบที่แก้จริง
        private static int UnlockAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/Sprites" });
            int changed = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null || tile.flags == TileFlags.None) continue;
                tile.flags = TileFlags.None; // ทิ้ง LockColor (และ LockTransform ที่ไม่ได้ใช้)
                EditorUtility.SetDirty(tile);
                changed++;
            }
            return changed;
        }
    }
}
