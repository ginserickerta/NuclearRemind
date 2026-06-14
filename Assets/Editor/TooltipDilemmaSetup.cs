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

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            SetupTooltip(canvasGO.transform, tooltip, font);
            SetupDilemmaPopup(canvasGO.transform, font);

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
            // ===== Full-screen overlay =====
            var overlay = CreatePanel("DilemmaPopupPanel", canvasTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.6f);

            // ===== Dialog box (center) =====
            var dialog = CreatePanel("DialogBox", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 320));
            var dialogImage = dialog.AddComponent<Image>();
            dialogImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // Scenario text (top)
            var scenarioText = CreateText("ScenarioText", dialog.transform, font, "Dilemma scenario text", 20, new Vector2(660, 180), TextAnchor.UpperLeft);
            var scenarioRect = scenarioText.GetComponent<RectTransform>();
            scenarioRect.anchorMin = new Vector2(0.5f, 1f);
            scenarioRect.anchorMax = new Vector2(0.5f, 1f);
            scenarioRect.pivot = new Vector2(0.5f, 1f);
            scenarioRect.anchoredPosition = new Vector2(0, -20);

            // Choice A button (bottom-left)
            var choiceAButton = CreateButton("ChoiceAButton", dialog.transform, font, "Choice A", new Color(0.2f, 0.5f, 0.8f), new Vector2(320, 60));
            var choiceARect = choiceAButton.GetComponent<RectTransform>();
            choiceARect.anchorMin = new Vector2(0f, 0f);
            choiceARect.anchorMax = new Vector2(0f, 0f);
            choiceARect.pivot = new Vector2(0f, 0f);
            choiceARect.anchoredPosition = new Vector2(20, 20);

            // Choice B button (bottom-right)
            var choiceBButton = CreateButton("ChoiceBButton", dialog.transform, font, "Choice B", new Color(0.8f, 0.4f, 0.2f), new Vector2(320, 60));
            var choiceBRect = choiceBButton.GetComponent<RectTransform>();
            choiceBRect.anchorMin = new Vector2(1f, 0f);
            choiceBRect.anchorMax = new Vector2(1f, 0f);
            choiceBRect.pivot = new Vector2(1f, 0f);
            choiceBRect.anchoredPosition = new Vector2(-20, 20);

            overlay.SetActive(false);

            // ===== DilemmaPopupController =====
            var controllerGO = new GameObject("DilemmaPopupController", typeof(RectTransform));
            controllerGO.transform.SetParent(canvasTransform, false);
            var controller = controllerGO.AddComponent<DilemmaPopupController>();

            controller.popupPanel = overlay;
            controller.scenarioText = scenarioText;
            controller.choiceAText = choiceAButton.GetComponentInChildren<Text>();
            controller.choiceBText = choiceBButton.GetComponentInChildren<Text>();
            controller.choiceAButton = choiceAButton.GetComponent<Button>();
            controller.choiceBButton = choiceBButton.GetComponent<Button>();

            UnityEventTools.AddPersistentListener(controller.choiceAButton.onClick, new UnityAction(controller.ChooseA));
            UnityEventTools.AddPersistentListener(controller.choiceBButton.onClick, new UnityAction(controller.ChooseB));

            EditorUtility.SetDirty(controller);
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
