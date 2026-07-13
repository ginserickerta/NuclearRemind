using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าสไปรต์สกินของการ์ดบันทึก + ควิซ (Assets/Resources/StoryUI/*.png) ให้เป็น Sprite ที่ UI ใช้ได้
    ///   • frame_wood  = กรอบไม้ 9-slice (แผงการ์ดบันทึก) — มุมโลหะ + ร่องขอบไม่ยืด
    ///   • plate_inset = แผ่นจมโลหะสะอาด 9-slice (กล่องโจทย์/ตัวเลือก/footer ของควิซ)
    ///   • btn_metal   = ปุ่มโลหะเข้ม 9-slice (ปุ่ม "รับทราบ" ของการ์ดบันทึก)
    ///   • smear       = รอยแปรงเข้ม (พื้นหลังหัวเรื่อง/ชื่อผู้บันทึก — ยืดแนวนอน)
    /// point filter + uncompressed (คมแบบ pixel art) — ต้องรันก่อน Setup HUD Canvas / Setup Story UI (ให้ sprite พร้อมก่อน build)
    /// รัน: เมนู NuclearReMind/Setup Story UI Sprites — หรือรวมใน Run All Setups
    /// </summary>
    public static class StoryUISpriteSetup
    {
        private const string Folder = "Assets/Resources/StoryUI";

        // 9-slice border ต่อไฟล์ (ซ้าย,ล่าง,ขวา,บน) — smear ไม่ slice (ยืดทั้งใบ)
        private static Vector4 BorderFor(string name)
        {
            switch (name)
            {
                case "frame_wood":  return new Vector4(60, 60, 60, 60);
                case "plate_inset": return new Vector4(26, 26, 26, 26);
                case "btn_metal":   return new Vector4(20, 20, 20, 20);
                default:            return Vector4.zero; // smear ฯลฯ
            }
        }

        [MenuItem("NuclearReMind/Setup Story UI Sprites")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[StoryUISpriteSetup] ไม่พบโฟลเดอร์ {Folder} — วางไฟล์สกินก่อน");
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
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = BorderFor(Path.GetFileNameWithoutExtension(assetPath));

                importer.SaveAndReimport();
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[StoryUISpriteSetup] ✅ นำเข้าสไปรต์ StoryUI {n} ไฟล์ (frame_wood/plate_inset/btn_metal = 9-slice) — " +
                      "รัน Setup HUD Canvas + Setup Story UI ต่อเพื่อ build แผงด้วยสกินนี้");
        }
    }
}
