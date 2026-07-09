using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// สร้าง/ลบ sprite คนงานบนแมพให้จำนวน = PopulationData.workers (V4 §5)
    /// และตั้งเป้าหมายให้แต่ละตัวไปยืนที่อาคารที่ถูก assign (WorkerAssignmentManager) หรือจุดพัก idle
    /// event-driven เหมือน BuildingVisualSpawner · อ่าน Assignments/Current แบบ read-only query
    /// </summary>
    public class WorkerVisualSpawner : MonoBehaviour
    {
        private const string UnitsSortingLayer = "Units";

        [Header("Sprite + parent (wire โดย Phase3PopulationSetup)")]
        public Sprite workerSprite;
        public Transform workersParent;

        [Header("จุดพักคนว่าง (idle staging) — รวมกลุ่มใกล้ CORE TOWER (กลางกริด)")]
        [Tooltip("เยื้องจากกลางกริดกี่ cell (ค่าลบ = ด้านหน้าเตา ไม่ทับ footprint)")]
        public Vector2 idleOffsetFromCenter = new Vector2(-2f, -2.5f);
        public int idlePerRow = 6;
        public float idleSpacing = 0.6f;

        private readonly List<WorkerView> _workers = new List<WorkerView>();

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

        private void Start() => RebuildLayout(snap: true); // spawn ตามจำนวน worker เริ่มเกม

        private void HandleAssignmentChanged(Vector2Int cell, int count) => RebuildLayout(snap: false);
        private void HandlePoolChanged(int idle, int total) => RebuildLayout(snap: false);
        private void HandlePopulationChanged(PopulationData pop) => RebuildLayout(snap: false);
        private void HandleSaveLoaded(SaveData save) => RebuildLayout(snap: true);

        // ปรับจำนวน sprite = จำนวน worker แล้วตั้งเป้าหมายทีละตัว: assigned ก่อน แล้ว idle
        private void RebuildLayout(bool snap)
        {
            if (GridManager.Instance == null || workerSprite == null) return;

            int total = PopulationManager.Instance != null ? PopulationManager.Instance.Current.workers : 0;
            while (_workers.Count < total) SpawnWorker();
            while (_workers.Count > total) DespawnLast();

            int idx = 0;
            var assign = WorkerAssignmentManager.Instance;
            if (assign != null)
            {
                foreach (var kvp in assign.Assignments)
                    for (int k = 0; k < kvp.Value && idx < _workers.Count; k++, idx++)
                        _workers[idx].SetAssigned(WorkerWorldPos(kvp.Key, k), kvp.Key, snap);
            }
            // ที่เหลือ = ว่างงาน → เดินเล่นวนรอบจุดพักใกล้ CORE TOWER
            for (int idle = 0; idx < _workers.Count; idx++, idle++)
                _workers[idx].SetIdle(IdleWorldPos(idle), snap);
        }

        private Vector3 WorkerWorldPos(Vector2Int cell, int k)
        {
            var j = Jitter[k % Jitter.Length];
            return GridManager.Instance.IsoToWorldF(cell.x + j.x, cell.y + j.y);
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

        private void SpawnWorker()
        {
            var go = new GameObject($"Worker_{_workers.Count}");
            go.transform.SetParent(workersParent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = workerSprite;
            sr.sortingLayerName = UnitsSortingLayer;
            _workers.Add(go.AddComponent<WorkerView>());
        }

        private void DespawnLast()
        {
            int last = _workers.Count - 1;
            if (last < 0) return;
            var view = _workers[last];
            _workers.RemoveAt(last);
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
