using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// WorkerManager-driven world avatars (v6.3). One sprite per real Worker (keyed by Worker.id),
    /// so each sprite's health badge reflects THAT worker's actual status — the correct-identity path
    /// chosen over the legacy class/count spawner.
    ///
    /// Positions each avatar by its job → building (farm/power/water/mine/lab/cool map to a building
    /// type; jobs with no building, or idle, cluster near CORE TOWER). Reuses WorkerView for
    /// movement/sorting. Suppresses the legacy WorkerVisualSpawner while active so sprites don't double.
    ///
    /// ★ v6.3 cutover (worker-walk fix): auto-spawns into the live game once WorkerManager is present —
    /// was previously created ONLY by Sprint1TestPanel, which is compiled out of release builds
    /// (#if UNITY_EDITOR || DEVELOPMENT_BUILD). Result in a real build: WorkerManager auto-spawns →
    /// legacy WorkerVisualSpawner stands down → NOTHING positioned workers, so they never walked to
    /// their assigned building. Same auto-spawn pattern as WorkerManager/CardManager/ReactorController.
    /// </summary>
    public class WorkerAvatarSpawner : MonoBehaviour
    {
        private const string SortingLayer = "Buildings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        private static void AutoSpawn()
        {
            try
            {
                // ★ Do NOT gate on WorkerManager here. It auto-spawns from its own sceneLoaded callback,
                // and the order between the two callbacks is whatever order Unity happened to run the
                // RuntimeInitializeOnLoadMethod hooks in at startup — not something this can rely on.
                //
                // Gating on it meant that whenever WorkerManager's callback ran second, this returned and
                // nothing ever called AutoSpawn again: sceneLoaded fires once per load, so the spawner was
                // simply absent for the rest of the run and no worker avatars walked around. That is the
                // Restart bug — the first load got a second chance from the AfterSceneLoad hook itself,
                // a reload only ever gets the one callback.
                //
                // The old comment claimed "a later sceneLoaded covers it". There is no later sceneLoaded.
                // TryInit() below is the real safety net: it retries every frame until WorkerManager
                // appears, so spawning early costs nothing and spawning never is fatal.
                //
                // EventManager is still checked because MainMenu has no core systems — and being
                // DontDestroyOnLoad it is always up by the time a reload lands here.
                if (EventManager.Instance == null) return;
                if (FindFirstObjectByType<WorkerAvatarSpawner>() != null) return;
                new GameObject("WorkerAvatarSpawner (auto)").AddComponent<WorkerAvatarSpawner>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorkerAvatarSpawner] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static readonly Dictionary<string, BuildingType> JobToBuilding =
            new Dictionary<string, BuildingType>
            {
                { WorkerJobs.Farm,  BuildingType.Farm },
                { WorkerJobs.Power, BuildingType.PowerPlant },
                { WorkerJobs.Water, BuildingType.WaterPlant },
                { WorkerJobs.Mine,  BuildingType.Mine },
                { WorkerJobs.Lab,   BuildingType.Laboratory },
                { WorkerJobs.Cool,  BuildingType.CoreTower },
            };

        // small stand offsets so multiple workers at one building don't fully overlap
        private static readonly Vector2[] Jitter =
        {
            new Vector2(0f, 0f), new Vector2(-0.5f, -0.15f), new Vector2(0.5f, -0.15f),
            new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f), new Vector2(0f, -0.4f),
            new Vector2(-0.6f, 0.2f), new Vector2(0.6f, 0.2f),
        };

        private Transform _parent;
        private Sprite _sprite;
        private readonly Dictionary<int, WorkerView> _avatars = new Dictionary<int, WorkerView>();
        private readonly Dictionary<int, WorkerHealthBadge> _badges = new Dictionary<int, WorkerHealthBadge>();
        private readonly Dictionary<int, WorkerNameTag> _tags = new Dictionary<int, WorkerNameTag>();
        private readonly Dictionary<int, string> _lastTarget = new Dictionary<int, string>();
        private WorkerManager _wm;
        private bool _initialized;
        private bool _dirty;

        // Lazy init: WorkerManager may not exist yet when this spawns (auto-spawn ordering), so we retry
        // in Update instead of permanently disabling — otherwise a one-frame race kills worker visuals.
        private bool TryInit()
        {
            _wm = WorkerManager.Instance;
            if (_wm == null) return false;

            var holder = new GameObject("WorkerAvatars");
            _parent = holder.transform;

            // borrow the wired worker sprite from the legacy spawner, then retire it (avoid double sprites)
            var legacy = FindFirstObjectByType<WorkerVisualSpawner>();
            if (legacy != null)
            {
                _sprite = legacy.workerSprite;
                legacy.ClearAllVisuals();
                legacy.enabled = false;
            }
            if (_sprite == null) _sprite = PlaceholderSprite();

            _wm.OnWorkersChanged += MarkDirty;
            var em = EventManager.Instance;
            if (em != null)
            {
                // A newly built/registered building is a fresh walk target; a demolished one re-routes its
                // workers; a loaded save re-places everyone. Re-sync on each (coalesced via the dirty flag,
                // so it doesn't matter whether BuildingRegistry updated before or after this handler runs).
                em.OnBuildingPlaced += HandleBuildingPlaced;
                em.OnBuildingRemoved += HandleBuildingRemoved;
                em.OnSaveLoaded += HandleSaveLoaded;
                // Moving a worker between two cells of the SAME job leaves job counts untouched (no
                // OnWorkersChanged) — without this the avatar would keep standing at the old cell.
                em.OnWorkerAssignmentChanged += HandleAssignmentChanged;
            }

            _initialized = true;
            Sync();
            return true;
        }

        private void MarkDirty() => _dirty = true;
        private void HandleAssignmentChanged(Vector2Int cell, int count) => _dirty = true;
        private void HandleBuildingPlaced(Cell cell, BuildingData data) => _dirty = true;
        private void HandleBuildingRemoved(Vector2Int cell) => _dirty = true;
        private void HandleSaveLoaded(SaveData save) => _dirty = true;

        private void Update()
        {
            if (!_initialized) { TryInit(); return; }
            if (_dirty) { _dirty = false; Sync(); }
        }

        private void OnDestroy()
        {
            if (_wm != null) _wm.OnWorkersChanged -= MarkDirty;
            var em = EventManager.Instance;
            if (em != null)
            {
                em.OnBuildingPlaced -= HandleBuildingPlaced;
                em.OnBuildingRemoved -= HandleBuildingRemoved;
                em.OnSaveLoaded -= HandleSaveLoaded;
                em.OnWorkerAssignmentChanged -= HandleAssignmentChanged;
            }
        }

        // Rebuild map of building cells per type each sync (cheap — dozens of buildings)
        private readonly Dictionary<BuildingType, List<Vector2Int>> _byType =
            new Dictionary<BuildingType, List<Vector2Int>>();

        private void Sync()
        {
            if (_wm == null || GridManager.Instance == null) return;

            RefreshBuildingCells();
            RefreshPatrolCenters(); // ต้องหลัง RefreshBuildingCells — อ่าน _byType

            var live = new HashSet<int>();
            foreach (var w in _wm.Workers) if (w.alive) live.Add(w.id);

            // ★ Per-cell assignment is the source of truth (WorkerAssignmentManager): a worker stands at the
            //   EXACT building / ore node they were sent to. This is what makes diggers walk to THEIR ore
            //   node and builders to THEIR construction site — the old job→building-type map could only find
            //   "a farm" and had no entry at all for ore nodes, so those workers never left the idle cluster.
            var stand = new Dictionary<int, (Vector2Int cell, int ring)>();
            var wam = WorkerAssignmentManager.Instance;
            if (wam != null)
            {
                var taken = new HashSet<int>();
                foreach (var kv in wam.Assignments)
                {
                    string job = WorkerAssignmentManager.JobForCell(kv.Key) ?? WorkerJobs.Build;
                    for (int ring = 0; ring < kv.Value; ring++)
                    {
                        Worker pick = null;
                        // prefer a worker whose job matches this cell, then any other working body
                        foreach (var w in _wm.Workers)
                            if (w.alive && !taken.Contains(w.id) && w.job == job) { pick = w; break; }
                        if (pick == null)
                            foreach (var w in _wm.Workers)
                                if (w.alive && !taken.Contains(w.id) && w.job != WorkerJobs.Idle) { pick = w; break; }
                        if (pick == null) break; // fewer bodies than the plan claims
                        taken.Add(pick.id);
                        stand[pick.id] = (kv.Key, ring);
                    }
                }
            }

            var jobCount = new Dictionary<string, int>();
            var idleCount = 0;

            foreach (var w in _wm.Workers)
            {
                if (!w.alive) continue;

                var view = GetOrSpawn(w.id);
                _badges[w.id].SetStatus(w.status);
                _tags[w.id].SetName(w.displayName);

                // 1) sent to a specific cell → stand there
                if (stand.TryGetValue(w.id, out var s))
                {
                    string cellKey = $"cell:{s.cell.x},{s.cell.y}:{s.ring}";
                    if (!_lastTarget.TryGetValue(w.id, out var prevCell) || prevCell != cellKey)
                    {
                        var jit = Jitter[s.ring % Jitter.Length];
                        var p = GridManager.Instance.IsoToWorldF(s.cell.x + 0.5f + jit.x, s.cell.y + 0.5f + jit.y);
                        view.SetAssigned(p, s.cell, s.ring, snap: !_lastTarget.ContainsKey(w.id));
                        _lastTarget[w.id] = cellKey;
                    }
                    continue;
                }

                // 2) fallback (no assignment manager / legacy): any building matching the job type
                int k = jobCount.TryGetValue(w.job, out int c) ? c : 0;
                jobCount[w.job] = k + 1;
                if (JobToBuilding.TryGetValue(w.job, out var type) &&
                    _byType.TryGetValue(type, out var cells) && cells.Count > 0)
                {
                    var cell = cells[k % cells.Count];
                    int ring = k / cells.Count;
                    string key = $"{w.job}:{cell.x},{cell.y}:{ring}";
                    if (!_lastTarget.TryGetValue(w.id, out var prev) || prev != key)
                    {
                        var j = Jitter[ring % Jitter.Length];
                        var pos = GridManager.Instance.IsoToWorldF(cell.x + 0.5f + j.x, cell.y + 0.5f + j.y);
                        view.SetAssigned(pos, cell, ring, snap: !_lastTarget.ContainsKey(w.id));
                        _lastTarget[w.id] = key;
                    }
                    continue;
                }

                // 3) nothing to do → patrol a landmark (CORE TOWER / Research Lab) instead of parking
                int lm = idleCount % _patrolCenters.Count;
                string idleKey = $"patrol:{lm}:{idleCount}";
                if (!_lastTarget.TryGetValue(w.id, out var prevIdle) || prevIdle != idleKey)
                {
                    view.SetPatrol(_patrolCenters[lm], PatrolInner, PatrolOuter, idleCount,
                                   snap: !_lastTarget.ContainsKey(w.id));
                    _lastTarget[w.id] = idleKey;
                }
                idleCount++;
            }

            // despawn avatars for workers that died / left
            if (_avatars.Count > live.Count)
            {
                var gone = new List<int>();
                foreach (var id in _avatars.Keys)
                    if (!live.Contains(id)) gone.Add(id);
                foreach (var id in gone)
                {
                    if (_avatars[id] != null) Destroy(_avatars[id].gameObject);
                    _avatars.Remove(id);
                    _badges.Remove(id);
                    _tags.Remove(id);
                    _lastTarget.Remove(id);
                }
            }
        }

        private void RefreshBuildingCells()
        {
            foreach (var kv in _byType) kv.Value.Clear();
            var reg = BuildingRegistry.Instance;
            if (reg == null) return;
            foreach (var kvp in reg.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null) continue;
                if (!_byType.TryGetValue(data.buildingType, out var list))
                {
                    list = new List<Vector2Int>();
                    _byType[data.buildingType] = list;
                }
                list.Add(kvp.Key);
            }
        }

        private WorkerView GetOrSpawn(int id)
        {
            if (_avatars.TryGetValue(id, out var view) && view != null) return view;

            var go = new GameObject($"Worker_{id}");
            go.transform.SetParent(_parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingLayerName = SortingLayer;

            view = go.AddComponent<WorkerView>();

            var badgeGo = new GameObject("HealthBadge");
            badgeGo.transform.SetParent(go.transform, false);
            var badge = badgeGo.AddComponent<WorkerHealthBadge>();
            badge.Init(sr);

            var tagGo = new GameObject("NameTag");
            tagGo.transform.SetParent(go.transform, false);
            var tag = tagGo.AddComponent<WorkerNameTag>();
            tag.Init(sr);

            _avatars[id] = view;
            _badges[id] = badge;
            _tags[id] = tag;
            return view;
        }

        // ===== จุดลาดตระเวนคนว่าง =====
        // เดิม: IdlePos() วางเป็นตาราง 6 คอลัมน์ ระยะห่าง 0.6 cell ที่จุดเดียวกลางกริด
        //       → คนว่างทุกคนกระจุกกันเป็นกองเดียว แล้ว wanderRadius 0.35 ก็ขยับได้แค่คืบเดียว
        // ตอนนี้: กระจายรอบแลนด์มาร์ก (CORE TOWER + Research Lab) แล้วเดินวนในวงแหวนตลอดเวลา
        private const float PatrolInner = 1.8f; // เว้นตัวอาคารไว้ ไม่ให้เดินทับ (world units)
        private const float PatrolOuter = 5.0f; // ความกว้างของลานที่เดินวน
        private static readonly BuildingType[] PatrolLandmarks =
            { BuildingType.CoreTower, BuildingType.Laboratory };

        private readonly List<Vector3> _patrolCenters = new List<Vector3>();

        private void RefreshPatrolCenters()
        {
            var g = GridManager.Instance;
            _patrolCenters.Clear();

            foreach (var type in PatrolLandmarks)
                if (_byType.TryGetValue(type, out var cells))
                    foreach (var c in cells)
                        _patrolCenters.Add(g.IsoToWorldF(c.x + 0.5f, c.y + 0.5f));

            // ยังไม่ได้สร้าง/ยังไม่ลงทะเบียนแลนด์มาร์กสักหลัง → เดินวนกลางกริด (ที่ตั้ง CORE TOWER)
            if (_patrolCenters.Count == 0)
                _patrolCenters.Add(g.IsoToWorldF((g.columns - 1) * 0.5f, (g.rows - 1) * 0.5f));
        }

        private static Sprite _placeholder;
        private static Sprite PlaceholderSprite()
        {
            if (_placeholder != null) return _placeholder;
            const int w = 24, h = 40;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            var body = new Color(0.35f, 0.55f, 0.85f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w * 0.5f) / (w * 0.5f);
                    bool inside = Mathf.Abs(dx) < (y > h * 0.6f ? 0.6f : 0.9f); // head narrower
                    px[y * w + x] = inside ? body : new Color(0, 0, 0, 0);
                }
            tex.SetPixels(px); tex.Apply();
            _placeholder = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
            return _placeholder;
        }
    }
}
