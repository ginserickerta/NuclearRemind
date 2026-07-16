namespace NuclearReMind
{
    /// <summary>
    /// Records-panel formatting (STORY.md §3). Pure string builders so the recovered-record card and
    /// the archive rows are testable; the F9 dev panel and any uGUI view render these. Distinct from
    /// the legacy UI/RecordsPanelController — this is the v6.3 Data-Recovery view.
    ///
    /// Records are the story reward, NOT gated content: the panel only ever shows what's been
    /// recovered (locked entries aren't teased here — unlike the Codex, the count itself is the hook).
    /// </summary>
    public static class RecordsPanel
    {
        public static string CountLine(int recovered, int total) => $"บันทึก Elara Vane   กู้คืนแล้ว {recovered} / {total}";

        /// <summary>The pop-up card shown the moment a record is recovered.</summary>
        public static string Card(RecordCardSO r)
        {
            if (r == null) return "";
            string status = string.IsNullOrEmpty(r.statusLabel) ? "กู้คืนสำเร็จ" : r.statusLabel;
            string who = string.IsNullOrEmpty(r.recorderName) ? r.authorLabel : r.recorderName;
            return $"[★ การ์ดบันทึก — {status}]\n{who}\n\n{r.bodyTH}";
        }

        /// <summary>One archive-list row.</summary>
        public static string ArchiveRow(RecordCardSO r) =>
            r == null ? "" : $"● {(string.IsNullOrEmpty(r.archiveTitle) ? r.recordId : r.archiveTitle)}";
    }
}
