using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor สร้างแผงเควสรายวัน (Day 2-30) ใต้ HUDCanvas
    /// พร้อมสร้าง QuestSchedule asset และต่อสาย QuestPanelController ให้อัตโนมัติ
    /// Quest Panel (Day 2-30) — day-guidance popup styled like the Day-1 tutorial checklist.
    /// Builds the panel under HUDCanvas, ensures the QuestSchedule asset exists
    /// (with sample entries on first creation), and wires QuestPanelController.
    /// Re-runnable: deletes the old panel first, keeps the schedule asset untouched.
    /// </summary>
    public static class QuestPanelSetup
    {
        private const string AssetDir = "Assets/Resources/Quests";
        private const string AssetPath = AssetDir + "/QuestSchedule.asset";

        // เมนูนี้: สร้างแผงเควสรายวันใน HUD (ลบของเก่าแล้วสร้างใหม่ — รันซ้ำได้)
        [MenuItem("NuclearReMind/Setup Quest Panel (Day 2-30)")]
        public static void Setup()
        {
            var canvasGO = GameObject.Find("HUDCanvas");
            if (canvasGO == null)
            {
                Debug.LogError("[QuestPanelSetup] ไม่พบ HUDCanvas — รัน 'Setup HUD Canvas' ก่อน");
                return;
            }

            var font = HUDCanvasSetup.LoadFont();
            var schedule = EnsureScheduleAsset();

            // re-runnable: drop the previous panel (Ctrl+Z restores)
            var old = canvasGO.transform.Find("QuestPanel");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // ── panel — same left corner as the tutorial checklist ──
            // (no clash: tutorial shows Day 1 only, quests start Day 2)
            var panel = new GameObject("QuestPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20, 107);
            rect.sizeDelta = new Vector2(340, 190);

            HUDCanvasSetup.EnsureIconImported("TutorialBg");
            var bg = panel.AddComponent<Image>();
            var bgSprite = HUDCanvasSetup.LoadIcon("TutorialBg");
            if (bgSprite != null) { bg.sprite = bgSprite; bg.type = Image.Type.Simple; bg.color = Color.white; }
            else bg.color = Palette.PanelBg;

            // title — pinned to the top edge so the panel can grow downward
            var title = MakeText("QuestTitle", panel.transform, font, "ภารกิจ Day 2 —", 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(-20, 26));
            title.color = Palette.Accent;
            title.fontStyle = FontStyle.Bold;

            // body — bullet list; height resized at runtime from preferredHeight
            var body = MakeText("QuestBody", panel.transform, font, "▸ …", 14, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(-24, 96));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.color = Palette.TextPrimary;

            // close button — same skin as the tutorial start button, pinned to the bottom edge
            var closeBtn = MakeButton("QuestCloseButton", panel.transform, font, "เข้าใจแล้ว ✔",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(300, 38));
            HUDCanvasSetup.EnsureIconImported("TutorialButton");
            var btnSprite = HUDCanvasSetup.LoadIcon("TutorialButton");
            if (btnSprite != null) { closeBtn.image.sprite = btnSprite; closeBtn.image.type = Image.Type.Simple; closeBtn.image.color = Color.white; }
            else closeBtn.image.color = Palette.Accent;
            var closeLabel = closeBtn.GetComponentInChildren<Text>();
            if (closeLabel != null) { closeLabel.color = Color.white; closeLabel.fontSize = 15; }

            // ── controller (found-or-create → survives re-run) ──
            var ctrlGO = GameObject.Find("QuestPanelController") ?? new GameObject("QuestPanelController");
            var ctrl = ctrlGO.GetComponent<QuestPanelController>() ?? ctrlGO.AddComponent<QuestPanelController>();
            ctrl.panel = panel;
            ctrl.panelRect = rect;
            ctrl.titleText = title;
            ctrl.bodyText = body;
            ctrl.closeButton = closeBtn;
            ctrl.schedule = schedule;
            EditorUtility.SetDirty(ctrl);

            panel.SetActive(false); // controller shows it when a quest day starts
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[QuestPanelSetup] สร้าง QuestPanel + QuestPanelController สำเร็จ — " +
                      $"แก้เนื้อหา/เพิ่มวันได้ที่ {AssetPath} (กด Save Scene)");
        }

        // Create the schedule asset on first run only — sample entries marked (ตัวอย่าง)
        // so the designer sees the format; later runs never overwrite edited content.
        private static QuestScheduleSO EnsureScheduleAsset()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<QuestScheduleSO>(AssetPath);
            if (schedule != null) return schedule;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(AssetDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Quests");

            schedule = ScriptableObject.CreateInstance<QuestScheduleSO>();
            schedule.entries = new List<QuestScheduleSO.DayQuest>
            {
                new QuestScheduleSO.DayQuest
                {
                    day = 2, title = "ตั้งฐานการผลิตให้มั่นคง (ตัวอย่าง)",
                    tasks = new List<string>
                    {
                        "สร้างโรงไฟฟ้า/โรงน้ำเพิ่ม ให้ผลิตพอเลี้ยงทั้งเมือง",
                        "จัดคนงานเข้าอาคารให้ครบทุกหลัง",
                        "เริ่มเก็บเหล็กไว้อัปเกรดอาคาร",
                    },
                },
                new QuestScheduleSO.DayQuest
                {
                    day = 3, title = "ขยายเมือง (ตัวอย่าง)",
                    tasks = new List<string>
                    {
                        "ขยาย Shelter เพิ่มเพดานประชากร",
                        "อัปเกรดโรงงานที่คนงานเต็มแล้ว",
                    },
                },
                new QuestScheduleSO.DayQuest
                {
                    day = 5, title = "เตรียมเข้าสู่เฟสถัดไป (ตัวอย่าง)",
                    tasks = new List<string>
                    {
                        "สะสมเหล็กไว้สร้างห้องปฏิบัติการ",
                        "ดูแลอาหาร/น้ำอย่าให้ขาด",
                    },
                },
            };
            AssetDatabase.CreateAsset(schedule, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestPanelSetup] สร้างแอสเซ็ตตารางเควสต์ใหม่ที่ {AssetPath} (มีตัวอย่าง 3 วัน — แก้/เพิ่มได้เลย)");
            return schedule;
        }

        // ── local UI helpers (anchor-aware variants of HUDCanvasSetup's) ──

        private static Text MakeText(string name, Transform parent, Font font, string content, int size, TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = align;
            text.text = content;
            text.verticalOverflow = VerticalWrapMode.Overflow; // Kanit line height > box — Truncate would blank the line
            return text;
        }

        private static Button MakeButton(string name, Transform parent, Font font, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.95f, 1f);
            cb.pressedColor = new Color(0.8f, 0.85f, 0.9f);
            btn.colors = cb;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trect = textGO.GetComponent<RectTransform>();
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return btn;
        }
    }
}
