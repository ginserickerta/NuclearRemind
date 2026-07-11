using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// กล้อง Orthographic สไตล์ city builder:
    /// - WASD/ลูกศร เลื่อนแบบมีแรงเฉื่อย (SmoothDamp เร่ง/หน่วงนุ่ม ไม่กระตุก)
    /// - คลิกกลางค้างแล้วลาก = จับแมพเลื่อน (จุดใต้เมาส์ตรึงอยู่กับที่)
    /// - Scroll ซูมแบบ ease เข้าหาตำแหน่งเมาส์ (zoom-to-cursor)
    /// - ความเร็วเลื่อนสเกลตามระยะซูม (ซูมไกล = เลื่อนไว, ซูมใกล้ = ละเอียด)
    /// - clamp ไม่ให้ศูนย์กลางกล้องหลุดขอบแมพ (อ่านมุมกริดจาก GridManager)
    /// ใช้ unscaledDeltaTime ทั้งหมด — กล้องยังเลื่อน/ซูมได้ระหว่าง pause (timeScale 0)
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan (WASD/ลูกศร)")]
        public float panSpeed = 12f;          // ความเร็วที่ซูมระดับกลาง (สเกลตามซูมอัตโนมัติ)
        public float panSmoothTime = 0.18f;   // เวลาหน่วงเร่ง/เบรก (วินาที) — ยิ่งมากยิ่งลื่นไหล
        public bool zoomScalesPanSpeed = true;

        [Header("Drag Pan (คลิกกลางลาก)")]
        public bool enableDragPan = true;

        [Header("Zoom")]
        public float zoomStepPerNotch = 1.6f; // ขนาดซูมต่อ 1 คลิกล้อเมาส์
        public float zoomSmoothTime = 0.12f;  // ease ของซูม
        public float minZoom = 3f;
        public float maxZoom = 15f;           // เพดานซูมออกสูงสุด (ใช้เมื่อ limitZoomToMap = false)
        public bool zoomToCursor = true;      // ซูมเข้าหาจุดใต้เมาส์ (มาตรฐาน city builder)

        [Tooltip("จำกัดซูมออกไม่ให้เกินขอบแมพ — คำนวณเพดานจากขนาดกริด×อัตราส่วนจอ (ซูมออกสุด = พอดีขอบแมพ)")]
        public bool limitZoomToMap = true;
        [Tooltip("เผื่อพื้นที่เลยขอบแมพตอนซูมออกสุด (world units) — มากขึ้น = เห็นขอบโล่งรอบแมพมากขึ้น")]
        public float mapFitPadding = 1f;

        [Header("Map Bounds")]
        public bool clampToGrid = true;
        public float boundsPadding = 2f;      // ยอมให้เลยขอบแมพได้กี่หน่วย world

        [Header("Start Focus")]
        [Tooltip("เริ่มเกมให้กล้องอยู่กลาง 'เมือง' (โซน A ที่สร้างได้ cols 0..zoneA-1) ไม่ใช่กลางกริดเต็ม — โซน B ดิน/รังสีขวาสุดล็อกไว้")]
        public bool centerOnCoreTowerAtStart = true;

        private Camera cam;
        private Vector2 _panVelocity;   // ความเร็วปัจจุบัน (มีแรงเฉื่อย)
        private Vector2 _panDampVel;    // state ภายในของ SmoothDamp
        private float _targetZoom;
        private float _zoomVel;
        private bool _dragging;
        private Vector3 _dragOriginWorld;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            _targetZoom = Mathf.Clamp(cam.orthographicSize, minZoom, EffectiveMaxZoom());
        }

        private void Start()
        {
            if (centerOnCoreTowerAtStart) CenterOnCityCenter();
        }

        // เล็งกล้องที่ "ศูนย์กลางเมือง" = กลางโซน A (คอลัมน์ 0..zoneA-1 ที่สร้างได้) ไม่ใช่กลางกริดเต็ม 43 คอลัมน์
        // โซน B (ดิน/รังสี ขวาสุด cols zoneA..42) ล็อกไว้ ผู้เล่นไม่ได้ใช้ → กลางกริดเต็มจะดันวิวไปชิดประตูโซน ดูเบี้ยว
        // อ่าน zoneAColumns จาก OreDepositManager (แหล่งความจริงเดียว) — ไม่พบ → fallback กลางกริดเต็ม (พฤติกรรมเดิม)
        private void CenterOnCityCenter()
        {
            var grid = GridManager.Instance;
            if (grid == null) return;

            var ore = OreDepositManager.Instance;
            int focusCols = (ore != null && ore.zoneAColumns > 0 && ore.zoneAColumns <= grid.columns)
                ? ore.zoneAColumns : grid.columns;

            Vector3 center = grid.IsoToWorldF((focusCols - 1) * 0.5f, (grid.rows - 1) * 0.5f);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // ไม่ผูก timeScale — เลื่อนดูเมืองระหว่าง pause ได้
            HandleKeyboardPan(dt);
            HandleDragPan();
            HandleZoom(dt);
            if (clampToGrid) ClampToBounds();
        }

        // ── คีย์บอร์ด: SmoothDamp ความเร็วเข้าหาเป้า → มีช่วงเร่งตอนเริ่มกด และไหลต่อนิดๆ ตอนปล่อย ──
        private void HandleKeyboardPan(float dt)
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize(); // เดินทแยงไม่เร็วกว่าแนวตรง

            // ซูมไกล (orthographicSize ใหญ่) → เลื่อนเร็วขึ้นตามสัดส่วน เพื่อความรู้สึกคงที่บนจอ
            float speed = panSpeed;
            if (zoomScalesPanSpeed)
                speed *= cam.orthographicSize / ((minZoom + maxZoom) * 0.5f);

            Vector2 targetVel = input * speed;
            _panVelocity = Vector2.SmoothDamp(_panVelocity, targetVel, ref _panDampVel,
                panSmoothTime, Mathf.Infinity, dt);

            if (_panVelocity.sqrMagnitude > 1e-6f)
                transform.position += (Vector3)(_panVelocity * dt);
        }

        // ── คลิกกลางลาก: ตรึงจุด world ใต้เมาส์ตอนเริ่มลากไว้ใต้เมาส์ตลอด (แมพติดมือ) ──
        private void HandleDragPan()
        {
            if (!enableDragPan) return;

            if (Input.GetMouseButtonDown(2))
            {
                _dragging = true;
                _dragOriginWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(2))
                _dragging = false;

            if (!_dragging) return;

            Vector3 diff = _dragOriginWorld - cam.ScreenToWorldPoint(Input.mousePosition);
            diff.z = 0f;
            transform.position += diff;

            // ระหว่างลาก ตัดแรงเฉื่อยคีย์บอร์ดทิ้ง — ไม่ให้สองระบบแย่งกล้องกัน
            _panVelocity = Vector2.zero;
            _panDampVel = Vector2.zero;
        }

        // ── ซูม: scroll ตั้งเป้า → SmoothDamp ตาม + ชดเชยตำแหน่งให้จุดใต้เมาส์อยู่กับที่ ──
        private void HandleZoom(float dt)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel"); // ±0.1 ต่อ notch
            float maxZ = EffectiveMaxZoom();
            if (!Mathf.Approximately(scroll, 0f))
                _targetZoom = Mathf.Clamp(_targetZoom - scroll * 10f * zoomStepPerNotch, minZoom, maxZ);
            // จอเปลี่ยนอัตราส่วน/เพดานหด → ดึง target ที่ค้างเกินเพดานกลับเข้ากรอบ
            else if (_targetZoom > maxZ)
                _targetZoom = maxZ;

            if (Mathf.Approximately(cam.orthographicSize, _targetZoom)) return;

            Vector3 mouseWorldBefore = zoomToCursor ? cam.ScreenToWorldPoint(Input.mousePosition) : default;

            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, _targetZoom, ref _zoomVel,
                zoomSmoothTime, Mathf.Infinity, dt);

            if (zoomToCursor)
            {
                // ขยับกล้องให้จุด world ใต้เมาส์เดิมกลับมาอยู่ใต้เมาส์ — ได้ฟีล "ซูมเข้าหาสิ่งที่ชี้"
                Vector3 mouseWorldAfter = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 offset = mouseWorldBefore - mouseWorldAfter;
                offset.z = 0f;
                transform.position += offset;
            }
        }

        // เพดานซูมออกที่ใช้จริง: จำกัดให้วิวไม่ใหญ่เกินขอบแมพ (เล็กสุดของ fit แนวตั้ง/แนวนอน)
        // orthographicSize = ครึ่งความสูงวิว · ความกว้างวิว = 2·size·aspect
        //   • fit สูง:  size ≤ mapH/2
        //   • fit กว้าง: size ≤ mapW/(2·aspect)
        // เลือกค่าเล็กสุดเพื่อไม่ให้เห็นเลยขอบด้านใดด้านหนึ่ง แล้ว clamp ไม่ให้ต่ำกว่า minZoom
        private float EffectiveMaxZoom()
        {
            if (!limitZoomToMap) return maxZoom;
            var grid = GridManager.Instance;
            if (grid == null || cam == null) return maxZoom;

            Vector3 c00 = grid.IsoToWorld(0, 0);
            Vector3 c10 = grid.IsoToWorld(grid.columns - 1, 0);
            Vector3 c01 = grid.IsoToWorld(0, grid.rows - 1);
            Vector3 c11 = grid.IsoToWorld(grid.columns - 1, grid.rows - 1);

            float mapW = Mathf.Max(c00.x, c10.x, c01.x, c11.x) - Mathf.Min(c00.x, c10.x, c01.x, c11.x);
            float mapH = Mathf.Max(c00.y, c10.y, c01.y, c11.y) - Mathf.Min(c00.y, c10.y, c01.y, c11.y);

            float aspect = cam.aspect > 0.01f ? cam.aspect : 1.7778f;
            float fit = Mathf.Min(mapH * 0.5f, mapW / (2f * aspect)) + mapFitPadding;
            return Mathf.Max(minZoom, fit);
        }

        // ── กันกล้องหลุดขอบแมพ — ใช้มุมทั้ง 4 ของกริด isometric (รูปขนมเปียกปูน) เป็นกรอบ ──
        private void ClampToBounds()
        {
            var grid = GridManager.Instance;
            if (grid == null) return;

            Vector3 c00 = grid.IsoToWorld(0, 0);
            Vector3 c10 = grid.IsoToWorld(grid.columns - 1, 0);
            Vector3 c01 = grid.IsoToWorld(0, grid.rows - 1);
            Vector3 c11 = grid.IsoToWorld(grid.columns - 1, grid.rows - 1);

            var p = transform.position;
            p.x = Mathf.Clamp(p.x,
                Mathf.Min(c00.x, c10.x, c01.x, c11.x) - boundsPadding,
                Mathf.Max(c00.x, c10.x, c01.x, c11.x) + boundsPadding);
            p.y = Mathf.Clamp(p.y,
                Mathf.Min(c00.y, c10.y, c01.y, c11.y) - boundsPadding,
                Mathf.Max(c00.y, c10.y, c01.y, c11.y) + boundsPadding);
            transform.position = p;
        }
    }
}
