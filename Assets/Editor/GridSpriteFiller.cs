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
    /// Ground ระบายเป็นลายหมากรุกด้วย tile หญ้าของทีม (GroundTileSetup) — ต้องรันเมนูนั้นก่อน
    /// ถ้าไม่มี tile หญ้าจะถอยไปใช้ Ground.asset (placeholder) แบบ tile เดียวเหมือนเดิม
    ///
    /// รัน 2 ทาง:
    ///   • เมนู (scene เปิดอยู่): NuclearReMind/Fill Grids (Ground + Fog)
    ///   • batch: -executeMethod NuclearReMind.Editor.GridSpriteFiller.FillFromBatch (เปิด+เซฟ scene เอง)
    /// </summary>
    public static class GridSpriteFiller
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string GroundTilePath = "Assets/Sprites/Tiles/Ground.asset"; // placeholder (fallback)
        // พื้นหญ้าของทีม — สลับสว่าง/เข้มเป็นลายหมากรุก (สร้างโดย GroundTileSetup)
        private const string GrassATilePath = "Assets/Sprites/Art/Tiles/GroundGrassA.asset";
        private const string GrassBTilePath = "Assets/Sprites/Art/Tiles/GroundGrassB.asset";
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
            TileBase grassB = AssetDatabase.LoadAssetAtPath<TileBase>(GrassBTilePath);
            TileBase legacy = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);

            // ขนาดจริงตาม logic grid ในซีน (เฟส 8 = 43×28)
            var grid = Object.FindFirstObjectByType<GridManager>();
            int columns = grid != null ? grid.columns : FallbackColumns;
            int rows = grid != null ? grid.rows : FallbackRows;
            if (grid == null)
                Debug.LogWarning($"[GridSpriteFiller] ไม่พบ GridManager — ใช้ fallback {FallbackColumns}×{FallbackRows}");

            // มีหญ้าทีม → ลายหมากรุก · ไม่มี → tile เดียวแบบเดิม
            // (ไม่ sample tile เดิมจากช่อง (0,0) อีกแล้ว — ไม่งั้นพื้นเก่าจะถูกยึดไว้ตลอด เปลี่ยน tile ไม่ติด)
            bool checker = grassA != null && grassB != null;
            if (!checker && legacy == null)
            {
                Debug.LogError($"[GridSpriteFiller] ไม่พบ tile ทั้ง {GrassATilePath} และ {GroundTilePath}");
                return (-1, 0);
            }
            if (!checker)
                Debug.LogWarning("[GridSpriteFiller] ไม่พบ tile หญ้าทีม — ใช้ placeholder (รัน Setup Ground Tiles ก่อน)");

            int ground = checker
                ? FillChecker(GroundObjectName, grassA, grassB, columns, rows)
                : FillTilemap(GroundObjectName, legacy, columns, rows);
            if (ground < 0) return (-1, 0);

            // fog: tile ใบเดียวพอ — tint ของ FogOfWar tilemap ทำให้เป็นหมอกดำเอง (ไม่ต้องเห็นลายหมากรุกใต้หมอก)
            int fog = FillTilemap(FogObjectName, grassA ?? legacy, columns, rows);
            return (ground, fog);
        }

        /// <summary>ระบายลายหมากรุก: (col+row) คู่ = tile สว่าง, คี่ = tile เข้ม (ตรงกับ Map.PNG ต้นฉบับ)</summary>
        private static int FillChecker(string objectName, TileBase light, TileBase dark, int columns, int rows)
        {
            Tilemap map = FindTilemap(objectName);
            if (map == null) return -1;

            Undo.RegisterCompleteObjectUndo(map, "Fill Grid");
            map.ClearAllTiles();

            int count = 0;
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), (x + y) % 2 == 0 ? light : dark);
                    count++;
                }

            map.CompressBounds();
            EditorUtility.SetDirty(map);
            Debug.Log($"[GridSpriteFiller] เติม '{objectName}' {columns}×{rows} = {count} ช่อง (ลายหมากรุก)");
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
