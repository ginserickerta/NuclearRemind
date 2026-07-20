using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
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

        [MenuItem(Root + "Log Card States (now)", false, 10)]
        private static void LogNow()
        {
            if (!RequirePlayMode()) return;

            var cm = CardManager.Instance;
            if (cm == null) { Debug.LogWarning("[CardTools] ยังไม่มี CardManager ในฉาก"); return; }

            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            Debug.Log("[CardTools] สถานะการ์ด ณ ตอนนี้\n" + cm.BuildTriggerReport(day, CardWorldState.Snapshot()));
        }

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

        [MenuItem(Root + "Clear Card Trace", false, 12)]
        private static void ClearTrace()
        {
            CardManager.TraceHistory.Clear();
            Debug.Log("[CardTools] ล้างประวัติแล้ว — เริ่มบันทึกรอบใหม่ได้");
        }

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

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[CardTools] ต้องกด Play ก่อน — เครื่องมือนี้อ่านสถานะจากเกมที่กำลังรัน");
            return false;
        }
    }
}
