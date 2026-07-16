using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// รั้วกั้นเขตรังสี + ประตู GATE บนเส้นแบ่งโซน A/B (V4 §5 — กริด 43×28)
    ///   โซน A (คอลัมน์ 0..barrierColumn-1) = Safe Radiation Area · โซน B (barrierColumn..) = High Radiation Area
    ///
    /// รั้ววางบน "ขอบร่วม" ระหว่างคอลัมน์ barrierColumn-1 กับ barrierColumn — ขอบเหล่านี้เรียงต่อกัน
    /// เป็นเส้นตรงในพิกัด isometric (ทิศ (-tileWidth/2, +tileHeight/2) ต่อ 1 แถว) จึงวาดเป็นแนวรั้วตรงได้
    ///
    /// ★ visual ล้วน — ไม่จอง Cell ใด ๆ (รั้วอยู่บนขอบ ไม่ใช่ในช่อง) การวางอาคาร/เดินคนงาน/เซฟ ไม่เปลี่ยน
    /// ประตูคือช่องว่างของรั้ว: สื่อว่า "ทางเดียวที่คนงานเข้าโซน B ได้" ซึ่งผูกกับบทเรียน ALARA (Q5)
    ///
    /// รั้วจริงสร้างตอน Start (Play mode) · ใน Editor ดูแนวได้จาก gizmo เส้นเหลือง/เขียว
    /// หรือคลิกขวาที่คอมโพเนนต์ → Rebuild Barrier เพื่อพรีวิวของจริง (object ไม่ถูกเซฟลงซีน)
    /// </summary>
    public class ZoneBarrierRenderer : MonoBehaviour
    {
        private const string SortingLayer = "Buildings";
        private const int PostPixelsPerUnit = 64;

        [Tooltip("เปิดรั้วเส้นตั้งคั่นโซน — Zone B เป็นแถบ NE (col ≥ barrierColumn) รั้วลากแนว NW↔SE")]
        public bool active = false;

        [Header("เส้นแบ่งโซน (ใช้เมื่อ active = true)")]
        [Tooltip("คอลัมน์แรกของโซน B — รั้ววางบนขอบระหว่างคอลัมน์นี้กับคอลัมน์ก่อนหน้า (= columns - zoneBorderThickness)")]
        public int barrierColumn = 36;

        [Header("สไปรต์แบริเออร์ (art จริง) — ว่าง = ใช้รั้วโปรซีเยอรัล (เสา+ราว)")]
        [Tooltip("สไปรต์แบริเออร์คอนกรีต (blast_barrier) — ตั้งแล้วจะวางเรียงตามแนวรั้วแทนราว · pivot ล่างกลาง")]
        public Sprite fenceSprite;
        [Tooltip("สเกลสไปรต์แบริเออร์ต่อชิ้น (จูนให้พอดี ~1 ช่อง)")]
        public float fenceSpriteScale = 1.2f;

        [Header("ประตู GATE")]
        [Tooltip("แถวกึ่งกลางประตู · -1 = กึ่งกลางแนวรั้วอัตโนมัติ")]
        public int gateRow = -1;
        [Tooltip("ความกว้างประตู (จำนวนแถวที่เว้นรั้ว)")]
        public int gateWidthRows = 3;

        [Header("รูปลักษณ์")]
        public Color postColor = new Color(0.28f, 0.30f, 0.34f);
        public Color railColor = new Color(0.40f, 0.43f, 0.48f);
        public Color gatePostColor = new Color(0.95f, 0.75f, 0.15f);
        [Range(0.01f, 0.10f)] public float railThickness = 0.035f;

        [Header("Gizmos")]
        [Tooltip("วาดเส้นแบ่งโซนใน Scene view (เหลือง = รั้ว · เขียว = ประตู)")]
        public bool showBoundaryGizmo = true;

        [Header("กันคนงานเดินทะลุ / ล็อกโซน B")]
        [Tooltip("รั้วกั้น 'การเดิน' ของคนงานจริง (ไม่ใช่แค่รูป) — ข้ามเส้นรั้วได้เฉพาะช่องประตูเท่านั้น")]
        public bool blockWorkers = true;
        [Tooltip("เริ่มเกมโดยประตูปิด (เดินเข้าโซน B ไม่ได้) จนกว่า Story Beat ที่ตั้ง unlocksZoneB จะยิง")]
        public bool startLocked = true;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        // ── สถานะรั้วสำหรับกันเดิน (อ่านโดย WorkerView/WorkerAssignmentManager ผ่าน static — ไม่ Find ทุกเฟรม) ──
        //   ★ static เพราะ WorkerView มีหลายสิบตัว เรียกทุกเฟรม · reset ผ่าน SubsystemRegistration กัน fast-play-mode ค้าง
        private static bool s_blocks;
        private static int s_barrierCol;
        private static bool s_gateOpen;
        private static int s_gateStart = 1, s_gateEnd; // ค่าเริ่ม start>end = ไม่มีประตู

        private bool _unlocked; // ประตูเปิดแล้ว (จาก startLocked=false หรือ OnZoneBUnlocked)

        /// <summary>โซน B เปิดให้เดินเข้าแล้วหรือยัง (ประตูเปิด)</summary>
        public static bool ZoneBUnlocked => s_gateOpen;

        /// <summary>คอลัมน์นี้อยู่ในโซน B ไหม (ฝั่ง NE ของรั้ว · เฉพาะเมื่อรั้วกันเดินเปิดอยู่)</summary>
        public static bool IsZoneBColumn(int col) => s_blocks && col >= s_barrierCol;

        /// <summary>ก้าวจาก from → to ถูกรั้วโซนกั้นไหม — ส่งเป็น predicate ให้ WorkerPathing.Step</summary>
        public static bool CrossBlocked(Vector2Int from, Vector2Int to)
            => s_blocks && ZoneBarrierMath.CrossingBlocked(from, to, s_barrierCol, s_gateOpen, s_gateStart, s_gateEnd);

        /// <summary>waypoint iso ที่คนงานควรเล็งก่อนเพื่อลอดประตู (ถ้าปลายทางอยู่คนละฝั่งรั้ว) · ไม่มีรั้ว = คืน to</summary>
        public static Vector2 RouteThroughGate(Vector2 from, Vector2 to)
            => s_blocks ? ZoneBarrierMath.RouteThroughGate(from, to, s_barrierCol, s_gateOpen, s_gateStart, s_gateEnd) : to;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_blocks = false; s_barrierCol = 0; s_gateOpen = false; s_gateStart = 1; s_gateEnd = 0;
        }

        // sprite สร้างครั้งเดียวแล้ว cache (idiom เดียวกับ BuildingVisualSpawner.GetShadowSprite)
        private static Sprite _postSprite;
        private static Sprite _gatePostSprite;
        private static Sprite _railSprite;
        private static Sprite _signSprite;

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnZoneBUnlocked += HandleZoneBUnlocked;
        }

        private void Start()
        {
            _unlocked |= !startLocked; // เปิดเลยถ้าไม่ล็อก · ถ้า event เปิดไปก่อน Start (โหลดเซฟ) ก็คงไว้
            if (active) Rebuild();
            PublishState();
        }

        // Story Beat ที่ตั้ง unlocksZoneB ยิง → เปิดประตูถาวร (เดิน/จัดคนเข้าโซน B ได้)
        private void HandleZoneBUnlocked()
        {
            if (_unlocked) return;
            _unlocked = true;
            if (active) Rebuild();  // เผื่ออนาคตเปลี่ยน visual ตอนเปิด (ตอนนี้รูปเท่าเดิม)
            PublishState();
            EventManager.Instance?.RaiseNotice("ประตูเขต Zone B เปิดแล้ว — ส่งคนงานเข้าไปได้");
        }

        // เผยแพร่สถานะรั้วปัจจุบันสู่ static ให้ระบบเดิน/จัดคนอ่าน (เรียกทุกครั้งที่ค่าเปลี่ยน)
        private void PublishState()
        {
            var grid = GridManager.Instance != null
                ? GridManager.Instance
                : Object.FindFirstObjectByType<GridManager>();
            int rows = grid != null ? grid.rows : 0;
            ComputeGate(rows, out int gStart, out int gEnd);

            s_blocks = active && blockWorkers;
            s_barrierCol = grid != null ? Mathf.Clamp(barrierColumn, 1, grid.columns - 1) : barrierColumn;
            s_gateOpen = _unlocked;
            s_gateStart = gStart;
            s_gateEnd = gEnd;
        }

        /// <summary>ลบรั้วเดิมแล้วสร้างใหม่ตามค่าปัจจุบัน — เรียกซ้ำได้ (Inspector/Editor tool)</summary>
        [ContextMenu("Rebuild Barrier")]
        public void Rebuild()
        {
            Clear();
            PublishState(); // ให้ static ตรงกับค่าปัจจุบันเสมอ (รวมตอนกด Rebuild จาก Inspector)

            var grid = GridManager.Instance != null
                ? GridManager.Instance
                : Object.FindFirstObjectByType<GridManager>();
            if (grid == null) return;

            int rows = grid.rows;
            int col = Mathf.Clamp(barrierColumn, 1, grid.columns - 1);
            if (rows <= 0) return;

            ComputeGate(rows, out int gateStart, out int gateEnd);

            // มีสไปรต์แบริเออร์ (art จริง) → วางเรียงตามแนวรั้วแทนเสา/ราวโปรซีเยอรัล (เว้นช่องประตู)
            if (fenceSprite != null)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (gateStart <= gateEnd && r >= gateStart && r <= gateEnd) continue;
                    SpawnBarrierSprite(grid, col, r);
                }
                if (gateStart <= gateEnd)
                    SpawnGateSign(grid, col, gateStart, gateEnd);
                return;
            }

            // เสารั้ว: rows+1 ต้น ที่ปลายขอบแต่ละแถว (แถว r กินขอบจากจุด r ถึงจุด r+1)
            for (int r = 0; r <= rows; r++)
            {
                bool isGatePost = gateStart <= gateEnd && (r == gateStart || r == gateEnd + 1);
                SpawnPost(grid, col, r, isGatePost);
            }

            // ราวรั้ว 2 ชั้นต่อช่วง — เว้นช่วงที่เป็นประตู
            for (int r = 0; r < rows; r++)
            {
                if (gateStart <= gateEnd && r >= gateStart && r <= gateEnd) continue;
                SpawnRail(grid, col, r, heightFactor: 0.82f);
                SpawnRail(grid, col, r, heightFactor: 0.42f);
            }

            if (gateStart <= gateEnd)
                SpawnGateSign(grid, col, gateStart, gateEnd);
        }

        // วางสไปรต์แบริเออร์ 1 ชิ้นกึ่งกลางขอบแถว r (pivot ล่างกลาง → ฐานนั่งบนแนวเส้น)
        private void SpawnBarrierSprite(GridManager grid, int col, int r)
        {
            Vector3 a = EdgePoint(grid, col, r);
            Vector3 b = EdgePoint(grid, col, r + 1);
            Vector3 mid = (a + b) * 0.5f;

            var go = NewChild($"Barrier_{r}");
            go.transform.position = mid;
            float s = fenceSpriteScale > 0f ? fenceSpriteScale : 1f;
            go.transform.localScale = new Vector3(s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fenceSprite;
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = SortingOrderAt(col, r);
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnZoneBUnlocked -= HandleZoneBUnlocked;
            Clear();
        }

        private void Clear()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private void ComputeGate(int rows, out int start, out int end)
            => ZoneBarrierMath.GateRange(rows, gateRow, gateWidthRows, out start, out end);

        // ─────────────────────────────────────────
        //  เรขาคณิต — จุดปลายขอบระหว่างคอลัมน์ (col-1) กับ col ที่ "รอยต่อ" ของแถว r
        // ─────────────────────────────────────────

        private static Vector3 EdgePoint(GridManager grid, int col, int r)
            => grid.IsoToWorldF(col - 0.5f, r - 0.5f);

        // ลึกเข้าจอ = col+row น้อย → วาดก่อน (สเกลเดียวกับ BuildingVisualSpawner ผ่าน GridManager.SortOrder)
        private static int SortingOrderAt(int col, int r) => GridManager.SortOrder(col, r) - 1;

        // ─────────────────────────────────────────
        //  Spawn
        // ─────────────────────────────────────────

        private void SpawnPost(GridManager grid, int col, int r, bool isGatePost)
        {
            var go = NewChild(isGatePost ? $"GatePost_{r}" : $"FencePost_{r}");
            go.transform.position = EdgePoint(grid, col, r);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = isGatePost ? GetGatePostSprite() : GetPostSprite();
            sr.color = isGatePost ? gatePostColor : postColor;
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = SortingOrderAt(col, r);
        }

        private void SpawnRail(GridManager grid, int col, int r, float heightFactor)
        {
            Vector3 a = EdgePoint(grid, col, r);
            Vector3 b = EdgePoint(grid, col, r + 1);
            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length <= 0.0001f) return;

            float railY = PostWorldHeight() * heightFactor;

            var go = NewChild($"FenceRail_{r}_{(heightFactor > 0.6f ? "top" : "bottom")}");
            go.transform.position = a + Vector3.up * railY;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(length, railThickness, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetRailSprite(); // 1×1 pivot ซ้ายกลาง PPU 1 → scale = ความยาวจริง
            sr.color = railColor;
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = SortingOrderAt(col, r);
        }

        // ป้ายเตือนลายทางเหลือง-ดำ ลอยเหนือกึ่งกลางประตู
        private void SpawnGateSign(GridManager grid, int col, int gateStart, int gateEnd)
        {
            Vector3 a = EdgePoint(grid, col, gateStart);
            Vector3 b = EdgePoint(grid, col, gateEnd + 1);
            Vector3 mid = (a + b) * 0.5f;

            var go = NewChild("GateSign");
            go.transform.position = mid + Vector3.up * (PostWorldHeight() * 1.15f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSignSprite();
            sr.sortingLayerName = SortingLayer;
            sr.sortingOrder = SortingOrderAt(col, gateEnd + 1) + 1;
        }

        private GameObject NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSave; // สร้างใหม่ทุกครั้ง — ไม่ยัดลง scene ให้ diff บวม
            _spawned.Add(go);
            return go;
        }

        private static float PostWorldHeight() => 28f / PostPixelsPerUnit;

        // ─────────────────────────────────────────
        //  Sprites (สร้างด้วยโค้ด — ไม่มีไฟล์ art ให้ดูแล)
        // ─────────────────────────────────────────

        private static Sprite GetPostSprite()
        {
            if (_postSprite == null) _postSprite = MakePost(shaded: true);
            return _postSprite;
        }

        private static Sprite GetGatePostSprite()
        {
            if (_gatePostSprite == null) _gatePostSprite = MakePost(shaded: false);
            return _gatePostSprite;
        }

        // เสา 6×28 px · pivot ล่างกลาง (ยืนบนพื้น) · shaded = ไล่เงาด้านขวาให้ดูมีปริมาตร
        private static Sprite MakePost(bool shaded)
        {
            const int w = 6, h = 28;
            var tex = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float shade = shaded ? Mathf.Lerp(1.15f, 0.7f, x / (float)(w - 1)) : 1f;
                    tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PostPixelsPerUnit);
        }

        // ราว: 1×1 ขาว pivot ซ้ายกลาง PPU 1 → localScale = (ความยาว, ความหนา) ในหน่วย world ตรง ๆ
        private static Sprite GetRailSprite()
        {
            if (_railSprite != null) return _railSprite;

            var tex = NewTex(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _railSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return _railSprite;
        }

        // ป้ายเตือน 28×12 px — ลายทางเฉียงเหลือง/ดำ + ขอบดำ
        private static Sprite GetSignSprite()
        {
            if (_signSprite != null) return _signSprite;

            const int w = 28, h = 12;
            var tex = NewTex(w, h);
            var yellow = new Color(0.97f, 0.78f, 0.12f);
            var black = new Color(0.10f, 0.10f, 0.12f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool stripe = ((x + y) / 4) % 2 == 0;
                    tex.SetPixel(x, y, border || stripe ? black : yellow);
                }

            tex.Apply();
            _signSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PostPixelsPerUnit);
            return _signSprite;
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

        // เส้นแบ่งโซนใน Scene view (เห็นแนวได้แม้ยังไม่กด Play — ล้อ GridManager.OnDrawGizmos)
        private void OnDrawGizmos()
        {
            if (!active || !showBoundaryGizmo) return;

            var grid = GridManager.Instance != null
                ? GridManager.Instance
                : Object.FindFirstObjectByType<GridManager>();
            if (grid == null || grid.rows <= 0) return;

            int col = Mathf.Clamp(barrierColumn, 1, grid.columns - 1);
            ComputeGate(grid.rows, out int gateStart, out int gateEnd);

            for (int r = 0; r < grid.rows; r++)
            {
                bool gate = gateStart <= gateEnd && r >= gateStart && r <= gateEnd;
                Gizmos.color = gate ? Color.green : Color.yellow;
                Gizmos.DrawLine(EdgePoint(grid, col, r), EdgePoint(grid, col, r + 1));
            }
        }
    }

    /// <summary>
    /// สูตร pure ของแนวรั้ว — EditMode ตรวจได้โดยไม่ต้องมี scene (ล้อ OreMath)
    /// </summary>
    public static class ZoneBarrierMath
    {
        /// <summary>
        /// ช่วงแถวของประตู (inclusive) บนแนวรั้ว rows ช่วง
        /// centerRow &lt; 0 = กึ่งกลางอัตโนมัติ · width ≤ 0 = ไม่มีประตู (คืน start &gt; end)
        /// ประตูถูก clamp ให้อยู่ในแนวรั้วเสมอ (ไม่ล้นขอบกริด)
        /// </summary>
        public static void GateRange(int rows, int centerRow, int width, out int start, out int end)
        {
            width = Mathf.Clamp(width, 0, Mathf.Max(rows, 0));
            if (width <= 0 || rows <= 0)
            {
                start = 1;
                end = 0;
                return;
            }

            int center = centerRow < 0 ? rows / 2 : centerRow;
            start = Mathf.Clamp(center - width / 2, 0, rows - width);
            end = start + width - 1;
        }

        /// <summary>
        /// ก้าวจากช่อง from → to ถูกรั้วโซนกั้นไหม (true = ก้าวไม่ได้)
        /// รั้วอยู่บนขอบคอลัมน์ barrierColumn (ระหว่าง col-1 กับ col) — กั้นเฉพาะการ "ข้ามเส้น" นั้น
        ///   gateOpen=false → โซน B ล็อก: กั้นทุกแถว (เข้าไม่ได้เลย)
        ///   gateOpen=true  → เปิดเฉพาะแถวประตู gateStart..gateEnd (นอกนั้นยังกั้น → ต้องอ้อมไปประตู)
        /// เดินภายในโซนเดียวกัน / ขนานรั้ว (ไม่ข้ามเส้น) = ไม่กั้น
        /// </summary>
        public static bool CrossingBlocked(Vector2Int from, Vector2Int to,
            int barrierColumn, bool gateOpen, int gateStart, int gateEnd)
        {
            bool fromB = from.x >= barrierColumn;
            bool toB = to.x >= barrierColumn;
            if (fromB == toB) return false;   // ไม่ได้ข้ามเส้นรั้ว
            if (!gateOpen) return true;       // โซน B ล็อก → ข้ามไม่ได้ทุกแถว
            int row = toB ? to.y : from.y;    // แถวฝั่งโซน B ของการข้าม
            bool throughGate = gateStart <= gateEnd && row >= gateStart && row <= gateEnd;
            return !throughGate;              // นอกช่องประตู → ยังกั้น
        }

        /// <summary>
        /// เป้าหมายชั่วคราวที่คนงานควรมุ่งไปก่อน "เพื่อให้ลอดประตูได้จริง" เมื่อปลายทางอยู่คนละฝั่งรั้ว
        ///   คืน waypoint ที่ช่องติดประตู (แถว = กึ่งกลางประตู) → การไถลตามรั้ว (WorkerPathing) จะพาแถวมาชิดประตูเองแล้วข้าม
        ///   ไม่ต้องข้าม / ข้ามไม่ได้ (ล็อก·ไม่มีประตู) → คืน to ตามเดิม (ปล่อยให้ Step กันเอง)
        /// pure: ไม่มี A* — อาศัยคุณสมบัติว่าถ้า "แถวเป้าหมาย = แถวประตู" การไถลจะ funnel เข้าประตูเสมอ
        /// </summary>
        public static Vector2 RouteThroughGate(Vector2 from, Vector2 to,
            int barrierColumn, bool gateOpen, int gateStart, int gateEnd)
        {
            bool fromB = from.x >= barrierColumn;
            bool toB = to.x >= barrierColumn;
            if (fromB == toB) return to;                       // อยู่ฝั่งเดียวกับปลายทาง → ไม่ต้องอ้อม
            if (!gateOpen || gateStart > gateEnd) return to;   // ข้ามไม่ได้/ไม่มีประตู → ปล่อยตามเดิม
            float gateRow = (gateStart + gateEnd) * 0.5f;
            float col = toB ? barrierColumn : barrierColumn - 1; // ช่องฝั่งปลายทางที่ติดประตู
            return new Vector2(col, gateRow);
        }
    }
}
