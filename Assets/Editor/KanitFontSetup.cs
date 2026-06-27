using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.Editor
{
    public static class KanitFontSetup
    {
        private const string Regular  = "Assets/Resources/Fonts/Kanit-Regular.ttf";
        private const string Bold     = "Assets/Resources/Fonts/Kanit-Bold.ttf";
        private const string SemiBold = "Assets/Resources/Fonts/Kanit-SemiBold.ttf";

        [MenuItem("NuclearReMind/Apply Kanit Font (Scene + Prefabs)")]
        public static void ApplyAll()
        {
            var regular  = AssetDatabase.LoadAssetAtPath<Font>(Regular);
            var bold     = AssetDatabase.LoadAssetAtPath<Font>(Bold);
            var semiBold = AssetDatabase.LoadAssetAtPath<Font>(SemiBold);

            if (regular == null)
            {
                Debug.LogError("[KanitFontSetup] ไม่พบ Kanit-Regular.ttf ใน Assets/Resources/Fonts/");
                return;
            }

            int changed = 0;

            // ── Scene objects ──────────────────────────────────────────────
            foreach (var txt in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                var chosen = ChooseWeight(txt, regular, bold, semiBold);
                if (txt.font == chosen) continue;
                Undo.RecordObject(txt, "Apply Kanit Font");
                txt.font = chosen;
                EditorUtility.SetDirty(txt);
                changed++;
            }

            // ── Prefabs in Assets/Prefabs/ ─────────────────────────────────
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            foreach (var guid in guids)
            {
                var path   = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool dirty = false;
                foreach (var txt in prefab.GetComponentsInChildren<Text>(true))
                {
                    var chosen = ChooseWeight(txt, regular, bold, semiBold);
                    if (txt.font == chosen) continue;
                    txt.font = chosen;
                    dirty = true;
                    changed++;
                }
                if (dirty)
                    PrefabUtility.SavePrefabAsset(prefab);
            }

            Debug.Log($"[KanitFontSetup] เปลี่ยน font เป็น Kanit ทั้งหมด {changed} components");
            EditorUtility.DisplayDialog("Kanit Font Setup",
                $"เปลี่ยน font เป็น Kanit สำเร็จ {changed} components\nอย่าลืม Save Scene (Ctrl+S)", "OK");
        }

        private static Font ChooseWeight(Text txt, Font regular, Font bold, Font semiBold)
        {
            if (txt.fontStyle == FontStyle.Bold || txt.fontStyle == FontStyle.BoldAndItalic)
                return bold ?? regular;

            var name = txt.gameObject.name.ToLower();
            if (name.Contains("title") || name.Contains("header") || name.Contains("key"))
                return semiBold ?? regular;

            return regular;
        }
    }
}
