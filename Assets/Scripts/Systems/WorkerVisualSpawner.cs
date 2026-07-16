using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// สร้าง/ลบ sprite คนงานบนแมพให้จำนวน = ประชากรทุกคลาส (Worker/Engineer/Medic/Farmer) (V4 §5)
    /// และตั้งเป้าหมายให้แต่ละตัวไปยืนที่อาคารที่ถูก assign (WorkerAssignmentManager) หรือจุดพัก idle
    /// แต่ละคลาสใช้ sprite ของตัวเอง (คนงาน/วิศวกร/หมอ) — คลาสที่ไม่ได้ wire sprite จะ fallback เป็น workerSprite
    /// event-driven เหมือน BuildingVisualSpawner · อ่าน Assignments/Current + PlacedBuildings แบบ read-only query
    ///
    /// ★ Reconcile แบบ diff (ไม่ remap ทั้งเมืองทุกครั้ง):
    ///   เดิม RebuildLayout เดินลิสต์คนงานทั้งคลาสใหม่หมดทุกครั้งที่มี event (จัดคน/ประชากรเปลี่ยน "ที่ไหนก็ได้ในเมือง")
    ///   แล้ว "เดา" ว่า list[idx] ตัวไหนควรไปอาคารไหนจากลำดับการวน Dictionary ล้วน ๆ — ไม่มีตัวตนคงที่เลย
    ///   ผลคือคนงานถูกสลับบทบาทกันเองบ่อยมาก (แม้อาคารของตัวเองไม่มีอะไรเปลี่ยน) เดินตัดกันไปมา ดูเหมือนเลเยอร์ซ้อน
    ///
    ///   ตอนนี้ WorkerView เก็บ (AssignedCell, Slot) เป็นตัวตนของตัวเอง — ReconcileCell() เทียบ "จำนวนที่ต้องการ"
    ///   กับ "คนที่อยู่ที่ cell นั้นแล้ว" เฉพาะ cell ที่เปลี่ยนจริง ส่วนเกินคืน idle (slot สูงสุดก่อน) ส่วนขาดดึงจาก
    ///   idle pool มาเติม slot ถัดไป — คนที่ slot ไม่เปลี่ยนจะไม่ถูกเรียก SetAssigned/SetIdle เลย จึงไม่ขยับ
    /// </summary>
    public class WorkerVisualSpawner : MonoBehaviour
    {
        // layer เดียวกับอาคาร → worker บัง/ถูกบังกับอาคารถูกต้องตาม iso depth (ไม่ใช่ทับตลอด)
        private const string WorkerSortingLayer = "Buildings";

        [Header("Sprite ต่อคลาส (wire โดย CharacterSpriteSetup / Phase3PopulationSetup)")]
        [Tooltip("คนงาน (Worker) — ใช้เป็น fallback ให้คลาสที่ไม่ได้ตั้ง sprite ด้วย")]
        public Sprite workerSprite;
        [Tooltip("วิศวกร (Engineer) — Lab/CORE/หล่อเย็น")]
        public Sprite engineerSprite;
        [Tooltip("หมอ (Medic) — Hospital/กันรังสี")]
        public Sprite medicSprite;
        [Tooltip("เกษตรกร (Farmer) — Farm/Agri Dome (ว่างได้ → fallback เป็น workerSprite)")]
        public Sprite farmerSprite;

        public Transform workersParent;

        [Header("จุดพักคนว่าง (idle staging) — รวมกลุ่มใกล้ CORE TOWER (กลางกริด)")]
        [Tooltip("เยื้องจากกลางกริดกี่ cell (ค่าลบ = ด้านหน้าเตา ไม่ทับ footprint)")]
        public Vector2 idleOffsetFromCenter = new Vector2(-2f, -2.5f);
        public int idlePerRow = 6;
        public float idleSpacing = 0.6f;

        // คนงานแยกตามคลาส — spawn/despawn + จัด layout ทีละคลาส (sprite ต่างกัน)
        private readonly Dictionary<WorkerClass, List<WorkerView>> _byClass =
            new Dictionary<WorkerClass, List<WorkerView>>();

        private static readonly WorkerClass[] Classes =
            { WorkerClass.Worker, WorkerClass.Engineer, WorkerClass.Medic, WorkerClass.Farmer };

        // jitter (iso-cell space) กันคนซ้อนกันเมื่ออยู่อาคารเดียวกัน
        private static readonly Vector2[] Jitter =
        {
            new Vector2(0f, 0.10f),    new Vector2(-0.26f, -0.04f), new Vector2(0.26f, -0.04f),
            new Vector2(-0.14f, -0.22f), new Vector2(0.14f, -0.22f), new Vector2(0f, -0.34f),
            new Vector2(-0.30f, 0.16f), new Vector2(0.30f, 0.16f),
        };

        private void OnEnable()
        {
            EventManager.Instance.OnWorkerAssignmentChanged += HandleAssignmentChanged;
            EventManager.Instance.OnWorkerPoolChanged += HandlePoolChanged;
            EventManager.Instance.OnPopulationChanged += HandlePopulationChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleAssignmentChanged;
            EventManager.Instance.OnWorkerPoolChanged -= HandlePoolChanged;
            EventManager.Instance.OnPopulationChanged -= HandlePopulationChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        // idle slot คงที่ต่อคนงาน — คืนเข้าคิวว่างให้ใช้ซ้ำเมื่อคนนั้นถูกดึงไปประจำอาคาร (กัน slot เลขวิ่งไม่หยุดตลอดเกม)
        private readonly SortedSet<int> _freeIdleSlots = new SortedSet<int>();
        private int _idleSlotWatermark;

        private void Start() => RebuildLayout(snap: true); // spawn ตามจำนวนประชากรเริ่มเกม

        private void HandleAssignmentChanged(Vector2Int cell, int count) => RebuildLayout(snap: false);
        private void HandlePoolChanged(int idle, int total) => RebuildLayout(snap: false);
        private void HandlePopulationChanged(PopulationData pop) => RebuildLayout(snap: false);
        private void HandleSaveLoaded(SaveData save) => RebuildLayout(snap: true);

        // v6.3: WorkerManager active → WorkerAvatarSpawner owns worker visuals (per-worker identity +
        // health badges). Legacy class/count sprites must stand down or they double up.
        private bool _retiredForWorkerManager;

        /// <summary>Despawn all legacy worker sprites — called by WorkerAvatarSpawner on takeover.</summary>
        public void ClearAllVisuals()
        {
            foreach (var kv in _byClass)
                foreach (var w in kv.Value)
                    if (w != null) DestroyVisual(w.gameObject);
            _byClass.Clear();
            _freeIdleSlots.Clear();
            _idleSlotWatermark = 0;
            _retiredForWorkerManager = true;
        }

        // ปรับจำนวน sprite = จำนวนคนต่อคลาส แล้ว reconcile เฉพาะ cell ที่ต้องการคนเปลี่ยนจริง (ดู doc หัวไฟล์)
        private void RebuildLayout(bool snap)
        {
            if (_retiredForWorkerManager || WorkerManager.Instance != null) return;
            if (GridManager.Instance == null || workerSprite == null) return;

            var assign = WorkerAssignmentManager.Instance;

            foreach (var cls in Classes)
            {
                int count = ClassCount(cls);
                var list = ListFor(cls);
                while (list.Count < count) SpawnWorker(cls, list);
                while (list.Count > count) DespawnLast(list);

                // จำนวนที่ "ต้องการ" ต่อ cell (เฉพาะอาคารคลาสนี้)
                var desired = new Dictionary<Vector2Int, int>();
                if (assign != null)
                    foreach (var kvp in assign.Assignments)
                        if (ClassOfCell(kvp.Key) == cls) desired[kvp.Key] = kvp.Value;

                // reconcile ทุก cell ที่ "ต้องการคนตอนนี้" รวมกับ cell ที่ "มีคนอยู่แล้ว" (เผื่อโดนถอด assignment/ทุบตึกไป)
                var touchedCells = new HashSet<Vector2Int>(desired.Keys);
                foreach (var w in list)
                    if (w.AssignedCell.HasValue) touchedCells.Add(w.AssignedCell.Value);

                foreach (var cell in touchedCells)
                {
                    int want = desired.TryGetValue(cell, out int d) ? d : 0;
                    ReconcileCell(cell, want, list, snap);
                }

                // คนที่ยังไม่มีบทบาทเลย (เพิ่งสร้าง/โหลดเซฟ) → เข้าคิว idle
                foreach (var w in list)
                    if (!w.AssignedCell.HasValue && w.Slot < 0)
                        PlaceIdle(w, snap);
            }
        }

        // เทียบจำนวนคนที่ต้องการ (want) กับคนที่ประจำ cell นี้อยู่แล้ว — แตะเฉพาะส่วนต่าง
        // ส่วนเกิน (slot สูงสุดก่อน — "เข้าหลังออกก่อน") คืน idle · ส่วนขาดดึงจาก idle pool มาเติม slot ถัดไป
        // คนที่ slot อยู่ในช่วง [0, want) เดิมอยู่แล้ว จะไม่ถูกเรียก SetAssigned เลย — ไม่ขยับ ไม่กระตุก
        private void ReconcileCell(Vector2Int cell, int want, List<WorkerView> list, bool snap)
        {
            var current = new List<WorkerView>();
            foreach (var w in list)
                if (w.AssignedCell == cell) current.Add(w);
            current.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            for (int i = current.Count - 1; i >= want; i--)
                PlaceIdle(current[i], snap);

            for (int slot = current.Count; slot < want; slot++)
            {
                var w = PullIdleWorker(list);
                if (w == null) break; // ไม่มีคนว่างพอ (ไม่ควรเกิด — WorkerAssignmentManager จำกัดจำนวนไว้แล้ว)
                if (w.Slot >= 0) FreeIdleSlot(w.Slot); // กำลังออกจาก idle — คืน slot พักให้คนอื่นใช้ต่อ
                w.SetAssigned(WorkerWorldPos(cell, slot), cell, slot, snap);
            }
        }

        // หาคนงานที่ยังว่าง (ไม่ประจำอาคารไหน) ตัวแรกในลิสต์ — ดึงไปเติม slot ที่ขาด
        private static WorkerView PullIdleWorker(List<WorkerView> list)
        {
            foreach (var w in list)
                if (!w.AssignedCell.HasValue) return w;
            return null;
        }

        // ส่งเข้า idle pool ด้วย slot คงที่ (ใหม่หรือของเดิมถ้ายังไม่เคยมี) — SetIdle เองจะข้ามถ้า slot ไม่เปลี่ยน
        private void PlaceIdle(WorkerView w, bool snap)
        {
            int slot = AcquireIdleSlot();
            w.SetIdle(IdleWorldPos(slot), slot, snap);
        }

        private int AcquireIdleSlot()
        {
            if (_freeIdleSlots.Count > 0)
            {
                int slot = _freeIdleSlots.Min;
                _freeIdleSlots.Remove(slot);
                return slot;
            }
            return _idleSlotWatermark++;
        }

        private void FreeIdleSlot(int slot)
        {
            if (slot >= 0) _freeIdleSlots.Add(slot);
        }

        // จำนวนคนของคลาสนี้ที่ "มีอยู่" (อ่านจาก PopulationData — source of truth)
        private static int ClassCount(WorkerClass cls)
        {
            var pm = PopulationManager.Instance;
            if (pm == null) return 0;
            var pop = pm.Current;
            switch (cls)
            {
                case WorkerClass.Engineer: return pop.engineers;
                case WorkerClass.Medic:    return pop.medics;
                case WorkerClass.Farmer:   return pop.farmers;
                default:                   return pop.workers;
            }
        }

        // คลาสที่อาคารที่ cell นี้ต้องใช้ (Worker ถ้าไม่พบอาคาร) — mirror WorkerAssignmentManager
        private static WorkerClass ClassOfCell(Vector2Int cell)
        {
            var reg = BuildingRegistry.Instance;
            if (reg != null && reg.PlacedBuildings.TryGetValue(cell, out var data) && data != null)
                return data.requiredClass;
            return WorkerClass.Worker;
        }

        // sprite ของคลาส (fallback เป็นคนงานถ้าไม่ได้ wire)
        private Sprite SpriteForClass(WorkerClass cls)
        {
            switch (cls)
            {
                case WorkerClass.Engineer: return engineerSprite != null ? engineerSprite : workerSprite;
                case WorkerClass.Medic:    return medicSprite != null ? medicSprite : workerSprite;
                case WorkerClass.Farmer:   return farmerSprite != null ? farmerSprite : workerSprite;
                default:                   return workerSprite;
            }
        }

        private List<WorkerView> ListFor(WorkerClass cls)
        {
            if (!_byClass.TryGetValue(cls, out var list))
            {
                list = new List<WorkerView>();
                _byClass[cls] = list;
            }
            return list;
        }

        // วางคนงานที่ประจำ "รอบๆ" อาคาร (นอก footprint) ไม่ทับตัวอาคารเลย — เรียงหน้าสุดก่อน
        private Vector3 WorkerWorldPos(Vector2Int cell, int k)
        {
            int sx = 1, sy = 1;
            var reg = BuildingRegistry.Instance;
            if (reg != null && reg.PlacedBuildings.TryGetValue(cell, out var data) && data != null)
            { sx = Mathf.Max(1, data.size.x); sy = Mathf.Max(1, data.size.y); }

            var ring = FootprintRing(cell, sx, sy);
            var spot = ring[k % ring.Count];
            var j = Jitter[k % Jitter.Length] * 0.35f; // jitter เล็ก (ยืนคนละ tile อยู่แล้ว)
            return GridManager.Instance.IsoToWorldF(spot.x + j.x, spot.y + j.y);
        }

        // วงแหวน tile รอบ footprint (หนา 1 ช่อง นอกตัวอาคาร) เรียง "หน้าสุด (col+row มาก) ก่อน"
        // → คนงานตัวแรกๆ ไปยืนด้านหน้าอาคาร เห็นชัด ไม่ถูกตึกบัง
        private static readonly List<Vector2Int> _ring = new List<Vector2Int>();
        private static List<Vector2Int> FootprintRing(Vector2Int o, int sx, int sy)
        {
            _ring.Clear();
            for (int c = o.x - 1; c <= o.x + sx; c++)
                for (int r = o.y - 1; r <= o.y + sy; r++)
                {
                    bool inside = c >= o.x && c <= o.x + sx - 1 && r >= o.y && r <= o.y + sy - 1;
                    if (!inside) _ring.Add(new Vector2Int(c, r));
                }
            _ring.Sort((a, b) => (b.x + b.y).CompareTo(a.x + a.y)); // หน้าสุดก่อน
            return _ring;
        }

        private Vector3 IdleWorldPos(int idle)
        {
            var grid = GridManager.Instance;
            // จุดพักอิงกลางกริด (= ตำแหน่ง CORE TOWER) + เยื้องด้านหน้า แล้วเรียงเป็นแถว
            float baseCol = (grid.columns - 1) * 0.5f + idleOffsetFromCenter.x;
            float baseRow = (grid.rows - 1) * 0.5f + idleOffsetFromCenter.y;
            float col = baseCol + (idle % idlePerRow) * idleSpacing;
            float row = baseRow - (idle / idlePerRow) * idleSpacing;
            return grid.IsoToWorldF(col, row);
        }

        // ===== เงาใต้เท้าคนงาน (ellipse นุ่ม · ยึดตัวละครกับพื้น ไม่ให้ดูลอย) =====
        private const float WorkerShadowAlpha       = 0.28f; // ความเข้มเงา
        private const float WorkerShadowWidthFactor = 0.60f; // ความกว้างเงาเทียบความกว้างสไปรต์
        private static Sprite _ellipseShadow;

        private void SpawnWorker(WorkerClass cls, List<WorkerView> list)
        {
            var go = new GameObject($"{cls}_{list.Count}");
            go.transform.SetParent(workersParent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteForClass(cls);
            sr.sortingLayerName = WorkerSortingLayer;

            var shadow = CreateShadow(go, sr);       // เงาใต้เท้า (child แยก SpriteRenderer)
            var view = go.AddComponent<WorkerView>();
            view.SetShadow(shadow);                  // WorkerView คุม sortingOrder เงาให้อยู่ใต้ตัวทุกเฟรม
            list.Add(view);
        }

        // เงา ellipse ใต้เท้า — วางที่ปลายล่างสุดของสไปรต์ (bounds.min.y · pivot ตัวละคร = กึ่งกลาง)
        private static SpriteRenderer CreateShadow(GameObject parent, SpriteRenderer body)
        {
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(parent.transform, false);

            float feetY = body.sprite != null ? body.sprite.bounds.min.y : 0f;
            float bodyW = body.sprite != null ? body.sprite.bounds.size.x : 0.5f;
            shadow.transform.localPosition = new Vector3(0f, feetY + 0.02f, 0f);
            float w = bodyW * WorkerShadowWidthFactor;
            shadow.transform.localScale = new Vector3(w, w * 0.5f, 1f); // ellipse 2:1

            var sr = shadow.AddComponent<SpriteRenderer>();
            sr.sprite = GetEllipseShadow();
            sr.color = new Color(0f, 0f, 0f, WorkerShadowAlpha);
            sr.sortingLayerName = WorkerSortingLayer;
            return sr;
        }

        // sprite เงา: ellipse alpha ไล่จากกลาง (1) ออกขอบ (0) — สร้างครั้งเดียว cache ไว้ (pivot กึ่งกลาง)
        private static Sprite GetEllipseShadow()
        {
            if (_ellipseShadow != null) return _ellipseShadow;

            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            float cx = w * 0.5f, cy = h * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / cx, dy = (y - cy) / cy;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * w + x] = new Color(0f, 0f, 0f, a * a); // soft falloff
                }
            tex.SetPixels(px);
            tex.Apply();
            _ellipseShadow = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            return _ellipseShadow;
        }

        // เอาคนว่าง (idle) ออกก่อนเสมอ — กันทุบคนที่ยังประจำอาคารอยู่จนเกิดช่องว่างกลาง slot ของ cell นั้น
        // (ไม่ควรมีคนประจำเหลือให้ทุบตอนนี้อยู่แล้ว เพราะ WorkerAssignmentManager ลด assignment ให้ตรงกับ
        //  ประชากรก่อนเสมอ — เผื่อไว้ด้วย fallback ตัวสุดท้ายของลิสต์เพื่อความทนทาน)
        private void DespawnLast(List<WorkerView> list)
        {
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].AssignedCell.HasValue) { idx = i; break; }
            if (idx < 0) idx = list.Count - 1;
            if (idx < 0) return;

            var view = list[idx];
            list.RemoveAt(idx);
            if (view == null) return;

            if (!view.AssignedCell.HasValue) FreeIdleSlot(view.Slot);
            DestroyVisual(view.gameObject);
        }

        // DestroyImmediate นอก Play mode (EditMode tests) — Destroy() ใช้ได้เฉพาะ Play mode
        private void DestroyVisual(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
