using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Routes every spoken line — NPC barks and Auren's inner voice — into the VN dialogue panel
    /// (StoryDialoguePanel / DialogueUIController) instead of the bottom-left toast feed.
    ///
    /// The panel was built for the legacy StoryDirector, which v6.3 retired (LegacyNarrativeSilencer).
    /// Nothing has driven it since; BarkManager and InnerVoiceDirector both speak through the same
    /// OnBarkFired event, so one adapter is enough to give them portraits, name plates and framed
    /// speech boxes without touching either manager.
    ///
    /// Two things this has to get right:
    ///
    ///   • BATCHING. A day-end can fire up to 2 barks plus several one-shot inner-voice lines, all in
    ///     the same frame. Sent one at a time each would re-open the panel and clobber the one before
    ///     it, so lines are collected for a frame and shown as ONE clickable sequence.
    ///
    ///   • YIELDING. The panel is modal and pauses the clock, and so are the record card and the crisis
    ///     card — all three on PauseReason.StoryCard. Barks are the least urgent of the three, so they
    ///     wait their turn rather than stacking modals on top of each other.
    ///
    /// If the panel is missing from the scene this router stands down entirely and BarkFeedHUD keeps
    /// showing toasts — a line is never silently dropped.
    /// </summary>
    // -16: after InnerVoiceDirector (-18) so its ▸ lines are already queued when the batch flushes.
    [DefaultExecutionOrder(-16)]
    public class BarkDialogueRouter : MonoBehaviour
    {
        public static BarkDialogueRouter Instance { get; private set; }

        /// <summary>
        /// True when spoken lines go to the dialogue panel. BarkFeedHUD reads this and stays quiet, so
        /// exactly one of the two channels is live and a line is never shown twice.
        /// </summary>
        public static bool HandlesBarks =>
            Instance != null && Instance.isActiveAndEnabled && DialogueUIController.Instance != null;

        private readonly List<BarkSO> _pending = new List<BarkSO>();
        private int _flushFrame = int.MaxValue;

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
                if (FindFirstObjectByType<BarkDialogueRouter>() != null) return;
                new GameObject("BarkDialogueRouter (auto)").AddComponent<BarkDialogueRouter>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BarkDialogue] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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

        private void HandleBarkFired(BarkSO bark)
        {
            if (bark == null || string.IsNullOrEmpty(bark.text)) return;
            if (DialogueUIController.Instance == null) return; // no panel — BarkFeedHUD has it

            _pending.Add(bark);
            // Collect everything spoken this frame, then show it as one sequence next frame.
            if (_flushFrame == int.MaxValue) _flushFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (_pending.Count == 0 || Time.frameCount < _flushFrame) return;
            TryShow();
        }

        /// <summary>
        /// Show the queued lines if nothing more important owns the screen.
        ///
        /// Polled rather than driven by a "modal closed" event on purpose: the three modals close
        /// through different paths (dialogue → OnStoryCardDismissed, crisis → ResolveOption, record →
        /// archive/ack), and a router that listened for only some of them would sit on a queued line
        /// forever after the one it missed. A per-frame check on a list that is almost always empty is
        /// cheaper than being wrong.
        /// </summary>
        private void TryShow()
        {
            var ui = DialogueUIController.Instance;
            if (ui == null) return;
            if (ui.IsShowing) return;                       // a sequence is still being clicked through
            if (CrisisCardPanelUI.IsShowing) return;        // a decision outranks ambient talk
            if (RecordCardUI.Instance != null && RecordCardUI.Instance.IsShowing) return;

            var lines = new DialogueLine[_pending.Count];
            for (int i = 0; i < _pending.Count; i++) lines[i] = ToLine(_pending[i]);
            _pending.Clear();
            _flushFrame = int.MaxValue;

            EventManager.Instance?.RaiseStoryDialogueShown(lines);
        }

        /// <summary>
        /// A bark carries no emotion, so pick the one that matches how the line is used: Auren's inner
        /// voice is written as her thinking it (and auren_thinking is the portrait the scene setup
        /// loads), while an NPC saying a fact out loud reads Neutral.
        /// </summary>
        private static DialogueLine ToLine(BarkSO b) => new DialogueLine
        {
            speaker = ToSpeaker(b.speaker),
            textTH = b.text,
            emotion = b.speaker == BarkSpeaker.Auren ? Emotion.Thinking : Emotion.Neutral,
        };

        private static Speaker ToSpeaker(BarkSpeaker s)
        {
            switch (s)
            {
                case BarkSpeaker.Kova:    return Speaker.Kova;
                case BarkSpeaker.Mira:    return Speaker.Mira;
                case BarkSpeaker.Dorn:    return Speaker.Dorn;
                case BarkSpeaker.Citizen: return Speaker.Citizen;
                case BarkSpeaker.Auren:   return Speaker.InnerVoice;
                default:                  return Speaker.System;
            }
        }
    }
}
