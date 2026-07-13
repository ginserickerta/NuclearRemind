using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// กล้อง Orthographic สไตล์ city builder:
    /// - WASD/ลูกศร เลื่อนแบบมีแรงเฉื่อย (SmoothDamp เร่ง/หน่วงนุ่ม ไม่กระตุก)
    /// - คลิกขวาค้างแล้วลาก = จับแมพเลื่อน (จุดใต้เมาส์ตรึงอยู่กับที่)
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

        [Header("Drag Pan (คลิกขวาลาก)")]
        public bool enableDragPan = true;

        [Header("Zoom")]
        public float zoomStepPerNotch = 1.6f; // ขนาดซูมต่อ 1 คลิกล้อเมาส์
        public float zoomSmoothTime = 0.12f;  // ease ของซูม
        public float minZoom = 3f;
        public float maxZoom = 9f;            // เพดานซูมออกสูงสุด (แข็ง — ต่อให้ limitZoomToMap ก็ไม่เกินค่านี้ กันซูมออกจนอาคารเล็กเกิน)
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

        [Header("Touch (iPad/มือถือ · ไฮบริดควบคู่เมาส์)")]
        public bool enableTouch = true;
        public float pinchZoomSpeed = 0.02f;  // ไวพินช์ซูม (ปรับบนอุปกรณ์จริงได้)

        private Camera cam;
        private Vector2 _panVelocity;   // ความเร็วปัจจุบัน (มีแรงเฉื่อย)
        private Vector2 _panDampVel;    // state ภายในของ SmoothDamp
        private float _targetZoom;
        private float _zoomVel;
        private bool _dragging;
        private Vector3 _dragOriginWorld;
        private bool _touchPanning;
        private Vector3 _touchPanOriginWorld;

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

        // เล็งกล้องที่ศูนย์กลาง "กริดเต็ม" 43×43 (CORE TOWER อยู่กลางแมพ — ผู้ใช้เลือกกลางแมพ ไม่ใช่กลาง Zone A)
        private void CenterOnCityCenter()
        {
            var grid = GridManager.Instance;
            if (grid == null) return;

            float centerCol = (grid.columns - 1) * 0.5f;
            float centerRow = (grid.rows - 1) * 0.5f;

            Vector3 center = grid.IsoToWorldF(centerCol, centerRow);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // ไม่ผูก timeScale — เลื่อนดูเมืองระหว่าง pause ได้
            HandleKeyboardPan(dt);
            HandleDragPan();
            HandleZoom(dt);
            if (enableTouch) HandleTouchPanZoom();
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

        // ── คลิกขวาลาก: ตรึงจุด world ใต้เมาส์ตอนเริ่มลากไว้ใต้เมาส์ตลอด (แมพติดมือ) ──
        // ใช้ปุ่มขวา (1) แทนปุ่มกลาง (2) — คลิกขวาสั้น ๆ ยังยกเลิกวาง/ทุบได้ตามเดิม (คนละ controller) ·
        // จะเริ่มเลื่อนจริงเมื่อ "ลาก" เท่านั้น คลิกเฉย ๆ ไม่ขยับกล้อง
        private void HandleDragPan()
        {
            if (!enableDragPan) return;

            if (Input.GetMouseButtonDown(1))
            {
                _dragging = true;
                _dragOriginWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(1))
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

            bool zoomCursor = zoomToCursor && Input.touchCount < 2; // 2 นิ้ว = พินช์ (ยึดจุดกลางนิ้วแทนเมาส์)
            Vector3 mouseWorldBefore = zoomCursor ? cam.ScreenToWorldPoint(Input.mousePosition) : default;

            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, _targetZoom, ref _zoomVel,
                zoomSmoothTime, Mathf.Infinity, dt);

            if (zoomCursor)
            {
                // ขยับกล้องให้จุด world ใต้เมาส์เดิมกลับมาอยู่ใต้เมาส์ — ได้ฟีล "ซูมเข้าหาสิ่งที่ชี้"
                Vector3 mouseWorldAfter = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 offset = mouseWorldBefore - mouseWorldAfter;
                offset.z = 0f;
                transform.position += offset;
            }
        }

        // ── Touch (iPad/มือถือ): 2 นิ้ว = แพน (ตรึงจุดกลางนิ้ว) + พินช์ซูม · 1 นิ้ว = แตะ/วาง (ผ่าน mouse sim เดิม) ──
        // เสริมควบคู่ของเดิม (ไฮบริด) — คีย์บอร์ด/เมาส์ PC ยังทำงานปกติ · reuse ClampToBounds/EffectiveMaxZoom เดิม
        // ใช้ 2 นิ้วสำหรับกล้อง เพื่อไม่ชนกับ 1 นิ้ว=แตะวาง/เลือก (ผ่านการจำลองเมาส์ของ Unity)
        private void HandleTouchPanZoom()
        {
            if (Input.touchCount < 2) { _touchPanning = false; return; }

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            Vector2 p0 = t0.position, p1 = t1.position;

            // พินช์ซูม: ระยะระหว่างนิ้วเปลี่ยน → ตั้งเป้าซูม (HandleZoom ทำ SmoothDamp ต่อเฟรมถัดไป)
            Vector2 p0Prev = p0 - t0.deltaPosition;
            Vector2 p1Prev = p1 - t1.deltaPosition;
            float distDelta = (p0 - p1).magnitude - (p0Prev - p1Prev).magnitude;
            if (Mathf.Abs(distDelta) > 0.01f)
                _targetZoom = Mathf.Clamp(_targetZoom - distDelta * pinchZoomSpeed, minZoom, EffectiveMaxZoom());

            // แพน 2 นิ้ว: ตรึงจุด world ที่กึ่งกลางสองนิ้วไว้ (แมพติดมือ — สูตรเดียวกับ drag-pan คลิกขวา)
            Vector2 midScreen = (p0 + p1) * 0.5f;
            Vector3 midWorld = cam.ScreenToWorldPoint(new Vector3(midScreen.x, midScreen.y, 0f));
            if (!_touchPanning)
            {
                _touchPanning = true;
                _touchPanOriginWorld = midWorld;
            }
            else
            {
                Vector3 diff = _touchPanOriginWorld - midWorld;
                diff.z = 0f;
                transform.position += diff;
                _panVelocity = Vector2.zero;   // ตัดแรงเฉื่อยคีย์บอร์ด ไม่ให้สองระบบแย่งกล้อง
                _panDampVel = Vector2.zero;
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
            // maxZoom เป็นเพดานแข็ง: ซูมออกได้ไม่เกิน min(พอดีขอบแมพ, maxZoom) — กันซูมออกไกลจนอาคารเล็กเกินไป
            return Mathf.Clamp(fit, minZoom, maxZoom);
        }

        // ── กันกล้องหลุดขอบแมพ — clamp "ขอบวิว" (ไม่ใช่แค่ศูนย์กล้อง) ให้อยู่ในกรอบแมพ ──
        // เดิม clamp เฉพาะจุดศูนย์ → ซูม/เลื่อนออกได้จนแมพไปกองมุมจอ เห็นดำเยอะ (บั๊กที่ผู้ใช้เจอ)
        // ตอนนี้เผื่อครึ่งขนาดวิว (orthographicSize × aspect) → ขอบวิวไม่เลยขอบแมพเกิน boundsPadding
        private void ClampToBounds()
        {
            var grid = GridManager.Instance;
            if (grid == null || cam == null) return;

            Vector3 c00 = grid.IsoToWorld(0, 0);
            Vector3 c10 = grid.IsoToWorld(grid.columns - 1, 0);
            Vector3 c01 = grid.IsoToWorld(0, grid.rows - 1);
            Vector3 c11 = grid.IsoToWorld(grid.columns - 1, grid.rows - 1);

            float minX = Mathf.Min(c00.x, c10.x, c01.x, c11.x);
            float maxX = Mathf.Max(c00.x, c10.x, c01.x, c11.x);
            float minY = Mathf.Min(c00.y, c10.y, c01.y, c11.y);
            float maxY = Mathf.Max(c00.y, c10.y, c01.y, c11.y);

            float halfH = cam.orthographicSize;               // ครึ่งความสูงวิว
            float halfW = halfH * (cam.aspect > 0.01f ? cam.aspect : 1.7778f); // ครึ่งความกว้างวิว

            var p = transform.position;
            p.x = ClampViewAxis(p.x, minX, maxX, halfW);
            p.y = ClampViewAxis(p.y, minY, maxY, halfH);
            transform.position = p;
        }

        // clamp ศูนย์กล้องแกนเดียวให้ขอบวิวอยู่ในแมพ · วิวใหญ่กว่าแมพ (lo>hi) → ตรึงกลางแมพ
        private float ClampViewAxis(float center, float min, float max, float halfView)
        {
            float lo = min + halfView - boundsPadding;
            float hi = max - halfView + boundsPadding;
            if (lo > hi) return (min + max) * 0.5f; // วิวใหญ่กว่าแมพ → กึ่งกลาง (ไม่ให้แมพไปมุมจอ)
            return Mathf.Clamp(center, lo, hi);
        }
    }
}
