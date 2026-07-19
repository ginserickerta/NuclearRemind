using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง UI สำหรับ Tooltip 3 ชั้น และ Dilemma Popup แบบ placeholder
    /// และผูก reference เข้า TooltipController / DilemmaPopupController ให้อัตโนมัติ — รันครั้งเดียวจาก Editor
    /// </summary>
    public static class TooltipDilemmaSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";

        [MenuItem("NuclearReMind/Setup Tooltip and Dilemma UI")]
        public static void SetupAll()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvasGO = GameObject.Find("HUDCanvas");
            if (canvasGO == null)
            {
                Debug.LogError("[TooltipDilemmaSetup] ไม่พบ GameObject 'HUDCanvas' ใน scene — รัน NuclearReMind/Setup HUD Canvas ก่อน");
                return;
            }

            var tooltipGO = GameObject.Find("TooltipController");
            var tooltip = tooltipGO != null ? tooltipGO.GetComponent<TooltipController>() : null;
            if (tooltip == null)
            {
                Debug.LogError("[TooltipDilemmaSetup] ไม่พบ GameObject 'TooltipController' (หรือไม่มี component TooltipController) ใน scene");
                return;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(RectTransform));
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // idempotent — ลบของเดิมก่อนสร้างใหม่ (รันเมนูซ้ำไม่ทับซ้อน)
            RemoveChild(canvasGO.transform, "TooltipPanel");
            RemoveChild(canvasGO.transform, "DilemmaPopupPanel");
            RemoveChild(canvasGO.transform, "DilemmaPopupController");

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            SetupTooltip(canvasGO.transform, tooltip, font);
            // Dilemma popup ไม่ถูกสร้างอีกแล้ว (v6.3 cutover) — CrisisCardPanelUI แทนที่แล้ว และ
            // DisableLegacy() ก็ปิดตัวเก่าตอนรันอยู่แล้ว · RemoveChild ข้างบนยังทำงาน = รันเมนูนี้
            // จะ "เก็บกวาด" ของเก่าออกจากซีนให้ด้วย แทนที่จะสร้างใหม่ทุกครั้ง

            EditorUtility.SetDirty(tooltip);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[TooltipDilemmaSetup] สร้าง Tooltip + Dilemma Popup UI, ผูก reference, และ save scene สำเร็จ");
        }

        private static void SetupTooltip(Transform canvasTransform, TooltipController tooltip, Font font)
        {
            // ===== Tooltip panel (bottom-left) =====
            var panel = CreatePanel("TooltipPanel", canvasTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20, 20), new Vector2(420, 180));
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            var nameCostText = CreateText("NameCostText", panel.transform, font, "Building Name\nMaterial 0 / Energy 0 / Worker 0", 18, new Vector2(396, 50), TextAnchor.UpperLeft);
            nameCostText.fontStyle = FontStyle.Bold;

            var descriptionText = CreateText("DescriptionText", panel.transform, font, "Description", 14, new Vector2(396, 60), TextAnchor.UpperLeft);

            var nuclearKnowledgeText = CreateText("NuclearKnowledgeText", panel.transform, font, "Nuclear Knowledge", 13, new Vector2(396, 60), TextAnchor.UpperLeft);
            nuclearKnowledgeText.color = new Color(0.6f, 0.9f, 1f);

            tooltip.tooltipPanel = panel;
            tooltip.nameCostText = nameCostText;
            tooltip.descriptionText = descriptionText;
            tooltip.nuclearKnowledgeText = nuclearKnowledgeText;

            panel.SetActive(false);
        }

        private static void SetupDilemmaPopup(Transform canvasTransform, Font font)
        {
            // สกินโลหะสนิม (frame_metal 9-slice มีหมุด/แผ่นมุมในตัว) + แผ่นอินเซ็ต + ปุ่มโลหะ — fallback สีถ้าไม่มีไฟล์
            var frameMetal = LoadUISkin("Assets/Resources/CodexUI/frame_metal.png");
            var plateInset = LoadUISkin("Assets/Resources/StoryUI/plate_inset.png");
            var btnMetal   = LoadUISkin("Assets/Resources/StoryUI/btn_metal.png");

            Color gold  = new Color(0.94f, 0.77f, 0.29f);
            Color light = new Color(0.90f, 0.90f, 0.86f);

            // ===== overlay เต็มจอ (บังคลิกทะลุ) =====
            var overlay = CreatePanel("DilemmaPopupPanel", canvasTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

            // ===== Dialog กรอบโลหะ (กลางจอ) =====
            const float DW = 940f, DH = 600f, P = 30f;
            var dialog = CreatePanel("DialogBox", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DW, DH));
            var dialogImage = dialog.AddComponent<Image>();
            if (frameMetal != null) { dialogImage.sprite = frameMetal; dialogImage.type = Image.Type.Sliced; dialogImage.color = Color.white; }
            else dialogImage.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);
            var dlgT = dialog.transform;

            float leftW = 540f, colGap = 22f;
            float rightX = P + leftW + colGap;
            float rightW = DW - rightX - P;
            float confirmH = 58f;
            float contentBottom = DH - P - confirmH - 16f;

            // --- คอลัมน์ซ้าย: กล่องหัวเรื่อง (ไฮไลต์ขอบเหลือง) ---
            float titleH = 66f;
            var titlePlate = MetalPlate("TitlePlate", dlgT, plateInset, P, P, leftW, titleH);
            var titleOutline = titlePlate.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = gold;
            titleOutline.effectDistance = new Vector2(3f, -3f);
            var titleText = CreateText("TitleText", titlePlate.transform, font, "วิกฤต", 30, new Vector2(leftW, titleH), TextAnchor.MiddleLeft);
            FillParentText(titleText, 16f);
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = gold;
            titleText.resizeTextForBestFit = true; titleText.resizeTextMinSize = 16; titleText.resizeTextMaxSize = 32;

            // --- เส้นคั่น ---
            float divY = P + titleH + 12f;
            var divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(dlgT, false);
            var divImg = divider.AddComponent<Image>();
            divImg.color = new Color(gold.r, gold.g, gold.b, 0.55f);
            PlaceTL(divImg.rectTransform, P, divY, leftW, 3f);

            // --- กล่องคำอธิบาย ---
            float descY = divY + 12f;
            float descH = contentBottom - descY;
            var descPlate = MetalPlate("DescriptionPlate", dlgT, plateInset, P, descY, leftW, descH);
            var scenarioText = CreateText("ScenarioText", descPlate.transform, font, "คำอธิบายวิกฤต …", 20, new Vector2(leftW, descH), TextAnchor.UpperLeft);
            FillParentText(scenarioText, 18f);
            scenarioText.color = light;
            scenarioText.resizeTextForBestFit = true; scenarioText.resizeTextMinSize = 14; scenarioText.resizeTextMaxSize = 22;

            // --- คอลัมน์ขวา: ปุ่มเลือก 3 อันเรียงตั้ง A/B/C เต็มความกว้างคอลัมน์ ---
            float choiceH = 96f, choiceGap = 14f;
            var (aBtn, aTxt) = MakeChoice("ChoiceAButton", dlgT, font, plateInset, 'A', rightX, P + 0 * (choiceH + choiceGap), rightW, choiceH, light);
            var (bBtn, bTxt) = MakeChoice("ChoiceBButton", dlgT, font, plateInset, 'B', rightX, P + 1 * (choiceH + choiceGap), rightW, choiceH, light);
            var (cBtn, cTxt) = MakeChoice("ChoiceCButton", dlgT, font, plateInset, 'C', rightX, P + 2 * (choiceH + choiceGap), rightW, choiceH, light);

            // --- ปุ่มยืนยัน (ล่างเต็มกว้าง) ---
            var confirmGO = MakeMetalButton("ConfirmButton", dlgT, font, btnMetal != null ? btnMetal : plateInset, "ยืนยัน", gold, P, DH - P - confirmH, DW - 2 * P, confirmH);
            var confirmBtn = confirmGO.GetComponent<Button>();

            overlay.SetActive(false);

            // ⚠ ARCHIVED (v6.3 cutover) — ไม่มีใครเรียกแล้ว เก็บไว้เผื่อต้องรื้อ layout ปุ่ม A/B/C มาใช้ซ้ำ
            // ===== DilemmaPopupController =====
            var controllerGO = new GameObject("DilemmaPopupController", typeof(RectTransform));
            controllerGO.transform.SetParent(canvasTransform, false);
            var controller = controllerGO.AddComponent<DilemmaPopupController>();

            controller.popupPanel = overlay;
            controller.titleText = titleText;
            controller.scenarioText = scenarioText;
            controller.choiceAText = aTxt;
            controller.choiceBText = bTxt;
            controller.choiceCText = cTxt;
            controller.choiceAButton = aBtn;
            controller.choiceBButton = bBtn;
            controller.choiceCButton = cBtn;
            controller.confirmButton = confirmBtn;

            // แตะ A/B/C = เลือก (ไฮไลต์) · ยืนยัน = commit — ผูก persistent listener
            UnityEventTools.AddPersistentListener(aBtn.onClick, new UnityAction(controller.SelectA));
            UnityEventTools.AddPersistentListener(bBtn.onClick, new UnityAction(controller.SelectB));
            UnityEventTools.AddPersistentListener(cBtn.onClick, new UnityAction(controller.SelectC));
            UnityEventTools.AddPersistentListener(confirmBtn.onClick, new UnityAction(controller.Confirm));

            EditorUtility.SetDirty(controller);
        }

        private static Sprite LoadUISkin(string assetPath) => AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        private static void RemoveChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        // วางลูกโดยยึดมุมซ้าย-บนของ parent (x ไปขวา · y ลงล่าง) — อ่าน layout กรอบง่าย
        private static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // แผ่นโลหะ (plate_inset 9-slice · tint ขาว = โชว์สีจริง) — fallback สีเข้มถ้าไม่มีสไปรต์
        private static Image MetalPlate(string name, Transform parent, Sprite plate, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (plate != null) { img.sprite = plate; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.12f, 0.13f, 0.17f, 0.98f);
            PlaceTL(img.rectTransform, x, y, w, h);
            return img;
        }

        // ยืด Text ให้เต็ม parent + padding (word-wrap ในกล่อง ไม่ล้นกรอบ)
        private static void FillParentText(Text t, float padding)
        {
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        // ปุ่มตัวเลือก A/B/C — แผ่นโลหะ + ชิปตัวอักษร + ป้าย + Outline เรืองขอบ (ปิดไว้ · controller เปิดตอนเลือก)
        private static (Button btn, Text label) MakeChoice(string name, Transform parent, Font font, Sprite plate, char letter, float x, float y, float w, float h, Color labelColor)
        {
            var img = MetalPlate(name, parent, plate, x, y, w, h);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.pressedColor = new Color(0.70f, 0.70f, 0.70f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.80f, 0.30f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            // ชิปตัวอักษร A/B/C (ซ้าย · จัตุรัสกลางแนวตั้ง)
            float chip = h - 24f;
            var chipGO = new GameObject("Chip", typeof(RectTransform));
            chipGO.transform.SetParent(img.transform, false);
            var chipImg = chipGO.AddComponent<Image>();
            chipImg.color = new Color(0.20f, 0.47f, 0.80f, 1f);
            PlaceTL(chipImg.rectTransform, 12f, 12f, chip, chip);
            var chipTxt = CreateText("ChipText", chipGO.transform, font, letter.ToString(), 30, new Vector2(chip, chip), TextAnchor.MiddleCenter);
            FillParentText(chipTxt, 0f);
            chipTxt.fontStyle = FontStyle.Bold;
            chipTxt.color = Color.white;

            // ป้ายตัวเลือก (ขวาของชิป · word-wrap + best-fit)
            var label = CreateText(name + "Text", img.transform, font, "ตัวเลือก", 19, new Vector2(w, h), TextAnchor.MiddleLeft);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = new Vector2(12f + chip + 12f, 10f);
            lrt.offsetMax = new Vector2(-12f, -10f);
            label.color = labelColor;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 20;

            return (btn, label);
        }

        // ปุ่มโลหะป้ายกลาง (ใช้กับปุ่มยืนยัน) — มี disabledColor ให้ดูหรี่ตอนยังเลือกไม่ครบ
        private static GameObject MakeMetalButton(string name, Transform parent, Font font, Sprite plate, string label, Color labelColor, float x, float y, float w, float h)
        {
            var img = MetalPlate(name, parent, plate, x, y, w, h);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.pressedColor = new Color(0.70f, 0.70f, 0.70f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var t = CreateText(name + "Text", img.transform, font, label, 26, new Vector2(w, h), TextAnchor.MiddleCenter);
            FillParentText(t, 6f);
            t.fontStyle = FontStyle.Bold;
            t.color = labelColor;
            return img.gameObject;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        private static GameObject CreateButton(string name, Transform parent, Font font, string label, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(name + "Text", go.transform, font, label, 18, size, TextAnchor.MiddleCenter);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }
    }
}
