using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup ครบชุดสำหรับ Day 11 — รันผ่าน NuclearReMind / Setup Day 11 Systems
    /// สิ่งที่ทำ:
    ///   1. เพิ่ม ConstructionController + สร้าง ConstructionProgressUI prefab
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
            SetupTutorialManager(hudCanvas, font);
            SetupBuildingSelectionUI(hudCanvas, font);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Day11Setup] เสร็จแล้ว! กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. ConstructionController + ProgressUI prefab
        // ─────────────────────────────────────────────

        private static void SetupConstructionController()
        {
            var go = GameObject.Find("ConstructionController");
            if (go == null) go = new GameObject("ConstructionController");

            var cc = go.GetComponent<ConstructionController>() ?? go.AddComponent<ConstructionController>();

            // สร้าง ConstructionProgressUI prefab (world-space progress bar เหนืออาคาร)
            // ConstructionProgressUI สร้าง bar (background + fill) เองตอน runtime — prefab เป็นแค่ GameObject เปล่า + script
            // regenerate ทุกครั้งเพื่อ overwrite prefab เวอร์ชันตัวเลข "x/10" เดิม
            var prefabPath = $"{PrefabPath}/ConstructionProgressUI.prefab";

            var temp = new GameObject("ConstructionProgressUI");
            temp.AddComponent<ConstructionProgressUI>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Day11Setup] สร้าง/อัปเดต prefab: {prefabPath} (progress bar)");

            cc.constructionProgressUIPrefab = prefab;
            EditorUtility.SetDirty(cc);
        }

        // ─────────────────────────────────────────────
        //  2. BuildingQueueUI (bottom-right HUD)
        // ─────────────────────────────────────────────

        private static void SetupBuildingQueueUI(GameObject hudCanvas, Font font)
        {
            // สร้าง entry prefab ก่อน
            var entryPrefabPath = $"{PrefabPath}/QueueEntry.prefab";
            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(entryPrefabPath);

            if (entryPrefab == null)
            {
                entryPrefab = CreateQueueEntryPrefab(entryPrefabPath, font);
            }

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
            cLblTxt.font = font; cLblTxt.fontSize = 11; cLblTxt.text = "✕";
            cLblTxt.alignment = TextAnchor.MiddleCenter; cLblTxt.color = Color.white;

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
            pLblTxt.font = font; pLblTxt.fontSize = 11; pLblTxt.text = "↑";
            pLblTxt.alignment = TextAnchor.MiddleCenter; pLblTxt.color = Color.white;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Day11Setup] สร้าง prefab: {path}");
            return prefab;
        }

        // ─────────────────────────────────────────────
        //  3. TutorialManager + popup panel
        // ─────────────────────────────────────────────

        private static void SetupTutorialManager(GameObject hudCanvas, Font font)
        {
            // Tutorial overlay panel (full screen, สีดำโปร่งแสง)
            var panelGO = GetOrCreate("TutorialPanel", hudCanvas.transform);
            {
                var rect = panelGO.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                var bg = panelGO.GetComponent<Image>() ?? panelGO.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.8f);
            }

            // Box กลางจอ
            var boxGO = GetOrCreate("TutorialBox", panelGO.transform);
            {
                var rect = boxGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot     = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(700f, 480f);
                var bg = boxGO.GetComponent<Image>() ?? boxGO.AddComponent<Image>();
                bg.color = new Color(0.06f, 0.06f, 0.12f, 1f);
            }

            // Title
            var titleGO = GetOrCreate("TutorialTitle", boxGO.transform);
            {
                var rect = titleGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -20);
                rect.sizeDelta = new Vector2(-40, 40);
                var txt = titleGO.GetComponent<Text>() ?? titleGO.AddComponent<Text>();
                txt.font = font; txt.fontSize = 26; txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.UpperCenter;
                txt.color = new Color(0.4f, 0.9f, 1f);
                txt.text = "ยินดีต้อนรับสู่ Veltara";
            }

            // Content
            var contentGO = GetOrCreate("TutorialContent", boxGO.transform);
            {
                var rect = contentGO.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(30, 70); rect.offsetMax = new Vector2(-30, -80);
                var txt = contentGO.GetComponent<Text>() ?? contentGO.AddComponent<Text>();
                txt.font = font; txt.fontSize = 17;
                txt.alignment = TextAnchor.UpperLeft;
                txt.color = new Color(0.9f, 0.9f, 0.85f);
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.lineSpacing = 1.4f;
                txt.text =
                    "เมือง Veltara ถูกทิ้งร้างไว้นานหลายปี — ภารกิจของคุณคือฟื้นฟูเมืองนี้\n" +
                    "และสร้าง CORE TOWER เครื่องปฏิกรณ์ฟิวชันให้สำเร็จก่อนที่ทรัพยากรจะหมด\n\n" +
                    "🏗  วางอาคาร — คลิกบนตารางเพื่อเลือกตำแหน่ง อาคารใช้เวลา 10 tick จึงจะสร้างเสร็จ\n\n" +
                    "📊  ดูแลทรัพยากร — อาหาร, น้ำ, พลังงาน, และรังสีต้องอยู่ในระดับปลอดภัย\n\n" +
                    "🤝  รักษาความไว้วางใจ — ถ้าประชาชนขาดแคลน ค่า Trust จะลด และเกิด Worker Strike\n\n" +
                    "📖  Learning Codex — กด Codex เพื่ออ่านความรู้นิวเคลียร์และ unlock entries ด้วย RP\n\n" +
                    "🏆  เป้าหมาย — สร้าง CORE TOWER ให้ครบ 3 Phase เพื่อชนะ";
            }

            // Dismiss button
            var btnGO = GetOrCreate("TutorialDismissBtn", boxGO.transform);
            {
                var rect = btnGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0); rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0); rect.anchoredPosition = new Vector2(0, 16);
                rect.sizeDelta = new Vector2(200, 44);
                var bg = btnGO.GetComponent<Image>() ?? btnGO.AddComponent<Image>();
                bg.color = new Color(0.15f, 0.45f, 0.15f, 1f);
                var btn = btnGO.GetComponent<Button>() ?? btnGO.AddComponent<Button>();
                btn.targetGraphic = bg;
                var lblGO = GetOrCreate("Label", btnGO.transform);
                var lblRect = lblGO.GetComponent<RectTransform>();
                lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
                lblRect.offsetMin = Vector2.zero; lblRect.offsetMax = Vector2.zero;
                var lblTxt = lblGO.GetComponent<Text>() ?? lblGO.AddComponent<Text>();
                lblTxt.font = font; lblTxt.fontSize = 18; lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.MiddleCenter; lblTxt.color = Color.white;
                lblTxt.text = "เริ่มเกม";
            }

            // TutorialManager component
            var tmGO = GetOrCreate("TutorialManager", hudCanvas.transform);
            var tm = tmGO.GetComponent<TutorialManager>() ?? tmGO.AddComponent<TutorialManager>();
            tm.tutorialPanel  = panelGO;
            tm.dismissButton  = boxGO.transform.Find("TutorialDismissBtn")?.GetComponent<Button>();
            EditorUtility.SetDirty(tm);
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
                float width = count * 94f + 8f; // 90px slot + 4px gap, 4px padding masing-masing
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

            // BuildingSelectionUI component
            var uiGO = GetOrCreate("BuildingSelectionUI", hudCanvas.transform);
            var selUI = uiGO.GetComponent<BuildingSelectionUI>() ?? uiGO.AddComponent<BuildingSelectionUI>();
            selUI.buildings        = placement.buildingHotbar;
            selUI.buttonContainer  = rowGO.transform;
            EditorUtility.SetDirty(selUI);

            Debug.Log($"[Day11Setup] สร้าง BuildingSelectionUI {placement.buildingHotbar.Length} ปุ่ม");
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
