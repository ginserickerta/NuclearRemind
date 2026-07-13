using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าสไปรต์สกินโลหะของ HUD (Assets/Resources/HUD/*.png) ให้เป็น Sprite ที่ UI ใช้ได้
    ///   • day_plate = แผ่นโลหะ "DAY __ /30" (ข้อความ baked · เว้นช่องกลางไว้ใส่เลขวัน) — Simple + preserveAspect
    /// ต่างจากสกิน Codex/Story (pixel-art, Point) — plate นี้เป็นเท็กซ์เจอร์โลหะละเอียด ย่อจาก ~1430px → ~320px
    /// จึงใช้ Bilinear ให้เนียน (ไม่ใช่ Point) · ไม่ 9-slice (ข้อความ baked ห้ามยืด)
    /// รันก่อน Setup HUD Canvas (SetupHUD โหลด plate ผ่าน LoadUISkin) — หรือรวมใน Run All Setups
    /// </summary>
    public static class HUDSpriteSetup
    {
        private const string Folder = "Assets/Resources/HUD";

        [MenuItem("NuclearReMind/Setup HUD Sprites")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[HUDSpriteSetup] ไม่พบโฟลเดอร์ {Folder} — วางไฟล์สกินก่อน");
                return;
            }

            int n = 0;
            foreach (var path in Directory.GetFiles(Folder, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;   // เท็กซ์เจอร์โลหะ ย่อเยอะ → เนียนกว่า Point
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = Vector4.zero;        // Simple/preserveAspect — ไม่ 9-slice

                importer.SaveAndReimport();
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HUDSpriteSetup] ✅ นำเข้าสไปรต์ HUD {n} ไฟล์ (day_plate = แผ่นวัน) — " +
                      "รัน Setup HUD Canvas ต่อเพื่อ build แผงวันด้วยสกินนี้");
        }
    }
}
