using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.Editor
{
    /// <summary>ปรับขนาด tile และจำนวน cell ของ Isometric Grid + Unity Grid component</summary>
    public static class GridTileScaler
    {
        [MenuItem("NuclearReMind/Set Grid Scale 1× (tileWidth=1)")]
        public static void SetScale1x() => ApplyScale(1f, 0.5f);

        [MenuItem("NuclearReMind/Set Grid Scale 1.5× (tileWidth=1.5)")]
        public static void SetScale1_5x() => ApplyScale(1.5f, 0.75f);

        [MenuItem("NuclearReMind/Set Grid Scale 2× (tileWidth=2)")]
        public static void SetScale2x() => ApplyScale(2f, 1f);

        [MenuItem("NuclearReMind/Set Grid Scale 3× (tileWidth=3)")]
        public static void SetScale3x() => ApplyScale(3f, 1.5f);

        private static void ApplyScale(float tileW, float tileH)
        {
            // ── GridManager ───────────────────────────────────────────────
            var gm = Object.FindFirstObjectByType<GridManager>();
            if (gm == null)
            {
                Debug.LogError("[GridTileScaler] ไม่พบ GridManager ใน scene");
                return;
            }
            Undo.RecordObject(gm, "Set Grid Tile Scale");
            gm.columns    = 20;
            gm.rows       = 12;
            gm.tileWidth  = tileW;
            gm.tileHeight = tileH;
            EditorUtility.SetDirty(gm);

            // ── Unity Grid component (ควบคุม Tilemap cell size) ──────────
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid != null)
            {
                Undo.RecordObject(grid, "Set Grid Tile Scale");
                grid.cellSize = new Vector3(tileW, tileH, 1f);
                EditorUtility.SetDirty(grid);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[GridTileScaler] ตั้งค่า tileWidth={tileW}, tileHeight={tileH} — กด Save Scene (Ctrl+S)");
        }
    }
}
