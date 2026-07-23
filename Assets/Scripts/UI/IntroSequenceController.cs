using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: อินโทรเปิดเกม (STORY.md §①) — การ์ดข้อความบนจอดำ 7 ใบ เล่นครั้งเดียวตอนเริ่มเกมใหม่
    /// [TH] คลิก/Space = ไปการ์ดถัดไป (ล็อกขั้นต่ำ 2 วิ/ใบ) · กดค้าง = ข้ามทั้งหมด (มีแถบ progress)
    /// [TH] spawn ตัวเองอัตโนมัติตอนโหลด Gamescene สร้าง Canvas ของตัวเอง ไม่ต้อง wire ในซีน
    ///
    /// Opening intro (STORY.md §①) — black-screen text cards shown once at the start of a new game,
    /// before the player touches the city. NOT a cutscene: plain centered text on black. Each advance
    /// CROSSFADES the text (fade out + slide up → swap → fade in); the final card fades the whole
    /// screen out into gameplay.
    ///
    /// ★ 2026-07-22 pacing rules (owner spec):
    ///   · each card must be on screen ≥ 2 s before a tap can advance it (the hint fades in when ready)
    ///   · HOLD (mouse / Space / Enter / Esc) skips the whole intro — a progress bar fills while holding
    ///   · long cards automatically drop to a smaller font so they never overflow the safe box
    ///
    /// ★ v6.3 cutover: self-spawns on the game scene (same RuntimeInitializeOnLoadMethod + sceneLoaded
    ///   pattern as WorkerManager/CardManager). The main menu only ever starts a fresh game (there is no
    ///   Continue), so this shows on every fresh Gamescene load. Builds its own top-most Canvas — no scene
    ///   wiring required. A full-screen raycast-blocking backdrop suppresses the city's click handlers
    ///   (they bail on EventSystem.IsPointerOverGameObject) while the intro is up.
    /// </summary>
    public class IntroSequenceController : MonoBehaviour
    {
        // STORY.md §① — verbatim card text (2026-07-22 full 7-card opening).
        private static readonly string[] Cards =
        {
            "ปี 2157",

            "โลกเข้าสู่วิกฤตพลังงาน หลังสองศตวรรษแห่งการเผาผลาญเชื้อเพลิงฟอสซิลเกินขีดจำกัด",

            "สหพันธ์ Aethon และ Keran สองมหาอำนาจร่วมมือกันสร้าง CORE TOWER\nปฏิกรณ์ฟิวชันความหวังสุดท้ายของมนุษยชาติ",

            "แต่ความโลภผลักดันให้ทุกอย่างเกินขีดจำกัด\nCORE TOWER ทั่วโลกระเบิดพร้อมกัน โลกดับมืด กัมมันตรังสีปกคลุมแผ่นดิน",

            "ผมคือ Dr. Auren Vasek วิศวกรนิวเคลียร์วัย 35 ปี ผู้รอดชีวิตเพียงหนึ่งเดียวจากทีมภารกิจ CORE TOWER\n\nในวันนั้น ผมรอดมาได้ แต่ไม่เคยให้อภัยตัวเอง เพราะสัญญาณเตือนที่ผมเลือกนิ่งเงียบ คือสิ่งที่ทำให้เพื่อนร่วมทีมทั้งหกต้องจบชีวิตลง",

            "วันนี้ Aethon และ Keran บีบให้ผมกลับไปที่ Veltara เมืองร้างที่ CORE TOWER แห่งสุดท้ายยังสร้างไม่เสร็จ\n\nพวกเขาบอกว่าผมมาเพื่อแก้ความผิดพลาดของมหาอำนาจ",

            "แต่สำหรับผม...\nนี่คือโอกาสเดียวที่จะขอโทษคนที่ผมทิ้งไว้ข้างหลัง",
        };

        private const float FadeDur = 0.30f;      // per-card fade (seconds, unscaled)
        private const float SlideY = 22f;         // subtle vertical drift during the fade
        private const float AdvanceDelay = 2f;    // each card locks taps for this long
        private const float HoldSkipDur = 1.2f;   // hold this long to skip the whole intro
        private const float TapMax = 0.3f;        // a press released faster than this counts as a tap
        private const float BarWidth = 320f;      // skip progress bar width

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
        private CanvasGroup _hintGroup;  // advance hint — hidden until the 2 s gate opens
        private CanvasGroup _barGroup;   // hold-to-skip progress bar — visible only while holding
        private Text _bodyText;
        private Text _hintText;
        private RectTransform _barFill;

        private int _index;
        private Phase _phase;
        private float _t;                // 0..1 progress within the current In/Out fade
        private float _fade = 1f;        // whole-canvas alpha while dismissing
        private bool _dismissing;
        private float _cardTime;         // seconds the current card has been on screen (gates taps)
        private float _holdTime;         // how long the skip gesture has been held
        private bool _wasHeld;

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
            brt.sizeDelta = new Vector2(1500, 500);
            brt.anchoredPosition = Vector2.zero;
            _bodyGroup = bodyHolder.AddComponent<CanvasGroup>();

            _bodyText = NewText("Text", bodyHolder.transform, "", 46, new Color(0.92f, 0.94f, 0.90f), TextAnchor.MiddleCenter);
            Stretch(_bodyText.rectTransform);

            // advance hint, bottom — kept invisible until the per-card tap gate opens
            var hintHolder = new GameObject("HintHolder", typeof(RectTransform));
            hintHolder.transform.SetParent(go.transform, false);
            var hhrt = hintHolder.GetComponent<RectTransform>();
            hhrt.anchorMin = hhrt.anchorMax = hhrt.pivot = new Vector2(0.5f, 0f);
            hhrt.sizeDelta = new Vector2(900, 40);
            hhrt.anchoredPosition = new Vector2(0, 70);
            _hintGroup = hintHolder.AddComponent<CanvasGroup>();
            _hintGroup.alpha = 0f;
            _hintText = NewText("Hint", hintHolder.transform, "", 22, new Color(0.55f, 0.6f, 0.52f), TextAnchor.LowerCenter);
            Stretch(_hintText.rectTransform);

            // hold-to-skip progress bar (above the hint) — fades in only while the gesture is held
            var barHolder = new GameObject("SkipBar", typeof(RectTransform));
            barHolder.transform.SetParent(go.transform, false);
            var bhrt = barHolder.GetComponent<RectTransform>();
            bhrt.anchorMin = bhrt.anchorMax = bhrt.pivot = new Vector2(0.5f, 0f);
            bhrt.sizeDelta = new Vector2(BarWidth, 46);
            bhrt.anchoredPosition = new Vector2(0, 118);
            _barGroup = barHolder.AddComponent<CanvasGroup>();
            _barGroup.alpha = 0f;
            _barGroup.blocksRaycasts = false;

            var barLabel = NewText("Label", barHolder.transform, "ข้ามอินโทร", 18, new Color(0.7f, 0.72f, 0.66f), TextAnchor.UpperCenter);
            var blrt = barLabel.rectTransform;
            blrt.anchorMin = new Vector2(0f, 1f); blrt.anchorMax = new Vector2(1f, 1f); blrt.pivot = new Vector2(0.5f, 1f);
            blrt.sizeDelta = new Vector2(0, 24); blrt.anchoredPosition = Vector2.zero;

            var barBg = NewImage("Bg", barHolder.transform, new Color(1f, 1f, 1f, 0.14f));
            var bbrt = barBg.rectTransform;
            bbrt.anchorMin = new Vector2(0f, 0f); bbrt.anchorMax = new Vector2(1f, 0f); bbrt.pivot = new Vector2(0.5f, 0f);
            bbrt.sizeDelta = new Vector2(0, 10); bbrt.anchoredPosition = Vector2.zero;

            var fill = NewImage("Fill", barBg.transform, new Color(0.96f, 0.62f, 0.20f, 0.95f));
            _barFill = fill.rectTransform;
            _barFill.anchorMin = new Vector2(0f, 0f); _barFill.anchorMax = new Vector2(0f, 1f); _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.sizeDelta = new Vector2(0f, 0f);
            _barFill.anchoredPosition = Vector2.zero;
        }

        // การ์ดยาวใช้ฟอนต์เล็กลงอัตโนมัติ ให้พอดีกล่อง 1500×500 เสมอ (legacy Text ไม่มี auto-fit)
        // Long cards shrink so they always fit the 1500×500 safe box (legacy Text has no auto-fit).
        private static int FontSizeFor(string card)
        {
            int len = card.Length;
            if (len <= 12) return 72;   // the year card — a title beat
            if (len <= 90) return 46;
            if (len <= 150) return 40;
            return 33;
        }

        // โหลดการ์ดใบที่ i แล้วเริ่มเฟดเข้า (รีเซ็ตตัวจับเวลา gate 2 วิ)
        // Load card i and start its fade-in.
        private void Show(int i)
        {
            _index = i;
            string card = Cards[Mathf.Clamp(i, 0, Cards.Length - 1)];
            if (_bodyText != null)
            {
                _bodyText.text = card;
                _bodyText.fontSize = FontSizeFor(card);
            }
            if (_hintText != null)
                _hintText.text = (i >= Cards.Length - 1)
                    ? "คลิกซ้าย / Spacebar เพื่อเริ่ม · กดค้างเพื่อข้ามทั้งหมด"
                    : "คลิกซ้าย / Spacebar เพื่อไปต่อ · กดค้างเพื่อข้ามทั้งหมด";
            _phase = Phase.In;
            _t = 0f;
            _cardTime = 0f;
            if (_bodyGroup != null) _bodyGroup.alpha = 0f;
            if (_hintGroup != null) _hintGroup.alpha = 0f;
            SetBodyY(SlideY);
        }

        // ขับเคลื่อนทั้งลำดับ: เฟดเข้า/ออกของการ์ด + อ่าน input (แตะสั้น=ไปต่อ · กดค้าง=ข้าม) + แถบ skip
        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // unscaled — works even if the game is paused

            if (_dismissing)
            {
                _fade -= dt / FadeDur;
                if (_group != null) _group.alpha = Mathf.Clamp01(_fade);
                if (_fade <= 0f)
                {
                    // ★ 2026-07-22: V01 ("มืดสนิท เริ่มจากไฟก่อน") no longer fires here — the owner cut
                    // the opening dialogue box entirely; the new 7-card intro already covers the beat.
                    Destroy(gameObject);
                }
                return;
            }

            float step = FadeDur > 0f ? dt / FadeDur : 1f;
            _cardTime += dt;

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

            // the tap gate: hint fades in once this card has been readable for AdvanceDelay seconds
            bool tapReady = _cardTime >= AdvanceDelay && _phase != Phase.Out;
            if (_hintGroup != null)
                _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, tapReady ? 1f : 0f, dt * 4f);

            // ── input: one gesture, two meanings ──
            // quick tap (released under TapMax) advances the current card once the 2 s gate is open;
            // holding HoldSkipDur fills the bar and skips the whole intro. Esc joins the hold gesture
            // (the old instant-Esc skip bypassed the reading gate the pacing is built around).
            bool held = Input.GetMouseButton(0)
                        || Input.GetKey(KeyCode.Space)
                        || Input.GetKey(KeyCode.Return)
                        || Input.GetKey(KeyCode.KeypadEnter)
                        || Input.GetKey(KeyCode.Escape);

            if (held)
            {
                _holdTime += dt;
                if (_holdTime >= HoldSkipDur) { Dismiss(); return; }
            }
            else
            {
                if (_wasHeld && _holdTime < TapMax && tapReady) Advance();
                _holdTime = 0f;
            }
            _wasHeld = held;

            // progress bar: appears only once the press is clearly a hold, not a tap
            if (_barGroup != null)
            {
                bool showBar = held && _holdTime > 0.15f;
                _barGroup.alpha = Mathf.MoveTowards(_barGroup.alpha, showBar ? 1f : 0f, dt * 8f);
            }
            if (_barFill != null)
                _barFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(_holdTime / HoldSkipDur), 0f);
        }

        // ไปการ์ดถัดไป (เริ่มเฟดออก) · การ์ดสุดท้าย → เฟดจอทั้งใบเข้าสู่เกม
        private void Advance()
        {
            if (_phase == Phase.Out) return;            // already crossfading — ignore
            if (_index >= Cards.Length - 1) { Dismiss(); return; } // last card → fade to gameplay
            _phase = Phase.Out;
            _t = 0f;
        }

        // ปิดอินโทรทั้งหมด: เฟดจอออกแล้ว Destroy ตัวเอง (เลิกบล็อกคลิกทันที)
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
