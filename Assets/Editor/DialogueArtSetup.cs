using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้าอาร์ตบทสนทนา (v9) ให้เป็น Sprite ที่ UI ใช้ได้ — ต้องรันก่อน Setup Story UI (SetupDialogueUI โหลดไปใช้)
    ///   • Kova portrait 9 อารมณ์ — Assets/Sprites/Characters/Kova/kova_*.png (ครึ่งตัว พื้นโปร่ง)
    ///   • Auren portrait 6 อารมณ์ — Assets/Sprites/Characters/Auren/auren_*.png (เสียงในใจ ฝั่งซ้าย)
    ///   • กรอบบทพูด 3 ขนาด — Assets/Resources/DialogueUI/frame_{short,medium,long}.png (เลือกตามความยาวประโยค)
    /// point filter + alphaIsTransparency (คมแบบ pixel art) · กรอบ uncompressed · portrait ย่อ 1024 ประหยัดหน่วยความจำ
    /// รัน: เมนู NuclearReMind/Setup Dialogue Art — หรือรวมใน Run All Setups
    /// </summary>
    public static class DialogueArtSetup
    {
        private const string KovaFolder  = "Assets/Sprites/Characters/Kova";
        private const string AurenFolder = "Assets/Sprites/Characters/Auren";
        private const string MiraFolder  = "Assets/Sprites/Characters/Mira";
        private const string DornFolder  = "Assets/Sprites/Characters/Dorn";
        private const string FrameFolder = "Assets/Resources/DialogueUI";

        [MenuItem("NuclearReMind/Setup Dialogue Art")]
        public static void Apply()
        {
            int n = 0;
            n += ImportFolder(KovaFolder, maxSize: 1024, uncompressed: false);
            n += ImportFolder(AurenFolder, maxSize: 1024, uncompressed: false);
            n += ImportFolder(MiraFolder, maxSize: 1024, uncompressed: false);
            n += ImportFolder(DornFolder, maxSize: 1024, uncompressed: false);
            n += ImportFolder(FrameFolder, maxSize: 2048, uncompressed: true);
            AssetDatabase.Refresh();
            Debug.Log($"[DialogueArtSetup] ✅ นำเข้าอาร์ตบทสนทนา {n} ไฟล์ (Kova 9 + Auren 6 + Mira 6 + Dorn 5 อารมณ์ + กรอบ 3 ขนาด) — " +
                      "รัน Setup Story UI ต่อเพื่อ build แผงบทสนทนาด้วยอาร์ตนี้");
        }

        private static int ImportFolder(string folder, int maxSize, bool uncompressed)
        {
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"[DialogueArtSetup] ไม่พบโฟลเดอร์ {folder} — วางไฟล์ก่อน");
                return 0;
            }
            int n = 0;
            foreach (var path in Directory.GetFiles(folder, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = uncompressed
                    ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.Compressed;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = Vector4.zero; // ใช้เป็น Simple/preserveAspect ไม่ 9-slice

                importer.SaveAndReimport();
                n++;
            }
            return n;
        }
    }
}
