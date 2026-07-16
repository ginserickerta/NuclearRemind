using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Research queue panel (GDD §19 UI) — the player's window into the knowledge economy:
    ///   · lab state (ruin → repair → level) + active job progress
    ///   · unlocked leads with their hints (NOTES.md lead hints)
    ///   · researchable notes with slots×days + cost, and a start button
    ///
    /// Row-template uGUI pattern (same as HopeBreakdownPanel). Buttons per row are built
    /// from a template Button whose label text carries the note title.
    /// </summary>
    public class ResearchQueuePanel : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text labStatusText;      // ซาก / กำลังซ่อม x/2 / Lab Lv1 (slots 3)
        [SerializeField] private Text activeJobText;      // งานปัจจุบัน + progress
        [SerializeField] private Transform listContainer; // vertical layout — leads + notes
        [SerializeField] private Text rowTemplate;        // disabled text row (leads / locked notes)
        [SerializeField] private Button startButtonTemplate; // disabled button row (researchable notes)
        [SerializeField] private Button repairButton;     // จ่ายเหล็ก 80 เริ่มซ่อม

        private readonly List<GameObject> _rows = new List<GameObject>();

        private void OnEnable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnLeadUnlocked += HandleChanged;
                EventManager.Instance.OnResearchNoteStarted += HandleChanged;
                EventManager.Instance.OnResearchNoteCompleted += HandleChanged;
                EventManager.Instance.OnResearchLabRepaired += Refresh;
                EventManager.Instance.OnDayStarted += HandleDayStarted;
            }
            if (repairButton != null) repairButton.onClick.AddListener(OnRepairClicked);
            Refresh();
        }

        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnLeadUnlocked -= HandleChanged;
                EventManager.Instance.OnResearchNoteStarted -= HandleChanged;
                EventManager.Instance.OnResearchNoteCompleted -= HandleChanged;
                EventManager.Instance.OnResearchLabRepaired -= Refresh;
                EventManager.Instance.OnDayStarted -= HandleDayStarted;
            }
            if (repairButton != null) repairButton.onClick.RemoveListener(OnRepairClicked);
        }

        private void HandleChanged(string _) => Refresh();
        private void HandleDayStarted(int day, bool timed) => Refresh();

        public void TogglePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf) Refresh();
        }

        private void OnRepairClicked()
        {
            ResearchLab.Instance?.StartRepair();
            Refresh();
        }

        public void Refresh()
        {
            var lab = ResearchLab.Instance;
            var db = KnowledgeDB.Instance;
            if (lab == null) return;

            if (labStatusText != null) labStatusText.text = LabStatusLine(lab);
            if (activeJobText != null) activeJobText.text = ActiveJobLine(lab);
            if (repairButton != null)
                repairButton.gameObject.SetActive(lab.IsRuined && !lab.RepairPaid);

            RebuildList(lab, db);
        }

        /// <summary>Status line — static + pure for tests.</summary>
        public static string LabStatusLine(ResearchLab lab)
        {
            if (lab.IsRuined)
                return lab.RepairPaid
                    ? $"🏚 กำลังซ่อม {lab.RepairProgress:0}/{GameConfigSO.Instance.repairDays} วัน (ต้องมีคน lab ≥ {GameConfigSO.Instance.repairWorkers})"
                    : $"🏚 ห้องวิจัยเป็นซาก — ซ่อม: เหล็ก {GameConfigSO.Instance.repairIron} · {GameConfigSO.Instance.repairDays} วัน · {GameConfigSO.Instance.repairWorkers} คน";
            return $"🔬 ห้องวิจัย Lv{lab.Level} · ที่นั่งนักวิจัย {lab.ResearcherSlots} · คิว {lab.QueueCapacity}";
        }

        public static string ActiveJobLine(ResearchLab lab)
        {
            if (lab.IsRuined || lab.ActiveJob == null) return "— ไม่มีงานวิจัย —";
            var j = lab.ActiveJob;
            var sb = new StringBuilder($"⏳ {j.note.title}  {j.progress:0.#}/{j.note.daysRequired} วัน");
            foreach (var q in lab.QueuedNoteIds) sb.Append($"\n   ▸ รอคิว: {q}");
            return sb.ToString();
        }

        private void RebuildList(ResearchLab lab, KnowledgeDB db)
        {
            if (listContainer == null || rowTemplate == null) return;

            foreach (var go in _rows) Destroy(go);
            _rows.Clear();

            foreach (var note in db.AllNotes)
            {
                if (db.HasNote(note.noteId))
                {
                    AddRow($"✅ {note.title}");
                }
                else if (db.IsResearchable(note))
                {
                    // ปุ่มเริ่มวิจัย — จ่ายครั้งเดียว (bug #2) ฝั่ง ResearchLab
                    if (startButtonTemplate != null)
                    {
                        var btn = Instantiate(startButtonTemplate, listContainer);
                        btn.gameObject.SetActive(true);
                        var label = btn.GetComponentInChildren<Text>();
                        if (label != null) label.text = NoteOfferLine(note);
                        string id = note.noteId;
                        btn.onClick.AddListener(() => { ResearchLab.Instance?.TryStartResearch(id); Refresh(); });
                        _rows.Add(btn.gameObject);
                    }
                }
                else if (!string.IsNullOrEmpty(note.requiredLead) && db.HasLead(note.requiredLead))
                {
                    AddRow($"⛔ {note.title} — ขาด prerequisite"); // lead มาแล้วแต่ prereq ยังไม่ครบ (tritium ← deuterium)
                }
                else
                {
                    // ยังไม่มี lead — โชว์ hint ถ้าระบบใบ้แล้ว, ไม่งั้นเป็น ??? (ห้ามบอกคำตอบก่อนวิจัย — กติกาข้อ 4)
                    AddRow($"🔒 ??? — ยังไม่มีเบาะแส");
                }
            }

            // leads ที่ปลดแล้ว = เบาะแสให้ผู้เล่นรู้ว่าค้นอะไรได้
            foreach (var kvp in KnowledgeDB.LeadMap)
            {
                if (!db.HasLead(kvp.Key)) continue;
                var note = db.GetNote(kvp.Value);
                if (note != null && !db.HasNote(note.noteId))
                    AddRow($"💡 \"{note.leadHint}\"");
            }
        }

        /// <summary>"เชื้อเพลิงที่ซ่อนอยู่ในน้ำ · 2 คน × 2 วัน · P80" — static for tests.</summary>
        public static string NoteOfferLine(ResearchNoteSO note)
        {
            var cost = new StringBuilder();
            if (note.costPower > 0) cost.Append($" P{note.costPower}");
            if (note.costIron > 0) cost.Append($" Fe{note.costIron}");
            if (note.costLabMat > 0) cost.Append($" Lab{note.costLabMat}");
            return $"🔬 {note.title} · {note.researcherSlots} คน × {note.daysRequired} วัน ·{cost}";
        }

        private void AddRow(string text)
        {
            var row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            row.text = text;
            _rows.Add(row.gameObject);
        }
    }
}
