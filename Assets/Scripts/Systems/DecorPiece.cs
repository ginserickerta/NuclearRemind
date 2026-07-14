using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ของประดับที่ "วางเอง" ในซีน (ลากสไปรต์จาก Assets/Sprites/Decor เข้ามาแล้วแนบตัวนี้)
    ///   • จัด sortingLayer "Buildings" + sortingOrder ตามความลึก iso อัตโนมัติ (Unit tier — ตรงกับ DecorSpawner เดิม)
    ///     ทำงานทั้งใน edit (ลาก/ขยับแล้วเรียงสดทันที) และ runtime
    ///   • inspect ไม่ได้: ลบ Collider2D ที่ติดมา (ถ้ามี) → คลิกไม่โดน ไม่เปิดแผงใด ๆ · ไม่มี BuildingClickTarget อยู่แล้ว
    /// วางกี่ชิ้น ตรงไหนก็ได้ตามใจ — ต่างจาก DecorSpawner ที่โรยอัตโนมัติทั่ว apron
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class DecorPiece : MonoBehaviour
    {
        [Tooltip("ชั้นวาด (ให้ตรงกับอาคาร/คนงาน)")]
        public string sortingLayer = "Buildings";

        private SpriteRenderer _sr;
        private GridManager _grid;

        private void OnEnable()
        {
            _sr = GetComponent<SpriteRenderer>();
            StripCollider();   // กัน inspect (ทำครั้งเดียวพอ — decor ไม่ได้เพิ่ม collider ทีหลัง)
            Apply();
        }

        private void Update()
        {
            // edit-time: ลาก/ขยับตำแหน่งแล้วอัปเดตลำดับวาดสด · runtime: decor นิ่ง ไม่ต้องคิดทุกเฟรม
            if (!Application.isPlaying) Apply();
        }

        private void Apply()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;

            _sr.sortingLayerName = sortingLayer;

            if (_grid == null)
                _grid = GridManager.Instance != null ? GridManager.Instance
                                                     : FindFirstObjectByType<GridManager>();
            if (_grid != null)
            {
                Vector2 iso = _grid.WorldToIsoF(transform.position);
                _sr.sortingOrder = GridManager.SortOrder(iso.x, iso.y, GridManager.SortTier.Unit);
            }
        }

        private void StripCollider()
        {
            var col = GetComponent<Collider2D>();
            if (col == null) return;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }
}
