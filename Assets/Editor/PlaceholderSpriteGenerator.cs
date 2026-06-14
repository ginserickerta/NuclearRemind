using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง placeholder sprite (สีพื้น) สำหรับอาคารและพื้น Tilemap
    /// ใช้ระหว่างรอ sprite จริงจากทีม Art
    /// </summary>
    public static class PlaceholderSpriteGenerator
    {
        private const string BuildingsFolder = "Assets/Sprites/Buildings";
        private const string TilesFolder = "Assets/Sprites/Tiles";
        private const string BuildingDataFolder = "Assets/ScriptableObjects/Buildings";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string GroundTilePath = TilesFolder + "/Ground.asset";

        private static readonly (string name, Color color)[] BuildingColors =
        {
            ("Habitat", new Color(0.40f, 0.70f, 1.00f)),
            ("Farm", new Color(0.40f, 0.80f, 0.30f)),
            ("WaterPlant", new Color(0.20f, 0.60f, 0.90f)),
            ("PowerPlant", new Color(0.95f, 0.85f, 0.20f)),
            ("RadiationShelter", new Color(0.95f, 0.50f, 0.10f)),
            ("Laboratory", new Color(0.60f, 0.30f, 0.80f)),
            ("CoreTower", new Color(0.90f, 0.20f, 0.20f)),
        };

        [MenuItem("NuclearReMind/Generate Placeholder Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(BuildingsFolder);
            Directory.CreateDirectory(TilesFolder);

            foreach (var (name, color) in BuildingColors)
                WritePng(Path.Combine(BuildingsFolder, name + ".png"), CreateSquareTexture(64, color));

            WritePng(Path.Combine(TilesFolder, "Ground.png"), CreateIsoDiamondTexture(128, 64,
                new Color(0.55f, 0.50f, 0.45f), new Color(0.35f, 0.32f, 0.28f)));

            AssetDatabase.Refresh();

            foreach (var (name, _) in BuildingColors)
                ConfigureSprite(Path.Combine(BuildingsFolder, name + ".png"), 64);
            ConfigureSprite(Path.Combine(TilesFolder, "Ground.png"), 128);

            AssetDatabase.Refresh();

            AssignBuildingIcons();
            CreateGroundTileAsset();
            PaintGroundTilemap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PlaceholderSpriteGenerator] สร้าง placeholder sprite, ใส่ icon ให้ BuildingData และวาด Ground tilemap 12x9 สำเร็จ");
        }

        private static Texture2D CreateSquareTexture(int size, Color fill)
        {
            const int border = 4;
            Color borderColor = new Color(0.12f, 0.12f, 0.12f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
                    tex.SetPixel(x, y, isBorder ? borderColor : fill);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateIsoDiamondTexture(int width, int height, Color fill, Color edge)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float cx = width / 2f;
            float cy = height / 2f;
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx) / cx;
                    float dy = Mathf.Abs(y + 0.5f - cy) / cy;
                    float d = dx + dy;
                    tex.SetPixel(x, y, d <= 1f ? (d > 0.92f ? edge : fill) : transparent);
                }
            }

            tex.Apply();
            return tex;
        }

        private static void WritePng(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void ConfigureSprite(string path, int pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void AssignBuildingIcons()
        {
            foreach (var (name, _) in BuildingColors)
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingData>(Path.Combine(BuildingDataFolder, name + ".asset"));
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(BuildingsFolder, name + ".png"));

                if (data == null || sprite == null)
                    continue;

                data.sprite = sprite;
                EditorUtility.SetDirty(data);
            }
        }

        private static void CreateGroundTileAsset()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(TilesFolder, "Ground.png"));
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(GroundTilePath);

            if (existing != null)
            {
                existing.sprite = sprite;
                EditorUtility.SetDirty(existing);
                return;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            AssetDatabase.CreateAsset(tile, GroundTilePath);
        }

        private static void PaintGroundTilemap()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var groundGO = GameObject.Find("Ground");

            if (groundGO == null)
                return;

            var tilemap = groundGO.GetComponent<Tilemap>();
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(GroundTilePath);

            if (tilemap == null || tile == null)
                return;

            for (int col = 0; col < 12; col++)
            {
                for (int row = 0; row < 9; row++)
                {
                    tilemap.SetTile(new Vector3Int(col, row, 0), tile);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
