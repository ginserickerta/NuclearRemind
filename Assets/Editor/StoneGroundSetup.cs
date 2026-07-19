using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Paves the stone plazas / paths / rubble patches (docs/GROUND_PLAN.md §3-4) onto a dedicated
    /// "StonePave" Tilemap that sits ON TOP of "Ground" (same sorting layer, order +1).
    ///
    /// Why a separate tilemap: re-running "Fill Grids" clears and repaints Ground — keeping stone on its
    /// own map means grass re-fills never wipe this work, the whole layer can be toggled, and the existing
    /// TileColorPainter overrides on Ground are untouched.
    ///
    /// Placement is fully deterministic (IsoGroundPainter.Hash — never Random), so the layout is identical
    /// on every run, every load, every machine:
    ///   • Plaza  — diamond radius 4 around the CORE TOWER, 1-cell pad around other pre-placed buildings.
    ///              Ring cells drop out at 45% probability so the edge is ragged, not a clean diamond.
    ///   • Paths  — from the central plaza toward N/S/E/W up to 10 cells, 1 wide + 35% chance of a
    ///              neighbour cell, using the lighter "worn" tiles.
    ///   • Patch  — ~1.2% seed cells grow 3-6 cell blobs of cracked/mossy stone (old paving under grass).
    ///   • Variant per cell: 60% full (3 shades) / 25% crack / 15% moss — but cells touching grass are
    ///              forced to moss/edge 50% of the time, which is what makes the border read organic.
    ///
    /// Visual layer only: StonePave has no collider and no logic — placement, pathing and gameplay
    /// never query it.
    ///
    /// Idempotent: clears StonePave before repainting. Run AFTER "Fill Grids (Ground + Fog)".
    /// Run: NuclearReMind/Setup Stone Ground (Plaza + Paths)
    /// </summary>
    public static class StoneGroundSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string StoneFolder = "Assets/Sprites/Art/Tiles/Stone";
        private const string GroundObjectName = "Ground";
        private const string StoneObjectName = "StonePave";
        private const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // ── tuning (GROUND_PLAN §4) ──
        private const int CorePlazaRadius   = 4;   // diamond radius around CORE TOWER
        private const int BuildingPad       = 1;   // ring around other pre-placed buildings
        private const int RingKeepPercent   = 55;  // ring cell survives if hash% < this (ragged edge)
        private const int PathLength        = 10;  // cells from plaza edge outward
        private const int PathWidenPercent  = 35;  // chance a neighbour cell joins the path
        private const int PatchSeedPerMille = 12;  // ~1.2% of cells seed a rubble blob
        private const int PatchMinCells     = 3;
        private const int PatchMaxCells     = 6;

        private static readonly int[] PathDirX = { 1, -1, 0, 0 };
        private static readonly int[] PathDirY = { 0, 0, 1, -1 };

        private enum Kind { Plaza, Path, Patch }

        [MenuItem("NuclearReMind/Setup Stone Ground (Plaza + Paths)")]
        public static void Apply()
        {
            // Generating tiles reimports 10 textures and refreshes the AssetDatabase, which can
            // interrupt this call. So: generate first, return, and paint on the next editor tick
            // once the import has settled — never both in one pass.
            if (ReadTiles() == null)
            {
                Debug.Log("[StoneGround] ยังไม่มีไทล์หิน — สร้างก่อน แล้วจะปูให้อัตโนมัติในเฟรมถัดไป");
                StoneTileGenerator.Apply();
                EditorApplication.delayCall += Paint;
                return;
            }
            Paint();
        }

        private static void Paint()
        {
            var tiles = LoadTiles();
            if (tiles == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null)
            {
                Debug.LogError("[StoneGround] ไม่พบ GridManager — รัน 'Setup Grid 43x43' ก่อน");
                return;
            }

            var map = FindOrCreateStoneTilemap();
            if (map == null) return;

            map.ClearAllTiles();
            // a freshly created tilemap can be left disabled by an aborted earlier run
            map.gameObject.SetActive(true);
            var mapRenderer = map.GetComponent<TilemapRenderer>();
            if (mapRenderer != null) mapRenderer.enabled = true;

            // 1) decide which cells are stone, and why (plaza / path / patch)
            var cells = new Dictionary<Vector2Int, Kind>();

            var preplaced = Object.FindObjectsByType<PrePlacedBuilding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PaintPlazas(preplaced, grid, cells);
            int afterPlaza = cells.Count;
            PaintPaths(preplaced, grid, cells);
            int afterPaths = cells.Count;
            PaintPatches(grid, cells);

            Debug.Log($"[StoneGround] grid {grid.columns}×{grid.rows} · อาคาร pre-placed {preplaced.Length} หลัง → " +
                      $"ลาน {afterPlaza} ช่อง · +ทางเดิน {afterPaths - afterPlaza} · +ซากปู {cells.Count - afterPaths} " +
                      $"= รวม {cells.Count} ช่อง");
            if (cells.Count == 0)
            {
                Debug.LogError("[StoneGround] ไม่มีช่องให้ปูเลย — ไม่พบ PrePlacedBuilding ที่มี building ผูกไว้ " +
                               "(รัน 'Setup Grid 43x43' + setup อาคารก่อน)");
                return;
            }

            // 2) choose a tile variant per cell — needs the full set first (border test looks at neighbours)
            int painted = 0;
            foreach (var kv in cells)
            {
                var c = kv.Key;
                var tile = PickTile(c, kv.Value, cells, tiles);
                if (tile == null) continue;
                map.SetTile(new Vector3Int(c.x, c.y, 0), tile);
                painted++;
            }

            // 3) hand-painted cells win — applied last so re-running never destroys manual work
            //    (Tile Color Painter → ทาอะไร = หินปูพื้น)
            int manual = ApplyManualOverrides(map);

            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StoneGround] ✅ ปูหิน {painted} ช่องบน '{StoneObjectName}' " +
                      (manual > 0 ? $"+ ทาเอง {manual} ช่อง " : "") +
                      $"(ลาน+ทางเดิน+ซากปู · deterministic) — ปรับสีได้ที่ __ASHFALL_Volume > Stone Tint");
        }

        // ── cell selection ────────────────────────────────────────────────────

        // Plaza around every pre-placed building; the CORE TOWER gets the big diamond.
        private static void PaintPlazas(PrePlacedBuilding[] preplaced, GridManager grid, Dictionary<Vector2Int, Kind> cells)
        {
            foreach (var pre in preplaced)
            {
                if (pre.building == null)
                {
                    Debug.LogWarning($"[StoneGround] '{pre.name}' ไม่มี building ผูกไว้ — ข้ามลานรอบอาคารนี้");
                    continue;
                }
                var size = pre.building.size;
                bool isCore = pre.building.isCoreTowerPart;

                if (isCore)
                {
                    // diamond measured from the footprint centre
                    var centre = new Vector2Int(pre.cell.x + size.x / 2, pre.cell.y + size.y / 2);
                    for (int dx = -CorePlazaRadius; dx <= CorePlazaRadius; dx++)
                        for (int dy = -CorePlazaRadius; dy <= CorePlazaRadius; dy++)
                        {
                            int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                            if (dist > CorePlazaRadius) continue;
                            var c = new Vector2Int(centre.x + dx, centre.y + dy);
                            // ragged outer ring — this is what keeps the plaza from looking stamped
                            if (dist == CorePlazaRadius &&
                                IsoGroundPainter.Hash(c.x + 91, c.y + 37) % 100 >= RingKeepPercent) continue;
                            TryAdd(grid, cells, c, Kind.Plaza);
                        }
                }
                else
                {
                    for (int x = -BuildingPad; x < size.x + BuildingPad; x++)
                        for (int y = -BuildingPad; y < size.y + BuildingPad; y++)
                        {
                            var c = new Vector2Int(pre.cell.x + x, pre.cell.y + y);
                            bool outerRing = x < 0 || y < 0 || x >= size.x || y >= size.y;
                            if (outerRing && IsoGroundPainter.Hash(c.x + 13, c.y + 71) % 100 >= 75) continue;
                            TryAdd(grid, cells, c, Kind.Plaza);
                        }
                }
            }
        }

        // Paths radiating from the core plaza toward the map edges (and past the other buildings)
        private static void PaintPaths(PrePlacedBuilding[] preplaced, GridManager grid, Dictionary<Vector2Int, Kind> cells)
        {
            var core = FindCoreCentre(preplaced, grid);
            for (int d = 0; d < 4; d++)
            {
                for (int step = CorePlazaRadius - 1; step < CorePlazaRadius + PathLength; step++)
                {
                    var c = new Vector2Int(core.x + PathDirX[d] * step, core.y + PathDirY[d] * step);
                    TryAdd(grid, cells, c, Kind.Path);

                    // widen irregularly so the path isn't a 1px ruler line
                    if (IsoGroundPainter.Hash(c.x + 501, c.y + 233) % 100 < PathWidenPercent)
                    {
                        var side = new Vector2Int(c.x + PathDirY[d], c.y + PathDirX[d]); // perpendicular
                        TryAdd(grid, cells, side, Kind.Path);
                    }
                }
            }
        }

        // Scattered remnants of old paving under the grass
        private static void PaintPatches(GridManager grid, Dictionary<Vector2Int, Kind> cells)
        {
            for (int col = 0; col < grid.columns; col++)
                for (int row = 0; row < grid.rows; row++)
                {
                    if (IsoGroundPainter.Hash(col + 53, row + 97) % 1000 >= PatchSeedPerMille) continue;

                    // grow a small blob with a deterministic walk
                    int count = PatchMinCells + IsoGroundPainter.Hash(col, row) % (PatchMaxCells - PatchMinCells + 1);
                    var cur = new Vector2Int(col, row);
                    for (int i = 0; i < count; i++)
                    {
                        TryAdd(grid, cells, cur, Kind.Patch);
                        int dir = (IsoGroundPainter.Hash(cur.x + i * 17, cur.y + i * 31) / 7) % 4;
                        cur = new Vector2Int(cur.x + PathDirX[dir], cur.y + PathDirY[dir]);
                    }
                }
        }

        // in-grid only; plaza wins over path/patch when they overlap.
        // Ore is spawned at runtime (BuildingRegistry), so there is nothing to avoid at edit time —
        // and ore sprites draw on the Buildings layer above this anyway, which reads fine (rubble under rock).
        private static void TryAdd(GridManager grid, Dictionary<Vector2Int, Kind> cells, Vector2Int c, Kind kind)
        {
            if (!grid.IsInBounds(c.x, c.y)) return;

            if (cells.TryGetValue(c, out var existing))
            {
                if (existing == Kind.Plaza || kind != Kind.Plaza) return;
            }
            cells[c] = kind;
        }

        private static Vector2Int FindCoreCentre(PrePlacedBuilding[] preplaced, GridManager grid)
        {
            foreach (var pre in preplaced)
                if (pre.building != null && pre.building.isCoreTowerPart)
                    return new Vector2Int(pre.cell.x + pre.building.size.x / 2, pre.cell.y + pre.building.size.y / 2);
            return new Vector2Int(grid.columns / 2, grid.rows / 2);
        }

        // ── variant choice ────────────────────────────────────────────────────

        private static TileBase PickTile(Vector2Int c, Kind kind, Dictionary<Vector2Int, Kind> cells, StoneTiles t)
        {
            // cells whose 4-neighbourhood leaves the stone area — moss/chipped edge reads organic
            bool border = !cells.ContainsKey(new Vector2Int(c.x + 1, c.y))
                       || !cells.ContainsKey(new Vector2Int(c.x - 1, c.y))
                       || !cells.ContainsKey(new Vector2Int(c.x, c.y + 1))
                       || !cells.ContainsKey(new Vector2Int(c.x, c.y - 1));

            int h = (IsoGroundPainter.Hash(c.x, c.y) / 7) % 100;

            if (kind == Kind.Patch)                       // old rubble: never pristine
                return h < 55 ? t.Crack[h % t.Crack.Length] : t.Moss[h % t.Moss.Length];

            if (kind == Kind.Path)
                return h < 80 ? t.Worn[h % t.Worn.Length] : t.Crack[h % t.Crack.Length];

            if (border && h < 50)
                return h < 20 && t.Edge.Length > 0 ? t.Edge[0] : t.Moss[h % t.Moss.Length];

            if (h < 60) return t.Full[h % t.Full.Length];
            if (h < 85) return t.Crack[h % t.Crack.Length];
            return t.Moss[h % t.Moss.Length];
        }

        /// <summary>
        /// Re-apply cells hand-painted with the Tile Color Painter. Runs after the automatic pass so
        /// manual edits always win; an override with an empty tileName means "keep this cell bare".
        /// </summary>
        private static int ApplyManualOverrides(Tilemap map)
        {
            var store = map.GetComponent<StonePaveOverrides>();
            if (store == null || store.overrides == null || store.overrides.Count == 0) return 0;

            int n = 0;
            foreach (var o in store.overrides)
            {
                var pos = new Vector3Int(o.cell.x, o.cell.y, 0);
                if (o.IsErase)
                {
                    map.SetTile(pos, null);
                    n++;
                    continue;
                }
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{StoneFolder}/{o.tileName}.asset");
                if (tile == null)
                {
                    Debug.LogWarning($"[StoneGround] override ที่ ({o.cell.x},{o.cell.y}) อ้างไทล์ '{o.tileName}' ที่ไม่มีอยู่ — ข้าม");
                    continue;
                }
                map.SetTile(pos, tile);
                n++;
            }
            return n;
        }

        // ── assets & scene plumbing ───────────────────────────────────────────

        private class StoneTiles
        {
            public TileBase[] Full, Crack, Moss, Worn, Edge;
        }

        private static StoneTiles LoadTiles()
        {
            var t = ReadTiles();
            if (t != null) return t;

            // Tiles missing → generate them now rather than failing on menu order (they are derived
            // assets, so regenerating is free and deterministic).
            Debug.Log("[StoneGround] ยังไม่มีไทล์หิน — สร้างให้อัตโนมัติ (Generate Stone Tiles)");
            StoneTileGenerator.Apply();

            t = ReadTiles();
            if (t == null)
                Debug.LogError("[StoneGround] สร้างไทล์หินไม่สำเร็จ — ดู error ของ StoneTileGenerator ด้านบน " +
                               "(ต้องมี Assets/Sprites/Art/Tiles/Flat/flat_000.png เป็นต้นฉบับ)");
            return t;
        }

        private static StoneTiles ReadTiles()
        {
            // grey tiles listed first, warm ones last — the pick weights below lean grey,
            // with warm slabs sprinkled in like the reference art
            var t = new StoneTiles
            {
                Full  = Load("stone_full_0", "stone_full_1", "stone_full_2", "stone_full_3", "stone_full_4"),
                Crack = Load("stone_crack_0", "stone_crack_1", "stone_crack_2"),
                Moss  = Load("stone_moss_0", "stone_moss_1", "stone_moss_2"),
                Worn  = Load("stone_worn_0", "stone_worn_1", "stone_worn_2"),
                Edge  = Load("stone_edge_0", "stone_edge_1"),
            };
            bool complete = t.Full.Length > 0 && t.Crack.Length > 0 && t.Moss.Length > 0 && t.Worn.Length > 0;
            return complete ? t : null;
        }

        private static TileBase[] Load(params string[] names)
        {
            var list = new List<TileBase>();
            foreach (var n in names)
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{StoneFolder}/{n}.asset");
                if (tile != null) list.Add(tile);
            }
            return list.ToArray();
        }

        /// <summary>Find "StonePave", or clone the Ground tilemap's setup so cell layout matches exactly.</summary>
        private static Tilemap FindOrCreateStoneTilemap()
        {
            Tilemap ground = null;
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tm.gameObject.name == StoneObjectName) return tm;
                if (tm.gameObject.name == GroundObjectName) ground = tm;
            }

            if (ground == null)
            {
                Debug.LogError($"[StoneGround] ไม่พบ Tilemap '{GroundObjectName}' — รัน Fill Grids ก่อน");
                return null;
            }

            // same parent Grid → identical cell layout/anchor, no coordinate maths needed
            var go = new GameObject(StoneObjectName);
            go.transform.SetParent(ground.transform.parent, false);
            go.transform.localPosition = ground.transform.localPosition;

            var map = go.AddComponent<Tilemap>();
            map.tileAnchor = ground.tileAnchor;
            map.orientation = ground.orientation;

            var groundRenderer = ground.GetComponent<TilemapRenderer>();
            var renderer = go.AddComponent<TilemapRenderer>();
            if (groundRenderer != null)
            {
                renderer.sortingLayerID = groundRenderer.sortingLayerID;
                renderer.sortingOrder = groundRenderer.sortingOrder + 1; // directly above the grass
                renderer.mode = groundRenderer.mode;
                renderer.sharedMaterial = groundRenderer.sharedMaterial; // Sprite-Lit-Default → takes 2D lights
            }
            else
            {
                renderer.sortingLayerName = "Ground";
                renderer.sortingOrder = 1;
                var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
                if (lit != null) renderer.sharedMaterial = lit;
            }

            Debug.Log($"[StoneGround] สร้าง Tilemap '{StoneObjectName}' (sorting order {renderer.sortingOrder}) แล้ว");
            return map;
        }
    }
}
