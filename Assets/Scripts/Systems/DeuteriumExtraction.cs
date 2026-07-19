using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Deuterium extraction is a choice the player makes, not something the Water Plant does on its own
    /// (ResearchLab_System_Spec §3, "ปุ่มสกัด Deuterium").
    ///
    /// Two gates, and they are different in kind:
    ///   • KNOWLEDGE — the `deuterium` research note must be complete. This is the v6.3 spelling of the
    ///     spec's `unlock_deuterium_button` flag; KnowledgeDB is the flag store, so no parallel one.
    ///   • SIZE — the plant must be at least BuildingData.deuteriumMinLevel. A small plant physically
    ///     cannot do it, however much you have read.
    /// Research alone is not enough, and neither is level. That is the point: knowledge is the mechanism.
    ///
    /// Then, per building, the player switches it on. Extraction eats water at deuteriumWaterPerUnit : 1,
    /// so it competes directly with drinking and with reactor cooling — turning it off during a heat
    /// crisis is a real decision, which is why the switch is per-plant and not a global setting.
    ///
    /// Default is OFF. The spec starts at deuteriumAllocation = 0, and a plant that silently began
    /// draining the reservoir the moment research landed would be exactly the "free choice" rule #5
    /// forbids. The trade-off is that the player has to be told the button exists — hence the notice.
    /// </summary>
    [DefaultExecutionOrder(-60)]  // before ResourceManager reads the switch during day production
    public class DeuteriumExtraction : MonoBehaviour
    {
        public static DeuteriumExtraction Instance { get; private set; }

        /// <summary>The research note that unlocks the button (Resources/ResearchNotes/deuterium.asset).</summary>
        public const string NoteId = "deuterium";

        private readonly HashSet<Vector2Int> _enabled = new HashSet<Vector2Int>();
        private bool _announced;   // the "button is available" notice fires once per run
        private bool _subscribed;

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
                if (EventManager.Instance == null) return;   // menu scene — nothing to run
                if (FindFirstObjectByType<DeuteriumExtraction>() != null) return;
                new GameObject("DeuteriumExtraction (auto)").AddComponent<DeuteriumExtraction>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DeuteriumExtraction] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnResearchNoteCompleted += HandleNoteCompleted;
            EventManager.Instance.OnBuildingRemoved += HandleDemolished;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnResearchNoteCompleted -= HandleNoteCompleted;
            EventManager.Instance.OnBuildingRemoved -= HandleDemolished;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            _subscribed = false;
        }

        // ── gates ─────────────────────────────────────────────────────────────

        /// <summary>Has the player researched extraction at all? (the spec's unlock_deuterium_button)</summary>
        public static bool Researched
            => KnowledgeDB.Instance != null && KnowledgeDB.Instance.HasNote(NoteId);

        /// <summary>Lowest level this building can extract at — falls back to max level (old behaviour).</summary>
        public static int MinLevelFor(BuildingData data)
        {
            int max = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.maxBuildingLevel : 3;
            if (data == null) return max;
            return data.deuteriumMinLevel > 0 ? data.deuteriumMinLevel : max;
        }

        /// <summary>Output per day at this level — 0 below the minimum, the min-level rate below max, full at max.</summary>
        public static float RateFor(BuildingData data, int level)
        {
            if (data == null || data.deuteriumProduction <= 0f) return 0f;
            int max = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.maxBuildingLevel : 3;
            if (level >= max) return data.deuteriumProduction;
            if (level >= MinLevelFor(data)) return data.deuteriumProductionMinLevel;
            return 0f;
        }

        /// <summary>Both gates passed — the button may be shown as usable (it may still be switched off).</summary>
        public static bool Unlocked(BuildingData data, int level)
            => Researched && RateFor(data, level) > 0f;

        // ── per-plant switch ──────────────────────────────────────────────────

        public bool IsOn(Vector2Int cell) => _enabled.Contains(cell);

        /// <summary>Returns the new state. Refused (and unchanged) while either gate is closed.</summary>
        public bool SetOn(Vector2Int cell, bool on, BuildingData data, int level)
        {
            if (on && !Unlocked(data, level)) return IsOn(cell);
            if (on) _enabled.Add(cell); else _enabled.Remove(cell);
            return on;
        }

        public bool Toggle(Vector2Int cell, BuildingData data, int level)
            => SetOn(cell, !IsOn(cell), data, level);

        /// <summary>What ResourceManager asks each day: is this plant actually extracting right now?</summary>
        public bool IsExtracting(Vector2Int cell, BuildingData data, int level)
            => IsOn(cell) && Unlocked(data, level);

        // ── events ────────────────────────────────────────────────────────────

        // Tell the player the button now exists. Without this the feature is invisible: nothing on screen
        // changes when the research lands, and a switch nobody knows about is the same as no switch.
        private void HandleNoteCompleted(string noteId)
        {
            if (noteId != NoteId || _announced) return;
            _announced = true;
            EventManager.Instance?.RaiseNotice(
                "🧪 ปลดล็อกการสกัดดิวเทอเรียม — เปิดสวิตช์ได้ที่แผงโรงน้ำ (กินน้ำ แลกกับเชื้อเพลิงเตา)");
        }

        // A demolished plant must not keep its switch: the cell can be rebuilt with something else.
        private void HandleDemolished(Vector2Int cell) => _enabled.Remove(cell);

        // ── save / load ───────────────────────────────────────────────────────

        public void WriteTo(SaveData save)
        {
            if (save == null) return;
            save.deuteriumExtractCells = new List<Vector2Int>(_enabled);
            save.deuteriumUnlockAnnounced = _announced;
        }

        private void HandleSaveLoaded(SaveData save) => RestoreFromSave(save);

        /// <summary>Restore body — public so EditMode tests can drive it without a live subscription.</summary>
        public void RestoreFromSave(SaveData save)
        {
            if (save == null) return;
            _enabled.Clear();
            if (save.deuteriumExtractCells != null)
                foreach (var c in save.deuteriumExtractCells) _enabled.Add(c);
            _announced = save.deuteriumUnlockAnnounced;
        }

        public void ResetForNewRun()
        {
            _enabled.Clear();
            _announced = false;
        }
    }
}
