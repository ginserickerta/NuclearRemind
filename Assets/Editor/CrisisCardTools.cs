using System.Text;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Play-mode diagnostics for the crisis-card pipeline (GDD §25 / CARDS.md).
    ///
    /// Two questions this answers, which look identical from inside the game and have opposite fixes:
    ///   1. "No card appeared" because no trigger reached its threshold  → Log World State
    ///   2. "No card appeared" because the presentation pipeline is broken → Force Present
    ///
    /// Force Present bypasses triggers and cooldown, so if a card still does not appear on screen the
    /// fault is in the UI path (CardManager → OnCrisisCardShown → CrisisCardPanelUI), not the rules.
    /// </summary>
    public static class CrisisCardTools
    {
        private const string Root = "NuclearReMind/Crisis Cards/";

        [MenuItem(Root + "Log World State vs Triggers", priority = 0)]
        private static void LogWorldState()
        {
            if (!RequirePlayMode()) return;

            var state = CardWorldState.Snapshot();
            var cm = CardManager.Instance;

            var sb = new StringBuilder("[Cards] สภาพโลกตอนนี้เทียบเกณฑ์การ์ด\n");
            foreach (var cardId in CardIds.All)
            {
                bool fires = CardTriggers.IsTriggered(cardId, state);
                bool hasAsset = cm != null && cm.GetCard(cardId) != null;
                sb.Append(fires ? "  ✔ " : "  · ").Append(cardId.PadRight(9))
                  .Append(CardTriggers.Describe(cardId, state));
                if (!hasAsset) sb.Append("   ★ ไม่มี asset ใน Resources/CrisisCards");
                sb.Append('\n');
            }
            if (cm != null && cm.HasPending)
                sb.Append($"  ⚠ มีการ์ด '{cm.Pending.cardId}' ค้างรอคำตอบ — การ์ดใหม่จะไม่ขึ้นจนกว่าจะตอบใบนี้\n");
            Debug.Log(sb.ToString());
        }

        [MenuItem(Root + "Toggle Day-End Trace", priority = 1)]
        private static void ToggleTrace()
        {
            CardManager.VerboseTrace = !CardManager.VerboseTrace;
            Debug.Log($"[Cards] day-end trace = {(CardManager.VerboseTrace ? "เปิด" : "ปิด")}");
        }

        [MenuItem(Root + "Clear Stuck Card", priority = 2)]
        private static void ClearStuck()
        {
            if (!RequirePlayMode()) return;
            var cm = CardManager.Instance;
            if (cm == null || !cm.HasPending) { Debug.Log("[Cards] ไม่มีการ์ดค้าง"); return; }
            cm.ClearPending();
        }

        // ── Force Present: one entry per card, in CARDS.md order ──
        [MenuItem(Root + "Force Present/1 · heat",     priority = 20)] private static void F1() => Force(CardIds.Heat);
        [MenuItem(Root + "Force Present/2 · sick",     priority = 21)] private static void F2() => Force(CardIds.Sick);
        [MenuItem(Root + "Force Present/3 · spoil",    priority = 22)] private static void F3() => Force(CardIds.Spoil);
        [MenuItem(Root + "Force Present/4 · hunger",   priority = 23)] private static void F4() => Force(CardIds.Hunger);
        [MenuItem(Root + "Force Present/5 · overwork", priority = 24)] private static void F5() => Force(CardIds.Overwork);
        [MenuItem(Root + "Force Present/6 · zoneb",    priority = 25)] private static void F6() => Force(CardIds.ZoneB);
        [MenuItem(Root + "Force Present/7 · triage",   priority = 26)] private static void F7() => Force(CardIds.Triage);
        [MenuItem(Root + "Force Present/8 · decree",   priority = 27)] private static void F8() => Force(CardIds.Decree);

        private static void Force(string cardId)
        {
            if (!RequirePlayMode()) return;
            var cm = CardManager.Instance;
            if (cm == null)
            {
                Debug.LogError("[Cards] ไม่มี CardManager ในซีน — AutoSpawn ไม่ทำงาน (นี่คือสาเหตุที่การ์ดไม่ขึ้น)");
                return;
            }
            if (cm.HasPending)
            {
                Debug.LogWarning($"[Cards] มีการ์ด '{cm.Pending.cardId}' ค้างอยู่ — ตอบใบนั้นก่อน หรือใช้ Clear Stuck Card");
                return;
            }
            var card = cm.ForcePresent(cardId);
            if (card != null)
                Debug.Log($"[Cards] สั่งแสดง '{cardId}' แล้ว — ถ้าจอไม่ขึ้นแผง แปลว่าปัญหาอยู่ที่ UI ไม่ใช่เงื่อนไข");
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[Cards] ต้องกด Play ก่อน — เครื่องมือนี้อ่านสถานะเกมที่กำลังรันอยู่");
            return false;
        }
    }
}
