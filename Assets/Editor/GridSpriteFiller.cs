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
    /// รัน 2 ทาง:
    ///   • เมนู (scene เปิดอยู่): NuclearReMind/Fill Grids (Ground + Fog)
    ///   • batch: -executeMethod NuclearReMind.Editor.GridSpriteFiller.FillFromBatch (เปิด+เซฟ scene เอง)
    /// </summary>
    public static class GridSpriteFiller
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string GroundTilePath = "Assets/Sprites/Tiles/Ground.asset";
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
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);

            // ขนาดจริงตาม logic grid ในซีน (เฟส 8 = 43×28)
            var grid = Object.FindFirstObjectByType<GridManager>();
            int columns = grid != null ? grid.columns : FallbackColumns;
            int rows = grid != null ? grid.rows : FallbackRows;
            if (grid == null)
                Debug.LogWarning($"[GridSpriteFiller] ไม่พบ GridManager — ใช้ fallback {FallbackColumns}×{FallbackRows}");

            // build grid: sample tile เดิมจากช่อง (0,0) ถ้ามี (กันกรณีเปลี่ยน tile)
            int ground = FillTilemap(GroundObjectName, tile, sampleExisting: true, columns, rows);
            if (ground < 0) return (-1, 0);

            // fog: ใช้ tile เดียวกัน — tint ของ FogOfWar tilemap จะทำให้เป็นหมอกดำเอง
            int fog = FillTilemap(FogObjectName, tile, sampleExisting: false, columns, rows);
            return (ground, fog);
        }

        private static int FillTilemap(string objectName, TileBase fallbackTile, bool sampleExisting, int columns, int rows)
        {
            Tilemap map = null;
            foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tm.gameObject.name == objectName)
                {
                    map = tm;
                    break;
                }
            }
            if (map == null)
            {
                Debug.LogWarning($"[GridSpriteFiller] ไม่พบ Tilemap ชื่อ '{objectName}' ใน scene — ข้าม");
                return -1;
            }

            TileBase tile = sampleExisting
                ? (map.GetTile(new Vector3Int(0, 0, 0)) ?? fallbackTile)
                : fallbackTile;
            if (tile == null)
            {
                Debug.LogError($"[GridSpriteFiller] ไม่พบ tile สำหรับ '{objectName}' (fallback {GroundTilePath})");
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
