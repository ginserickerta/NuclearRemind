using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ตั้งค่า import ของ sprite แผง CORE TOWER (Resources/CoreTowerUI) ให้เป็น Sprite
    /// จำเป็นเพราะ CoreTowerPanelUI โหลด runtime ผ่าน Resources.Load&lt;Sprite&gt; — texture ธรรมดาโหลดไม่ได้
    ///
    /// รัน: เมนู NuclearReMind/Setup Core Tower UI Sprites (รันครั้งเดียวหลังนำเข้าไฟล์ · idempotent)
    /// </summary>
    public static class CoreTowerUISetup
    {
        private const string Folder = "Assets/Resources/CoreTowerUI";

        [MenuItem("NuclearReMind/Setup Core Tower UI Sprites")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogError($"[CoreTowerUISetup] ไม่พบโฟลเดอร์ {Folder}");
                return;
            }

            int done = 0;
            foreach (var path in Directory.GetFiles(Folder, "*.png"))
            {
                var assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                var s = new TextureImporterSettings();
                importer.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;   // เต็ม quad — วาง/ pop ไม่เพี้ยน
                s.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(s);

                importer.SaveAndReimport();
                done++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CoreTowerUISetup] ✅ ตั้ง import เป็น Sprite แล้ว {done} ไฟล์ ใน {Folder}");
        }
    }
}
