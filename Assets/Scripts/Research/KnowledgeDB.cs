using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ฐานข้อมูลความรู้กลางของเกม — จำว่าเบาะแส (lead) ไหนถูกปลดแล้ว
    /// โน้ตวิจัยใบไหนเสร็จแล้ว และใช้ข้อมูลนี้ล็อก/ปลดล็อกสิ่งปลูกสร้าง
    /// ระบบอื่น query แบบอ่านอย่างเดียว (HasNote/HasLead) ได้ตรง ๆ — โน้ตที่ยังไม่มีเบาะแสต้องถูกซ่อนเป็น ???
    ///
    /// Central knowledge registry (GDD §19) — which leads are unlocked, which notes are
    /// completed, and what that gates. Plain class (no scene object); ResearchLab drives it.
    ///
    /// CRITICAL (bug #11): the lead→note map is code-enforced and MUST cover all 8 notes.
    /// A note whose lead is missing from this map can never be researched — the game is
    /// silently unwinnable. ResearchSystemTests asserts full coverage against the catalog.
    ///
    /// Read-only queries (HasNote/HasLead/IsBuildingUnlocked) may be called directly from
    /// any system — same exception as MasteryRegistry/GameConfigSO (CLAUDE.md).
    /// </summary>
    public class KnowledgeDB
    {
        private static KnowledgeDB _instance;
        public static KnowledgeDB Instance => _instance ?? (_instance = new KnowledgeDB());

        /// <summary>Fresh DB for EditMode tests / game restart (meta progress is NOT stored here).</summary>
        public static void ResetForTest() => _instance = new KnowledgeDB();

        // [TH] แผนที่ เบาะแส→โน้ต ตายตัวในโค้ด — ต้องครบทั้ง 8 ใบ ไม่งั้นเกมชนะไม่ได้แบบเงียบ ๆ (บั๊ก #11)
        /// <summary>
        /// ★ lead_map (bug #11) — every one of the 8 notes, no exceptions.
        /// storm_detection's lead comes from Record #3 only (STORY.md) — same map, different source.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> LeadMap = new Dictionary<string, string>
        {
            { "water_analysis", "deuterium" },
            { "magnetic_theory", "confinement" },
            { "radiation_biology", "nuclear_medicine" },
            { "food_preservation", "irradiation" },
            { "lithium_breeding", "tritium" },
            { "storm_detection", "storm_detection" },
            { "food_logistics", "food_logistics" },
            { "shift_management", "shift_management" },
        };

        private readonly Dictionary<string, ResearchNoteSO> _catalog = new Dictionary<string, ResearchNoteSO>();
        private readonly HashSet<string> _unlockedLeads = new HashSet<string>();
        private readonly HashSet<string> _completedNotes = new HashSet<string>();
        private bool _catalogLoaded;

        // ─────────────────────────────────────────
        //  Catalog
        // ─────────────────────────────────────────

        // [TH] ลงทะเบียนโน้ตทั้งหมดเข้าแคตาล็อก (ตอนเล่นจริงโหลดอัตโนมัติจาก Resources)
        /// <summary>Register note assets (tests call this directly; play mode auto-loads from Resources).</summary>
        public void RegisterNotes(IEnumerable<ResearchNoteSO> notes)
        {
            foreach (var n in notes)
                if (n != null && !string.IsNullOrEmpty(n.noteId))
                    _catalog[n.noteId] = n;
            _catalogLoaded = true;
        }

        private void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;
            var loaded = Resources.LoadAll<ResearchNoteSO>("ResearchNotes"); // Assets/Resources/ResearchNotes
            if (loaded != null && loaded.Length > 0)
                RegisterNotes(loaded);
        }

        public ResearchNoteSO GetNote(string noteId)
        {
            EnsureCatalog();
            return _catalog.TryGetValue(noteId, out var n) ? n : null;
        }

        public IEnumerable<ResearchNoteSO> AllNotes
        {
            get { EnsureCatalog(); return _catalog.Values; }
        }

        // ─────────────────────────────────────────
        //  Leads (unlock once per game — GDD §25 table)
        // ─────────────────────────────────────────

        // [TH] ปลดเบาะแส — คืน true เฉพาะครั้งแรก (ปลดได้ครั้งเดียวต่อเกม) แล้วยิง event แจ้งระบบอื่น
        /// <summary>Unlock a lead — returns true only the first time (once per game).</summary>
        public bool UnlockLead(string leadId)
        {
            if (string.IsNullOrEmpty(leadId) || !_unlockedLeads.Add(leadId)) return false;
            EventManager.Instance?.RaiseLeadUnlocked(leadId);
            return true;
        }

        public bool HasLead(string leadId) => _unlockedLeads.Contains(leadId);

        public static string NoteIdForLead(string leadId) =>
            LeadMap.TryGetValue(leadId, out var noteId) ? noteId : null;

        // ─────────────────────────────────────────
        //  Notes
        // ─────────────────────────────────────────

        public bool HasNote(string noteId) => _completedNotes.Contains(noteId);

        /// <summary>Mark a note completed (ResearchLab calls this — do not call from gameplay).</summary>
        public void CompleteNote(string noteId)
        {
            if (string.IsNullOrEmpty(noteId)) return;
            _completedNotes.Add(noteId);
        }

        // [TH] เช็คว่าโน้ตใบนี้เริ่มวิจัยได้ไหม: มีเบาะแสแล้ว + โน้ตก่อนหน้าเสร็จแล้ว + ยังไม่เคยวิจัย
        /// <summary>Researchable = lead unlocked + prerequisites completed + not already done.</summary>
        public bool IsResearchable(ResearchNoteSO note)
        {
            if (note == null || HasNote(note.noteId)) return false;
            if (!string.IsNullOrEmpty(note.requiredLead) && !HasLead(note.requiredLead)) return false;
            if (note.prerequisiteNotes != null)
                foreach (var pre in note.prerequisiteNotes)
                    if (!string.IsNullOrEmpty(pre) && !HasNote(pre))
                        return false;
            return true;
        }

        // ─────────────────────────────────────────
        //  Building gate — SINGLE mechanism (Sprint 2 acceptance: no note = no Extractor)
        // ─────────────────────────────────────────

        // [TH] ประตูเดียวของการล็อกสิ่งปลูกสร้าง: ตึกจะสร้างได้ก็ต่อเมื่อโน้ตที่มันต้องการวิจัยเสร็จแล้ว
        /// <summary>
        /// The one building gate: a building is locked while its BuildingData.requiredNoteId names
        /// a note that isn't completed. Buildings with no requiredNoteId are always free
        /// (basic production buildings). PlacementController AND the acceptance test both call this
        /// exact method, so the test proves the shipped gate — no second reverse-lookup path.
        /// (note.unlocksBuildings stays as display data for the research panel, NOT a gate.)
        /// </summary>
        public bool IsBuildingUnlocked(BuildingData data) =>
            data == null || IsNoteGateOpen(data.requiredNoteId);

        /// <summary>Gate keyed directly on a note id — "" means no gate (always open).</summary>
        public bool IsNoteGateOpen(string requiredNoteId) =>
            string.IsNullOrEmpty(requiredNoteId) || HasNote(requiredNoteId);

        // [TH] ตรวจสุขภาพแผนที่ lead→note ว่าครบทุกโน้ต — test บังคับให้ผลลัพธ์ต้องว่างเปล่า
        /// <summary>
        /// bug #11 self-check: every catalog note's requiredLead must exist in LeadMap and
        /// map back to that note. Returns offending noteIds (empty = healthy). Tests assert empty.
        /// </summary>
        public List<string> ValidateLeadMap()
        {
            EnsureCatalog();
            var broken = new List<string>();
            foreach (var note in _catalog.Values)
            {
                if (string.IsNullOrEmpty(note.requiredLead)) { broken.Add(note.noteId); continue; }
                if (NoteIdForLead(note.requiredLead) != note.noteId) broken.Add(note.noteId);
            }
            return broken;
        }
    }
}
