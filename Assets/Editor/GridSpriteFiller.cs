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
        private const int DirtMaxIndex = 21; // index ≤ 21 = ดิน (Zone B) · ≥ 22 = หญ้า (Zone A)
        private const string GroundObjectName = "Ground";
        private const string FogObjectName = "FogOfWar";
        // fallback เมื่อไม่พบ GridManager ในซีน — ขนาดจริงอ่านจาก GridManager.columns×rows
        private const int FallbackColumns = 20;
        private const int FallbackRows = 12;

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

            // ★ ทางหลัก: ไทล์ที่ผู้ใช้คัดไว้ — บล็อกเต็ม tile_XXX (ขอบหน้า) + หน้าบนแบน flat_XXX (ข้างใน)
            var (blockGrass, blockDirt) = LoadZonedTiles(ArtTilesFolder, "tile_");
            var (flatGrass, flatDirt) = LoadZonedTiles(FlatTilesFolder, "flat_");
            if (blockGrass.Count > 0 || blockDirt.Count > 0)
            {
                if (flatGrass.Count == 0 && flatDirt.Count == 0)
                    Debug.LogWarning("[GridSpriteFiller] ยังไม่มีไทล์แบน (flat_XXX) — ข้างในจะใช้บล็อกเต็ม (ผนังโผล่) · " +
                                     "รัน 'Flatten Ground Tiles (Top Face)' ก่อนเพื่อพื้นข้างในเรียบ");
                int zoneA = ReadZoneAColumns();
                int g = FillZonedRandom(GroundObjectName, flatGrass, flatDirt, blockGrass, blockDirt, columns, rows, zoneA);
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
                int zoneA = ReadZoneAColumns();
                int isoGround = FillZonedIso(GroundObjectName, columns, rows, zoneA);
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

        // เส้นแบ่งโซนจาก OreDepositManager (แหล่งความจริงเดียว) — fallback 29 (ค่าตั้งต้นบนกริด 43×28)
        private static int ReadZoneAColumns()
        {
            var ore = Object.FindFirstObjectByType<NuclearReMind.OreDepositManager>();
            return ore != null ? ore.zoneAColumns : 29;
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

        private static TileBase FirstOr(List<TileBase> a, List<TileBase> b)
            => a.Count > 0 ? a[0] : (b.Count > 0 ? b[0] : null);

        // ระบาย Ground: ทุกช่อง (รวมขอบ) = หน้าบนแบน flat_XXX — พื้นเรียบเนียนทั้งแมพ ไม่มีผนัง/ขอบยกสูง
        //   บล็อกเต็ม (blockGrass/blockDirt) เก็บไว้เป็น fallback เท่านั้น (ใช้เมื่อยังไม่ได้สร้างไทล์แบน)
        // ★ สุ่มด้วย IsoGroundPainter.Hash(col,row) ไม่ใช่ Random — ลายคงที่ทุก re-fill/โหลดเซฟ (อาคารไม่ขยับตามพื้น)
        private static int FillZonedRandom(string objectName,
                                           List<TileBase> flatGrass, List<TileBase> flatDirt,
                                           List<TileBase> blockGrass, List<TileBase> blockDirt,
                                           int columns, int rows, int zoneAColumns)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int count = 0;
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    bool zoneA = NuclearReMind.IsoGroundPainter.IsZoneA(x, zoneAColumns);

                    // ทุกช่องใช้หน้าบนแบนของโซน · ถ้ายังไม่มีไทล์แบน ถอยไปบล็อกเต็ม
                    var flat = zoneA ? flatGrass : flatDirt;
                    var block = zoneA ? blockGrass : blockDirt;
                    var list = flat.Count > 0 ? flat : block;

                    // ถ้าชุดของโซนว่าง ยืมอีกโซน (กันพื้นโหว่)
                    if (list.Count == 0) list = FallbackList(zoneA, flatGrass, flatDirt, blockGrass, blockDirt);
                    if (list.Count == 0) continue;

                    int h = NuclearReMind.IsoGroundPainter.Hash(x, y);
                    map.SetTile(new Vector3Int(x, y, 0), list[h % list.Count]);
                    count++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            Debug.Log($"[GridSpriteFiller] เติม '{objectName}' {columns}×{rows} = {count} ช่อง " +
                      $"(หน้าบนแบนทั้งแมพ · หญ้าแบน {flatGrass.Count}/บล็อก {blockGrass.Count} · " +
                      $"ดินแบน {flatDirt.Count}/บล็อก {blockDirt.Count})");
            return count;
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
        private static int FillZonedIso(string objectName, int columns, int rows, int zoneAColumns)
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
                    int idx = IsoGroundPainter.TileIndexFor(x, y, zoneAColumns);
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
                      $"(Zone A หญ้า cols 0–{zoneAColumns - 1} · Zone B ดิน cols {zoneAColumns}–{columns - 1})");
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
