using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>One quiz's status in the Codex list (CODEX.md §6).</summary>
    public enum QuizState { Locked, Answerable, Earned }

    public struct QuizView
    {
        public QuizQuestionSO quiz;
        public CodexEntrySO codex;
        public QuizState state;
    }

    /// <summary>Result of answering — the panel shows explanation ALWAYS, right or wrong (QUIZZES.md).</summary>
    public struct SubmitResult
    {
        public bool valid;            // false = quiz unknown / not answerable (locked or already earned)
        public bool correct;
        public string explanation;    // shown in every valid case
        public bool masteryEarned;    // true only on the first correct answer
        public string codexUnlockedId;
    }

    /// <summary>
    /// v6.3 Codex-quiz flow (GDD §21 / QUIZZES.md / CODEX.md). Replaces the legacy forced-QuizManager
    /// + event CodexManager on the new path; both legacy classes stay compiled for the old scene until
    /// cutover, so this one carries a distinct name (no CS0101 collision with CodexManager).
    ///
    /// Quizzes are OPTIONAL and live in the Codex — no forced popup, no timer (QUIZZES.md rule table).
    /// A quiz is answerable only once its knowledge has been USED (requiresApplied → QuizAvailability,
    /// incl. bug #3). Correct → permanent MasteryRegistry.Grant + Codex entry unlock (MetaProgress,
    /// persistent across runs). Wrong → no penalty, explanation still shown, retry tomorrow.
    ///
    /// Plain class (mirrors KnowledgeDB / MasteryRegistry) so tests need no scene object.
    /// </summary>
    public class CodexQuizManager
    {
        private static CodexQuizManager _instance;
        public static CodexQuizManager Instance => _instance ?? (_instance = new CodexQuizManager());

        /// <summary>Fresh manager for EditMode tests / restart (does not clear MetaProgress).</summary>
        public static void ResetForTest() => _instance = new CodexQuizManager();

        private readonly Dictionary<string, QuizQuestionSO> _quizzes = new Dictionary<string, QuizQuestionSO>();
        private readonly Dictionary<string, CodexEntrySO> _codexByQuiz = new Dictionary<string, CodexEntrySO>();
        private readonly List<CodexEntrySO> _codexAll = new List<CodexEntrySO>();
        private bool _loaded;

        private MasteryAppliedState _applied;
        public MasteryAppliedState Applied => _applied;

        // Reveal delay (QUIZZES.md pacing) — a quiz surfaces a beat AFTER its knowledge is used, not the
        // same instant. _appliedOnDay latches the first day each quiz became applied; the quiz is withheld
        // until _today − appliedDay ≥ _revealDelay. _today = 0 means no day clock (EditMode tests) → immediate.
        private int _today;
        private int _revealDelay = 1;
        private readonly Dictionary<string, int> _appliedOnDay = new Dictionary<string, int>();

        // ─────────────────────────────────────────
        //  Catalog (tests register directly; play mode auto-loads from Resources)
        // ─────────────────────────────────────────

        public void RegisterCatalog(IEnumerable<QuizQuestionSO> quizzes, IEnumerable<CodexEntrySO> codex)
        {
            if (quizzes != null)
                foreach (var q in quizzes)
                    if (q != null && !string.IsNullOrEmpty(q.quizId)) _quizzes[q.quizId] = q;
            if (codex != null)
                foreach (var c in codex)
                    if (c != null && !string.IsNullOrEmpty(c.entryId))
                    {
                        _codexAll.Add(c);
                        if (!string.IsNullOrEmpty(c.unlockedFromQuiz)) _codexByQuiz[c.unlockedFromQuiz] = c;
                    }
            _loaded = true;
        }

        private void EnsureCatalog()
        {
            if (_loaded) return;
            _loaded = true;
            RegisterCatalog(Resources.LoadAll<QuizQuestionSO>("Quizzes"),   // Assets/Resources/Quizzes
                            Resources.LoadAll<CodexEntrySO>("Codex"));       // Assets/Resources/Codex
        }

        public int TotalCodex { get { EnsureCatalog(); return _codexAll.Count; } }

        /// <summary>Header count "x / 11" — counts all unlocked entries, never per-category (CODEX.md §6).</summary>
        public int UnlockedCodexCount
        {
            get
            {
                EnsureCatalog();
                int n = 0;
                foreach (var c in _codexAll)
                    if (MetaProgress.UnlockedCodex.Contains(c.entryId)) n++;
                return n;
            }
        }

        public bool IsCodexUnlocked(string entryId) => MetaProgress.UnlockedCodex.Contains(entryId);

        // ─────────────────────────────────────────
        //  Applied state — the game loop / tests feed this (requiresApplied)
        // ─────────────────────────────────────────

        public void SetApplied(MasteryAppliedState s) => _applied = s;

        /// <summary>
        /// Advance the reveal clock one day (called by QuizAppliedWatcher on OnDayEnded). Latches the first
        /// day each quiz's knowledge became "applied" so it surfaces quizRevealDelayDays later — a beat after
        /// the lesson, not the moment you use it (QUIZZES.md pacing).
        /// </summary>
        public void AdvanceDay(int day)
        {
            EnsureCatalog();
            _today = day;
            var cfg = GameConfigSO.Instance;
            if (cfg != null) _revealDelay = Mathf.Max(0, cfg.quizRevealDelayDays);
            foreach (var id in _quizzes.Keys)
                if (!_appliedOnDay.ContainsKey(id)
                    && !MasteryRegistry.Instance.Has(id)
                    && QuizAvailability.IsApplied(id, _applied))
                    _appliedOnDay[id] = day;
        }

        /// <summary>
        /// Also the V06 trigger ("Extractor เดินครั้งแรก"). v6.3 has no Extractor building — a Water Plant
        /// at max level doing deuterium extraction IS the extractor (see QuizAppliedWatcher), and this is
        /// the one place that knows it ran. Fire on the 0→1 edge; the bark is onceOnly regardless.
        /// </summary>
        public void MarkExtractorRan()
        {
            bool first = _applied.extractorRanDays == 0;
            _applied.extractorRanDays++;
            if (first && InnerVoiceDirector.Instance != null)
                InnerVoiceDirector.Instance.Fire("V06");
        }
        public void SetCoilTypes(int n)   => _applied.coilTypesInstalled = n;
        public void MarkMedBayHealed()    => _applied.medBayHealedCount++;
        public void MarkRiskZoneEntered() => _applied.riskZoneEntered = true;
        public void MarkMutationLab()     => _applied.mutationLabRan = true;
        public void MarkCo60()            => _applied.co60Ran = true;
        public void MarkTritiumFed()      => _applied.tritiumFed = true;
        /// <summary>Zone B produced tritium today — latches tritiumEverProduced (★ bug #3).</summary>
        public void MarkTritiumProduced() { _applied.tritiumEverProduced = true; _applied.zoneBProducedDays++; }
        public void SetCoreProgress(float core) => _applied.coreProgress = core;
        public void MarkEndingReached()   => _applied.reachedEnding = true;

        // ─────────────────────────────────────────
        //  Availability
        // ─────────────────────────────────────────

        /// <summary>Answerable now: catalog has it, not already earned, and requiresApplied is met.</summary>
        public bool IsAnswerable(string quizId)
        {
            EnsureCatalog();
            if (!_quizzes.TryGetValue(quizId, out var q) || q == null) return false;
            if (MasteryRegistry.Instance.Has(quizId)) return false;         // already earned
            if (!q.requiresApplied) return true;
            if (!QuizAvailability.IsApplied(quizId, _applied)) return false;
            // reveal a beat after the knowledge was applied (~1-2 days). No day clock (tests) → immediate.
            if (_today <= 0 || _revealDelay <= 0) return true;
            return _appliedOnDay.TryGetValue(quizId, out int d) && (_today - d) >= _revealDelay;
        }

        /// <summary>How many quizzes are answerable-but-unanswered right now — the notification badge count.</summary>
        public int AnswerableCount
        {
            get
            {
                EnsureCatalog();
                int n = 0;
                foreach (var id in _quizzes.Keys)
                    if (IsAnswerable(id)) n++;
                return n;
            }
        }

        /// <summary>Any quiz answerable-but-unanswered → HUD red dot on the Codex button (QUIZZES.md UI).</summary>
        public bool HasNewQuiz
        {
            get
            {
                EnsureCatalog();
                foreach (var id in _quizzes.Keys)
                    if (IsAnswerable(id)) return true;
                return false;
            }
        }

        public IEnumerable<QuizView> GetAll()
        {
            EnsureCatalog();
            foreach (var kv in _quizzes)
            {
                var q = kv.Value;
                _codexByQuiz.TryGetValue(q.quizId, out var codex);
                QuizState st = MasteryRegistry.Instance.Has(q.quizId) ? QuizState.Earned
                             : IsAnswerable(q.quizId)                  ? QuizState.Answerable
                                                                       : QuizState.Locked;
                yield return new QuizView { quiz = q, codex = codex, state = st };
            }
        }

        /// <summary>Codex list ordered for the panel — all entries, locked ones INCLUDED (never hidden).</summary>
        public IReadOnlyList<CodexEntrySO> AllCodex { get { EnsureCatalog(); return _codexAll; } }

        // ─────────────────────────────────────────
        //  Answering
        // ─────────────────────────────────────────

        /// <summary>
        /// Answer a quiz. Wrong → no penalty, explanation returned, quiz stays answerable (retry next
        /// day). Correct → grant permanent mastery + unlock the linked Codex entry (persistent).
        /// </summary>
        public SubmitResult Submit(string quizId, int optionIndex)
        {
            EnsureCatalog();
            var res = new SubmitResult();
            if (!_quizzes.TryGetValue(quizId, out var q) || q == null) return res; // unknown quiz
            if (!IsAnswerable(quizId)) return res;                                  // locked / earned

            res.valid = true;
            res.explanation = q.explainText;                    // shown ALWAYS (QUIZZES.md — every case)
            res.correct = optionIndex == q.correctIndex;
            if (!res.correct) return res;                       // no penalty; try again tomorrow

            // Correct → permanent mastery + codex unlock
            res.masteryEarned = MasteryRegistry.Instance.Grant(quizId);
            res.codexUnlockedId = UnlockCodexFor(q);
            EventManager.Instance?.RaiseQuizAnswered(quizId, true);
            return res;
        }

        // codex link: prefer the CodexEntrySO mapped by unlockedFromQuiz, fall back to quiz.codexUnlockId
        private string UnlockCodexFor(QuizQuestionSO q)
        {
            string entryId = null;
            if (_codexByQuiz.TryGetValue(q.quizId, out var codex) && codex != null) entryId = codex.entryId;
            else if (!string.IsNullOrEmpty(q.codexUnlockId)) entryId = q.codexUnlockId;
            if (string.IsNullOrEmpty(entryId)) return null;

            if (Application.isPlaying) MetaProgress.AddCodex(entryId);        // persist (writes PlayerPrefs)
            else MetaProgress.UnlockedCodex.Add(entryId);                     // tests: in-memory only
            return entryId;
        }
    }
}
