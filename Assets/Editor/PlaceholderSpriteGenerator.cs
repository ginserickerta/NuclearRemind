using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง placeholder sprite (ทรงไอโซเมตริกแบบเรียบง่าย เฉพาะตัวต่ออาคาร) สำหรับอาคารและพื้น Tilemap
    /// ใช้ระหว่างรอ sprite จริงจากทีม Art
    /// </summary>
    public static class PlaceholderSpriteGenerator
    {
        private const string BuildingsFolder = "Assets/Sprites/Buildings";
        private const string TilesFolder = "Assets/Sprites/Tiles";
        private const string BuildingDataFolder = "Assets/ScriptableObjects/Buildings";
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string GroundTilePath = TilesFolder + "/Ground.asset";
        private const int BuildingPixelsPerUnit = 64;

        // ===== Dark theme (post-apocalyptic) =====
        private static readonly Color BuildingFill    = Hex("#4A5568"); // เทาเข้ม silhouette
        private static readonly Color BuildingOutline = Hex("#718096"); // เส้นขอบสว่างกว่า
        private static readonly Color GroundFill       = Hex("#1C2333"); // พื้น tile
        private static readonly Color GroundEdge       = Hex("#2D3748"); // เส้นขอบ tile
        private static readonly Color CameraBackground = Hex("#0D1117"); // ดำอมเทา

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static readonly string[] BuildingNames =
        {
            "Habitat",
            "Farm",
            "WaterPlant",
            "PowerPlant",
            "RadiationShelter",
            "Laboratory",
            "CoreTower",
        };

        [MenuItem("NuclearReMind/Generate Placeholder Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(BuildingsFolder);
            Directory.CreateDirectory(TilesFolder);

            foreach (var name in BuildingNames)
            {
                var tex = CreateBuildingTexture(name);
                ApplySilhouette(tex, BuildingFill, BuildingOutline); // dark theme: เทาเข้ม + outline
                WritePng(Path.Combine(BuildingsFolder, name + ".png"), tex);
            }

            WritePng(Path.Combine(TilesFolder, "Ground.png"), CreateIsoDiamondTexture(128, 64,
                GroundFill, GroundEdge));

            AssetDatabase.Refresh();

            foreach (var name in BuildingNames)
                ConfigureBuildingSprite(Path.Combine(BuildingsFolder, name + ".png"));
            ConfigureSprite(Path.Combine(TilesFolder, "Ground.png"), 128);

            AssetDatabase.Refresh();

            AssignBuildingIcons();
            CreateGroundTileAsset();
            PaintGroundTilemap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PlaceholderSpriteGenerator] สร้าง dark-theme silhouette sprite, ตั้ง camera bg #0D1117, วาด Ground tilemap 20x12 สำเร็จ");
        }

        private static Texture2D CreateBuildingTexture(string name)
        {
            switch (name)
            {
                case "Habitat": return DrawHabitat();
                case "Farm": return DrawFarm();
                case "WaterPlant": return DrawWaterPlant();
                case "PowerPlant": return DrawPowerPlant();
                case "RadiationShelter": return DrawRadiationShelter();
                case "Laboratory": return DrawLaboratory();
                case "CoreTower": return DrawCoreTower();
                default: return NewCanvas(64, 64);
            }
        }

        // ===== Building drawings (front-facing silhouettes, pivot = bottom-center) =====

        private static Texture2D DrawHabitat()
        {
            var tex = NewCanvas(64, 64);
            Color wall = new Color(0.85f, 0.80f, 0.65f);
            Color roof = new Color(0.55f, 0.22f, 0.15f);
            Color door = new Color(0.30f, 0.18f, 0.10f);
            Color window = new Color(0.95f, 0.90f, 0.55f);

            FillRect(tex, 8, 4, 55, 31, wall);
            FillTriangle(tex, new Vector2(4, 32), new Vector2(60, 32), new Vector2(32, 58), roof);
            FillRect(tex, 26, 4, 37, 17, door);
            FillRect(tex, 12, 18, 21, 27, window);
            FillRect(tex, 42, 18, 51, 27, window);

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawFarm()
        {
            var tex = NewCanvas(96, 48);
            Color soil = new Color(0.42f, 0.28f, 0.15f);
            Color crop = new Color(0.35f, 0.68f, 0.22f);
            Color barnWall = new Color(0.70f, 0.20f, 0.15f);
            Color barnRoof = new Color(0.45f, 0.12f, 0.10f);

            FillRect(tex, 2, 2, 93, 39, soil);
            for (int i = 0; i < 4; i++)
            {
                int y0 = 6 + i * 8;
                FillRect(tex, 6, y0, 90, y0 + 3, crop);
            }
            FillRect(tex, 70, 28, 92, 39, barnWall);
            FillTriangle(tex, new Vector2(68, 39), new Vector2(94, 39), new Vector2(81, 47), barnRoof);

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawWaterPlant()
        {
            var tex = NewCanvas(64, 96);
            Color leg = new Color(0.30f, 0.30f, 0.32f);
            Color tank = new Color(0.25f, 0.55f, 0.85f);
            Color cap = new Color(0.50f, 0.78f, 0.95f);

            FillRect(tex, 14, 0, 18, 22, leg);
            FillRect(tex, 28, 0, 36, 22, leg);
            FillRect(tex, 46, 0, 50, 22, leg);
            FillRect(tex, 10, 22, 53, 80, tank);
            FillEllipse(tex, 32, 80, 22, 10, cap);

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawPowerPlant()
        {
            var tex = NewCanvas(128, 140);
            Color body = new Color(0.50f, 0.50f, 0.55f);
            Color roof = new Color(0.38f, 0.38f, 0.42f);
            Color chimney = new Color(0.35f, 0.35f, 0.40f);
            Color stripe = new Color(0.95f, 0.85f, 0.20f);
            Color smoke = new Color(0.85f, 0.85f, 0.88f, 0.55f);

            FillRect(tex, 8, 8, 119, 90, body);
            FillRect(tex, 8, 90, 119, 100, roof);
            FillRect(tex, 8, 40, 119, 52, stripe);
            FillRect(tex, 80, 100, 104, 130, chimney);
            FillCircle(tex, 92, 134, 12, smoke);
            FillCircle(tex, 104, 140, 9, smoke);

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawRadiationShelter()
        {
            var tex = NewCanvas(64, 56);
            Color baseColor = new Color(0.40f, 0.40f, 0.44f);
            Color dome = new Color(0.85f, 0.45f, 0.10f);
            Color trefoil = new Color(0.15f, 0.10f, 0.05f);

            FillEllipse(tex, 32, 18, 26, 26, dome);
            FillRect(tex, 6, 0, 57, 18, baseColor);

            Vector2 center = new Vector2(32, 30);
            DrawWedge(tex, center, 10f, 0f, trefoil);
            DrawWedge(tex, center, 10f, 120f, trefoil);
            DrawWedge(tex, center, 10f, 240f, trefoil);
            FillCircle(tex, 32, 30, 4, trefoil);

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawLaboratory()
        {
            var tex = NewCanvas(64, 88);
            Color wall = new Color(0.45f, 0.30f, 0.65f);
            Color roof = new Color(0.33f, 0.20f, 0.48f);
            Color pole = new Color(0.6f, 0.6f, 0.6f);
            Color dish = new Color(0.85f, 0.85f, 0.9f);
            Color window = new Color(0.75f, 0.90f, 0.95f);

            FillRect(tex, 8, 4, 55, 49, wall);
            FillRect(tex, 8, 50, 55, 55, roof);
            FillRect(tex, 30, 56, 34, 73, pole);
            FillCircle(tex, 32, 76, 8, dish);

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int x0 = 14 + col * 12;
                    int y0 = 12 + row * 16;
                    FillRect(tex, x0, y0, x0 + 8, y0 + 10, window);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D DrawCoreTower()
        {
            var tex = NewCanvas(160, 176);
            Color baseColor = new Color(0.45f, 0.15f, 0.15f);
            Color mid = new Color(0.58f, 0.20f, 0.20f);
            Color top = new Color(0.70f, 0.25f, 0.25f);
            Color stripe = new Color(0.95f, 0.80f, 0.20f);
            Color glow = new Color(1f, 0.95f, 0.45f, 0.5f);
            Color core = new Color(1f, 0.95f, 0.45f);

            FillRect(tex, 30, 0, 129, 39, baseColor);
            FillRect(tex, 45, 40, 114, 129, mid);
            FillRect(tex, 60, 130, 99, 159, top);
            FillRect(tex, 45, 70, 114, 78, stripe);
            FillRect(tex, 45, 100, 114, 108, stripe);
            FillCircle(tex, 80, 168, 16, glow);
            FillCircle(tex, 80, 168, 10, core);

            tex.Apply();
            return tex;
        }

        // ===== Drawing primitives =====

        private static Texture2D NewCanvas(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            tex.SetPixels(pixels);
            return tex;
        }

        private static void SetPixelSafe(Texture2D tex, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, c);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPixelSafe(tex, x, y, c);
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        SetPixelSafe(tex, x, y, c);
        }

        private static void FillEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float nx = (x - cx) / (float)rx;
                    float ny = (y - cy) / (float)ry;
                    if (nx * nx + ny * ny <= 1f)
                        SetPixelSafe(tex, x, y, c);
                }
            }
        }

        private static void FillTriangle(Texture2D tex, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x));
            int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x));
            int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y));
            int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                        SetPixelSafe(tex, x, y, color);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static void DrawWedge(Texture2D tex, Vector2 center, float length, float angleDeg, Color color)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 tip = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * length;
            float spread = 18f * Mathf.Deg2Rad;
            Vector2 left = center + new Vector2(Mathf.Cos(rad - spread), Mathf.Sin(rad - spread)) * (length * 0.4f);
            Vector2 right = center + new Vector2(Mathf.Cos(rad + spread), Mathf.Sin(rad + spread)) * (length * 0.4f);
            FillTriangle(tex, tip, left, right, color);
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

        /// <summary>
        /// แปลงสไปรต์ที่วาดไว้ให้เป็น silhouette สีเดียว + เส้นขอบ (dark theme)
        /// pixel ทึบที่ติดขอบ (มีเพื่อนบ้านโปร่งใส/นอกภาพ) = outline, ที่เหลือ = fill, โปร่งใสคงเดิม
        /// คงรูปทรง (alpha mask) ของอาคารแต่ละหลังไว้ แค่เปลี่ยนเป็นเทาเข้มทั้งก้อน
        /// </summary>
        private static void ApplySilhouette(Texture2D tex, Color fill, Color outline)
        {
            int w = tex.width, h = tex.height;
            Color[] src = tex.GetPixels();
            Color[] dst = new Color[src.Length];
            Color transparent = new Color(0, 0, 0, 0);

            bool Solid(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return false;
                return src[y * w + x].a > 0.01f;
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (src[i].a <= 0.01f) { dst[i] = transparent; continue; }

                    bool edge = !Solid(x - 1, y) || !Solid(x + 1, y) ||
                                !Solid(x, y - 1) || !Solid(x, y + 1);
                    dst[i] = edge ? outline : fill;
                }
            }

            tex.SetPixels(dst);
            tex.Apply();
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

        private static void ConfigureBuildingSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = BuildingPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static void AssignBuildingIcons()
        {
            foreach (var name in BuildingNames)
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

            // ตั้งสีพื้นหลังกล้องเป็น dark theme (#0D1117)
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = CameraBackground;
                EditorUtility.SetDirty(cam);
            }

            var groundGO = GameObject.Find("Ground");
            if (groundGO == null)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            var tilemap = groundGO.GetComponent<Tilemap>();
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(GroundTilePath);

            if (tilemap != null && tile != null)
            {
                tilemap.ClearAllTiles();
                for (int col = 0; col < 20; col++)        // = GridManager.columns
                    for (int row = 0; row < 12; row++)    // = GridManager.rows
                        tilemap.SetTile(new Vector3Int(col, row, 0), tile);
                tilemap.CompressBounds();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
