using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าสไปรต์สกินโลหะของห้องวิจัย (Assets/Resources/LabUI/*.png) ให้เป็น Sprite ที่ UI ใช้ได้
    ///   • icon_lab / icon_engineer / icon_microscope = ไอคอนในกรอบโลหะ (หัวข้อ/ฝึก/โครงงาน)
    ///   • btn_train / btn_research = ปุ่มโลหะ (ตัวหนังสือ baked — โค้ดทับเลข/สถานะสดทับอีกที)
    /// point filter + uncompressed (คมชัดแบบ pixel art) · Simple (ไม่ 9-slice — คุมสัดส่วนเองในโค้ด)
    /// รัน: เมนู NuclearReMind/Setup Lab UI Sprites (LabPanelUI โหลด sprite ตอนรัน → รันเมนูนี้ครั้งเดียวก็พอ)
    /// </summary>
    public static class LabUISetup
    {
        private const string Folder = "Assets/Resources/LabUI";

        [MenuItem("NuclearReMind/Setup Lab UI Sprites")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[LabUISetup] ไม่พบโฟลเดอร์ {Folder} — วางไฟล์สกินก่อน");
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
                importer.spriteBorder = Vector4.zero; // Simple — คุมสัดส่วนเองในโค้ด

                importer.SaveAndReimport();
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[LabUISetup] ✅ นำเข้าสไปรต์ห้องวิจัย {n} ไฟล์ — กด Play เปิดแผงห้องวิจัยดูสกินใหม่");
        }
    }
}
