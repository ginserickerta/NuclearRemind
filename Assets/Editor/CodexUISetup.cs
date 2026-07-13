using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าสไปรต์สกินโลหะของ Codex (Assets/Resources/CodexUI/*.png) ให้เป็น Sprite ที่ UI ใช้ได้
    ///   • frame_metal = กรอบโลหะ 9-slice (พื้นแผง/แผงย่อย) — ตั้ง spriteBorder ให้มุม/ขอบไม่ยืด
    ///   • plate_blank = พื้นแถวรายการ (มีช่องไอคอนซ้าย — ยืดเต็มแถว)
    ///   • tab_* = ปุ่มแท็บ (ข้อความ baked) · header_book = ไอคอนหัว · icon_* = ไอคอน entry (glyph โปร่ง)
    /// point filter + uncompressed (คมชัดแบบ pixel art) — รันก่อน Setup Codex System (ให้ sprite พร้อมก่อน build UI)
    /// รัน: เมนู NuclearReMind/Setup Codex UI Sprites — หรือรวมใน Run All Setups
    /// </summary>
    public static class CodexUISetup
    {
        private const string Folder = "Assets/Resources/CodexUI";

        // frame_metal 9-slice border (ซ้าย,ล่าง,ขวา,บน) — วัดจากกรอบสนิม + สลักมุม (ภาพ 922×208, ขอบทึบขวา ~895)
        private static readonly Vector4 FrameBorder = new Vector4(46, 44, 57, 44);

        [MenuItem("NuclearReMind/Setup Codex UI Sprites")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[CodexUISetup] ไม่พบโฟลเดอร์ {Folder} — วางไฟล์สกินก่อน");
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

                // เฉพาะ frame_metal = 9-slice (กรอบแผง) · ที่เหลือ sprite เดี่ยว
                bool isFrame = Path.GetFileNameWithoutExtension(assetPath) == "frame_metal";
                importer.spriteBorder = isFrame ? FrameBorder : Vector4.zero;

                importer.SaveAndReimport();
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CodexUISetup] ✅ นำเข้าสไปรต์สกิน Codex {n} ไฟล์ (frame_metal = 9-slice) — " +
                      "รัน Setup Codex System ต่อเพื่อ build แผงด้วยสกินนี้");
        }
    }
}
