using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้างชุดไทล์หินปูพื้น 10 ใบ จากไทล์หินในไทล์เซ็ตเดิม
    /// (ตัดหน้าบน + เติมรอยแตก/มอส/ขอบกร่อน) — ผลลัพธ์เหมือนเดิมทุกการรัน ไม่ใช้ Random
    /// Generates the stone paving tile set (docs/GROUND_PLAN.md §2) — 10 tiles, 32×32 top-face rhombus,
    /// same format as flat_XXX produced by GroundTileFlattener.
    ///
    /// SOURCE = the tileset's own stone blocks, chosen by the user:
    ///   tile_061 = cobblestone (rough, plazas)   ·   tile_063 = smooth slab (paths, clean paving)
    /// Those are 2.5D blocks with side walls, so we cut the top-face rhombus exactly like
    /// GroundTileFlattener does — walls removed, tiles butt together seamlessly when laid on the grid.
    ///
    /// Pixels are copied through UNCHANGED for the base variants: this is real hand-drawn pixel art and
    /// recolouring it would only degrade it. Variants add detail on top (shade multiply, crack pixels,
    /// moss speckle, chipped border) rather than repainting the stone.
    ///
    /// ★ Replacing with other art later needs NO code change: either point CobbleIndex/SmoothIndex at
    ///   different tileset entries, or drop PNGs with these filenames into Assets/Sprites/Art/Tiles/Stone/.
    ///   The placement logic (StoneGroundSetup) only ever references tiles by name.
    ///
    /// Deterministic: detail uses a pure hash of (x, y, seed) — never Random — so re-running produces
    /// byte-identical PNGs (no asset churn in git).
    ///
    /// Run: NuclearReMind/Generate Stone Tiles
    /// </summary>
    public static class StoneTileGenerator
    {
        private const string SourceFolder = "Assets/Sprites/Tiles/IsoNature";
        private const string StoneFolder  = "Assets/Sprites/Art/Tiles/Stone";
        private const int TilePixelsPerUnit = 32;

        // ── source tiles (change these to swap the look) ──
        private const int CobbleIndex = 61; // rough cobblestone
        private const int SmoothIndex = 63; // smooth slab

        // ── colour families ──
        // The raw tileset stone is cyan-tinted grey (#708586). Reference art wants warm, light paving,
        // so each pixel is first pulled toward its own luminance (kills the cyan cast) and then scaled
        // per channel. Two families, both light, matching the reference plazas:
        //   Grey  -> ~#A8A498 (cobble) / #BCB6A7 (smooth)      Warm -> ~#BBB28B / #D3C599
        private const float Desaturate = 0.35f;
        private static readonly Vector3 GreyMul = new Vector3(1.44f, 1.26f, 1.16f);
        private static readonly Vector3 WarmMul = new Vector3(1.62f, 1.36f, 1.06f);

        private enum Palette { Grey, Warm }

        // ── variant tuning ──
        private const float DarkShade   = 0.86f; // extra plaza shade = same stone, darker
        private const float WornLighten = 1.10f; // paths read lighter than the plaza
        private const float CrackDarken = 0.52f;
        private const int   CrackPercent = 8;   // chance per 2×2 pixel block, inside the face
        private const int   MossPercent  = 24;  // chance per pixel, near the border
        private const int   ChipPercent  = 30;  // chance per 3×3 block on the rim (edge tile)
        private static readonly Vector3 MossTint = new Vector3(0.12f, 0.17f, 0.08f);

        private enum Kind { Plain, Crack, Moss, Chip }

        // (outputName, sourceIndex, palette, kind, brightness, seed)
        // Naming stays stable — StoneGroundSetup and the painter reference these names only.
        private static readonly (string name, int src, Palette pal, Kind kind, float mul, int seed)[] TileSpecs =
        {
            // plaza / full slabs — 0-2 grey, 3-4 warm
            ("stone_full_0",  CobbleIndex, Palette.Grey, Kind.Plain, 1f,          0),
            ("stone_full_1",  SmoothIndex, Palette.Grey, Kind.Plain, 1f,          0),
            ("stone_full_2",  CobbleIndex, Palette.Grey, Kind.Plain, DarkShade,   0),
            ("stone_full_3",  CobbleIndex, Palette.Warm, Kind.Plain, 1f,          0),
            ("stone_full_4",  SmoothIndex, Palette.Warm, Kind.Plain, 1f,          0),
            // cracked
            ("stone_crack_0", CobbleIndex, Palette.Grey, Kind.Crack, 1f,         11),
            ("stone_crack_1", SmoothIndex, Palette.Grey, Kind.Crack, 1f,         29),
            ("stone_crack_2", CobbleIndex, Palette.Warm, Kind.Crack, 1f,         19),
            // mossy (edges / rubble)
            ("stone_moss_0",  CobbleIndex, Palette.Grey, Kind.Moss,  1f,          5),
            ("stone_moss_1",  SmoothIndex, Palette.Grey, Kind.Moss,  1f,         17),
            ("stone_moss_2",  CobbleIndex, Palette.Warm, Kind.Moss,  1f,         31),
            // paths
            ("stone_worn_0",  SmoothIndex, Palette.Grey, Kind.Plain, WornLighten,  3),
            ("stone_worn_1",  SmoothIndex, Palette.Grey, Kind.Crack, WornLighten, 41),
            ("stone_worn_2",  SmoothIndex, Palette.Warm, Kind.Plain, WornLighten,  7),
            // chipped rim
            ("stone_edge_0",  CobbleIndex, Palette.Grey, Kind.Chip,  1f,         23),
            ("stone_edge_1",  CobbleIndex, Palette.Warm, Kind.Chip,  1f,         37),
        };

        // เมนูนี้: สร้างไฟล์ PNG ไทล์หิน 10 ใบลง Assets/Sprites/Art/Tiles/Stone
        [MenuItem("NuclearReMind/Generate Stone Tiles")]
        public static void Apply()
        {
            if (!Directory.Exists(StoneFolder)) Directory.CreateDirectory(StoneFolder);

            var cobble = LoadSource(CobbleIndex);
            var smooth = LoadSource(SmoothIndex);
            if (cobble.px == null || smooth.px == null) return;

            int made = 0;
            foreach (var spec in TileSpecs)
            {
                var src = spec.src == CobbleIndex ? cobble : smooth;
                if (MakeTile(spec.name, src, spec.pal, spec.kind, spec.mul, spec.seed)) made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StoneTileGenerator] ✅ สร้างไทล์หิน {made}/{TileSpecs.Length} ใบ จาก " +
                      $"tile_{CobbleIndex:000} (หินก้อน) + tile_{SmoothIndex:000} (หินเรียบ) — ตัดผนังข้างออกเหลือหน้าบนแบน");
        }

        private static (Color32[] px, int w, int h) LoadSource(int index)
        {
            string path = $"{SourceFolder}/tile_{index:000}.png";
            if (!File.Exists(path))
            {
                Debug.LogError($"[StoneTileGenerator] ไม่พบต้นฉบับ {path}");
                return (null, 0, 0);
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                Debug.LogError($"[StoneTileGenerator] อ่าน {path} ไม่ได้");
                return (null, 0, 0);
            }
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            return (px, w, h);
        }

        private static bool MakeTile(string name, (Color32[] px, int w, int h) src, Palette pal, Kind kind, float mul, int seed)
        {
            Vector3 palMul = pal == Palette.Warm ? WarmMul : GreyMul;
            int w = src.w, h = src.h;
            var dst = new Color32[src.px.Length];   // transparent by default

            // top-face rhombus — identical maths to GroundTileFlattener (2:1, matches cellSize 1×0.5)
            float cx = (w - 1) / 2f;
            float cy = h / 2f;
            float halfW = w / 2f;
            float halfH = h / 4f;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float rh = Mathf.Abs(x - cx) / halfW + Mathf.Abs(y - cy) / halfH;
                    if (rh > 1f) continue;                 // side wall / outside → dropped
                    var s = src.px[y * w + x];
                    if (s.a == 0) continue;

                    float d = 1f - rh;                     // 0 at the rim, 1 at the centre

                    // recolour: neutralise the cyan cast, then scale into the target family
                    float r = s.r, g = s.g, b = s.b;
                    float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                    r += (lum - r) * Desaturate;
                    g += (lum - g) * Desaturate;
                    b += (lum - b) * Desaturate;
                    r *= palMul.x * mul;
                    g *= palMul.y * mul;
                    b *= palMul.z * mul;

                    switch (kind)
                    {
                        case Kind.Crack:
                            if (d > 0.12f && d < 0.88f && Hash(x / 2, y / 2, seed) % 100 < CrackPercent)
                            { r *= CrackDarken; g *= CrackDarken; b *= CrackDarken; }
                            break;

                        case Kind.Moss:
                            if (d < 0.45f && Hash(x, y, seed + 7) % 100 < MossPercent)
                            {
                                r = r * 0.55f + MossTint.x * 255f;
                                g = g * 0.62f + MossTint.y * 255f;
                                b = b * 0.50f + MossTint.z * 255f;
                            }
                            break;

                        case Kind.Chip:
                            if (d < 0.22f && Hash(x / 3, y / 3, seed + 3) % 100 < ChipPercent)
                                continue;                  // chipped away → transparent
                            break;
                    }

                    dst[y * w + x] = new Color32(
                        (byte)Mathf.Clamp(r, 0f, 255f),
                        (byte)Mathf.Clamp(g, 0f, 255f),
                        (byte)Mathf.Clamp(b, 0f, 255f),
                        s.a);
                }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(dst);
            outTex.Apply();
            string png = $"{StoneFolder}/{name}.png";
            File.WriteAllBytes(png, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(png);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            if (sprite == null)
            {
                Debug.LogWarning($"[StoneTileGenerator] import {png} ไม่สำเร็จ");
                return false;
            }

            string tilePath = $"{StoneFolder}/{name}.asset";
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

        // Same importer settings as GroundTileFlattener.ConfigureSprite — must match flat_XXX exactly
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

        /// <summary>Deterministic non-negative hash — detail must be identical on every re-run.</summary>
        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int hh = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                hh = (hh ^ (hh >> 13)) * 1274126177;
                return (hh ^ (hh >> 16)) & 0x7fffffff;
            }
        }
    }
}
