using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// v6.3 worker-assignment panel (GDD §17) — the live in-game way to move workers between JOBS
    /// (farm/power/water/mine/lab/cool), replacing the legacy per-building cell assignment. Self-contained:
    /// auto-spawns onto HUDCanvas and builds its own uGUI (same pattern as ResearchQueuePanel). Toggled by a
    /// key (default J) since the v6.3 job pool is global, not per-building. Pauses the day clock while open.
    ///
    /// ★ cutover (slice 2 Workers): production reads Σ efficiency per job (ResourceManager.ComputeV63…),
    /// so assigning workers here is what makes food/water/iron/power flow once WorkerManager is live.
    /// </summary>
    public class WorkerAssignPanel : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CText      = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted     = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold      = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CPlus      = new Color(0.20f, 0.34f, 0.24f, 1f);
        static readonly Color CMinus     = new Color(0.36f, 0.20f, 0.20f, 1f);
        static readonly Color CBtnDim    = new Color(0.15f, 0.15f, 0.13f, 1f);

        public KeyCode toggleKey = KeyCode.J;

        // job id → display label (GDD §17 jobs the player can staff)
        private static readonly (string job, string label)[] Jobs =
        {
            (WorkerJobs.Farm,  "🌾 ฟาร์ม (อาหาร)"),
            (WorkerJobs.Water, "💧 น้ำ"),
            (WorkerJobs.Power, "⚡ โรงไฟ (พลังงาน)"),
            (WorkerJobs.Mine,  "⛏ เหมือง (เหล็ก)"),
            (WorkerJobs.Lab,   "🔬 ห้องวิจัย"),
            (WorkerJobs.Cool,  "❄ ระบายความร้อนเตา"),
        };

        private Font _font;
        private bool _shown;
        private GameObject _backdrop, _root;
        private Text _header;
        private readonly Dictionary<string, Text> _countTexts = new Dictionary<string, Text>();

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
            try { AutoSpawnUnsafe(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorkerAssignPanel] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<WorkerAssignPanel>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return;
            var go = new GameObject("WorkerAssignPanel (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<WorkerAssignPanel>();
        }

        private static Canvas FindBestCanvas()
        {
            Canvas fallback = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                var root = c.rootCanvas != null ? c.rootCanvas : c;
                if (root.name == "HUDCanvas") return root;
                if (fallback == null) fallback = root;
            }
            return fallback;
        }

        private void Awake() => _font = LoadFont();

        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (f == null) f = Resources.Load<Font>("Fonts/Kanit");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        private void Start()
        {
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (_shown) Hide(); else Open();
            }
        }

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) _backdrop.SetActive(true);
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup);
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        private void Move(string job, int dir)
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;

            if (dir > 0)
            {
                foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
                    if (w.alive && !w.resting && w.strikeDaysLeft == 0) { wm.AssignJob(w, job); break; }
            }
            else
            {
                var inJob = wm.GetWorkers(job);
                if (inJob.Count > 0) wm.AssignJob(inJob[0], WorkerJobs.Idle);
            }
            Refresh();
        }

        private void Refresh()
        {
            var wm = WorkerManager.Instance;
            if (_header != null)
            {
                int idle = wm != null ? wm.GetWorkers(WorkerJobs.Idle).Count : 0;
                int alive = wm != null ? wm.AliveCount : 0;
                _header.text = wm == null
                    ? "จัดคนงาน — ระบบยังไม่พร้อม"
                    : $"จัดคนงาน (v6.3) · ว่าง {idle} / ทั้งหมด {alive} คน";
            }
            foreach (var kv in _countTexts)
            {
                int n = wm != null ? wm.GetWorkers(kv.Key).Count : 0;
                kv.Value.text = n.ToString();
            }
        }

        // ═══════════════ BUILD ═══════════════
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var bdBtn = _backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 560f);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);

            _header = MakeText("Header", _root.transform, "", 24, CGold, TextAnchor.UpperLeft);
            Anchor(_header.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-24f, -70f));

            var close = MakeButton("Close", _root.transform, "✕", CBtnDim, Hide);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-16f, -16f); crt.sizeDelta = new Vector2(48f, 48f);

            var hint = MakeText("Hint", _root.transform, $"กด [{toggleKey}] เพื่อปิด · คลิกนอกกรอบก็ปิด", 15, CMuted, TextAnchor.UpperLeft);
            Anchor(hint.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -74f), new Vector2(-24f, -100f));

            // job rows
            var list = NewUI("List", _root.transform, new Color(0f, 0f, 0f, 0.25f));
            Anchor(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -104f));
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            foreach (var (job, label) in Jobs) BuildJobRow(list.transform, job, label);
        }

        private void BuildJobRow(Transform parent, string job, string label)
        {
            var row = NewUI($"Row_{job}", parent, new Color(1f, 1f, 1f, 0.04f));
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 54f; le.preferredHeight = 54f;

            var name = MakeText("Name", row.transform, label, 20, CText, TextAnchor.MiddleLeft);
            Anchor(name.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-200f, 0f));

            var minus = MakeButton("Minus", row.transform, "−", CMinus, () => Move(job, -1));
            PlaceRight(minus, -140f, 44f);

            var count = MakeText("Count", row.transform, "0", 22, CGold, TextAnchor.MiddleCenter);
            var crt = count.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f); crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-90f, 0f); crt.sizeDelta = new Vector2(44f, 44f);
            _countTexts[job] = count;

            var plus = MakeButton("Plus", row.transform, "+", CPlus, () => Move(job, +1));
            PlaceRight(plus, -12f, 44f);
        }

        private static void PlaceRight(Button b, float x, float size)
        {
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(size, size);
        }

        // ── uGUI helpers (same as ResearchQueuePanel) ──
        private static GameObject NewUI(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            return go;
        }

        private static void Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = anchor; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            return t;
        }

        private Button MakeButton(string name, Transform parent, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewUI(name, parent, bg);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = MakeText("Label", go.transform, text, 22, CText, TextAnchor.MiddleCenter);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
