using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Note completion popup (GDD §19 + NOTES.md) — fires on OnResearchNoteCompleted:
    ///   page 1: knowledgeBody (the actual knowledge, verbatim from the asset)
    ///   page 2: the 1-line NPC reaction (completionSpeaker/completionLine)
    ///
    /// ★ Deliberately NOT routed through BarkManager — barks are capped at 2/day but this
    ///   dialogue must ALWAYS show (NOTES.md implement note).
    /// Pauses the day clock while open (PauseReason.NotePopup — pause-reason stack §3,
    /// never Time.timeScale).
    /// </summary>
    public class NoteCardPopup : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;      // note title (+ category)
        [SerializeField] private Text bodyText;       // knowledgeBody / NPC line
        [SerializeField] private Text speakerText;    // page 2 speaker name (hidden on page 1)
        [SerializeField] private Button continueButton;

        private ResearchNoteSO _note;
        private int _page; // 0 = knowledgeBody, 1 = NPC line

        private void OnEnable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnResearchNoteCompleted += HandleNoteCompleted;
            if (continueButton != null)
                continueButton.onClick.AddListener(Advance);
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
                EventManager.Instance.OnResearchNoteCompleted -= HandleNoteCompleted;
            if (continueButton != null)
                continueButton.onClick.RemoveListener(Advance);
        }

        private void HandleNoteCompleted(string noteId)
        {
            var note = KnowledgeDB.Instance.GetNote(noteId);
            if (note == null) return;
            Show(note);
        }

        /// <summary>Open the two-page popup for a note (public so tests/panels can drive it).</summary>
        public void Show(ResearchNoteSO note)
        {
            _note = note;
            _page = 0;
            TimeManager.Instance?.Pause(PauseReason.NotePopup); // นาฬิกาหยุด — popup Note (§3)
            if (panelRoot != null) panelRoot.SetActive(true);
            Render();
        }

        /// <summary>Continue button: knowledgeBody → NPC line → close.</summary>
        public void Advance()
        {
            // ข้ามหน้า NPC ถ้าโน้ตไม่มีบท (ไม่ควรเกิด — NOTES.md มีครบ 8)
            if (_page == 0 && !string.IsNullOrEmpty(_note?.completionLine))
            {
                _page = 1;
                Render();
                return;
            }
            Close();
        }

        private void Render()
        {
            if (_note == null) return;

            if (titleText != null)
                titleText.text = _page == 0 ? $"{_note.title}" : "";

            if (_page == 0)
            {
                if (speakerText != null) speakerText.text = "";
                if (bodyText != null) bodyText.text = _note.knowledgeBody;
            }
            else
            {
                if (speakerText != null) speakerText.text = _note.completionSpeaker;
                if (bodyText != null) bodyText.text = $"“{_note.completionLine}”";
            }
        }

        private void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            TimeManager.Instance?.Resume(PauseReason.NotePopup);
            _note = null;
        }
    }
}
