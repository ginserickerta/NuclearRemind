using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.Editor
{
    /// <summary>
    /// เติม tilemap ของ "build grid" (Ground) และ "FogOfWar" ให้ครบเท่ากัน
    /// ตามขนาด logic grid จริง (GridManager.columns×rows) — เดิม Ground paint 12×9 และ Fog ว่างเปล่า
    ///
    /// Tilemap เป็น isometric (cellSize 1×0.5) → cell (x,y) ตรงกับ (col,row) ของ GridManager พอดี
    /// FogOfWar มี tint ของตัวเอง (m_Color ~0.1,0.1,0.15,α0.7) ทำให้ tile เดียวกันกลายเป็นหมอกดำโปร่ง
    ///
    /// Ground: ทางหลัก = ไทล์ที่ผู้ใช้คัดไว้ สุ่มต่อช่องแบ่งโซน หญ้า(≥22)/ดิน(≤21)
    ///   • ทุกช่อง (รวมขอบ) = หน้าบนแบน flat_XXX (Flat/ — สร้างโดย GroundTileFlattener) พื้นเรียบทั้งแมพ ไม่มีผนัง/ขอบยกสูง
    ///   • บล็อกเต็ม tile_XXX (Art/Tiles) เก็บไว้เป็น fallback เท่านั้น (ใช้เมื่อยังไม่ได้สร้างไทล์แบน)
    /// fallback: GroundGrassA แบนใบเดียว → IsoNature ชุดเต็ม → placeholder Ground.asset
    ///
    /// รัน 2 ทาง:
    ///   • เมนู (scene เปิดอยู่): NuclearReMind/Fill Grids (Ground + Fog)
    ///   • batch: -executeMethod NuclearReMind.Editor.GridSpriteFiller.FillFromBatch (เปิด+เซฟ scene เอง)
    /// </summary>
    public static class GridSpriteFiller
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string GroundTilePath = "Assets/Sprites/Tiles/Ground.asset"; // placeholder (fallback)
        // พื้นหญ้าของทีม (fallback ใบเดียว เมื่อไม่มี IsoNature — สร้างโดย GroundTileSetup)
        private const string GrassATilePath = "Assets/Sprites/Art/Tiles/GroundGrassA.asset";
        // ★ ไทล์ที่ผู้ใช้คัดเอง — ทางหลัก: สุ่มต่อช่องแบ่งโซน
        //   บล็อกเต็ม tile_XXX (Art/Tiles) ใช้ที่ "ขอบหน้า" · หน้าบนแบน flat_XXX (Flat/) ใช้ "ข้างใน"
        //   แยกโซนด้วย index ตามช่วง IsoTilesetSetup: ดิน 0–21 · หญ้า 22–40
        private const string ArtTilesFolder = "Assets/Sprites/Art/Tiles";
        private const string FlatTilesFolder = "Assets/Sprites/Art/Tiles/Flat";
        private const string FlatDarkTilesFolder = "Assets/Sprites/Art/Tiles/Flat/Dark"; // คู่เข้ม (tint เทา) สำหรับลายหมากรุก
        private const int DirtMaxIndex = 21; // index ≤ 21 = ดิน (Zone B) · ≥ 22 = หญ้า (Zone A)
        // Zone A (หญ้า) ใช้เฉพาะไทล์ "สีอ่อนเรียบ" — ตัดใบเข้ม/พุ่มไม้ออก (27,28,31,32) แล้วสุ่มต่อช่อง
        private static readonly int[] ZoneAGrassIndices = { 22, 23, 24, 37, 38, 39 };
        private const string GroundObjectName = "Ground";
        private const string FogObjectName = "FogOfWar";
        // fallback เมื่อไม่พบ GridManager ในซีน — ขนาดจริงอ่านจาก GridManager.columns×rows
        private const int FallbackColumns = 20;
        private const int FallbackRows = 12;
        // ขอบพื้นตกแต่ง "รกร้าง" รอบนอกกริดจริง — ค่าคุม (margin/darken/edge) เป็น serialized บน OreDepositManager
        // ช่องนอก [0,columns)×[0,rows) ไม่ใช่ cell ของ GridManager → GetCell คืน null → วาง building ไม่ได้เอง
        // fallback margin เมื่อไม่พบ OreDepositManager ในซีน (พาเลตต์สี fallback = GroundPalette.Default)
        private const int FallbackApronMargin = 35;

        [MenuItem("NuclearReMind/Fill Grids (Ground + Fog)")]
        public static void FillFromMenu()
        {
            var (ground, fog) = FillBoth();
            if (ground < 0) return;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Fill Grids",
                $"Ground = {ground} ช่อง\nFogOfWar = {fog} ช่อง\n\nอย่าลืม Save Scene (Ctrl+S)", "OK");
        }

        // entry สำหรับ batchmode — เปิด scene + เซฟเอง
        public static void FillFromBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var (ground, fog) = FillBoth();
            if (ground < 0) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GridSpriteFiller] FillFromBatch เซฟ scene แล้ว — Ground {ground}, Fog {fog} ช่อง");
        }

        private static (int ground, int fog) FillBoth()
        {
            TileBase grassA = AssetDatabase.LoadAssetAtPath<TileBase>(GrassATilePath);
            TileBase legacy = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);

            // ขนาดจริงตาม logic grid ในซีน (เฟส 8 = 43×28)
            var grid = Object.FindFirstObjectByType<GridManager>();
            int columns = grid != null ? grid.columns : FallbackColumns;
            int rows = grid != null ? grid.rows : FallbackRows;
            if (grid == null)
                Debug.LogWarning($"[GridSpriteFiller] ไม่พบ GridManager — ใช้ fallback {FallbackColumns}×{FallbackRows}");

            // ★ ทางหลัก: ไทล์ที่ผู้ใช้คัดไว้ — Zone B ดิน = สุ่มทั้งชุด · Zone A หญ้า = แค่ ZoneAGrassIndices (สลับกัน)
            var (_, blockDirt) = LoadZonedTiles(ArtTilesFolder, "tile_");
            var (_, flatDirt) = LoadZonedTiles(FlatTilesFolder, "flat_");
            var blockGrass = LoadTilesByIndex(ArtTilesFolder, "tile_", ZoneAGrassIndices);
            var flatGrass = LoadTilesByIndex(FlatTilesFolder, "flat_", ZoneAGrassIndices);
            if (blockGrass.Count > 0 || blockDirt.Count > 0)
            {
                if (flatGrass.Count == 0 && flatDirt.Count == 0)
                    Debug.LogWarning("[GridSpriteFiller] ยังไม่มีไทล์แบน (flat_XXX) — ข้างในจะใช้บล็อกเต็ม (ผนังโผล่) · " +
                                     "รัน 'Flatten Ground Tiles (Top Face)' ก่อนเพื่อพื้นข้างในเรียบ");
                int border = ReadBorderThickness();
                // ★ ทางหลัก: วางสไปรต์จริงต่อช่อง — Zone A หญ้า / Zone B ดิน (สุ่ม variety) · ไล่รอยต่อแบบ dither
                //   หญ้า = flat_022.. (ตัดใบพุ่มเข้มออก) · ดิน = flat_000.. · fallback บล็อกเต็มถ้าไม่มีแบน
                var (gMargin, gPal) = ReadGroundConfig();
                var groundGrass = flatGrass.Count > 0 ? flatGrass : blockGrass;
                var groundDirt = flatDirt.Count > 0 ? flatDirt : blockDirt;
                int g = FillZonedSprites(GroundObjectName, groundGrass, groundDirt, columns, rows, border, gMargin, gPal);
                if (g < 0) return (-1, 0);
                // fog: ไทล์แบนใบเดียวถ้ามี (สะอาดใต้หมอก) ไม่งั้นไทล์แบน/บล็อกตัวแรกในชุด
                TileBase fogTile = grassA
                    ?? FirstOr(flatGrass, flatDirt) ?? FirstOr(blockGrass, blockDirt);
                int f = FillTilemap(FogObjectName, fogTile, columns, rows);
                return (g, f);
            }

            // fallback 1: หญ้าแบน GroundGrassA ใบเดียว
            if (grassA != null)
            {
                Debug.LogWarning("[GridSpriteFiller] ไม่พบไทล์คัด (tile_XXX) ใน Art/Tiles — ใช้ GroundGrassA แบนใบเดียว");
                int g = FillTilemap(GroundObjectName, grassA, columns, rows);
                if (g < 0) return (-1, 0);
                int f = FillTilemap(FogObjectName, grassA, columns, rows);
                return (g, f);
            }

            // fallback 2: IsoNature แบ่งโซน (ชุดเต็มใน IsoNature/)
            var isoProbe = AssetDatabase.LoadAssetAtPath<TileBase>(
                EditorTools.IsoTilesetSetup.TileAssetPath(0));
            if (isoProbe != null)
            {
                int border = ReadBorderThickness();
                int isoGround = FillZonedIso(GroundObjectName, columns, rows, border);
                if (isoGround < 0) return (-1, 0);
                int isoFog = FillTilemap(FogObjectName, isoProbe, columns, rows);
                return (isoGround, isoFog);
            }

            // fallback 3: placeholder (Ground.asset)
            if (legacy == null)
            {
                Debug.LogError($"[GridSpriteFiller] ไม่พบ tile ใดเลย ({ArtTilesFolder}, {GrassATilePath}, {GroundTilePath})");
                return (-1, 0);
            }
            Debug.LogWarning("[GridSpriteFiller] ไม่พบไทล์คัด/หญ้าแบน/IsoNature — ใช้ placeholder");
            int ground = FillTilemap(GroundObjectName, legacy, columns, rows);
            if (ground < 0) return (-1, 0);
            int fog = FillTilemap(FogObjectName, legacy, columns, rows);
            return (ground, fog);
        }

        // ความหนากรอบ Zone B รอบนอกจาก OreDepositManager (แหล่งความจริงเดียว) — fallback 7 (Zone A กลาง 29×29 บนกริด 43×43)
        private static int ReadBorderThickness()
        {
            var ore = Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();
            return ore != null ? ore.zoneBorderThickness : 7;
        }

        // โหลดไทล์จาก folder/prefix ที่กำหนด แยกเป็นหญ้า (index ≥ 22) / ดิน (index ≤ 21) ตามช่วง IsoTilesetSetup
        // สแกน index 0–40 — ที่ไม่มีไฟล์ก็ข้าม · ไฟล์ชื่อแปลก (เช่น "tile_009 1") ไม่ถูกโหลด
        private static (List<TileBase> grass, List<TileBase> dirt) LoadZonedTiles(string folder, string prefix)
        {
            var grass = new List<TileBase>();
            var dirt = new List<TileBase>();
            for (int i = 0; i <= 40; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<TileBase>($"{folder}/{prefix}{i:000}.asset");
                if (t == null) continue;
                (i <= DirtMaxIndex ? dirt : grass).Add(t);
            }
            return (grass, dirt);
        }

        // โหลดไทล์ตาม index ที่ระบุ เรียงตามลำดับใน indices (ข้ามใบที่ไม่มีไฟล์) — ใช้เลือกไทล์ Zone A เฉพาะเจาะจง
        private static List<TileBase> LoadTilesByIndex(string folder, string prefix, int[] indices)
        {
            var list = new List<TileBase>();
            foreach (int i in indices)
            {
                var t = AssetDatabase.LoadAssetAtPath<TileBase>($"{folder}/{prefix}{i:000}.asset");
                if (t != null) list.Add(t);
            }
            return list;
        }

        private static TileBase FirstOr(List<TileBase> a, List<TileBase> b)
            => a.Count > 0 ? a[0] : (b.Count > 0 ? b[0] : null);

        // ทาสีพื้น isometric ต่อ tile ด้วย IsoGroundPainter.ColorForTile — carrier tile ใบเดียวเป็นตัวรับสี (SetColor)
        //   ในกริด [0,columns)×[0,rows): ไล่เฟด A(เขียว)→B(น้ำตาล) เนียนต่อ tile + หมากรุก base/alt (คุมด้วยสี ไม่ใช่ไทล์)
        //   นอกกริด (apron margin ช่องทุกด้าน): น้ำตาลคูณ outsideDarken ไล่เนียนที่ขอบ · นอก cell กริด = วาง building ไม่ได้
        // ★ Hash(col,row) deterministic → ลายคงที่ทุก re-fill (ไม่ใช้ Random)
        private static int FillTintedGround(string objectName, TileBase carrier,
                                            int columns, int rows, int border, int margin, NuclearReMind.GroundPalette pal)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;
            if (carrier == null) { Debug.LogError($"[GridSpriteFiller] ไม่มี carrier tile สำหรับ '{objectName}'"); return -1; }

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int m = Mathf.Max(0, margin);
            int count = 0, inner = 0;
            for (int x = -m; x < columns + m; x++)
                for (int y = -m; y < rows + m; y++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    map.SetTile(pos, carrier);
                    map.SetTileFlags(pos, TileFlags.None); // ปลดล็อกสี ให้ SetColor มีผล
                    map.SetColor(pos, NuclearReMind.IsoGroundPainter.ColorForTile(x, y, columns, rows, border, pal));
                    count++;
                    if (x >= 0 && x < columns && y >= 0 && y < rows) inner++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            Debug.Log($"[GridSpriteFiller] ทาสีพื้น '{objectName}' ในกริด {inner} + นอกกริด {count - inner} = {count} ช่อง " +
                      $"(margin {m} · transition {pal.transitionWidth} · outsideDarken {pal.outsideDarken:0.00})");
            return count;
        }

        // วางสไปรต์พื้นจริงต่อช่อง — Zone A หญ้า / Zone B ดิน (สุ่ม variety ต่อช่อง) · ไล่รอยต่อแบบ dither (IsoGroundPainter)
        //   ในกริด [0,columns)×[0,rows): PickZoneA เลือกโซน (มี transition band) → VarietyPick เลือกสไปรต์ในโซน · สีจริง (ขาว)
        //   นอกกริด (apron margin): ดินล้วน + คูณ outsideDarken ไล่เนียนที่ขอบ (SetColor) — นอก cell กริด = วาง building ไม่ได้
        // ★ Hash(col,row) deterministic → ลาย/โซนคงที่ทุก re-fill (ไม่ใช้ Random)
        private static int FillZonedSprites(string objectName, List<TileBase> grass, List<TileBase> dirt,
                                            int columns, int rows, int border, int margin, NuclearReMind.GroundPalette pal)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;
            if (grass.Count == 0 && dirt.Count == 0)
            { Debug.LogError($"[GridSpriteFiller] ไม่มีสไปรต์โซนสำหรับ '{objectName}'"); return -1; }

            // ถ้าโซนใดว่าง ใช้อีกโซนแทน (กันช่องว่าง)
            var grassList = grass.Count > 0 ? grass : dirt;
            var dirtList = dirt.Count > 0 ? dirt : grass;

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int m = Mathf.Max(0, margin);
            int count = 0, inner = 0, grassCells = 0;
            for (int x = -m; x < columns + m; x++)
                for (int y = -m; y < rows + m; y++)
                {
                    bool outside = x < 0 || x >= columns || y < 0 || y >= rows;
                    bool zoneA = !outside && NuclearReMind.IsoGroundPainter.PickZoneA(
                        x, y, columns, rows, border, pal.transitionWidth, pal.jitterStrength);
                    var zone = zoneA ? grassList : dirtList;   // apron (outside) = ดิน (รกร้าง)

                    var pos = new Vector3Int(x, y, 0);
                    map.SetTile(pos, zone[NuclearReMind.IsoGroundPainter.VarietyPick(x, y, zone.Count)]);
                    map.SetTileFlags(pos, TileFlags.None);      // ปลดล็อกสี ให้ SetColor มีผล
                    if (outside)
                    {
                        float f = NuclearReMind.IsoGroundPainter.OutsideDarken(x, y, columns, rows, pal);
                        map.SetColor(pos, new Color(f, f, f, 1f));
                    }
                    else { map.SetColor(pos, Color.white); inner++; if (zoneA) grassCells++; }
                    count++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            Debug.Log($"[GridSpriteFiller] วางสไปรต์พื้น '{objectName}' ในกริด {inner} (หญ้า {grassCells}/ดิน {inner - grassCells}) " +
                      $"+ นอกกริด {count - inner} = {count} ช่อง · หญ้า {grass.Count}·ดิน {dirt.Count} ใบ · transition {pal.transitionWidth}");
            return count;
        }

        // ค่าคุมพื้น (margin + พาเลตต์สี) จาก OreDepositManager (serialized · แหล่งความจริงเดียว) — fallback ถ้าไม่พบ/ยังไม่ตั้ง
        private static (int margin, NuclearReMind.GroundPalette pal) ReadGroundConfig()
        {
            var ore = Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();
            if (ore != null)
            {
                var pal = ore.groundPalette;
                if (pal.zoneA_base.a < 0.5f) pal = NuclearReMind.GroundPalette.Default; // instance เก่ายังไม่ตั้ง (struct ศูนย์)
                return (Mathf.Max(0, ore.apronMargin), pal);
            }
            return (FallbackApronMargin, NuclearReMind.GroundPalette.Default);
        }

        // ชุดสำรองเมื่อโซนที่ต้องการว่าง — ไล่จากแบนโซนนั้น → บล็อกโซนนั้น → อีกโซน
        private static List<TileBase> FallbackList(bool zoneA, List<TileBase> flatGrass, List<TileBase> flatDirt,
                                                   List<TileBase> blockGrass, List<TileBase> blockDirt)
        {
            if (zoneA)
                return flatGrass.Count > 0 ? flatGrass : blockGrass.Count > 0 ? blockGrass
                     : flatDirt.Count > 0 ? flatDirt : blockDirt;
            return flatDirt.Count > 0 ? flatDirt : blockDirt.Count > 0 ? blockDirt
                 : flatGrass.Count > 0 ? flatGrass : blockGrass;
        }

        // ระบาย Ground แบ่งโซน: Zone A หญ้า / Zone B ดิน — พื้นเนียนใบเดียว + โรย variety (IsoGroundPainter)
        private static int FillZonedIso(string objectName, int columns, int rows, int border)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;

            var cache = new Dictionary<int, TileBase>();
            TileBase Load(int idx)
            {
                if (!cache.TryGetValue(idx, out var t))
                {
                    t = AssetDatabase.LoadAssetAtPath<TileBase>(EditorTools.IsoTilesetSetup.TileAssetPath(idx));
                    cache[idx] = t;
                }
                return t;
            }

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int count = 0, missing = 0;
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    int idx = IsoGroundPainter.TileIndexFor(x, y, columns, rows, border);
                    var tile = Load(idx);
                    if (tile == null) { missing++; continue; }
                    map.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            if (missing > 0)
                Debug.LogWarning($"[GridSpriteFiller] ขาด Tile asset {missing} ช่อง — รัน 'Setup Iso Nature Tileset' ให้ครบก่อน");
            Debug.Log($"[GridSpriteFiller] เติม '{objectName}' {columns}×{rows} = {count} ช่อง " +
                      $"(Zone A หญ้า สี่เหลี่ยมกลาง · Zone B ดิน กรอบรอบนอกหนา {border} ช่อง)");
            return count;
        }

        private static Tilemap FindTilemap(string objectName)
        {
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                if (tm.gameObject.name == objectName)
                    return tm;

            Debug.LogWarning($"[GridSpriteFiller] ไม่พบ Tilemap ชื่อ '{objectName}' ใน scene — ข้าม");
            return null;
        }

        private static int FillTilemap(string objectName, TileBase tile, int columns, int rows)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;

            if (tile == null)
            {
                Debug.LogError($"[GridSpriteFiller] ไม่พบ tile สำหรับ '{objectName}'");
                return -1;
            }

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int count = 0;
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            Debug.Log($"[GridSpriteFiller] เติม '{objectName}' {columns}×{rows} = {count} ช่อง");
            return count;
        }
    }
}
