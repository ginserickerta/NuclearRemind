using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// ★ v6.3 cutover (slice 6 Barks): the on-screen channel for OnBarkFired — until now BarkManager
    /// and InnerVoiceDirector spoke into the void (only tests listened). Bottom-left toast feed:
    ///
    ///   • NPC barks (BARKS.md): "โควา: เก้าสิบแล้ว ..." — speaker name + line
    ///   • Inner Voice (Auren): "▸ ..." — the nameless prompt (BARKS.md §Inner Voice)
    ///
    /// Max 3 toasts stacked, each fades after a few seconds on UNSCALED time (barks fire at day-end
    /// when the clock may be paused). Never blocks clicks (no raycast targets). Self-contained
    /// code-built uGUI, auto-spawns onto HUDCanvas — same pattern as WorkerAssignPanel.
    /// </summary>
    public class BarkFeedHUD : MonoBehaviour
    {
        private const int MaxToasts = 3;
        private const float ToastSeconds = 9f;   // visible lifetime
        private const float FadeSeconds = 1.5f;  // fade-out tail inside the lifetime

        /// <summary>
        /// Draw order for the feed. The project's ladder: HUD 0 · Story/Record 60 · Quiz 70 ·
        /// Pause 100 · GameUIStack panels 200+ · Codex 200 · fade 32000.
        ///
        /// 50 puts barks above every HUD widget — the hotbar, the panels baked into HUDCanvas — while
        /// still sitting under anything the player deliberately opened. That is the intent: a bark is
        /// ambient colour, not something to read right now, so it should never cover a Codex entry or a
        /// crisis card the player is actually reading. Raise this to 250 to float it over everything.
        /// </summary>
        private const int SortingOrder = 50;

        private static readonly Color CBg = new Color(0.06f, 0.06f, 0.05f, 0.82f);
        private static readonly Color CText = new Color(0.93f, 0.92f, 0.86f, 1f);
        private static readonly Color CInner = new Color(0.80f, 0.86f, 0.95f, 1f); // Auren ▸ tint

        private Font _font;
        private RectTransform _stack;
        private readonly List<(GameObject go, CanvasGroup cg, float bornAt)> _toasts
            = new List<(GameObject, CanvasGroup, float)>();

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
                if (EventManager.Instance == null) return; // no core yet (MainMenu) — retry on next scene
                if (FindFirstObjectByType<BarkFeedHUD>() != null) return;
                var canvas = FindBestCanvas();
                if (canvas == null) return;
                var go = new GameObject("BarkFeedHUD (auto)");
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<BarkFeedHUD>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BarkFeedHUD] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
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

        private void Awake()
        {
            _font = UIFonts.Body;
            BuildStack();
        }

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBarkFired += HandleBarkFired;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBarkFired -= HandleBarkFired;
        }

        private void Update()
        {
            // age + fade on unscaled time; drop dead toasts
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _toasts[i].bornAt;
                if (age >= ToastSeconds)
                {
                    Destroy(_toasts[i].go);
                    _toasts.RemoveAt(i);
                }
                else if (age >= ToastSeconds - FadeSeconds && _toasts[i].cg != null)
                {
                    _toasts[i].cg.alpha = Mathf.Clamp01((ToastSeconds - age) / FadeSeconds);
                }
            }
        }

        private void HandleBarkFired(BarkSO bark)
        {
            if (bark == null || string.IsNullOrEmpty(bark.text)) return;
            // The VN dialogue panel owns spoken lines when it is present (BarkDialogueRouter). This feed
            // stays as the fallback for scenes without it, so a line is never dropped — and never doubled.
            if (BarkDialogueRouter.HandlesBarks) return;

            while (_toasts.Count >= MaxToasts) // oldest out first
            {
                Destroy(_toasts[0].go);
                _toasts.RemoveAt(0);
            }

            bool inner = bark.speaker == BarkSpeaker.Auren; // ▸ Inner Voice — no name (BARKS.md)
            string line = inner ? $"▸ {bark.text}" : $"{SpeakerName(bark.speaker)}: {bark.text}";
            var toast = MakeToast(line, inner ? CInner : CText);
            _toasts.Add((toast, toast.GetComponent<CanvasGroup>(), Time.unscaledTime));
        }

        private static string SpeakerName(BarkSpeaker s)
        {
            switch (s)
            {
                case BarkSpeaker.Kova: return "โควา";
                case BarkSpeaker.Mira: return "มิรา";
                case BarkSpeaker.Dorn: return "ดอร์น";
                case BarkSpeaker.Citizen: return "ชาวเมือง";
                default: return "";
            }
        }

        // ═══════════════ BUILD ═══════════════

        /// <summary>
        /// Give the feed its own nested Canvas so its draw order is a number we chose, not an accident.
        ///
        /// Without this the feed inherits HUDCanvas at order 0 and loses to every GameUIStack panel
        /// (base 200), so the inner voice vanished under whatever the player had open. Sibling index
        /// cannot fix that — it only orders within one canvas.
        ///
        /// Deliberately no GraphicRaycaster: a nested canvas without one takes no clicks, which is
        /// exactly right for a passive feed ("never blocks clicks"). Adding one would let the invisible
        /// stack rect swallow presses meant for the map underneath.
        /// </summary>
        private void EnsureOwnCanvas()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            // Stretch to the parent canvas so child anchors still mean screen corners.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
        }
        private void BuildStack()
        {
            EnsureOwnCanvas();

            var go = new GameObject("BarkStack", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _stack = go.GetComponent<RectTransform>();
            _stack.anchorMin = _stack.anchorMax = new Vector2(0f, 0f);
            _stack.pivot = new Vector2(0f, 0f);
            _stack.anchoredPosition = new Vector2(16f, 150f); // above the hotbar strip
            _stack.sizeDelta = new Vector2(520f, 0f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private GameObject MakeToast(string line, Color textColor)
        {
            var go = new GameObject("Toast", typeof(RectTransform));
            go.transform.SetParent(_stack, false);

            var bg = go.AddComponent<Image>();
            bg.color = CBg;
            bg.raycastTarget = false; // never block the game under it

            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var pad = go.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(12, 12, 8, 8);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = true;

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var txt = tgo.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = 18;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = line;

            return go;
        }
    }
}
