using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Bakes the code-built Research Lab panel into editable prefabs so the owner can tune every
    /// position/size/color/font in the Inspector instead of asking for code edits per pixel.
    ///
    /// Produces 6 prefabs in Assets/Resources/ResearchUI/:
    ///   ResearchLabPanel   — the whole panel (backdrop → frame → header → stats → sidebar → list)
    ///   Row_Note           — research-topic row (title, badges, meta, hint, start button, bar)
    ///   Row_RecordActive   — record-being-decoded row (head, hint, decode button, bar)
    ///   Row_RecordDone     — recovered-record row
    ///   Row_Simple         — locked/placeholder text row
    ///   Chip_Mastery       — mastery chip
    ///
    /// ResearchLabPanelUI loads these at runtime when present (falls back to the code-built look
    /// when a prefab is missing). Behaviour (listeners, live text, visibility) is rewired by code
    /// on load, so the prefabs carry ONLY layout and skin — safe to edit freely. Idempotent:
    /// re-running overwrites the prefabs with the code-default layout (a reset button, in effect).
    /// Run: menu NuclearReMind > Setup Research Lab Prefabs.
    /// </summary>
    public static class ResearchLabPrefabSetup
    {
        private const string Dir = "Assets/Resources/ResearchUI";

        [MenuItem("NuclearReMind/Setup Research Lab Prefabs")]
        public static void Apply()
        {
            Directory.CreateDirectory(Dir);
            UIFonts.ClearCache(); // edit-mode bake — make sure the font comes fresh from Resources

            var host = new GameObject("~ResearchLabPrefabBake");
            int saved = 0;
            try
            {
                var ui = host.AddComponent<ResearchLabPanelUI>(); // Awake ไม่ถูกเรียกใน edit mode — bake เรียกเองข้างใน
                saved += Save(ui.BakePanelSource(), "ResearchLabPanel");
                saved += Save(ui.BakeNoteRowTemplate(), "Row_Note");
                saved += Save(ui.BakeRecordActiveRowTemplate(), "Row_RecordActive");
                saved += Save(ui.BakeRecordDoneRowTemplate(), "Row_RecordDone");
                saved += Save(ui.BakeSimpleRowTemplate(), "Row_Simple");
                saved += Save(ui.BakeMasteryChipTemplate(), "Chip_Mastery");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ResearchLabPrefabSetup] สร้าง/อัปเดต prefab {saved}/6 ตัวที่ {Dir} — " +
                      "แก้ตำแหน่ง/ขนาด/สี/ฟอนต์ใน prefab ได้เลย เกมโหลดให้อัตโนมัติ (รันเมนูนี้ซ้ำ = รีเซ็ตกลับค่าโค้ด)");
        }

        private static int Save(GameObject src, string name)
        {
            if (src == null)
            {
                Debug.LogError($"[ResearchLabPrefabSetup] bake {name} ล้มเหลว — ได้ hierarchy ว่าง");
                return 0;
            }
            // Detach so the throwaway host never leaks into the asset, then save and clean up.
            src.transform.SetParent(null, false);
            string path = $"{Dir}/{name}.prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(src, path, out bool ok);
            Object.DestroyImmediate(src);
            if (!ok || asset == null)
            {
                Debug.LogError($"[ResearchLabPrefabSetup] เซฟ {path} ไม่สำเร็จ");
                return 0;
            }
            return 1;
        }
    }
}
