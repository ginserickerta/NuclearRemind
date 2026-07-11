using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// นำเข้า isometric nature tileset (115 ไทล์ 32×32) ให้พร้อมใช้กับกริดเดิม —
    /// ★ ยังไม่แทนที่พื้นเดิม (ไม่แตะ GridSpriteFiller/scene) · แค่ import + สร้าง Tile asset ให้เลือกใช้ทีหลัง
    ///
    /// เรขาคณิต: หน้าบนข้าวหลามตัดกว้างเต็ม 32px สูง 16px (2:1) · แถวกว้างสุด ≈ กลางภาพ
    ///   → PPU 32 = ไทล์กว้าง 1 unit · หน้าบนสูง 0.5 unit = ตรง Grid.cellSize (1, 0.5) พอดี
    ///   → pivot กลาง (0.5,0.5) เหมือน GroundGrassA/B (ต่างแค่ PPU 128→32)
    ///   16px ล่าง = ผนังบล็อกห้อยลงช่องถัดไป (ลุค 2.5D — Tilemap sort ทับให้เอง)
    ///
    /// จับคู่โซน (ตามที่ตกลง — ยังไม่ระบาย รอสั่งวิธีใส่):
    ///   Zone A (หญ้า)      = GrassGround
    ///   Zone B (ดิน/หิน)   = DirtGround
    /// ที่เหลือ (ดอกไม้/ก้อนหิน/น้ำ/น้ำแข็ง) import ไว้ให้ใช้เป็น decoration/variety
    ///
    /// รัน: เมนู NuclearReMind/Setup Iso Nature Tileset (Import Only)
    /// </summary>
    public static class IsoTilesetSetup
    {
        private const string Folder = "Assets/Sprites/Tiles/IsoNature";
        private const string TileFolder = Folder + "/TileAssets";
        private const int TileCount = 115;

        // PPU 32 → หน้าบน 32×16 px = 1×0.5 unit = Grid.cellSize เดิม
        private const int TilePixelsPerUnit = 32;

        // ── จับคู่โซน (index ของ tile_XXX.png) — ปรับได้ที่นี่ที่เดียว ──
        /// <summary>Zone B — ดิน/หิน/ไม้กระดาน (บล็อกพื้นสีน้ำตาล)</summary>
        public static readonly (int from, int to) DirtGround = (0, 21);
        /// <summary>Zone A — หญ้า (บล็อกพื้นเขียว หลายระดับความหนา)</summary>
        public static readonly (int from, int to) GrassGround = (22, 40);

        // decoration / variety (ยังไม่จับโซน — ให้ผู้ใช้เลือก)
        public static readonly (int from, int to) Flowers = (41, 47);
        public static readonly (int from, int to) SmallRocksLogs = (48, 60);
        public static readonly (int from, int to) LargeRocks = (61, 83);
        public static readonly (int from, int to) Water = (86, 103);
        public static readonly (int from, int to) Ice = (104, 114);

        /// <summary>path ของ Tile asset ที่ index นี้ (ให้โค้ด wiring ภายหลังโหลดใช้)</summary>
        public static string TileAssetPath(int index) => $"{TileFolder}/tile_{index:000}.asset";

        /// <summary>list ของ Tile asset ในช่วง index (โหลดตอน wiring — คืนเฉพาะที่โหลดได้)</summary>
        public static List<TileBase> LoadRange((int from, int to) range)
        {
            var list = new List<TileBase>();
            for (int i = range.from; i <= range.to; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetPath(i));
                if (t != null) list.Add(t);
            }
            return list;
        }

        [MenuItem("NuclearReMind/Setup Iso Nature Tileset (Import Only)")]
        public static void Apply()
        {
            if (!System.IO.Directory.Exists(TileFolder))
                System.IO.Directory.CreateDirectory(TileFolder);

            int sprites = 0, tiles = 0, missing = 0;
            for (int i = 0; i < TileCount; i++)
            {
                string png = $"{Folder}/tile_{i:000}.png";
                if (AssetImporter.GetAtPath(png) == null) { missing++; continue; }

                ConfigureSprite(png);
                sprites++;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if (sprite == null) { missing++; continue; }

                if (EnsureTile(TileAssetPath(i), sprite)) tiles++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[IsoTilesetSetup] ✅ import {sprites} sprite · สร้าง/อัปเดต {tiles} Tile asset " +
                      (missing > 0 ? $"· ขาด {missing} ไฟล์ " : "") +
                      $"@ PPU {TilePixelsPerUnit} pivot กลาง\n" +
                      $"   Zone A (หญ้า) = tile {GrassGround.from}–{GrassGround.to} · " +
                      $"Zone B (ดิน/หิน) = tile {DirtGround.from}–{DirtGround.to}\n" +
                      "   ★ ยังไม่ระบายพื้น — บอกวิธีใส่ได้เลย เดี๋ยว wire ให้");
        }

        private static void ConfigureSprite(string png)
        {
            var importer = AssetImporter.GetAtPath(png) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TilePixelsPerUnit;
            importer.filterMode = FilterMode.Point;              // pixel art ห้าม bilinear
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 32;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; // pivot กลาง = จุดกึ่งกลาง cell (เหมือน GroundGrass)
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static bool EnsureTile(string tilePath, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }
            return true;
        }
    }
}
