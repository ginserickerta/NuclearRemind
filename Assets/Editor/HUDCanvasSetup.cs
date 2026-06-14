using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// สร้าง Canvas + UI elements (Slider/Text/Panel) สำหรับ HUD แบบ placeholder
    /// และผูก reference เข้า UIManagerHUD ให้อัตโนมัติ — รันครั้งเดียวจาก Editor
    /// </summary>
    public static class HUDCanvasSetup
    {
        [MenuItem("NuclearReMind/Setup HUD Canvas")]
        public static void SetupHUD()
        {
            var hudGO = GameObject.Find("UIManagerHUD");
            if (hudGO == null)
            {
                Debug.LogError("[HUDCanvasSetup] ไม่พบ GameObject 'UIManagerHUD' ใน scene");
                return;
            }
            var hud = hudGO.GetComponent<UIManagerHUD>();
            if (hud == null)
            {
                Debug.LogError("[HUDCanvasSetup] GameObject 'UIManagerHUD' ไม่มี component UIManagerHUD");
                return;
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ===== Canvas =====
            var canvasGO = new GameObject("HUDCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem", typeof(RectTransform));
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // ===== Resource panel (top-left) =====
            var resourcePanel = CreatePanel("ResourcePanel", canvasGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(260, 180));
            var resourceLayout = resourcePanel.AddComponent<VerticalLayoutGroup>();
            resourceLayout.spacing = 6f;
            resourceLayout.childControlHeight = false;
            resourceLayout.childForceExpandHeight = false;

            hud.foodBar = CreateResourceBar("FoodBar", resourcePanel.transform, font, new Color(0.4f, 0.8f, 0.2f));
            hud.waterBar = CreateResourceBar("WaterBar", resourcePanel.transform, font, new Color(0.2f, 0.6f, 1f));
            hud.radiationProtectionBar = CreateResourceBar("RadiationProtectionBar", resourcePanel.transform, font, new Color(0.8f, 0.6f, 1f));
            hud.energyBar = CreateResourceBar("EnergyBar", resourcePanel.transform, font, new Color(1f, 0.8f, 0.2f));
            hud.workersBar = CreateResourceBar("WorkersBar", resourcePanel.transform, font, new Color(0.8f, 0.8f, 0.8f));

            // ===== Tower panel (top-center) =====
            var towerPanel = CreatePanel("TowerPanel", canvasGO.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(320, 60));
            hud.towerPhaseText = CreateText("TowerPhaseText", towerPanel.transform, font, "CORE TOWER — Phase 1/3", 18, new Vector2(0, -2), new Vector2(320, 24), TextAnchor.UpperCenter);
            hud.towerProgressBar = CreateSlider("TowerProgressBar", towerPanel.transform, new Color(1f, 0.4f, 0.2f), new Vector2(0, -30), new Vector2(320, 20));

            // ===== Population panel (top-right) =====
            var popPanel = CreatePanel("PopulationPanel", canvasGO.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(260, 110));
            var popLayout = popPanel.AddComponent<VerticalLayoutGroup>();
            popLayout.spacing = 6f;
            popLayout.childControlHeight = false;
            popLayout.childForceExpandHeight = false;

            hud.populationText = CreateTextRow("PopulationText", popPanel.transform, font, "Population: 50");
            hud.trustText = CreateTextRow("TrustText", popPanel.transform, font, "Trust: 70%");
            hud.trustBar = CreateSliderRow("TrustBar", popPanel.transform, new Color(1f, 0.9f, 0.2f));
            hud.strikeWarning = CreateWarningText("StrikeWarning", popPanel.transform, font, "WORKERS ON STRIKE", Color.red);

            // ===== Riot warning (center) =====
            hud.riotWarning = CreateCenterWarning("RiotWarning", canvasGO.transform, font, "RIOT! — Trust Collapsed", Color.red);

            // ===== Game Over panel (full screen) =====
            var goPanel = CreatePanel("GameOverPanel", canvasGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var goRect = goPanel.GetComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;
            var goImage = goPanel.AddComponent<Image>();
            goImage.color = new Color(0f, 0f, 0f, 0.75f);
            hud.gameOverText = CreateText("GameOverText", goPanel.transform, font, "", 36, Vector2.zero, new Vector2(1200, 200), TextAnchor.MiddleCenter);
            var goTextRect = hud.gameOverText.GetComponent<RectTransform>();
            goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            goTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            hud.gameOverPanel = goPanel;
            goPanel.SetActive(false);

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[HUDCanvasSetup] สร้าง HUD Canvas และผูก reference เข้า UIManagerHUD สำเร็จ — กด Save Scene (Ctrl+S)");
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

        private static UIManagerHUD.ResourceBarUI CreateResourceBar(string name, Transform parent, Font font, Color fillColor)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(260, 26);

            var slider = CreateSlider(name + "Slider", row.transform, fillColor, new Vector2(-30, 0), new Vector2(170, 20));
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0, 0);

            var valueText = CreateText(name + "ValueText", row.transform, font, "0 / 0", 16, new Vector2(0, 0), new Vector2(80, 24), TextAnchor.MiddleRight);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0, 0);

            var fillImage = slider.transform.Find("Fill Area/Fill").GetComponent<Image>();

            return new UIManagerHUD.ResourceBarUI
            {
                bar = slider,
                valueText = valueText,
                fillImage = fillImage
            };
        }

        private static Text CreateTextRow(string name, Transform parent, Font font, string content)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 24);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleRight;
            text.text = content;
            return text;
        }

        private static Slider CreateSliderRow(string name, Transform parent, Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 20);
            return BuildSlider(go, fillColor);
        }

        private static GameObject CreateWarningText(string name, Transform parent, Font font, string content, Color color)
        {
            var text = CreateTextRow(name, parent, font, content);
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.gameObject.SetActive(false);
            return text.gameObject;
        }

        private static GameObject CreateCenterWarning(string name, Transform parent, Font font, string content, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -100);
            rect.sizeDelta = new Vector2(600, 40);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            go.SetActive(false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;
            return text;
        }

        private static Slider CreateSlider(string name, Transform parent, Color fillColor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return BuildSlider(go, fillColor);
        }

        private static Slider BuildSlider(GameObject go, Color fillColor)
        {
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = fillColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }
    }
}
