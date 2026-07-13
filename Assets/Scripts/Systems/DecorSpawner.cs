using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// โรยของประดับ (ต้นไม้/ก้อนหิน) รอบกริดในเขต apron นอกพื้นที่เล่น (V4 §5 decor) —
    /// deterministic ล้วนจาก IsoGroundPainter.Hash (ไม่ใช้ Random) → ลายคงที่ทุกครั้งที่ spawn/โหลดเซฟ
    /// (Random จะสลับลายทุก re-spawn = บั๊กเงียบแบบเดียวกับที่เลี่ยงใน WorkerSeparation/SpriteAnimation)
    ///
    /// วางเฉพาะ "นอกกริด" (ช่องใน [-apronMargin, columns/rows+apronMargin) ที่อยู่นอก [0,columns)×[0,rows)) —
    /// อาคารอยู่ในกริดล้วน ของประดับจึงไม่ชน footprint (ไม่ต้องเช็ก BuildingRegistry)
    /// sort: layer "Buildings" + SortTier.Unit → occlude ถูกตาม iso depth · ไม่ทับตัวอาคาร/คนงานในกริด
    /// </summary>
    public class DecorSpawner : MonoBehaviour
    {
        private const string BuildingsSortingLayer = "Buildings";

        [Header("สไปรต์ของประดับ (assign โดย DecorSetup — ว่าง = ไม่โรยอะไร)")]
        public Sprite[] decorSprites;

        [Header("พารามิเตอร์")]
        [Range(0, 30)] public int densityPercent = 8;     // % ของช่องนอกกริดที่จะมีของประดับ (ผู้ใช้เลือก 8%)
        public int apronMargin = 35;                       // ระยะ apron รอบกริด (ให้ตรง IsoGroundPainter/OreDepositManager)
        [Range(0f, 0.5f)] public float jitter = 0.25f;     // ยึกยักตำแหน่งย่อยในช่อง (tile) — deterministic ไม่ใช้ Random
        public bool spawnOnStart = true;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        /// <summary>โรยของประดับใหม่ทั้งหมด (ลบของเดิมก่อน) — deterministic ตาม Hash วางเดิมทุกครั้ง</summary>
        public void Spawn()
        {
            Clear();

            if (decorSprites == null || decorSprites.Length == 0) return;
            var grid = GridManager.Instance;
            if (grid == null) return;

            int cols = grid.columns;
            int rows = grid.rows;
            int m = Mathf.Max(0, apronMargin);

            for (int x = -m; x < cols + m; x++)
            {
                for (int y = -m; y < rows + m; y++)
                {
                    // เว้นในกริด (พื้นที่เล่น) — โรยเฉพาะ apron รอบนอก
                    if (x >= 0 && x < cols && y >= 0 && y < rows) continue;

                    int h = IsoGroundPainter.Hash(x, y);
                    if (h % 100 >= densityPercent) continue;      // ประตูความหนาแน่น (เลขหลักหน่วย/สิบ)

                    var sprite = decorSprites[(h / 100) % decorSprites.Length]; // เลือกชิ้นจากหลักสูงของ hash
                    if (sprite == null) continue;

                    // jitter deterministic จาก Hash (perturb พิกัด) → ไม่ใช้ Random รายเฟรม ลายคงที่
                    float jx = (IsoGroundPainter.Hash(x + 101, y) / 2147483647f - 0.5f) * 2f * jitter;
                    float jy = (IsoGroundPainter.Hash(x, y + 101) / 2147483647f - 0.5f) * 2f * jitter;

                    var go = new GameObject($"Decor_{x}_{y}");
                    go.transform.SetParent(transform, false);
                    go.transform.position = grid.IsoToWorldF(x + jx, y + jy);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingLayerName = BuildingsSortingLayer;
                    sr.sortingOrder = GridManager.SortOrder(x, y, GridManager.SortTier.Unit);

                    _spawned.Add(go);
                }
            }
        }

        /// <summary>ลบของประดับที่ spawn ไว้ทั้งหมด (Destroy ใน Play · DestroyImmediate นอก Play/เทส)</summary>
        public void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
        }
    }
}
