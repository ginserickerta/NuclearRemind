using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// ทรานซิชันเปลี่ยนวัน — จอเฟดดำ + ขึ้น "DAY X / N" แล้วเฟดกลับ
    /// auto-spawn ใต้ HUDCanvas (ไม่ต้องวาง component ในซีน) · โค้ดล้วน ไม่ต้องมี asset
    ///
    /// ★ ตรวจวันด้วยการ poll GameManager.CurrentDay ใน Update (ไม่ subscribe OnDayStarted)
    ///   เพราะ OnDayStarted เป็น multicast — ถ้า subscriber ตัวก่อนหน้า throw ตัวที่เหลือจะไม่ถูกเรียก
    ///   (DayTransition spawn ท้ายสุด = โดนตัดง่ายสุด) · poll ตรง ๆ จึงทนทาน ไม่ขึ้นกับ event ใคร
    /// </summary>
    public class DayTransitionUI : MonoBehaviour
    {
        // ★ AfterSceneLoad ยิงครั้งเดียวที่ซีนแรกของแอป (build = MainMenu) — ตัวที่ spawn ในเมนูถูกทำลาย
        //   ตอน LoadScene(Gamescene) → ต้อง spawn ซ้ำทุก sceneLoaded (ดูคำอธิบายเต็มที่ CoreTowerPanelUI)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        // ★ WebGL build: webGLExceptionSupport ตั้งจับเฉพาะ throw ตรง ๆ — exception จาก runtime เอง (เช่น
        //   NullReferenceException) จะทำให้เงียบสนิท ไม่มี error ขึ้น console (ดูรายละเอียดที่ CoreTowerPanelUI)
        private static void AutoSpawn()
        {
            try { AutoSpawnUnsafe(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[DayTransitionUI] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<DayTransitionUI>() != null) return;
            var canvas = FindBestCanvas();
            var go = new GameObject("DayTransitionUI (auto)");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            go.AddComponent<DayTransitionUI>();
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

        [Header("จังหวะ (วินาที)")]
        public float fadeIn = 0.4f;
        public float hold = 0.9f;
        public float fadeOut = 0.55f;
        [Tooltip("โชว์การ์ดวันแรก (Day 1) ด้วยไหม")]
        public bool showFirstDay = true;

        private const float MaxStep = 1f / 30f; // clamp deltaTime กันเฟรมกระโดด

        private CanvasGroup _cg;
        private Text _label;
        private Coroutine _running;
        private int _lastDay;

        private void Start()
        {
            Build();
            // showFirstDay → ให้ Update ยิงวันปัจจุบันทันที (วันแรก) · ไม่โชว์ → sync กับวันตอนนี้เลย
            _lastDay = showFirstDay ? int.MinValue
                     : (GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1);
        }

        private void Update()
        {
            var gm = GameManager.Instance;       // singleton (cache ใน Awake ของ GameManager) — ไม่ใช่ Find
            if (gm == null || _cg == null) return;
            if (gm.CurrentDay == _lastDay) return;

            _lastDay = gm.CurrentDay;
            _label.text = $"DAY {gm.CurrentDay} / {GameManager.MaxDay}";
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Play());
            Debug.Log($"[DayTransitionUI] เล่นทรานซิชัน Day {gm.CurrentDay}");
        }

        private IEnumerator Play()
        {
            _cg.blocksRaycasts = true;      // บล็อกคลิกทะลุระหว่างทรานซิชัน
            yield return Fade(0f, 1f, fadeIn);
            yield return WaitUnscaled(hold);
            yield return Fade(1f, 0f, fadeOut);
            _cg.blocksRaycasts = false;
            _running = null;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (dur <= 0f) { _cg.alpha = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                _cg.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            _cg.alpha = to;
        }

        private static IEnumerator WaitUnscaled(float s)
        {
            float t = 0f;
            while (t < s) { t += Mathf.Min(Time.unscaledDeltaTime, MaxStep); yield return null; }
        }

        private void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("DayTransition",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var cv = go.GetComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 1000;
            Stretch(go.GetComponent<RectTransform>());
            _cg = go.GetComponent<CanvasGroup>(); _cg.alpha = 0f; _cg.blocksRaycasts = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(go.transform, false);
            Stretch(black.GetComponent<RectTransform>());
            black.GetComponent<Image>().color = Color.black;

            var txtGO = new GameObject("DayLabel", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(go.transform, false);
            Stretch(txtGO.GetComponent<RectTransform>());
            _label = txtGO.GetComponent<Text>();
            _label.font = LoadFont();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontStyle = FontStyle.Bold;
            _label.color = new Color(0.93f, 0.89f, 0.76f);
            _label.raycastTarget = false;
            _label.resizeTextForBestFit = true;
            _label.resizeTextMinSize = 40;
            _label.resizeTextMaxSize = 130;
            _label.text = "";
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("HUD/Fonts/Kanit-Regular");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }
}
