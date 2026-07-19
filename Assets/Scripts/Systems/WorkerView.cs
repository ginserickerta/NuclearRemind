using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// sprite คนงานหนึ่งตัว (V4 §5) — เดินเข้าหาตำแหน่งเป้าหมายแบบ Lerp
    /// • ประจำอาคาร (assigned): เดินไปยืนที่อาคารแล้วอยู่นิ่ง
    /// • ว่างงาน (idle): เดินเล่นวนไปมารอบจุดพักใกล้ CORE TOWER (เหมือนคนว่างงาน)
    /// recompute sortingOrder ทุกเฟรมจากตำแหน่งปัจจุบัน (จัดลำดับในหมู่คนงานด้วยกันตอนเดิน)
    ///
    /// ★ ตัวตนคงที่ (AssignedCell + Slot): WorkerVisualSpawner เรียก SetAssigned/SetIdle เฉพาะตอนบทบาท
    ///   "เปลี่ยนจริง ๆ" เท่านั้น — คนงานที่ยังอยู่ slot เดิมจะไม่ถูกแตะเลยแม้มีเหตุการณ์ประชากร/จัดคนที่อื่นในเมืองเกิดขึ้น
    ///   (แก้บั๊กเดิม: ทุก event เคยสั่ง rebuild ตำแหน่งคนงานทั้งเมืองใหม่หมด ทำให้เดินตัดกัน/ซ้อนกันเรื่อย ๆ)
    /// ★ กันเดินทะลุกัน: LateUpdate แก้ตำแหน่งให้ไม่ซ้อนกัน (WorkerSeparation — ดูเหตุผลที่ไม่ใช้ฟิสิกส์ที่นั่น)
    ///   ทำใน LateUpdate เพราะทุกตัวเดินเสร็จใน Update แล้ว → ทุกคนเห็นตำแหน่งล่าสุดของกันและกัน
    /// ★ กันเดินทะลุอาคาร: ทุกการขยับ (ทั้งเดินเองและถูกเพื่อนดัน) ผ่าน MoveAvoidingBuildings
    ///   ซึ่งเช็ค Cell.isOccupied แล้วไถลตามกำแพง (WorkerPathing — ไม่ใช้ Collider2D, ดูเหตุผลที่นั่น)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorkerView : MonoBehaviour
    {
        [Tooltip("ความเร็วเดิน (world units/วินาที)")]
        public float speed = 1.0f;

        [Header("Idle wander (คนว่างงานเดินเล่น)")]
        [Tooltip("รัศมีเดินเล่นรอบจุดพัก (world units) — ใช้เฉพาะโหมด SetIdle แบบเก่า")]
        public float wanderRadius = 0.35f;
        [Tooltip("ช่วงเวลาหยุดพักก่อนเดินไปจุดใหม่ (วินาที)")]
        public Vector2 idlePauseRange = new Vector2(0.6f, 2.4f);
        [Tooltip("เดินเล่นนานเกินนี้ยังไม่ถึง (โดนเบียดขวาง) → เลิกดัน เลือกจุดใหม่ · " +
                 "ต้องมากกว่าเวลาเดินข้ามวงลาดตระเวน (~10 unit ที่ speed 1) ไม่งั้นจะเลิกกลางทางทุกรอบ")]
        public float wanderTimeout = 12f;

        [Header("กันเดินทะลุกัน (crowd separation)")]
        [Tooltip("ระยะห่างศูนย์กลางต่ำสุดระหว่างคนงาน (world x-units) — 0 = ปิดระบบ · " +
                 "0.55 กว้างกว่า DefaultRadius เพื่อให้ตัวละครไม่ดูซ้อนกัน")]
        public float personalRadius = 0.55f;
        [Tooltip("ระยะผลักสูงสุดต่อวินาที — กันตัวละครกระเด็นตอนคนแออัด")]
        public float maxPushSpeed = 2f;
        [Tooltip("ถ้าถูกเบียดจนห่างจุดประจำเกินนี้ ให้เดินกลับ (world units)")]
        public float returnSlack = 0.5f;

        /// <summary>cell ของอาคารที่ประจำ (null = ว่าง/idle เดินเล่น)</summary>
        public Vector2Int? AssignedCell { get; private set; }

        /// <summary>
        /// ตัวตนที่คงที่ของ "บทบาท" ปัจจุบัน — slot ที่ N ของอาคาร (เมื่อ AssignedCell มีค่า)
        /// หรือ slot พักที่ N ของแถวคนว่าง (เมื่อ AssignedCell เป็น null) · -1 = ยังไม่มีบทบาท (เพิ่งสร้าง)
        /// WorkerVisualSpawner ใช้ค่านี้กันไม่ให้ "สลับตัว" คนงานทุกครั้งที่มีเหตุการณ์ไม่เกี่ยวข้องเกิดขึ้นที่อื่นในเมือง
        /// </summary>
        public int Slot { get; private set; } = -1;

        // ระยะที่ถือว่า "ถึงแล้ว" — ต้องใหญ่กว่า MoveTowards step ปกติเล็กน้อย
        private const float ArriveEpsilonSqr = 1e-4f;

        // ทะเบียนคนงานที่ยังมีชีวิต — separation ต้องรู้จักเพื่อนบ้าน แต่ห้าม Find ทุกเฟรม (กฎข้อ 6)
        private static readonly List<WorkerView> _active = new List<WorkerView>();
        private static readonly List<Vector3> _neighbors = new List<Vector3>();

        private Vector3 _target;
        private Vector3 _idleAnchor;
        // วงแหวนลาดตระเวน: สุ่มจุดหมายในช่วง [inner, outer] รอบ _idleAnchor
        // inner = 0 → เป็นวงกลมเต็ม (พฤติกรรม SetIdle แบบเดิม) · outer = 0 → ใช้ wanderRadius
        private float _patrolInner;
        private float _patrolOuter;
        private bool _wandering;
        private bool _arrived;      // ถึงเป้าแล้ว → หยุดเดิน ปล่อยให้ separation ดันได้โดยไม่ดึงกลับ (กันสั่น)
        private float _pauseTimer;
        private float _seekTimer;
        private SpriteRenderer _sr;
        private SpriteRenderer _shadow; // เงาใต้เท้า (child) — ตามลำดับความลึกใต้ตัวเสมอ

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        /// <summary>ผูกเงาใต้เท้า (สร้างโดย WorkerVisualSpawner) — จะถูกจัด sortingOrder ให้อยู่ใต้ตัวทุกครั้งที่คนงานขยับ</summary>
        public void SetShadow(SpriteRenderer shadow) => _shadow = shadow;

        // Decorations that must ride the body's depth (health badge, name tag). A fixed high order made
        // them float over buildings the worker was standing behind — a face and a name showing through a
        // wall the body was hidden by. Registered once at spawn; no per-frame lookups.
        private readonly List<(Renderer r, int offset)> _sortFollowers = new List<(Renderer, int)>();

        /// <summary>Keep a child renderer at (worker order + offset) every time the worker is re-sorted.</summary>
        public void AddSortFollower(Renderer r, int offset)
        {
            if (r != null) _sortFollowers.Add((r, offset));
        }

        private void OnEnable() => _active.Add(this);
        private void OnDisable() => _active.Remove(this);

        /// <summary>
        /// ประจำอาคาร: เดินไปยืนที่ target แล้วหยุด (snap=true สำหรับ spawn ครั้งแรก/โหลดเซฟ)
        /// slot = ตำแหน่งที่ N ของอาคารนี้ (คงที่ตราบใดที่ยังประจำอยู่ — WorkerVisualSpawner เป็นผู้คุม)
        /// เรียกเฉพาะตอน "ได้ slot ใหม่จริง ๆ" เท่านั้น (ไม่เรียกซ้ำถ้ายังอยู่ slot เดิม) จึงไม่มี guard ในนี้
        /// </summary>
        public void SetAssigned(Vector3 target, Vector2Int cell, int slot, bool snap)
        {
            AssignedCell = cell;
            Slot = slot;
            _wandering = false;
            _target = target;
            _seekTimer = 0f;
            _arrived = false;
            if (snap) transform.position = target;
            UpdateSorting();
        }

        /// <summary>
        /// ว่างงาน: เดินเล่นวนรอบ anchor (จุดพักใกล้ CORE TOWER)
        /// slot = ตำแหน่งพักที่ N (คงที่ตราบใดที่ยังว่างงาน) — slot เดิมเหมือนเดิม → ไม่รีเซ็ต (เดินเล่นต่อ ไม่กระตุกกลับ)
        /// </summary>
        public void SetIdle(Vector3 anchor, int slot, bool snap)
        {
            bool alreadyHere = !AssignedCell.HasValue && Slot == slot && _wandering;
            AssignedCell = null;
            Slot = slot;
            _idleAnchor = anchor;
            _patrolInner = 0f;
            _patrolOuter = 0f;   // 0 = ใช้ wanderRadius (พฤติกรรมเดิม)
            _wandering = true;
            if (alreadyHere) return; // slot พักเดิม — เดินเล่นต่อ ไม่กระตุกกลับ anchor ทุกครั้งที่ rebuild

            _target = anchor;
            _pauseTimer = Random.Range(0f, idlePauseRange.y); // เหลื่อมเวลากันไม่ให้ทุกคนขยับพร้อมกัน
            _seekTimer = 0f;
            _arrived = false;
            if (snap) transform.position = anchor;
            UpdateSorting();
        }

        /// <summary>
        /// ลาดตระเวน: เดินวนไปมาใน "วงแหวน" รอบแลนด์มาร์ก (CORE TOWER / Research Lab) ตลอดเวลา
        /// ต่างจาก SetIdle ตรงที่ไม่มี anchor ประจำตัว — ทั้งวงคือพื้นที่เดินของทุกคน จึงกระจายกันเองตามธรรมชาติ
        /// แทนที่จะยืนเป็นตารางกระจุกกันที่จุดเดียว (บั๊กเดิม: IdlePos วางเป็นกริด 6 คอลัมน์ ห่างกัน 0.6)
        ///
        /// inner > 0 กันไม่ให้เดินเข้าไปทับตัวอาคาร · slot ใช้กระจายจุดเริ่มต้นด้วยมุมทอง (ไม่ให้เกิดมาซ้อนกัน)
        /// </summary>
        public void SetPatrol(Vector3 center, float inner, float outer, int slot, bool snap)
        {
            // ศูนย์กลางเดิม + slot เดิม → กำลังเดินวนอยู่แล้ว ปล่อยเดินต่อ (ไม่กระตุกกลับทุกครั้งที่ Sync)
            bool sameLoop = !AssignedCell.HasValue && _wandering && Slot == slot
                            && (_idleAnchor - center).sqrMagnitude < 1e-4f;

            AssignedCell = null;
            Slot = slot;
            _idleAnchor = center;
            _patrolInner = Mathf.Max(0f, inner);
            _patrolOuter = Mathf.Max(_patrolInner + 0.01f, outer);
            _wandering = true;
            if (sameLoop) return;

            // จุดเริ่มต้นกระจายด้วยมุมทอง + รัศมีสลับ — ทุกคนเริ่มคนละมุมของวง ไม่ต้องรอ separation ดันออก
            float ang = slot * GoldenAngle;
            float rad = Mathf.Lerp(_patrolInner, _patrolOuter, (slot * 0.618034f) % 1f);
            _target = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f, 0f) * rad;
            _pauseTimer = Random.Range(0f, idlePauseRange.y); // เหลื่อมเวลากันไม่ให้ขยับพร้อมกันทั้งฝูง
            _seekTimer = 0f;
            _arrived = false;
            if (snap) transform.position = _target;
            UpdateSorting();
        }

        // มุมทอง — กระจายจุดเริ่มต้นให้ไม่ซ้ำมุมกัน (เหตุผลเดียวกับใน WorkerSeparation)
        private const float GoldenAngle = 2.39996323f;

        private void Update()
        {
            // ถูกเบียดจนหลุดจากจุดประจำไกลเกินไป → เดินกลับ (ไม่งั้นคนงานจะค่อย ๆ ลอยหนีอาคาร)
            if (_arrived && !_wandering && (transform.position - _target).sqrMagnitude > returnSlack * returnSlack)
                _arrived = false;

            if (!_arrived)
            {
                // ถ้าปลายทางอยู่คนละฝั่งรั้วโซน B → เล็ง waypoint ที่ประตูก่อน (การไถลตามรั้วจะพาเข้าประตูเอง)
                Vector3 immediate = GateRoutedTarget(_target);
                var desired = Vector3.MoveTowards(transform.position, immediate, speed * Time.deltaTime);
                transform.position = MoveAvoidingBuildings(transform.position, desired);
                _seekTimer += Time.deltaTime;

                if ((transform.position - _target).sqrMagnitude <= ArriveEpsilonSqr) _arrived = true;
                // เดินเล่นแล้วไปไม่ถึงสักที = มีคนยืนขวางจุดหมาย → ยอมแพ้ แล้วรอสุ่มจุดใหม่ (กันดันกันค้าง)
                else if (_wandering && _seekTimer >= wanderTimeout) _arrived = true;

                UpdateSorting();
                return;
            }

            // ถึงจุดแล้ว + ว่างงาน → พักสุ่มเวลา แล้วเดินไปจุดสุ่มใหม่รอบ anchor
            if (_wandering)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                {
                    _target = PickWanderTarget();
                    _pauseTimer = Random.Range(idlePauseRange.x, idlePauseRange.y);
                    _seekTimer = 0f;
                    _arrived = false;
                }
            }
        }

        // สุ่มจุดเดินเล่นที่ไม่ตกใส่อาคาร — ไม่งั้นคนงานจะเดินไปชนตึกแล้วยืนรอ wanderTimeout ทุกรอบ
        private Vector3 PickWanderTarget()
        {
            const int attempts = 8;
            var grid = GridManager.Instance;
            float outer = _patrolOuter > 0f ? _patrolOuter : wanderRadius;

            for (int i = 0; i < attempts; i++)
            {
                // สุ่มในวงแหวน [inner, outer] — ใช้ sqrt กระจายพื้นที่เท่ากันทุกรัศมี
                // (ถ้าสุ่มรัศมีตรง ๆ ความหนาแน่นจะกองที่ขอบใน = กระจุกกลางวงอีก)
                float ang = Random.value * Mathf.PI * 2f;
                float t = Mathf.Sqrt(Random.value);
                float rad = Mathf.Lerp(_patrolInner, outer, t);
                // ย่อแกน y ครึ่งหนึ่ง — วงกลมบนพื้น iso ฉายลงจอเป็นวงรี 2:1 (เหตุผลเดียวกับ WorkerSeparation)
                var candidate = _idleAnchor + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f, 0f) * rad;
                if (grid == null || !IsBlocked(WorkerPathing.CellOf(grid.WorldToIsoF(candidate))))
                    return candidate;
            }
            // สุ่มไม่ผ่านเลย → ยืนอยู่ที่เดิม รอสุ่มใหม่รอบหน้า
            // (เดิมคืน _idleAnchor ทำให้จุดกลางกลายเป็นแม่เหล็กดูดทุกคนมากองรวมกัน)
            return transform.position;
        }

        /// <summary>ช่องนี้เดินผ่านไม่ได้? (มีอาคาร/แหล่งแร่ตั้งอยู่ หรืออยู่นอกกริด)</summary>
        private static bool IsBlocked(Vector2Int cell)
        {
            var grid = GridManager.Instance;
            if (grid == null) return false; // ไม่มีกริด (เทสต์/ซีนเปล่า) → เดินได้อิสระ
            var c = grid.GetCell(cell.x, cell.y);
            return c == null || c.isOccupied; // null = นอกกริด → กันคนงานเดินตกขอบแมพ
        }

        // ปลายทางอยู่คนละฝั่งรั้วโซน B → คืน waypoint ที่ประตู (ไม่งั้นคืนปลายทางจริง)
        private Vector3 GateRoutedTarget(Vector3 realTarget)
        {
            var grid = GridManager.Instance;
            if (grid == null) return realTarget;
            Vector2 toIso = grid.WorldToIsoF(realTarget);
            Vector2 routed = ZoneBarrierRenderer.RouteThroughGate(grid.WorldToIsoF(transform.position), toIso);
            if (routed == toIso) return realTarget; // ไม่ต้องอ้อม
            return grid.IsoToWorldF(routed.x, routed.y);
        }

        // ขยับจาก pos ไป desired โดยไถลอ้อมอาคารแทนการทะลุ (ใช้ทั้งตอนเดินเองและตอนถูกเพื่อนดัน)
        private static Vector3 MoveAvoidingBuildings(Vector3 pos, Vector3 desired)
        {
            var grid = GridManager.Instance;
            if (grid == null) return desired;

            // IsBlocked = อาคาร/ขอบกริด · ZoneBarrierRenderer.CrossBlocked = รั้วโซน B (ข้ามได้เฉพาะประตู/เมื่อปลดล็อก)
            Vector2 stepped = WorkerPathing.Step(grid.WorldToIsoF(pos), grid.WorldToIsoF(desired),
                IsBlocked, ZoneBarrierRenderer.CrossBlocked);
            return grid.IsoToWorldF(stepped.x, stepped.y);
        }

        // ดันตัวออกจากคนที่ซ้อนกัน — หลังทุกตัวเดินเสร็จแล้ว (Update ครบทุก instance)
        private void LateUpdate()
        {
            if (personalRadius <= 0f || _active.Count < 2) return;

            float yStretch = WorkerSeparation.DefaultYStretch;
            var grid = GridManager.Instance;
            if (grid != null && grid.tileHeight > 0f) yStretch = grid.tileWidth / grid.tileHeight;

            var pos = transform.position;

            // broadphase ถูก ๆ: คัดด้วยกล่องสี่เหลี่ยมก่อนค่อยวัดระยะจริง
            // O(N²) แต่ N = ประชากรทั้งเมือง (หลักสิบ) — ไม่คุ้มที่จะทำ spatial hash
            _neighbors.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                var other = _active[i];
                if (other == this || other == null) continue;
                var p = other.transform.position;
                if (Mathf.Abs(p.x - pos.x) >= personalRadius) continue;
                if (Mathf.Abs(p.y - pos.y) * yStretch >= personalRadius) continue;
                _neighbors.Add(p);
            }
            if (_neighbors.Count == 0) return;

            var correction = WorkerSeparation.Correction(
                pos, _neighbors, personalRadius, yStretch, maxPushSpeed * Time.deltaTime);
            if (correction == Vector3.zero) return;

            // ดันได้ แต่ห้ามดันทะลุอาคาร (คนแออัดหน้าตึกต้องเบียดกันเอง ไม่ใช่ทะลุเข้าไปข้างใน)
            transform.position = MoveAvoidingBuildings(pos, pos + correction);
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null || GridManager.Instance == null) return;
            // ★ ต้องเป็น WorldToIsoF (ทศนิยม) ไม่ใช่ WorldToIso ที่ปัดเป็นช่องเต็ม — ไม่งั้น sortingOrder
            //   กระโดดทีละ SortScale (16) ตอนข้ามเส้นแบ่งช่อง คนงานเลยเด้งจากหลังอาคารมาหน้าอาคารทันที
            //   ณ จุดที่ไม่ตรงกับที่ตาเห็น และ bias ต่างประเภท (+8 ของอาคาร) ก็ใช้ไม่ได้เพราะ "ครึ่งช่อง"
            //   ไม่มีอยู่จริงเมื่อปัดเศษทิ้ง — WorldToIsoF ถูกเขียนมาเพื่อกรณีนี้โดยเฉพาะ
            var iso = GridManager.Instance.WorldToIsoF(transform.position);
            // Player(unit) = ชั้นล่างสุดตอนซ้อน depth เดียวกัน (อาคาร/แร่/tower ที่ depth เท่ากันวาดทับ)
            int orderVal = GridManager.SortOrder(iso.x, iso.y, GridManager.SortTier.Unit);
            // ยืนหน้าอาคาร → ยกเหนืออาคารนั้น · ยืนหลังอาคาร → กดลงไปใต้มัน (ดูเหตุผลที่ ResolveWorkerOrder)
            orderVal = BuildingDepthSort.ResolveWorkerOrder(_sr.bounds, transform.position, orderVal);
            _sr.sortingOrder = orderVal;
            if (_shadow != null) _shadow.sortingOrder = orderVal - 1; // เงาอยู่ใต้ตัวคนงานเสมอ
            for (int i = 0; i < _sortFollowers.Count; i++)
            {
                var f = _sortFollowers[i];
                if (f.r != null) f.r.sortingOrder = orderVal + f.offset;
            }
        }
    }
}
