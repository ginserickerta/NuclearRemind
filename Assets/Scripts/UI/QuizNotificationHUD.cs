using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Small quiz notification icon + badge (bottom-right HUD) — mirrors RecordNotificationHUD. When a quiz
    /// becomes answerable (its knowledge was used and the reveal delay has passed, QUIZZES.md pacing), the
    /// icon appears with a red badge counting how many are ready and a one-shot pulse; clicking it opens the
    /// Codex to answer. No forced popup, no timer — the player answers when they choose (QUIZZES.md rule).
    ///
    /// ★ v6.3 cutover: self-contained (auto-spawn onto HUDCanvas + code-built uGUI, same pattern as
    ///   CodexPanelUI). Recomputes on OnDayStarted (after QuizAppliedWatcher advanced the reveal clock) and
    ///   OnQuizAnswered (count drops as quizzes are answered). Hidden entirely while nothing is answerable.
    /// </summary>
    public class QuizNotificationHUD : MonoBehaviour
    {
        static readonly Color CIcon   = new Color(0.20f, 0.42f, 0.60f, 0.96f); // codex-quiz blue
        static readonly Color CBorder = new Color(0.55f, 0.78f, 0.95f, 1f);
        static readonly Color CBadge  = new Color(0.82f, 0.22f, 0.20f, 1f);

        public float pulseScale = 1.25f;
        public float pulseSeconds = 0.28f;

        // Verification aid: force the icon visible even with no answerable quiz, so its placement/render can
        // be confirmed in-game. MUST stay false for shipping — otherwise the icon is permanently on with no
        // badge, which trains the player to ignore it and defeats the point of the notification.
        private const bool DebugAlwaysShow = false;

        private Font _font;
        private GameObject _iconRoot;   // whole icon (hidden when nothing answerable)
        private GameObject _badge;      // red count bubble (hidden when count == 0)
        private Button _button;
        private Text _badgeText;
        private int _shownCount = -1;
        private Coroutine _pulse;

        // ── auto-spawn onto the HUD canvas ──
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
                if (EventManager.Instance == null) return; // MainMenu has no core systems
                if (FindFirstObjectByType<QuizNotificationHUD>() != null) return;
                var canvas = FindBestCanvas();
                if (canvas == null) return;
                var go = new GameObject("QuizNotificationHUD (auto)");
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<QuizNotificationHUD>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuizNotificationHUD] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
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

        private void Awake() => _font = LoadFont();

        private void OnEnable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnQuizAnswered += HandleQuizAnswered;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnQuizAnswered -= HandleQuizAnswered;
        }

        private void Start()
        {
            Build();
            Refresh();
        }

        private void HandleDayStarted(int day, bool timed) => Refresh();
        private void HandleQuizAnswered(string quizId, bool correct) => Refresh();

        private void Refresh()
        {
            if (_iconRoot == null) return;
            int count = CodexQuizManager.Instance != null ? CodexQuizManager.Instance.AnswerableCount : 0;

            _iconRoot.SetActive(DebugAlwaysShow || count > 0);
            if (_badge != null) _badge.SetActive(count > 0);
            if (count > 0 && _badgeText != null) _badgeText.text = count > 9 ? "9+" : count.ToString();

            if (count > _shownCount && count > 0) Pulse(); // a new quiz became available
            _shownCount = count;
        }

        private void OpenCodex()
        {
            if (CodexPanelUI.Instance != null) CodexPanelUI.Instance.OpenFromHud();
        }

        // ── build the icon (bottom-right, above the record icon) ──
        private void Build()
        {
            // Parent the icon straight to the Canvas so its bottom-right anchoring is relative to the full
            // screen rect (robust regardless of this component's own GameObject transform type).
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _iconRoot = new GameObject("QuizIcon", typeof(RectTransform), typeof(Image), typeof(Button));
            _iconRoot.transform.SetParent(parent, false);
            var rt = _iconRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-28f, 150f); // stacked above the record icon; tweak if it overlaps
            rt.sizeDelta = new Vector2(60f, 60f);

            var bg = _iconRoot.GetComponent<Image>();
            bg.color = CIcon;
            var outline = _iconRoot.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, 2f); outline.useGraphicAlpha = false;

            _button = _iconRoot.GetComponent<Button>();
            _button.onClick.AddListener(OpenCodex);

            var q = NewText("Glyph", _iconRoot.transform, "?", 40, new Color(0.95f, 0.97f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(q.rectTransform);
            q.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            q.raycastTarget = false;

            // red badge (top-right corner)
            _badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            var badge = _badge;
            badge.transform.SetParent(_iconRoot.transform, false);
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(6f, 6f);
            brt.sizeDelta = new Vector2(26f, 26f);
            badge.GetComponent<Image>().color = CBadge;
            badge.GetComponent<Image>().raycastTarget = false;

            _badgeText = NewText("Count", badge.transform, "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(_badgeText.rectTransform);
            _badgeText.raycastTarget = false;

            _iconRoot.SetActive(false);
        }

        private void Pulse()
        {
            if (_iconRoot == null) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine(_iconRoot.transform));
        }

        // grow-then-settle (unscaled — works while the game is paused)
        private IEnumerator PulseRoutine(Transform t)
        {
            float half = Mathf.Max(0.01f, pulseSeconds * 0.5f);
            float e = 0f;
            while (e < half) { e += Time.unscaledDeltaTime; t.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, e / half); yield return null; }
            e = 0f;
            while (e < half) { e += Time.unscaledDeltaTime; t.localScale = Vector3.one * Mathf.Lerp(pulseScale, 1f, e / half); yield return null; }
            t.localScale = Vector3.one;
            _pulse = null;
        }

        // ── helpers ──
        private Text NewText(string name, Transform parent, string text, int size, Color col, TextAnchor anchor, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Font LoadFont()
        {
            return UIFonts.Body;
        }
    }
}
