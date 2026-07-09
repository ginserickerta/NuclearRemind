using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ใส่ art จริงของทีม (Assets/Sprites/Art/Buildings/*.png) แทน placeholder ที่โค้ดวาด
    ///
    /// ศิลป์ชุดนี้วาดกลาง canvas 1600×1600 โดย margin แต่ละด้าน "ไม่เท่ากันสักไฟล์"
    /// (เช่น โรงไฟฟ้า ซ้าย 177 ขวา 50) ถ้าใช้ pivot ล่างกลางของ canvas ตึกจะเยื้อง+ลอยไม่เท่ากัน
    /// สคริปต์นี้จึงอ่าน pixel หา "กรอบศิลป์จริง" (alpha ≥ 128) แล้ว
    ///   1. pivot = ล่างกลางของ *กรอบศิลป์* ไม่ใช่ของ canvas → ฐานตึกนั่งบนช่องพอดีทุกหลัง
    ///   2. PPU = ความกว้างศิลป์ / ความกว้างเป้าหมาย → ตึกกว้างพอดี footprint ไม่ว่า art ขนาดไหน
    ///
    /// ความกว้างเป้าหมาย: กรอบ iso ของ footprint (a×b) กว้าง (a+b)/2 unit (tileWidth=1)
    /// คูณ WidthFactor 0.833 = สัดส่วนเดียวกับ CoreTower (3×3 → กรอบ 3.0 → art 2.5)
    ///
    /// อ่าน pixel ผ่าน ImageConversion.LoadImage (texture ชั่วคราวอ่านได้เสมอ)
    /// จึงไม่ต้องสลับ isReadable + reimport สองรอบ
    ///
    /// ต้องรัน "หลัง" Setup Hospital — Hospital.asset ถูกสร้างที่ขั้นนั้น
    /// รัน: เมนู NuclearReMind/Apply Building Art (Team Pixel Art)
    /// </summary>
    public static class BuildingArtSetup
    {
        private const string ArtFolder = "Assets/Sprites/Art/Buildings";
        private const string DataFolder = "Assets/ScriptableObjects/Buildings";

        // CoreTower: footprint 3×3 → กรอบ iso 3.0 unit, art กว้าง 2.5 → 0.8333
        private const float WidthFactor = 2.5f / 3.0f;

        // 1600px ย่อเหลือ 512 พอสำหรับตึกที่กินจอ ~200px — ประหยัด VRAM 9 เท่า
        private const int MaxTextureSize = 512;

        // CoreTower ไม่อยู่ในนี้ — มี art คนละชุด (CoreTowerSpriteSetup)
        // Mine / Memorial / OreDeposit / RadiationShelter / PowerConduit ยังไม่มี art จากทีม → คง placeholder
        private static readonly string[] Names =
        {
            "PowerPlant",
            "WaterPlant",
            "Farm",
            "Laboratory",
            "Hospital",
            "Habitat",
        };

        [MenuItem("NuclearReMind/Apply Building Art (Team Pixel Art)")]
        public static void Apply()
        {
            int applied = 0;

            foreach (var name in Names)
            {
                string spritePath = $"{ArtFolder}/{name}.png";
                string dataPath = $"{DataFolder}/{name}.asset";

                if (!File.Exists(spritePath))
                {
                    Debug.LogWarning($"[BuildingArtSetup] ไม่พบ {spritePath} — ข้าม {name}");
                    continue;
                }

                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingArtSetup] ไม่พบ {dataPath} — ข้าม {name} " +
                                     "(ถ้าเป็น Hospital ให้รัน Setup Hospital ก่อน)");
                    continue;
                }

                if (!TryGetArtBounds(spritePath, out int texW, out int texH,
                                     out int minX, out int minY, out int maxX, out int maxY))
                {
                    Debug.LogWarning($"[BuildingArtSetup] {name}: หา pixel ทึบไม่เจอ (ภาพโปร่งใสหมด?) — ข้าม");
                    continue;
                }

                int artW = maxX - minX + 1;

                // กรอบ iso ของ footprint กว้าง (a+b)/2 unit — footprint 0 ถือเป็น 1 กัน DivideByZero
                int sx = Mathf.Max(1, data.size.x);
                int sy = Mathf.Max(1, data.size.y);
                float targetWidth = (sx + sy) * 0.5f * WidthFactor;
                float ppu = artW / targetWidth;

                // GetPixels32 นับแถวจากล่างขึ้นบน → minY คือขอบล่างของศิลป์แล้ว (ตรงกับแกน pivot ของ Unity)
                var pivot = new Vector2((minX + maxX + 1) * 0.5f / texW, (float)minY / texH);

                ConfigureImporter(spritePath, ppu, pivot);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogError($"[BuildingArtSetup] reimport แล้วโหลด Sprite จาก {spritePath} ไม่ได้");
                    continue;
                }

                data.sprite = sprite;
                EditorUtility.SetDirty(data);
                applied++;

                Debug.Log($"[BuildingArtSetup] {name}: art {artW}×{maxY - minY + 1}px @ PPU {ppu:0.#} " +
                          $"= {targetWidth:0.##}×{(maxY - minY + 1) / ppu:0.##} unit · footprint {sx}×{sy} · " +
                          $"pivot ({pivot.x:0.###}, {pivot.y:0.###})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildingArtSetup] ✅ ใส่ art จริง {applied}/{Names.Length} หลัง " +
                      "(CoreTower ใช้ CoreTowerSpriteSetup · Mine/Memorial/RadiationShelter/OreDeposit ยังเป็น placeholder)");
        }

        /// <summary>
        /// หา bounding box ของ pixel ที่ alpha ≥ 128 — โหลด PNG เป็น texture ชั่วคราว (อ่านได้เสมอ)
        /// ค่า y นับจากล่างขึ้นบนตามแบบ Unity
        /// </summary>
        private static bool TryGetArtBounds(string path, out int texW, out int texH,
                                            out int minX, out int minY, out int maxX, out int maxY)
        {
            texW = texH = 0;
            minX = minY = int.MaxValue;
            maxX = maxY = -1;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                    return false;

                texW = tex.width;
                texH = tex.height;
                var px = tex.GetPixels32();

                for (int y = 0; y < texH; y++)
                {
                    int row = y * texW;
                    for (int x = 0; x < texW; x++)
                    {
                        if (px[row + x].a < 128) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                return maxX >= 0;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        private static void ConfigureImporter(string path, float ppu, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;                     // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxTextureSize;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;     // pivot ตามกรอบศิลป์ ไม่ใช่ BottomCenter ของ canvas
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
