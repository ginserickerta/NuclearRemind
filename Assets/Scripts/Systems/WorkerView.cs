using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// sprite คนงานหนึ่งตัว (V4 §5) — เดินเข้าหาตำแหน่งเป้าหมายแบบ Lerp
    /// • ประจำอาคาร (assigned): เดินไปยืนที่อาคารแล้วอยู่นิ่ง
    /// • ว่างงาน (idle): เดินเล่นวนไปมารอบจุดพักใกล้ CORE TOWER (เหมือนคนว่างงาน)
    /// recompute sortingOrder ทุกเฟรมจากตำแหน่งปัจจุบัน (จัดลำดับในหมู่คนงานด้วยกันตอนเดิน)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorkerView : MonoBehaviour
    {
        [Tooltip("ความเร็วเดิน (world units/วินาที)")]
        public float speed = 1.0f;

        [Header("Idle wander (คนว่างงานเดินเล่น)")]
        [Tooltip("รัศมีเดินเล่นรอบจุดพัก (world units)")]
        public float wanderRadius = 0.9f;
        [Tooltip("ช่วงเวลาหยุดพักก่อนเดินไปจุดใหม่ (วินาที)")]
        public Vector2 idlePauseRange = new Vector2(0.6f, 2.4f);

        /// <summary>cell ของอาคารที่ประจำ (null = ว่าง/idle เดินเล่น)</summary>
        public Vector2Int? AssignedCell { get; private set; }

        private Vector3 _target;
        private Vector3 _idleAnchor;
        private bool _wandering;
        private float _pauseTimer;
        private SpriteRenderer _sr;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        /// <summary>ประจำอาคาร: เดินไปยืนที่ target แล้วหยุด (snap=true สำหรับ spawn ครั้งแรก/โหลดเซฟ)</summary>
        public void SetAssigned(Vector3 target, Vector2Int cell, bool snap)
        {
            AssignedCell = cell;
            _wandering = false;
            _target = target;
            if (snap) transform.position = target;
            UpdateSorting();
        }

        /// <summary>ว่างงาน: เดินเล่นวนรอบ anchor (จุดพักใกล้ CORE TOWER) — ไม่รีเซ็ตถ้ากำลังเดินเล่นจุดเดิมอยู่แล้ว</summary>
        public void SetIdle(Vector3 anchor, bool snap)
        {
            bool alreadyWanderingHere = _wandering && (_idleAnchor - anchor).sqrMagnitude < 0.01f;
            AssignedCell = null;
            _idleAnchor = anchor;
            _wandering = true;
            if (alreadyWanderingHere) return; // เดินเล่นต่อ ไม่กระตุกกลับ anchor ทุกครั้งที่ rebuild

            _target = anchor;
            _pauseTimer = Random.Range(0f, idlePauseRange.y); // เหลื่อมเวลากันไม่ให้ทุกคนขยับพร้อมกัน
            if (snap) transform.position = anchor;
            UpdateSorting();
        }

        private void Update()
        {
            if ((transform.position - _target).sqrMagnitude > 1e-4f)
            {
                transform.position = Vector3.MoveTowards(transform.position, _target, speed * Time.deltaTime);
                UpdateSorting();
                return;
            }

            // ถึงจุดแล้ว + ว่างงาน → พักสุ่มเวลา แล้วเดินไปจุดสุ่มใหม่รอบ anchor
            if (_wandering)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f)
                {
                    Vector2 off = Random.insideUnitCircle * wanderRadius;
                    _target = _idleAnchor + new Vector3(off.x, off.y, 0f);
                    _pauseTimer = Random.Range(idlePauseRange.x, idlePauseRange.y);
                }
            }
        }

        private void UpdateSorting()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null || GridManager.Instance == null) return;
            var iso = GridManager.Instance.WorldToIso(transform.position);
            _sr.sortingOrder = GridManager.SortOrder(iso.x, iso.y);
        }
    }
}
