using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: เครื่องมือดีบักการ์ดวิกฤต (ใช้ตอนกด Play) — ดูรายงาน "ทำไมการ์ดยังไม่ขึ้น",
    /// เซฟ/ล้างประวัติ trigger ลงไฟล์ และบังคับโชว์การ์ดทีละใบเพื่อทดสอบหน้าจอ
    /// Play-mode diagnostics for the crisis-card triggers (GDD §25 / CONFIG.md 🔒 CARDS).
    ///
    /// Six of the eight cards were reported as "never appear". The thresholds are 🔒 and cannot be
    /// lowered without nrm_sim.py, which is not in the repo, so the question that has to be answered
    /// first is which of these two it is:
    ///   1. the trigger state is genuinely never reached in a real run → the threshold is the problem
    ///   2. the state IS reached but something else blocks it (cooldown, missing asset, onceOnly)
    ///
    /// CardManager already logs a per-card "why not" table at each day end, but it scrolls away behind
    /// everything else and cannot be handed to anyone. These tools print it on demand and export the
    /// whole session to a file.
    /// </summary>
    public static class CardTools
    {
        private const string Root = "NuclearReMind/Cards/";

        // เมนูนี้: พิมพ์รายงานสถานะ trigger ของการ์ดทุกใบ ณ ตอนนี้ ลง Console
        [MenuItem(Root + "Log Card States (now)", false, 10)]
        private static void LogNow()
        {
            if (!RequirePlayMode()) return;

            var cm = CardManager.Instance;
            if (cm == null) { Debug.LogWarning("[CardTools] ยังไม่มี CardManager ในฉาก"); return; }

            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            Debug.Log("[CardTools] สถานะการ์ด ณ ตอนนี้\n" + cm.BuildTriggerReport(day, CardWorldState.Snapshot()));
        }

        // เมนูนี้: บันทึกประวัติ trigger ของทั้งเซสชันเป็นไฟล์ card-trace.log ที่รากโปรเจกต์
        [MenuItem(Root + "Save Card Trace to file", false, 11)]
        private static void SaveTrace()
        {
            var history = CardManager.TraceHistory;
            if (history.Count == 0)
            {
                Debug.LogWarning("[CardTools] ยังไม่มีประวัติ — ต้องเล่นให้ผ่านสิ้นวันอย่างน้อย 1 วันก่อน " +
                                 "(บันทึกเฉพาะวันที่ไม่มีการ์ดขึ้น)");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Crisis card trigger trace");
            sb.AppendLine($"# {history.Count} day(s) recorded — days on which no card fired");
            sb.AppendLine();
            foreach (var entry in history) sb.AppendLine(entry).AppendLine();

            // Project root, next to Assets/ — easy to find and to attach to a message.
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "card-trace.log");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[CardTools] เขียนแล้ว → {path}  ({history.Count} วัน)");
            EditorUtility.RevealInFinder(path);
        }

        // เมนูนี้: ล้างประวัติ trace เริ่มบันทึกรอบใหม่
        [MenuItem(Root + "Clear Card Trace", false, 12)]
        private static void ClearTrace()
        {
            CardManager.TraceHistory.Clear();
            Debug.Log("[CardTools] ล้างประวัติแล้ว — เริ่มบันทึกรอบใหม่ได้");
        }

        // เมนูนี้: เปิด/ปิดโหมด trace ละเอียด (log ทุกวัน ไม่ใช่เฉพาะวันที่การ์ดไม่ขึ้น)
        [MenuItem(Root + "Verbose Trace", false, 30)]
        private static void ToggleVerbose()
        {
            CardManager.VerboseTrace = !CardManager.VerboseTrace;
            Debug.Log($"[CardTools] Verbose trace = {CardManager.VerboseTrace}");
        }

        [MenuItem(Root + "Verbose Trace", true)]
        private static bool ToggleVerboseValidate()
        {
            Menu.SetChecked(Root + "Verbose Trace", CardManager.VerboseTrace);
            return true;
        }

        // ── Force present: check the PRESENTATION path without waiting for a trigger ──
        //
        // This proves the dialogue → card → options chain works and that the asset's text and lock states
        // render. It proves NOTHING about whether the trigger can ever fire in a real run — that is what
        // "Log Card States" and the trace file are for. Keep the two questions apart: a card that force-
        // shows fine but never appears in play has a threshold problem, not a UI problem.
        // เมนูกลุ่ม Force Show: บังคับโชว์การ์ดใบที่เลือกทันที (ทดสอบหน้าจอ/ข้อความ — ไม่พิสูจน์ว่า trigger จริงทำงาน)
        [MenuItem(Root + "Force Show/1 · heat", false, 50)]      private static void F1() => Force(CardIds.Heat);
        [MenuItem(Root + "Force Show/2 · sick", false, 51)]      private static void F2() => Force(CardIds.Sick);
        [MenuItem(Root + "Force Show/3 · spoil", false, 52)]     private static void F3() => Force(CardIds.Spoil);
        [MenuItem(Root + "Force Show/4 · hunger", false, 53)]    private static void F4() => Force(CardIds.Hunger);
        [MenuItem(Root + "Force Show/5 · overwork", false, 54)]  private static void F5() => Force(CardIds.Overwork);
        [MenuItem(Root + "Force Show/6 · zoneb", false, 55)]     private static void F6() => Force(CardIds.ZoneB);
        [MenuItem(Root + "Force Show/7 · triage", false, 56)]    private static void F7() => Force(CardIds.Triage);
        [MenuItem(Root + "Force Show/8 · decree", false, 57)]    private static void F8() => Force(CardIds.Decree);

        private static void Force(string cardId)
        {
            if (!RequirePlayMode()) return;

            var cm = CardManager.Instance;
            if (cm == null) { Debug.LogWarning("[CardTools] ยังไม่มี CardManager ในฉาก"); return; }

            if (cm.HasPending)
            {
                Debug.LogWarning($"[CardTools] มีการ์ด '{cm.Pending.cardId}' ค้างอยู่ — ตอบให้จบก่อน " +
                                 "(ทีละใบเท่านั้น ตามกติกาของ CardManager)");
                return;
            }

            var card = cm.ForcePresent(cardId);
            if (card == null)
                Debug.LogError($"[CardTools] สั่งแสดง '{cardId}' ไม่สำเร็จ — ไม่มี asset ใน Resources/CrisisCards " +
                               "หรือแผงไม่ขึ้น (ดู Console บรรทัดของ [Cards])");
            else
                Debug.Log($"[CardTools] สั่งแสดง '{cardId}' แล้ว — ข้ามเงื่อนไขและ cooldown");
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[CardTools] ต้องกด Play ก่อน — เครื่องมือนี้อ่านสถานะจากเกมที่กำลังรัน");
            return false;
        }
    }
}
