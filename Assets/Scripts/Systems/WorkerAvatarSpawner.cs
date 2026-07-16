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
    /// Spawned by Sprint1TestPanel during playtest (where WorkerManager is live). Safe to add to a
    /// real scene later once WorkerManager becomes the scene authority.
    /// </summary>
    public class WorkerAvatarSpawner : MonoBehaviour
    {
        private const string SortingLayer = "Buildings";

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
        private readonly Dictionary<int, string> _lastTarget = new Dictionary<int, string>();
        private WorkerManager _wm;

        private void Start()
        {
            _wm = WorkerManager.Instance;
            if (_wm == null) { enabled = false; return; }

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

            _wm.OnWorkersChanged += Sync;
            Sync();
        }

        private void OnDestroy()
        {
            if (_wm != null) _wm.OnWorkersChanged -= Sync;
        }

        // Rebuild map of building cells per type each sync (cheap — dozens of buildings)
        private readonly Dictionary<BuildingType, List<Vector2Int>> _byType =
            new Dictionary<BuildingType, List<Vector2Int>>();

        private void Sync()
        {
            if (_wm == null || GridManager.Instance == null) return;

            RefreshBuildingCells();

            var live = new HashSet<int>();
            var jobCount = new Dictionary<string, int>();
            var idleCount = 0;

            foreach (var w in _wm.Workers)
            {
                if (!w.alive) continue;
                live.Add(w.id);

                var view = GetOrSpawn(w.id);
                var badge = _badges[w.id];
                badge.SetStatus(w.status);

                // resolve a stand target from the job
                int k = jobCount.TryGetValue(w.job, out int c) ? c : 0;
                jobCount[w.job] = k + 1;

                bool placed = false;
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
                    placed = true;
                }

                if (!placed)
                {
                    string key = $"idle:{idleCount}";
                    if (!_lastTarget.TryGetValue(w.id, out var prev) || prev != key)
                    {
                        view.SetIdle(IdlePos(idleCount), idleCount, snap: !_lastTarget.ContainsKey(w.id));
                        _lastTarget[w.id] = key;
                    }
                    idleCount++;
                }
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

            _avatars[id] = view;
            _badges[id] = badge;
            return view;
        }

        private Vector3 IdlePos(int i)
        {
            var g = GridManager.Instance;
            float baseCol = (g.columns - 1) * 0.5f - 2f + (i % 6) * 0.6f;
            float baseRow = (g.rows - 1) * 0.5f - 2.5f - (i / 6) * 0.6f;
            return g.IsoToWorldF(baseCol, baseRow);
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
