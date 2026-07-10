using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// สร้าง/ลบ sprite คนงานบนแมพให้จำนวน = ประชากรทุกคลาส (Worker/Engineer/Medic/Farmer) (V4 §5)
    /// และตั้งเป้าหมายให้แต่ละตัวไปยืนที่อาคารที่ถูก assign (WorkerAssignmentManager) หรือจุดพัก idle
    /// แต่ละคลาสใช้ sprite ของตัวเอง (คนงาน/วิศวกร/หมอ) — คลาสที่ไม่ได้ wire sprite จะ fallback เป็น workerSprite
    /// event-driven เหมือน BuildingVisualSpawner · อ่าน Assignments/Current + PlacedBuildings แบบ read-only query
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

        private void Start() => RebuildLayout(snap: true); // spawn ตามจำนวนประชากรเริ่มเกม

        private void HandleAssignmentChanged(Vector2Int cell, int count) => RebuildLayout(snap: false);
        private void HandlePoolChanged(int idle, int total) => RebuildLayout(snap: false);
        private void HandlePopulationChanged(PopulationData pop) => RebuildLayout(snap: false);
        private void HandleSaveLoaded(SaveData save) => RebuildLayout(snap: true);

        // ปรับจำนวน sprite = จำนวนคนต่อคลาส แล้วตั้งเป้าหมายทีละตัว: assigned ก่อน แล้ว idle
        private void RebuildLayout(bool snap)
        {
            if (GridManager.Instance == null || workerSprite == null) return;

            var assign = WorkerAssignmentManager.Instance;
            int idleCounter = 0; // ตำแหน่งจุดพักไล่ต่อเนื่องข้ามคลาส (ไม่ให้ทับกัน)

            foreach (var cls in Classes)
            {
                int count = ClassCount(cls);
                var list = ListFor(cls);
                while (list.Count < count) SpawnWorker(cls, list);
                while (list.Count > count) DespawnLast(list);

                int idx = 0;
                // assigned: เฉพาะอาคารที่ต้องใช้คลาสนี้
                if (assign != null)
                {
                    foreach (var kvp in assign.Assignments)
                    {
                        if (ClassOfCell(kvp.Key) != cls) continue;
                        for (int k = 0; k < kvp.Value && idx < list.Count; k++, idx++)
                            list[idx].SetAssigned(WorkerWorldPos(kvp.Key, k), kvp.Key, snap);
                    }
                }
                // ที่เหลือ = ว่างงาน → เดินเล่นวนรอบจุดพักใกล้ CORE TOWER
                for (; idx < list.Count; idx++)
                    list[idx].SetIdle(IdleWorldPos(idleCounter++), snap);
            }
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

        private void SpawnWorker(WorkerClass cls, List<WorkerView> list)
        {
            var go = new GameObject($"{cls}_{list.Count}");
            go.transform.SetParent(workersParent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteForClass(cls);
            sr.sortingLayerName = WorkerSortingLayer;
            list.Add(go.AddComponent<WorkerView>());
        }

        private void DespawnLast(List<WorkerView> list)
        {
            int last = list.Count - 1;
            if (last < 0) return;
            var view = list[last];
            list.RemoveAt(last);
            if (view != null) DestroyVisual(view.gameObject);
        }

        // DestroyImmediate นอก Play mode (EditMode tests) — Destroy() ใช้ได้เฉพาะ Play mode
        private void DestroyVisual(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
