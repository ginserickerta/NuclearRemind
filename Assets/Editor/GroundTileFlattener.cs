using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง "ไทล์หน้าบนแบน" (flat_XXX) จากไทล์บล็อก 2.5D ที่ผู้ใช้คัดไว้ใน Art/Tiles (tile_XXX)
    ///
    /// ปัญหา: ไทล์ IsoNature เป็นบล็อก (หน้าบน + ผนังห้อยลง) เรียงแล้วผนังโผล่ ซ้อนเป็นก้อน
    /// วิธี (ตามที่ผู้ใช้ออกแบบ): ข้างในใช้ "หน้าบนอย่างเดียว วางแบน" · ขอบใช้บล็อกเต็ม (ยกขอบ)
    ///   → สคริปต์นี้ตัดเฉพาะ rhombus หน้าบน (2:1 เต็มช่อง) ที่เหลือทำโปร่งใส แล้วเซฟเป็น flat_XXX + Tile asset
    ///
    /// GridSpriteFiller: ช่องข้างใน → flat_XXX (แบน ไร้ผนัง เรียงเนียน) · ช่องขอบหน้า → tile_XXX (บล็อกเต็ม = ผนังยกขอบ)
    ///
    /// rhombus หน้าบน: |x-cx|/(W/2) + |y-cy|/(H/4) ≤ 1  (สูง = ครึ่งของกว้าง = 2:1 พอดี cellSize 1×0.5)
    /// อ่าน pixel จาก PNG ต้นฉบับผ่าน LoadImage (ไม่ต้องแตะ import ต้นฉบับ)
    ///
    /// รัน: เมนู NuclearReMind/Flatten Ground Tiles (Top Face) — หรือใน Run All Setups (ก่อน Fill Grids)
    /// </summary>
    public static class GroundTileFlattener
    {
        private const string ArtTilesFolder = "Assets/Sprites/Art/Tiles";
        private const string FlatFolder = "Assets/Sprites/Art/Tiles/Flat";
        private const string DarkFolder = "Assets/Sprites/Art/Tiles/Flat/Dark"; // คู่ "เข้ม" (สไปรต์เดิม tint เทา) สำหรับลายหมากรุก
        private const string SourceFolder = "Assets/Sprites/Tiles/IsoNature";
        private const int TilePixelsPerUnit = 32;
        // ระดับความเข้มของช่องเข้มในลายหมากรุก (1 = เท่าเดิม, ต่ำลง = เข้มขึ้น) — ปรับได้
        // 0.93 = ต่างกันเล็กน้อยแบบ Clash of Clans (ต้องมองดีๆ ถึงแยกออก)
        private static readonly Color DarkTint = new Color(0.93f, 0.93f, 0.93f, 1f);

        [MenuItem("NuclearReMind/Flatten Ground Tiles (Top Face)")]
        public static void Apply()
        {
            Directory.CreateDirectory(FlatFolder);
            Directory.CreateDirectory(DarkFolder);

            var indices = CuratedIndices();
            if (indices.Count == 0)
            {
                Debug.LogWarning($"[GroundTileFlattener] ไม่พบ tile_XXX.asset ใน {ArtTilesFolder} — คัดไทล์ใส่ก่อน");
                return;
            }

            int made = 0;
            foreach (int i in indices)
                if (MakeFlatTile(i)) made++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GroundTileFlattener] ✅ สร้างไทล์หน้าบนแบน {made}/{indices.Count} ใบ ใน {FlatFolder} " +
                      "— รัน Fill Grids เพื่อระบาย (ข้างในแบน · ขอบบล็อกเต็ม)");
        }

        // index ของ tile_XXX.asset ที่ผู้ใช้คัดไว้ใน Art/Tiles (ช่วง 0–40 = ดิน+หญ้า)
        private static List<int> CuratedIndices()
        {
            var list = new List<int>();
            for (int i = 0; i <= 40; i++)
                if (File.Exists($"{ArtTilesFolder}/tile_{i:000}.asset"))
                    list.Add(i);
            return list;
        }

        // ตัดหน้าบน rhombus จาก IsoNature/tile_XXX.png → flat_XXX.png + Tile asset
        private static bool MakeFlatTile(int index)
        {
            string srcPng = $"{SourceFolder}/tile_{index:000}.png";
            if (!File.Exists(srcPng))
            {
                Debug.LogWarning($"[GroundTileFlattener] ไม่พบต้นฉบับ {srcPng} — ข้าม index {index}");
                return false;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(srcPng)))
            {
                Object.DestroyImmediate(tex);
                Debug.LogWarning($"[GroundTileFlattener] อ่าน {srcPng} ไม่ได้ — ข้าม");
                return false;
            }

            int w = tex.width, h = tex.height;
            var src = tex.GetPixels32();
            var dst = new Color32[src.Length];      // เริ่มจากโปร่งใสหมด (a = 0)

            float cx = (w - 1) / 2f;
            float cy = h / 2f;                       // กึ่งกลาง canvas = จุดหมุน (pivot center)
            float halfW = w / 2f;
            float halfH = h / 4f;                    // 2:1 → สูงครึ่งของกว้าง

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float rh = Mathf.Abs(x - cx) / halfW + Mathf.Abs(y - cy) / halfH;
                    if (rh <= 1f) dst[y * w + x] = src[y * w + x]; // อยู่ใน rhombus หน้าบน → เก็บ
                }

            Object.DestroyImmediate(tex);

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(dst);
            outTex.Apply();
            string outPng = $"{FlatFolder}/flat_{index:000}.png";
            File.WriteAllBytes(outPng, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(outPng, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(outPng);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPng);
            if (sprite == null) return false;

            string tilePath = $"{FlatFolder}/flat_{index:000}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                tile.sprite = sprite;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }

            // คู่ "เข้ม": สไปรต์เดียวกัน แต่ tile.color = เทา → เรนเดอร์เข้มลง (ไม่ต้องสร้าง PNG ใหม่)
            string darkPath = $"{DarkFolder}/flat_{index:000}.asset";
            var darkTile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(darkPath);
            if (darkTile == null)
            {
                darkTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                darkTile.sprite = sprite;
                darkTile.color = DarkTint;
                AssetDatabase.CreateAsset(darkTile, darkPath);
            }
            else
            {
                darkTile.sprite = sprite;
                darkTile.color = DarkTint;
                EditorUtility.SetDirty(darkTile);
            }
            return true;
        }

        private static void ConfigureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TilePixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 64;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(s);
            importer.SaveAndReimport();
        }
    }
}
