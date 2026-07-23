using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: เครื่องมือ Editor ช่วยเซฟค่าที่จัด object ตอนกด Play ไม่ให้หายเมื่อหยุดเล่น
    /// Play Mode Saver — เก็บค่าที่จัด/ปรับตอน Play Mode ไว้ แล้วเขียนกลับซีนหลังหยุดเล่น
    ///
    /// ปัญหา: Unity ตั้งใจให้การแก้ object ในซีนตอน Play หายหมดเมื่อกดหยุด
    /// ทางแก้: ตอนเล่น เลือก object ที่จัดเสร็จ → กด "Capture Selection" → พอออก Play มันเขียนค่ากลับให้อัตโนมัติ
    ///
    /// วิธีทำงาน:
    ///   • capture: EditorJsonUtility.ToJson ของทุก component ในตัวที่เลือก · key ด้วย GlobalObjectId (คงที่ข้าม play/edit)
    ///   • snapshot เก็บใน SessionState (รอด domain reload ตอนสลับ play→edit)
    ///   • restore: พอ EnteredEditMode → หา component เดิมจาก GlobalObjectId แล้ว FromJsonOverwrite ค่ากลับ
    ///
    /// ★ ใช้ได้เฉพาะ object ที่ "มีอยู่ในซีน" (scene object) — ตัวที่ Instantiate ตอน runtime ล้วนจะ map กลับไม่ได้ (ข้าม)
    /// ★ เป็น dev tool ใน Editor เท่านั้น · เหมาะจัด layout/จูนค่า ไม่ใช่ระบบเซฟเกม (ใช้ SaveManager สำหรับ state จริง)
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSaver
    {
        private const string SessionKey = "NuclearReMind.PlayModeSaver.Snapshot";
        private const string MenuCapture = "NuclearReMind/Play Mode Saver/Capture Selection %#k"; // Ctrl+Shift+K
        private const string MenuClear = "NuclearReMind/Play Mode Saver/Clear Pending";

        [Serializable]
        private class CompSnap
        {
            public string globalId; // GlobalObjectId ของ component (คงที่ข้าม play/edit สำหรับ scene object)
            public string typeName; // ไว้ log ตอน map ไม่เจอ
            public string json;     // EditorJsonUtility ของ component
        }

        [Serializable]
        private class Snapshot
        {
            public List<CompSnap> comps = new List<CompSnap>();
            public int objectCount;
        }

        static PlayModeSaver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // ── capture (ตอน Play) ─────────────────────────────────────────
        // เมนูนี้: เก็บ snapshot ของ object ที่เลือกไว้ตอน Play (Ctrl+Shift+K) เพื่อเขียนกลับซีนหลังหยุดเล่น
        [MenuItem(MenuCapture)]
        private static void CaptureSelection()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PlayModeSaver] ต้องอยู่ใน Play Mode ตอนกด Capture (จัด object ให้เสร็จก่อน แล้วค่อยกด)");
                return;
            }

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[PlayModeSaver] ยังไม่ได้เลือก object — เลือกตัวที่จัดไว้ใน Hierarchy ก่อน");
                return;
            }

            var snap = new Snapshot { objectCount = selected.Length };
            foreach (var go in selected)
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue; // missing script
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(comp).ToString();
                    snap.comps.Add(new CompSnap
                    {
                        globalId = gid,
                        typeName = comp.GetType().Name,
                        json = EditorJsonUtility.ToJson(comp)
                    });
                }
            }

            SessionState.SetString(SessionKey, JsonUtility.ToJson(snap));
            Debug.Log($"[PlayModeSaver] ✅ เก็บ {selected.Length} object ({snap.comps.Count} component) — " +
                      "ค่าจะถูกเขียนกลับซีนอัตโนมัติเมื่อออก Play Mode");
        }

        [MenuItem(MenuCapture, validate = true)]
        private static bool CaptureValidate() => EditorApplication.isPlaying;

        // เมนูนี้: ยกเลิก snapshot ที่ค้างอยู่ (จะไม่เขียนค่ากลับตอนออก Play)
        [MenuItem(MenuClear)]
        private static void ClearPending()
        {
            SessionState.EraseString(SessionKey);
            Debug.Log("[PlayModeSaver] ล้าง snapshot ที่ค้างแล้ว (จะไม่เขียนกลับตอนออก Play)");
        }

        // ── restore (ตอนกลับ Edit Mode) ────────────────────────────────
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            var raw = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            SessionState.EraseString(SessionKey); // ใช้ครั้งเดียว กัน apply ซ้ำ

            Snapshot snap;
            try { snap = JsonUtility.FromJson<Snapshot>(raw); }
            catch (Exception e) { Debug.LogError($"[PlayModeSaver] อ่าน snapshot ไม่ได้: {e.Message}"); return; }
            if (snap == null || snap.comps == null) return;

            int applied = 0, skipped = 0;
            foreach (var cs in snap.comps)
            {
                if (!GlobalObjectId.TryParse(cs.globalId, out var gid)) { skipped++; continue; }

                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj is Component comp && comp != null)
                {
                    Undo.RecordObject(comp, "Play Mode Saver Restore");
                    EditorJsonUtility.FromJsonOverwrite(cs.json, comp);
                    EditorUtility.SetDirty(comp);
                    applied++;
                }
                else
                {
                    // ตัวที่ Instantiate ตอน runtime หรือถูกลบไปแล้ว — map กลับไม่ได้
                    skipped++;
                }
            }

            if (applied > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[PlayModeSaver] เขียนค่ากลับ {applied} component" +
                      (skipped > 0 ? $" · ข้าม {skipped} (runtime-only/ถูกลบ)" : "") +
                      " — กด Ctrl+S เพื่อบันทึกซีน");
        }
    }
}
