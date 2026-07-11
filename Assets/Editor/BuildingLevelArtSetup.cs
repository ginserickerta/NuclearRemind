using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ใส่ art จริงของทีมแบบ "แยกตามระดับอัปเกรด" (L1/L2/L3) เข้า BuildingData.levelSprites
    ///   • ไฟล์อยู่ที่ Assets/Sprites/Art/Buildings/Levels/{Name}_L{1,2,3}.png
    ///   • pivot = ล่างกลางของ *กรอบศิลป์จริง* (alpha ≥ 128) → ฐานตึกนั่งช่องพอดีทุกระดับ
    ///     (L3 สูงกว่า L1 ได้ แต่ยังนั่งฐานเดิม — โตขึ้นด้านบน)
    ///   • PPU = ความกว้างศิลป์ / ความกว้างเป้าหมายตาม footprint → footprint กว้างเท่ากันทุกระดับ
    ///   • sprite เดี่ยว (data.sprite) ตั้งเป็น L1 — ใช้กับ hotbar/ghost/ไอคอน
    ///
    /// BuildingVisualSpawner สลับ sprite ตาม OnBuildingUpgraded (L1→L2→L3) ให้ตอน runtime
    /// ต้องรัน "หลัง" Setup Hospital + Apply Building Art (asset ถูกสร้าง/ตั้ง sprite เดี่ยวมาก่อน)
    /// รัน: เมนู NuclearReMind/Apply Building Level Art (L1-L3)
    /// </summary>
    public static class BuildingLevelArtSetup
    {
        private const string LevelFolder = "Assets/Sprites/Art/Buildings/Levels";
        private const string DataFolder = "Assets/ScriptableObjects/Buildings";

        // สัดส่วนเดียวกับ BuildingArtSetup (CoreTower 3×3 → กรอบ 3.0 → art 2.5)
        private const float WidthFactor = 2.5f / 3.0f;
        private const int MaxTextureSize = 512;

        // อาคารที่ทีมส่ง art แยกระดับมา (Laboratory/CoreTower มี art คนละชุด — ไม่อยู่ในนี้)
        private static readonly string[] Names =
        {
            "PowerPlant",   // ไฟฟ้า
            "WaterPlant",   // โรงน้ำ
            "Farm",         // ฟาร์ม
            "Hospital",     // รพ
            "Habitat",      // พักพิง
        };

        [MenuItem("NuclearReMind/Apply Building Level Art (L1-L3)")]
        public static void Apply()
        {
            int applied = 0;

            foreach (var name in Names)
            {
                string dataPath = $"{DataFolder}/{name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingLevelArtSetup] ไม่พบ {dataPath} — ข้าม {name}");
                    continue;
                }

                var sprites = new Sprite[3];
                bool ok = true;
                for (int lvl = 1; lvl <= 3; lvl++)
                {
                    string spritePath = $"{LevelFolder}/{name}_L{lvl}.png";
                    if (!File.Exists(spritePath))
                    {
                        Debug.LogWarning($"[BuildingLevelArtSetup] ไม่พบ {spritePath} — ข้าม {name}");
                        ok = false;
                        break;
                    }

                    if (!TryGetArtBounds(spritePath, out int texW, out int texH,
                                         out int minX, out int minY, out int maxX, out int maxY))
                    {
                        Debug.LogWarning($"[BuildingLevelArtSetup] {name}_L{lvl}: หา pixel ทึบไม่เจอ — ข้าม");
                        ok = false;
                        break;
                    }

                    int artW = maxX - minX + 1;
                    int sx = Mathf.Max(1, data.size.x);
                    int sy = Mathf.Max(1, data.size.y);
                    float targetWidth = (sx + sy) * 0.5f * WidthFactor;
                    float ppu = artW / targetWidth;
                    var pivot = new Vector2((minX + maxX + 1) * 0.5f / texW, (float)minY / texH);

                    ConfigureImporter(spritePath, ppu, pivot);

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null)
                    {
                        Debug.LogError($"[BuildingLevelArtSetup] reimport แล้วโหลด Sprite จาก {spritePath} ไม่ได้");
                        ok = false;
                        break;
                    }
                    sprites[lvl - 1] = sprite;

                    Debug.Log($"[BuildingLevelArtSetup] {name}_L{lvl}: art {artW}×{maxY - minY + 1}px @ PPU {ppu:0.#} " +
                              $"pivot ({pivot.x:0.###}, {pivot.y:0.###})");
                }

                if (!ok) continue;

                data.levelSprites = sprites;
                data.sprite = sprites[0]; // L1 = ภาพเริ่มต้น (hotbar/ghost/ไอคอน)
                EditorUtility.SetDirty(data);
                applied++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildingLevelArtSetup] ✅ ใส่ art แยกระดับ {applied}/{Names.Length} หลัง (L1-L3) — " +
                      "BuildingVisualSpawner สลับภาพให้เมื่ออัปเกรด");
        }

        // ── helpers (คัดลอกจาก BuildingArtSetup — private ที่ต้นทาง) ──

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
            importer.filterMode = FilterMode.Point;                 // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxTextureSize;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; // pivot ตามกรอบศิลป์
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
