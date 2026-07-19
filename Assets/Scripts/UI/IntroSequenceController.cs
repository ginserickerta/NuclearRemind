using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Opening intro (STORY.md §①) — three black-screen text cards shown once at the start of a new game,
    /// before the player touches the city. NOT a cutscene: plain centered text on black · click / Space /
    /// Enter advances · Esc skips. Each advance CROSSFADES the text (fade out + slide up → swap → fade in)
    /// on the black backdrop; the final card fades the whole screen out into gameplay.
    ///
    /// The hidden threads (the six who died, the report Auren sent) are deliberately NOT spoken here —
    /// the player uncovers them through the Memorial and the Elara Records (STORY.md §②/§③).
    ///
    /// ★ v6.3 cutover: self-spawns on the game scene (same RuntimeInitializeOnLoadMethod + sceneLoaded
    ///   pattern as WorkerManager/CardManager). The main menu only ever starts a fresh game (there is no
    ///   Continue), so this shows on every fresh Gamescene load. Builds its own top-most Canvas — no scene
    ///   wiring required. A full-screen raycast-blocking backdrop suppresses the city's click handlers
    ///   (they bail on EventSystem.IsPointerOverGameObject) while the intro is up.
    /// </summary>
    public class IntroSequenceController : MonoBehaviour
    {
        // STORY.md §① — verbatim card text.
        private static readonly string[] Cards =
        {
            "ปี 2157 — โลกหมดพลังงานสะอาด\nหลัง CORE TOWER ทั่วโลกระเบิดพร้อมกัน",
            "เหลือหอคอยแห่งสุดท้ายที่เมืองร้าง Veltara\nสร้างค้างไว้ที่ 30%",
            "คุณคือ Auren Vasek วิศวกรคนสุดท้ายที่รอด\nภารกิจ: สร้างมันให้เสร็จ",
        };

        private const float FadeDur = 0.30f; // per-card fade (seconds, unscaled)
        private const float SlideY = 22f;     // subtle vertical drift during the fade

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
                // EventManager exists only inside the game scene (the menu destroys it) — this guards MainMenu.
                if (EventManager.Instance == null) return;
                if (FindFirstObjectByType<IntroSequenceController>() != null) return;
                new GameObject("IntroSequenceController (auto)").AddComponent<IntroSequenceController>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IntroSequenceController] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private enum Phase { In, Hold, Out }

        private Font _font;
        private Canvas _canvas;
        private CanvasGroup _group;      // whole-screen (dismiss fade)
        private CanvasGroup _bodyGroup;  // card text (per-card crossfade)
        private Text _bodyText;
        private Text _hintText;

        private int _index;
        private Phase _phase;
        private float _t;                // 0..1 progress within the current In/Out fade
        private float _fade = 1f;        // whole-canvas alpha while dismissing
        private bool _dismissing;

        private void Start()
        {
            _font = LoadFont();
            Build();
            Show(0);
        }

        private void Build()
        {
            var go = new GameObject("IntroCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000; // above every gameplay panel

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true; // eat clicks so they don't reach the city behind

            // opaque black backdrop (raycast target → city click handlers see IsPointerOverGameObject)
            var bg = NewImage("Black", go.transform, Color.black);
            Stretch(bg.rectTransform);

            // centered body text, wrapped in its own CanvasGroup for the crossfade
            var bodyHolder = new GameObject("Body", typeof(RectTransform));
            bodyHolder.transform.SetParent(go.transform, false);
            var brt = bodyHolder.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1500, 400);
            brt.anchoredPosition = Vector2.zero;
            _bodyGroup = bodyHolder.AddComponent<CanvasGroup>();

            _bodyText = NewText("Text", bodyHolder.transform, "", 46, new Color(0.92f, 0.94f, 0.90f), TextAnchor.MiddleCenter);
            Stretch(_bodyText.rectTransform);

            // advance hint, bottom (stays visible through fades)
            _hintText = NewText("Hint", go.transform, "", 22, new Color(0.55f, 0.6f, 0.52f), TextAnchor.LowerCenter);
            var hrt = _hintText.rectTransform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0f);
            hrt.sizeDelta = new Vector2(900, 40);
            hrt.anchoredPosition = new Vector2(0, 70);
        }

        // Load card i and start its fade-in.
        private void Show(int i)
        {
            _index = i;
            if (_bodyText != null) _bodyText.text = Cards[Mathf.Clamp(i, 0, Cards.Length - 1)];
            if (_hintText != null)
                _hintText.text = (i >= Cards.Length - 1)
                    ? "คลิกซ้าย / Spacebar เพื่อเริ่ม · Esc ข้าม"
                    : "คลิกซ้าย / Spacebar เพื่อไปต่อ · Esc ข้าม";
            _phase = Phase.In;
            _t = 0f;
            if (_bodyGroup != null) _bodyGroup.alpha = 0f;
            SetBodyY(SlideY);
        }

        private void Update()
        {
            if (_dismissing)
            {
                _fade -= Time.unscaledDeltaTime / FadeDur; // unscaled — works even if the game is paused
                if (_group != null) _group.alpha = Mathf.Clamp01(_fade);
                if (_fade <= 0f)
                {
                    // V01 "มืดสนิท เริ่มจากไฟก่อน" — BARKS.md says "เข้าเกมครั้งแรก", i.e. the moment the
                    // intro clears into the city, not the end of day 1. Fire() is once-only, so the
                    // day-end backstop in InnerVoiceDirector stays harmless if the intro never ran.
                    InnerVoiceDirector.Instance?.Fire("V01");
                    Destroy(gameObject);
                }
                return;
            }

            float step = FadeDur > 0f ? Time.unscaledDeltaTime / FadeDur : 1f;

            if (_phase == Phase.In)
            {
                _t = Mathf.Min(1f, _t + step);
                float e = Ease(_t);
                if (_bodyGroup != null) _bodyGroup.alpha = e;
                SetBodyY(Mathf.Lerp(SlideY, 0f, e));
                if (_t >= 1f) _phase = Phase.Hold;
            }
            else if (_phase == Phase.Out)
            {
                _t = Mathf.Min(1f, _t + step);
                float e = Ease(_t);
                if (_bodyGroup != null) _bodyGroup.alpha = 1f - e;
                SetBodyY(Mathf.Lerp(0f, -SlideY, e));
                if (_t >= 1f) Show(_index + 1); // fade the next card in
            }

            // input — Esc skips the whole intro; click/Space/Enter advances (allowed while In or Hold)
            if (Input.GetKeyDown(KeyCode.Escape)) { Dismiss(); return; }

            bool next = Input.GetMouseButtonDown(0)
                        || Input.GetKeyDown(KeyCode.Space)
                        || Input.GetKeyDown(KeyCode.Return)
                        || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (next) Advance();
        }

        private void Advance()
        {
            if (_phase == Phase.Out) return;            // already crossfading — ignore
            if (_index >= Cards.Length - 1) { Dismiss(); return; } // last card → fade to gameplay
            _phase = Phase.Out;
            _t = 0f;
        }

        private void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            if (_group != null) _group.blocksRaycasts = false;
        }

        private void SetBodyY(float y)
        {
            if (_bodyText == null) return;
            var rt = (RectTransform)_bodyText.transform.parent; // the body holder
            if (rt != null) rt.anchoredPosition = new Vector2(0f, y);
        }

        // smoothstep — ease in/out for a soft fade
        private static float Ease(float t) => t * t * (3f - 2f * t);

        // ── tiny self-contained UI helpers (no scene deps) ──
        private static Image NewImage(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go.GetComponent<Image>();
        }

        private Text NewText(string name, Transform parent, string text, int size, Color col, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.2f;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Font LoadFont()
        {
            // Chakra Petch (Cadson Demak) — Thai + Latin, techy/sci-fi cut that suits the intro cards.
            return UIFonts.Body;
        }
    }
}
