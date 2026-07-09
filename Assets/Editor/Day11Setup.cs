using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup ครบชุดสำหรับ Day 11 — รันผ่าน NuclearReMind / Setup Day 11 Systems
    /// สิ่งที่ทำ:
    ///   1. เพิ่ม ConstructionController (บาร์ก่อสร้างย้ายเข้าแผง hover แล้ว — ดูเมนู "Update Building Hover Panel")
    ///   2. สร้าง BuildingQueueUI + entry prefab ใน HUDCanvas
    ///   3. สร้าง TutorialManager + popup panel ใน HUDCanvas
    ///   4. สร้าง BuildingSelectionUI hotbar ด้านล่างจอ
    /// </summary>
    public static class Day11Setup
    {
        private const string PrefabPath = "Assets/Prefabs/UI";

        [MenuItem("NuclearReMind/Setup Day 11 Systems")]
        public static void SetupAll()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Gamescene") && !scene.name.Contains("Game"))
            {
                Debug.LogWarning("[Day11Setup] กรุณาเปิด Gamescene.unity ก่อน");
                return;
            }

            System.IO.Directory.CreateDirectory(PrefabPath);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null)
            {
                Debug.LogError("[Day11Setup] ไม่พบ HUDCanvas — รัน NuclearReMind/Setup HUD Canvas ก่อน");
                return;
            }

            SetupConstructionController();
            SetupBuildingQueueUI(hudCanvas, font);
            // Tutorial (Day 1 checklist) ย้ายไปตั้งค่าใน HUDCanvasSetup แล้ว — ดูเมนู "Setup HUD Canvas"
            SetupBuildingSelectionUI(hudCanvas, font);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Day11Setup] เสร็จแล้ว! กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. ConstructionController
        // ─────────────────────────────────────────────

        private static void SetupConstructionController()
        {
            var go = GameObject.Find("ConstructionController");
            if (go == null) go = new GameObject("ConstructionController");

            var cc = go.GetComponent<ConstructionController>() ?? go.AddComponent<ConstructionController>();
            EditorUtility.SetDirty(cc);
        }

        // ─────────────────────────────────────────────
        //  2. BuildingQueueUI (bottom-right HUD)
        // ─────────────────────────────────────────────

        private static void SetupBuildingQueueUI(GameObject hudCanvas, Font font)
        {
            // สร้าง entry prefab ใหม่ทุกครั้ง — overwrite เวอร์ชันเก่าที่ label ✕/↑ ใช้ glyph
            // ที่ Kanit ไม่มี + กล่อง 16px โดน line height ของ Kanit truncate ทั้งบรรทัด
            var entryPrefabPath = $"{PrefabPath}/QueueEntry.prefab";
            var entryPrefab = CreateQueueEntryPrefab(entryPrefabPath, font);

            // สร้าง BuildingQueuePanel ใน HUDCanvas (bottom-right)
            var panelGO = GetOrCreate("BuildingQueuePanel", hudCanvas.transform);
            {
                var rect = panelGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot     = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-10f, 60f);
                rect.sizeDelta = new Vector2(300f, 56f);

                var bg = panelGO.GetComponent<Image>() ?? panelGO.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.5f);

                var hlg = panelGO.GetComponent<HorizontalLayoutGroup>() ?? panelGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4f;
                hlg.padding = new RectOffset(4, 4, 4, 4);
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childForceExpandWidth = false;
            }

            // สร้าง header label "กำลังสร้าง:"
            var labelGO = GetOrCreate("QueueLabel", hudCanvas.transform);
            {
                var rect = labelGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot     = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-10f, 120f);
                rect.sizeDelta = new Vector2(120f, 24f);
                var txt = labelGO.GetComponent<Text>() ?? labelGO.AddComponent<Text>();
                txt.font = font; txt.fontSize = 14; txt.color = new Color(0.8f, 0.8f, 0.8f);
                txt.text = "กำลังสร้าง:"; txt.alignment = TextAnchor.MiddleRight;
            }

            // wire BuildingQueueUI component
            var uiGO = GetOrCreate("BuildingQueueUI", hudCanvas.transform);
            var queueUI = uiGO.GetComponent<BuildingQueueUI>() ?? uiGO.AddComponent<BuildingQueueUI>();
            queueUI.queueContainer = panelGO.transform;
            queueUI.entryPrefab    = entryPrefab;
            queueUI.maxVisible     = 7;
            EditorUtility.SetDirty(queueUI);
        }

        private static GameObject CreateQueueEntryPrefab(string path, Font font)
        {
            var temp = new GameObject("QueueEntry");
            var rect = temp.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(48f, 48f);

            var bg = temp.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // Icon (building sprite)
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(temp.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(2, 14); iconRect.offsetMax = new Vector2(-2, -2);
            iconGO.AddComponent<Image>().color = Color.white;

            // ProgressText "x/10"
            var txtGO = new GameObject("ProgressText", typeof(RectTransform));
            txtGO.transform.SetParent(temp.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0, 0); txtRect.anchorMax = new Vector2(1, 0);
            txtRect.pivot = new Vector2(0.5f, 0); txtRect.anchoredPosition = new Vector2(0, 2);
            txtRect.sizeDelta = new Vector2(0, 14);
            var txt = txtGO.AddComponent<Text>();
            txt.font = font; txt.fontSize = 11; txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.9f, 0.9f, 0.4f); txt.text = "0/10";
            txt.verticalOverflow = VerticalWrapMode.Overflow; // กัน Kanit โดน truncate ในกล่อง 14px

            // CancelBtn (top-left ✕)
            var cancelGO = new GameObject("CancelBtn", typeof(RectTransform));
            cancelGO.transform.SetParent(temp.transform, false);
            var cancelRect = cancelGO.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0, 1); cancelRect.anchorMax = new Vector2(0, 1);
            cancelRect.pivot = new Vector2(0, 1);
            cancelRect.anchoredPosition = new Vector2(0, 0); cancelRect.sizeDelta = new Vector2(16, 16);
            cancelGO.AddComponent<Image>().color = new Color(0.7f, 0.1f, 0.1f, 0.9f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelGO.GetComponent<Image>();
            var cLblGO = new GameObject("Label", typeof(RectTransform));
            cLblGO.transform.SetParent(cancelGO.transform, false);
            var cLblRect = cLblGO.GetComponent<RectTransform>();
            cLblRect.anchorMin = Vector2.zero; cLblRect.anchorMax = Vector2.one;
            cLblRect.offsetMin = Vector2.zero; cLblRect.offsetMax = Vector2.zero;
            var cLblTxt = cLblGO.AddComponent<Text>();
            // "X" แทน "✕" (U+2715) — Kanit ไม่มี glyph นั้น + overflow กันกล่อง 16px truncate
            cLblTxt.font = font; cLblTxt.fontSize = 12; cLblTxt.fontStyle = FontStyle.Bold; cLblTxt.text = "X";
            cLblTxt.alignment = TextAnchor.MiddleCenter; cLblTxt.color = Color.white;
            cLblTxt.verticalOverflow = VerticalWrapMode.Overflow;
            cLblTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

            // PrioritizeBtn (top-right ↑)
            var prioGO = new GameObject("PrioritizeBtn", typeof(RectTransform));
            prioGO.transform.SetParent(temp.transform, false);
            var prioRect = prioGO.GetComponent<RectTransform>();
            prioRect.anchorMin = new Vector2(1, 1); prioRect.anchorMax = new Vector2(1, 1);
            prioRect.pivot = new Vector2(1, 1);
            prioRect.anchoredPosition = new Vector2(0, 0); prioRect.sizeDelta = new Vector2(16, 16);
            prioGO.AddComponent<Image>().color = new Color(0.1f, 0.5f, 0.1f, 0.9f);
            var prioBtn = prioGO.AddComponent<Button>();
            prioBtn.targetGraphic = prioGO.GetComponent<Image>();
            var pLblGO = new GameObject("Label", typeof(RectTransform));
            pLblGO.transform.SetParent(prioGO.transform, false);
            var pLblRect = pLblGO.GetComponent<RectTransform>();
            pLblRect.anchorMin = Vector2.zero; pLblRect.anchorMax = Vector2.one;
            pLblRect.offsetMin = Vector2.zero; pLblRect.offsetMax = Vector2.zero;
            var pLblTxt = pLblGO.AddComponent<Text>();
            // "^" แทน "↑" (U+2191) — Kanit ไม่มี glyph นั้น + overflow กันกล่อง 16px truncate
            pLblTxt.font = font; pLblTxt.fontSize = 14; pLblTxt.fontStyle = FontStyle.Bold; pLblTxt.text = "^";
            pLblTxt.alignment = TextAnchor.MiddleCenter; pLblTxt.color = Color.white;
            pLblTxt.verticalOverflow = VerticalWrapMode.Overflow;
            pLblTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Day11Setup] สร้าง prefab: {path}");
            return prefab;
        }

        // ─────────────────────────────────────────────
        //  4. BuildingSelectionUI hotbar (bottom-center)
        // ─────────────────────────────────────────────

        private static void SetupBuildingSelectionUI(GameObject hudCanvas, Font font)
        {
            // หา PlacementController เพื่อดึง buildingHotbar array
            var placement = Object.FindFirstObjectByType<PlacementController>();
            if (placement == null)
            {
                Debug.LogWarning("[Day11Setup] ไม่พบ PlacementController ใน scene — ข้าม BuildingSelectionUI");
                return;
            }

            if (placement.buildingHotbar == null || placement.buildingHotbar.Length == 0)
            {
                Debug.LogWarning("[Day11Setup] PlacementController.buildingHotbar ว่างเปล่า — ใส่ BuildingData assets ก่อน");
                return;
            }

            // Panel พื้นหลัง (bottom-center)
            var panelGO = GetOrCreate("BuildingSelectionPanel", hudCanvas.transform);
            {
                var rect = panelGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot     = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 4f);

                int count   = placement.buildingHotbar.Length;
                float width = (count + 1) * 94f + 8f; // 90px slot + 4px gap ต่อช่อง · +1 = ปุ่มทุบอาคาร
                rect.sizeDelta = new Vector2(width, 118f);

                var bg = panelGO.GetComponent<Image>() ?? panelGO.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.65f);
            }

            // Row ปุ่ม (HorizontalLayoutGroup)
            var rowGO = GetOrCreate("ButtonRow", panelGO.transform);
            {
                var rect = rowGO.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);

                var hlg = rowGO.GetComponent<HorizontalLayoutGroup>() ?? rowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false; hlg.childForceExpandWidth = false;
                hlg.childControlHeight = false; hlg.childForceExpandHeight = false;
            }

            // ปุ่มหูจับพับ/กางแถบ — สังกัด hudCanvas (ไม่ใช่ panel) จึงยังเห็นตอนพับ · กลางบนของแถบ
            // ยึด top ที่ y=130 (= ขอบล่าง CoreTowerPanel) หูจับห้อยลงมา 18px → ไม่ทับ core panel
            var toggleGO = GetOrCreate("HotbarToggle", hudCanvas.transform);
            var toggleRect = toggleGO.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.5f, 0f);
            toggleRect.anchorMax = new Vector2(0.5f, 0f);
            toggleRect.pivot     = new Vector2(0.5f, 1f);
            toggleRect.anchoredPosition = new Vector2(0f, 130f);
            toggleRect.sizeDelta = new Vector2(140f, 18f);

            var toggleImg = toggleGO.GetComponent<Image>() ?? toggleGO.AddComponent<Image>();
            toggleImg.color = new Color(0f, 0f, 0f, 0.75f);
            var toggleBtn = toggleGO.GetComponent<Button>() ?? toggleGO.AddComponent<Button>();
            toggleBtn.targetGraphic = toggleImg;

            var toggleLabelGO = GetOrCreate("Label", toggleGO.transform);
            var tlRect = toggleLabelGO.GetComponent<RectTransform>();
            tlRect.anchorMin = Vector2.zero; tlRect.anchorMax = Vector2.one;
            tlRect.offsetMin = Vector2.zero; tlRect.offsetMax = Vector2.zero;
            var toggleTxt = toggleLabelGO.GetComponent<Text>() ?? toggleLabelGO.AddComponent<Text>();
            toggleTxt.font = font; toggleTxt.fontSize = 13; toggleTxt.fontStyle = FontStyle.Bold;
            toggleTxt.alignment = TextAnchor.MiddleCenter; toggleTxt.color = Color.white;
            toggleTxt.text = "▼ อาคาร";

            // BuildingSelectionUI component
            var uiGO = GetOrCreate("BuildingSelectionUI", hudCanvas.transform);
            var selUI = uiGO.GetComponent<BuildingSelectionUI>() ?? uiGO.AddComponent<BuildingSelectionUI>();
            selUI.buildings        = placement.buildingHotbar;
            selUI.buttonContainer  = rowGO.transform;
            selUI.panelRoot        = panelGO;
            selUI.toggleButton     = toggleBtn;
            selUI.toggleLabel      = toggleTxt;
            EditorUtility.SetDirty(selUI);

            Debug.Log($"[Day11Setup] สร้าง BuildingSelectionUI {placement.buildingHotbar.Length} ปุ่ม + ปุ่มพับแถบ");
        }

        // ─────────────────────────────────────────────
        //  Helper
        // ─────────────────────────────────────────────

        private static GameObject GetOrCreate(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
