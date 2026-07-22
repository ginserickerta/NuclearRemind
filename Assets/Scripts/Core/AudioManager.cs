using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Game-wide audio (★ 2026-07-22, owner-picked clips in Resources/Audio):
    ///   • BGM per scene — bgm_menu on MainMenu, bgm_game everywhere else (starts under the intro
    ///     cards, which run on Gamescene load). Crossfades between tracks, volume in PlayerPrefs.
    ///   • A small "เพลง − % +" widget, built in code on the main menu only, adjusts BGM volume.
    ///   • Click + hover SFX on EVERY Button — a periodic sweep attaches UIButtonSfx to each one
    ///     (same pattern as ThaiGlyphFixer), so no per-panel wiring and code-built popups get it too.
    ///   • PlayAlert() — the bottom-right alert feed (AlertController calls it on spawn).
    ///
    /// Auto-spawns on every scene including the menu; survives scene loads (DontDestroyOnLoad).
    /// SFX play through PlayOneShot on an unpaused source, and BGM fades use unscaled time, so
    /// pausing the sim never chokes the audio.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string PrefBgmVolume = "nrm_bgm_volume";
        private const float DefaultBgmVolume = 0.7f;
        private const float VolumeStep = 0.1f;
        private const float CrossfadeDur = 1.5f;
        private const int SweepFrames = 10;
        private const float SfxVolume = 0.9f;
        private const float HoverVolume = 0.45f; // hover fires often — keep it under the clicks
        private const float AlertMinGap = 0.25f; // a burst of alerts should not machine-gun the sting

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioManager (auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }

        private AudioClip _bgmMenu, _bgmGame, _click, _hover, _alert;
        private AudioSource _bgmA, _bgmB, _sfx;
        private AudioSource _activeBgm;
        private Coroutine _fade;
        private float _bgmVolume;
        private float _lastAlertTime = -10f;
        private int _frame;
        private GameObject _volumeWidget;
        private Text _volumeLabel;

        public float BgmVolume => _bgmVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _bgmMenu = Resources.Load<AudioClip>("Audio/bgm_menu");
            _bgmGame = Resources.Load<AudioClip>("Audio/bgm_game");
            _click   = Resources.Load<AudioClip>("Audio/sfx_click");
            _hover   = Resources.Load<AudioClip>("Audio/sfx_hover");
            _alert   = Resources.Load<AudioClip>("Audio/sfx_alert");
            _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefBgmVolume, DefaultBgmVolume));

            _bgmA = gameObject.AddComponent<AudioSource>();
            _bgmB = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _bgmA, _bgmB })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.ignoreListenerPause = true;
            }
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.ignoreListenerPause = true;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplySceneAudio(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => ApplySceneAudio(s.name);

        // ─────────────────────────────────────────
        //  BGM
        // ─────────────────────────────────────────

        private void ApplySceneAudio(string sceneName)
        {
            bool isMenu = sceneName == "MainMenu";
            PlayBgm(isMenu ? _bgmMenu : _bgmGame);

            if (isMenu) BuildVolumeWidget();
            else if (_volumeWidget != null) Destroy(_volumeWidget);
        }

        private void PlayBgm(AudioClip clip)
        {
            if (clip == null) return;
            if (_activeBgm != null && _activeBgm.clip == clip) return;

            var from = _activeBgm;
            var to = _activeBgm == _bgmA ? _bgmB : _bgmA;
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            _activeBgm = to;

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(from, to));
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float fromStart = from != null ? from.volume : 0f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / CrossfadeDur)
            {
                float e = t * t * (3f - 2f * t);
                to.volume = _bgmVolume * e;
                if (from != null) from.volume = fromStart * (1f - e);
                yield return null;
            }
            to.volume = _bgmVolume;
            if (from != null) { from.Stop(); from.clip = null; }
            _fade = null;
        }

        public void AdjustBgmVolume(float delta)
        {
            _bgmVolume = Mathf.Clamp01(_bgmVolume + delta);
            PlayerPrefs.SetFloat(PrefBgmVolume, _bgmVolume);
            PlayerPrefs.Save();
            if (_fade == null && _activeBgm != null) _activeBgm.volume = _bgmVolume;
            if (_volumeLabel != null) _volumeLabel.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}%";
        }

        // ─────────────────────────────────────────
        //  SFX
        // ─────────────────────────────────────────

        public void PlayClick() { if (_click != null) _sfx.PlayOneShot(_click, SfxVolume); }
        public void PlayHover() { if (_hover != null) _sfx.PlayOneShot(_hover, HoverVolume); }

        public void PlayAlert()
        {
            if (_alert == null || Time.unscaledTime - _lastAlertTime < AlertMinGap) return;
            _lastAlertTime = Time.unscaledTime;
            _sfx.PlayOneShot(_alert, SfxVolume);
        }

        // sweep: every Button in the scene gets a UIButtonSfx exactly once (inactive included,
        // so popups are wired before their first show)
        private void LateUpdate()
        {
            if (_frame++ % SweepFrames != 0) return;
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in buttons)
                if (b.GetComponent<UIButtonSfx>() == null)
                    b.gameObject.AddComponent<UIButtonSfx>();
        }

        // ─────────────────────────────────────────
        //  main-menu volume widget (code-built — no scene wiring)
        // ─────────────────────────────────────────

        private void BuildVolumeWidget()
        {
            if (_volumeWidget != null) return;
            var font = UIFonts.Body;

            _volumeWidget = new GameObject("BgmVolumeWidget");
            var canvas = _volumeWidget.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _volumeWidget.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _volumeWidget.AddComponent<GraphicRaycaster>();
            // the widget lives only while the menu scene does
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                _volumeWidget, UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            var root = new GameObject("Row", typeof(RectTransform));
            root.transform.SetParent(_volumeWidget.transform, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 0f);
            rrt.anchoredPosition = new Vector2(-24, 24);
            rrt.sizeDelta = new Vector2(280, 44);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.09f, 0.72f);

            Text Label(string name, string txt, float x, float w, int size, TextAnchor anchor)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(w, 40);
                var t = go.AddComponent<Text>();
                t.font = font; t.fontSize = size; t.color = new Color(0.88f, 0.9f, 0.86f);
                t.alignment = anchor; t.text = txt;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                return t;
            }

            Button Btn(string name, string txt, float x)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0);
                rt.sizeDelta = new Vector2(40, 34);
                var img = go.AddComponent<Image>();
                img.color = new Color(0.18f, 0.2f, 0.24f, 0.95f);
                var b = go.AddComponent<Button>();
                b.targetGraphic = img;
                var label = Label(name + "Label", txt, 0, 40, 24, TextAnchor.MiddleCenter);
                label.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                return b;
            }

            Label("Title", "เพลง", 14, 70, 20, TextAnchor.MiddleLeft);
            Btn("Minus", "−", 88).onClick.AddListener(() => AdjustBgmVolume(-VolumeStep));
            _volumeLabel = Label("Value", $"{Mathf.RoundToInt(_bgmVolume * 100)}%", 136, 70, 20, TextAnchor.MiddleCenter);
            Btn("Plus", "+", 214).onClick.AddListener(() => AdjustBgmVolume(+VolumeStep));
        }
    }

    /// <summary>
    /// Per-button pointer SFX — attached automatically by AudioManager's sweep. Plays hover on
    /// pointer-enter and click on pointer-click, only while the button is interactable.
    /// </summary>
    public class UIButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private Button _btn;

        private void Awake() => _btn = GetComponent<Button>();

        private bool Usable => _btn == null || (_btn.interactable && _btn.enabled);

        public void OnPointerEnter(PointerEventData e)
        {
            if (Usable) AudioManager.Instance?.PlayHover();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Usable)
                AudioManager.Instance?.PlayClick();
        }
    }
}
