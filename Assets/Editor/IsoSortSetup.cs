using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ตั้งการเรียงลำดับ (sort) ให้พื้น isometric ที่เป็นไทล์บล็อก 2.5D วางทับกันถูกต้อง —
    /// ★ นี่คือขั้นที่ทำให้ "ผนังข้างไทล์ไม่โผล่เป็นช่องดำ"
    ///
    /// ปัญหาเดิม: ไทล์ IsoNature เป็นบล็อก (หน้าบน + ผนังห้อยลงช่องหน้า) แต่
    ///   • Camera Transparency Sort = Default (เรียงตามแกน Z) → ไทล์ไม่เรียงตาม Y
    ///   • Ground TilemapRenderer Mode = Chunk → ทั้งแมพเป็นก้อนเดียว ไทล์ในก้อนไม่ sort กันเอง
    ///   ⇒ ตัวหลังไม่ถูกตัวหน้าทับ ผนังเลยโผล่
    ///
    /// แก้: Custom Axis (0,1,0) — เรียงตาม Y (ช่องหน้า/ล่างจอ = Y ต่ำ = วาดทับ) + Individual mode (แต่ละไทล์ sort เอง)
    ///   → ช่องหน้าเอาหน้าบนทับผนังช่องหลัง = พื้นต่อเนื่องเนียน (ดูตัวอย่างในแชต)
    ///
    /// ปลอดภัยกับอาคาร/คนงาน: พวกนั้นอยู่คนละ Sorting Layer + ตั้ง sortingOrder เองต่อเฟรม (แกน sort ไม่กระทบ)
    ///
    /// รัน: เมนู NuclearReMind/Fix Isometric Sort (Tile Walls) — หรือใน Run All Setups (ก่อน Fill Grids)
    /// </summary>
    public static class IsoSortSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private static readonly string[] GroundTilemaps = { "Ground", "FogOfWar" };

        [MenuItem("NuclearReMind/Fix Isometric Sort (Tile Walls)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int camDone = FixCameras();
            int mapDone = FixTilemapRenderers();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[IsoSortSetup] ✅ ตั้ง Custom Axis (0,1,0) ให้กล้อง {camDone} ตัว · " +
                      $"Individual mode ให้ tilemap {mapDone} ตัว — เซฟซีนแล้ว · รัน Fill Grids เพื่อเห็นผล");
        }

        // กล้องทุกตัวในซีน — เรียง sprite ตามแกน Y (มาตรฐาน isometric)
        private static int FixCameras()
        {
            int n = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(cam, "Iso Sort");
                cam.transparencySortMode = TransparencySortMode.CustomAxis;
                cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
                EditorUtility.SetDirty(cam);
                n++;
            }
            return n;
        }

        // Ground/FogOfWar — Individual mode ให้แต่ละไทล์ sort ตามแกน Y (ผนังถูกตัวหน้าทับ)
        private static int FixTilemapRenderers()
        {
            int n = 0;
            foreach (var tr in Object.FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None))
            {
                bool isGround = System.Array.IndexOf(GroundTilemaps, tr.gameObject.name) >= 0;
                if (!isGround) continue;

                Undo.RecordObject(tr, "Iso Sort");
                tr.mode = TilemapRenderer.Mode.Individual;
                EditorUtility.SetDirty(tr);
                n++;
            }
            return n;
        }
    }
}
