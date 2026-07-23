namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: ฟังก์ชันประกอบข้อความ (string ล้วน) ของแผง Records — การ์ดบันทึกที่กู้คืน + แถวรายการในคลัง
    /// [TH] แยกเป็น static pure function เพื่อให้เทสต์ได้ · แผงนี้โชว์เฉพาะที่กู้ได้แล้ว ไม่ล่อด้วยรายการล็อก (ต่างจาก Codex)
    /// Records-panel formatting (STORY.md §3). Pure string builders so the recovered-record card and
    /// the archive rows are testable; the F9 dev panel and any uGUI view render these. Distinct from
    /// the legacy UI/RecordsPanelController — this is the v6.3 Data-Recovery view.
    ///
    /// Records are the story reward, NOT gated content: the panel only ever shows what's been
    /// recovered (locked entries aren't teased here — unlike the Codex, the count itself is the hook).
    /// </summary>
    public static class RecordsPanel
    {
        // [TH] บรรทัดหัวแผง "กู้คืนแล้ว x / y"
        public static string CountLine(int recovered, int total) => $"บันทึก Elara Vane   กู้คืนแล้ว {recovered} / {total}";

        /// <summary>[TH] ข้อความการ์ดที่เด้งขึ้นทันทีตอนกู้บันทึกสำเร็จ
        /// The pop-up card shown the moment a record is recovered.</summary>
        public static string Card(RecordCardSO r)
        {
            if (r == null) return "";
            string status = string.IsNullOrEmpty(r.statusLabel) ? "กู้คืนสำเร็จ" : r.statusLabel;
            string who = string.IsNullOrEmpty(r.recorderName) ? r.authorLabel : r.recorderName;
            return $"[★ การ์ดบันทึก — {status}]\n{who}\n\n{r.bodyTH}";
        }

        /// <summary>[TH] ข้อความหนึ่งแถวในรายการคลังบันทึก
        /// One archive-list row.</summary>
        public static string ArchiveRow(RecordCardSO r) =>
            r == null ? "" : $"● {(string.IsNullOrEmpty(r.archiveTitle) ? r.recordId : r.archiveTitle)}";
    }
}
