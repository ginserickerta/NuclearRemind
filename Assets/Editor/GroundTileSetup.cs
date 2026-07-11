using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// พื้นหญ้าของทีม (fallback ใบเดียว เมื่อไม่มี IsoNature) — ตั้ง import settings ของ GroundGrassA.png แล้วสร้าง Tile asset
    ///
    /// ที่มาของ sprite: ตัด "หนึ่งช่องตาราง 24×24 px" จาก Map.PNG (mockup มองจากบน) แล้วบิดเป็นข้าวหลามตัด
    /// 128×64 ด้วยสูตรเดียวกับ GridManager (x' = x−y, y' = (x+y)/2)
    ///   • ใช้ไทล์เดียว (GroundGrassA) ทั้งพื้น — ไม่สลับลายหมากรุก (กวนตา)
    ///   • ขอบข้าวหลามตัดคม (ไม่ anti-alias) — ขอบนุ่มจะเห็นเป็นเส้นตารางจางๆ ตอน tile วางติดกัน
    ///
    /// PPU 128 = ความกว้าง tile → tile กว้าง 1 unit สูง 0.5 unit ตรงกับ Grid.cellSize (1, 0.5)
    ///
    /// ต้องรัน "ก่อน" Fill Grids — GridSpriteFiller โหลด Tile asset นี้ (fallback เมื่อไม่มี IsoNature)
    /// รัน: เมนู NuclearReMind/Setup Ground Tiles (Team Grass)
    /// </summary>
    public static class GroundTileSetup
    {
        private const string Folder = "Assets/Sprites/Art/Tiles";
        public const string TileAPath = Folder + "/GroundGrassA.asset";

        // tile กว้าง 128 px = 1 world unit (Grid.cellSize.x) → PPU 128
        private const int TilePixelsPerUnit = 128;

        [MenuItem("NuclearReMind/Setup Ground Tiles (Team Grass)")]
        public static void Apply()
        {
            var a = EnsureTile("GroundGrassA");

            AssetDatabase.SaveAssets();

            if (a == null)
            {
                Debug.LogWarning("[GroundTileSetup] สร้าง tile ไม่ได้ — Fill Grids จะถอยไปใช้ Ground.asset (placeholder)");
                return;
            }

            Debug.Log("[GroundTileSetup] ✅ พื้นหญ้าพร้อม — GroundGrassA (ไทล์เดียว ไม่มีลายหมากรุก) @ PPU " +
                      $"{TilePixelsPerUnit} · รัน Fill Grids เพื่อระบายพื้น");
        }

        /// <summary>ตั้ง importer ของ png แล้วสร้าง/อัปเดต Tile asset ชื่อเดียวกัน</summary>
        private static Tile EnsureTile(string name)
        {
            string pngPath = $"{Folder}/{name}.png";
            string tilePath = $"{Folder}/{name}.asset";

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[GroundTileSetup] ไม่พบ {pngPath} — รันสคริปต์สร้าง tile จาก Map.PNG ก่อน");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TilePixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 128;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; // pivot กลาง — Tilemap วางจากจุดกึ่งกลาง cell
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null)
            {
                Debug.LogError($"[GroundTileSetup] reimport แล้วโหลด Sprite จาก {pngPath} ไม่ได้");
                return null;
            }

            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                AssetDatabase.CreateAsset(tile, tilePath);
                Debug.Log($"[GroundTileSetup] สร้าง {tilePath}");
            }
            else
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }

            return tile;
        }
    }
}
