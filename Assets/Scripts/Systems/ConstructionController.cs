using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการ construction queue: ทุก building ที่วางใหม่ต้องรอ 10 tick จึงจะ active
    /// ระหว่างสร้าง building ไม่ produce resource และไม่นับเป็น CoreTower part
    /// Cancel = คืน energyCost (workers เป็น reserve pool ไม่ถูกหัก) | Prioritize = completes บน tick ถัดไป
    /// </summary>
    public class ConstructionController : MonoBehaviour
    {
        public static ConstructionController Instance { get; private set; }

        public const int TotalConstructionTicks = 10;

        [Header("Progress UI — prefab ที่มี ConstructionProgressUI (สร้าง progress bar เองตอน runtime)")]
        public GameObject constructionProgressUIPrefab;

        // List รักษาลำดับสำหรับ Prioritize, Dict ให้ O(1) lookup
        private readonly List<Vector2Int> _queue = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _progress = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, GameObject> _progressUI = new Dictionary<Vector2Int, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingPlaced            += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved           += HandleBuildingRemoved;
            EventManager.Instance.OnGameTick                  += HandleGameTick;
            EventManager.Instance.OnSaveLoaded                += HandleSaveLoaded;
            EventManager.Instance.OnConstructionCancelRequested     += HandleCancelRequested;
            EventManager.Instance.OnConstructionPrioritizeRequested += HandlePrioritizeRequested;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingPlaced            -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved           -= HandleBuildingRemoved;
            EventManager.Instance.OnGameTick                  -= HandleGameTick;
            EventManager.Instance.OnSaveLoaded                -= HandleSaveLoaded;
            EventManager.Instance.OnConstructionCancelRequested     -= HandleCancelRequested;
            EventManager.Instance.OnConstructionPrioritizeRequested -= HandlePrioritizeRequested;
        }

        // ─────────────────────────────────────────
        //  Public queries (read-only — ใช้ได้โดย ResourceManager / CoreTowerManager)
        // ─────────────────────────────────────────

        public bool IsUnderConstruction(Vector2Int cell) => _progress.ContainsKey(cell);

        public int GetProgress(Vector2Int cell) =>
            _progress.TryGetValue(cell, out int p) ? p : TotalConstructionTicks;

        // ─────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            var pos = new Vector2Int(cell.col, cell.row);
            if (_progress.ContainsKey(pos)) return;

            _queue.Add(pos);
            _progress[pos] = 0;
            SpawnProgressUI(pos, 0);
            EventManager.Instance.RaiseConstructionProgressChanged(pos, 0);
        }

        private void HandleBuildingRemoved(Vector2Int pos)
        {
            _queue.Remove(pos);
            _progress.Remove(pos);
            DestroyProgressUI(pos);
        }

        private void HandleGameTick()
        {
            // snapshot ก่อน iterate เผื่อ Complete ลบ entry ออกระหว่างลูป
            var snapshot = new List<Vector2Int>(_queue);

            foreach (var pos in snapshot)
            {
                if (!_progress.TryGetValue(pos, out int current)) continue;

                int next = current + 1;

                if (next >= TotalConstructionTicks)
                {
                    _progress.Remove(pos);
                    _queue.Remove(pos);
                    DestroyProgressUI(pos);

                    if (BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data))
                        EventManager.Instance.RaiseConstructionComplete(pos, data);
                }
                else
                {
                    _progress[pos] = next;
                    EventManager.Instance.RaiseConstructionProgressChanged(pos, next);
                }
            }
        }

        private void HandleCancelRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            // ดึง BuildingData ก่อนที่ OnBuildingRemoved จะลบออกจาก Registry
            BuildingRegistry.Instance.PlacedBuildings.TryGetValue(pos, out var data);

            // cascade: GridManager, BuildingVisualSpawner, BuildingRegistry,
            // และ HandleBuildingRemoved ของ ConstructionController เอง
            EventManager.Instance.RaiseBuildingRemoved(pos);

            if (data == null) return;

            // คืนเฉพาะ energyCost (ต้นทุนสร้าง) — workers เป็น reserve pool ไม่ถูกหักตอนวาง จึงไม่ต้องคืน
            EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, data.energyCost);
        }

        private void HandlePrioritizeRequested(Vector2Int pos)
        {
            if (!_progress.ContainsKey(pos)) return;

            // ตั้ง progress เป็น TotalTicks-1 → จะเสร็จบน tick ถัดไป
            _progress[pos] = TotalConstructionTicks - 1;

            // เลื่อนขึ้นหน้าสุดของ queue เพื่อให้ tick ก่อนตัวอื่น
            _queue.Remove(pos);
            _queue.Insert(0, pos);

            EventManager.Instance.RaiseConstructionProgressChanged(pos, TotalConstructionTicks - 1);
        }

        private void HandleSaveLoaded(SaveData save)
        {
            _queue.Clear();
            _progress.Clear();

            if (save.underConstructionCells == null) return;

            for (int i = 0; i < save.underConstructionCells.Count; i++)
            {
                var pos = save.underConstructionCells[i];
                int prog = (save.constructionProgress != null && i < save.constructionProgress.Count)
                    ? save.constructionProgress[i] : 0;

                _queue.Add(pos);
                _progress[pos] = prog;
            }
        }

        // ─────────────────────────────────────────
        //  Progress UI helpers
        // ─────────────────────────────────────────

        private void SpawnProgressUI(Vector2Int pos, int initialProgress)
        {
            if (constructionProgressUIPrefab == null || GridManager.Instance == null) return;

            var worldPos = GridManager.Instance.IsoToWorld(pos.x, pos.y);
            worldPos.y += 0.6f; // ลอยขึ้นเหนือ sprite

            var go = Instantiate(constructionProgressUIPrefab, worldPos, Quaternion.identity);
            var ui = go.GetComponent<ConstructionProgressUI>();
            if (ui != null) ui.Init(pos, initialProgress);

            _progressUI[pos] = go;
        }

        private void DestroyProgressUI(Vector2Int pos)
        {
            if (!_progressUI.TryGetValue(pos, out var go)) return;
            _progressUI.Remove(pos);
            if (go != null) Destroy(go);
        }

        // เรียกโดย SaveManager ตอน Save()
        public (List<Vector2Int> cells, List<int> progress) GetSaveState()
        {
            var cells = new List<Vector2Int>(_queue);
            var progress = new List<int>();
            foreach (var pos in _queue)
                progress.Add(_progress.TryGetValue(pos, out int p) ? p : 0);
            return (cells, progress);
        }
    }
}
