using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ติดตั้งรั้วกั้นเขต Zone A/B (ZoneBarrier) ในซีนเกม
    /// จัดการ GameObject "ZoneBarrier" (ZoneBarrierRenderer) ในซีน
    ///   • โมเดลใหม่ (2026-07): Zone B = แถบ NE (col ≥ columns-border) → เปิดรั้วเส้นตั้งลากแนว NW↔SE
    ///   • ตั้ง barrierColumn = columns - zoneBorderThickness (ให้ตรงกับ IsoGroundPainter.IsZoneA)
    ///   • wire สไปรต์แบริเออร์ blast_barrier.png (art ผู้ใช้) — ว่างเมื่อไม่พบ → ใช้รั้วโปรซีเยอรัล
    ///
    /// รัน: เมนู NuclearReMind/Setup Zone Barrier (Fence + Gate) — หรือรวมใน Run All Setups
    /// </summary>
    public static class ZoneBarrierSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string FenceSpritePath = "Assets/Sprites/Art/Fence/blast_barrier.png";

        // เมนูนี้: สร้าง/ตั้งค่า ZoneBarrier ในซีน + wire สไปรต์แบริเออร์
        [MenuItem("NuclearReMind/Setup Zone Barrier (Fence + Gate)")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = GameObject.Find("ZoneBarrier");
            if (go == null)
            {
                go = new GameObject("ZoneBarrier");
                Debug.Log("[ZoneBarrierSetup] สร้าง ZoneBarrier ใน scene");
            }

            var barrier = go.GetComponent<ZoneBarrierRenderer>() ?? go.AddComponent<ZoneBarrierRenderer>();

            // เปิดรั้ว + ตั้งเส้นแบ่งให้ตรงกับ Zone B (แถบ NE = col ≥ columns-border)
            barrier.active = true;

            int columns = 43, border = 7;
            var grid = Object.FindFirstObjectByType<GridManager>();
            if (grid != null) columns = grid.columns;
            var ore = Object.FindFirstObjectByType<OreDepositManager>();
            if (ore != null) border = ore.zoneBorderThickness;
            barrier.barrierColumn = Mathf.Clamp(columns - border, 1, columns - 1);

            // สไปรต์แบริเออร์ (art ผู้ใช้) — ไม่พบ → ปล่อย null (renderer ใช้รั้วโปรซีเยอรัลแทน)
            var fence = AssetDatabase.LoadAssetAtPath<Sprite>(FenceSpritePath);
            if (fence != null) barrier.fenceSprite = fence;
            else Debug.LogWarning($"[ZoneBarrierSetup] ไม่พบสไปรต์ {FenceSpritePath} — ใช้รั้วโปรซีเยอรัลแทน (เสา+ราว)");

            EditorUtility.SetDirty(barrier);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[ZoneBarrierSetup] ✅ เปิดรั้ว (active = true) · barrierColumn = {barrier.barrierColumn} " +
                      $"(columns {columns} - border {border}) · Zone B = แถบ NE · " +
                      (fence != null ? "ใช้สไปรต์ blast_barrier" : "ใช้รั้วโปรซีเยอรัล"));
        }
    }
}
