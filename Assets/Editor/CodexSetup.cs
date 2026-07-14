using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// ระบบ Codex ตาม Codex_Spec v8 — รันผ่าน NuclearReMind / Setup Codex System
    /// ขั้นตอน:
    ///   1. สร้าง/อัปเดต CodexEntry assets ทั้ง 11 (id/หมวด/icon/แหล่งปลด/เนื้อหา ตามสเปก §3+§7)
    ///      + ลบ asset เก่านอกลิสต์ 11 ทิ้ง (ยุคก่อนมี 28 — สเปกใหม่เหลือ 11 ปลดจากควิซเท่านั้น)
    ///   2. เพิ่ม CodexManager ใน scene + wire allCodexEntries (เรียงลำดับสเปก 1–11)
    ///   3. สร้างหน้าจอ Codex ใหม่: หัว (ชื่อ+ปลดแล้ว x/11+ย้ำถาวร) · แถบกรอง 5 หมวด ·
    ///      ลิสต์ซ้าย (ล็อก = "? ? ?") · รายละเอียดขวา (pill หมวด + แหล่งปลด + เนื้อหา + ท้าย +2)
    ///   4. ปุ่ม Codex บน HUD (ค่าเมตา — เข้าถึงได้ตลอด ไม่ผูกห้องวิจัย)
    /// การปลดล็อก: QuizManager.SubmitAnswer → codexUnlockId (ตั้งใน QuizSetup) → CodexManager.UnlockById
    /// </summary>
    public static class CodexSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string EntriesPath = "Assets/ScriptableObjects/CodexEntries";

        [MenuItem("NuclearReMind/Setup Codex System")]
        public static void SetupAll()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            System.IO.Directory.CreateDirectory(EntriesPath);

            var entries = CreateOrLoadEntries();
            DeleteStaleEntries(entries);
            SetupCodexManager(entries);
            SetupCodexUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[CodexSetup] ✅ Codex 11 entry (สเปก v8) + UI ใหม่พร้อม — กด Save Scene (Ctrl+S)");
        }

        /// <summary>
        /// เมนูแยก: build/รีเฟรช "เฉพาะหน้า Codex UI" (สกินโลหะใหม่) โดยไม่แตะ entry/manager/ลบของเก่า
        /// ใช้ตอนอยากรีสไตล์แผงอย่างเดียว — ต่างจาก "Setup Codex System" ที่ setup ครบทั้งระบบ
        /// (CodexManager ต้องมีในซีนอยู่แล้ว = เคยรัน Setup Codex System มาก่อน)
        /// </summary>
        [MenuItem("NuclearReMind/UI/Rebuild Codex UI (skin)")]
        public static void RebuildCodexUIOnly()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (GameObject.Find("CodexManager") == null)
                Debug.LogWarning("[CodexSetup] ไม่พบ CodexManager ในซีน — รัน 'Setup Codex System' ก่อน 1 ครั้ง " +
                                 "(เมนูนี้ build เฉพาะหน้า UI · ตัวข้อมูล/entry มาจาก CodexManager)");

            SetupCodexUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CodexSetup] ✅ rebuild เฉพาะหน้า Codex UI (สกินใหม่) แล้ว — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. สร้าง / อัปเดต CodexEntry assets ทั้ง 11
        // ─────────────────────────────────────────────
        private static CodexEntry[] CreateOrLoadEntries()
        {
            var defs = BuildEntryDefs();
            var result = new CodexEntry[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                string path = $"{EntriesPath}/{defs[i].id}_CodexEntry.asset";
                var asset = AssetDatabase.LoadAssetAtPath<CodexEntry>(path);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CodexEntry>();
                    AssetDatabase.CreateAsset(asset, path);
                    Debug.Log($"[CodexSetup] สร้าง {path}");
                }

                asset.entryId = defs[i].id;
                asset.title = defs[i].titleTh;
                asset.titleEn = defs[i].titleEn;
                asset.category = defs[i].category;
                asset.branch = defs[i].category.ToString(); // ฟิลด์เก่า — sync กับหมวดใหม่
                asset.content = defs[i].body;
                asset.iconName = defs[i].icon;
                asset.unlockedFrom = defs[i].unlockedFrom;
                asset.researchPointCost = 0;   // สเปกใหม่: ไม่มีซื้อด้วย RP
                asset.unlockedByEvent = "";    // สเปกใหม่: ปลดจากควิซเท่านั้น

                EditorUtility.SetDirty(asset);
                result[i] = asset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        // ลบ CodexEntry asset ที่ไม่อยู่ในลิสต์ 11 (ของเก่าจากยุค 28 entry) — กันโผล่ใน UI/นับเกิน
        private static void DeleteStaleEntries(CodexEntry[] keep)
        {
            var keepPaths = new HashSet<string>();
            foreach (var e in keep) keepPaths.Add(AssetDatabase.GetAssetPath(e));

            int removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CodexEntry", new[] { EntriesPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (keepPaths.Contains(path)) continue;
                AssetDatabase.DeleteAsset(path);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"[CodexSetup] ลบ entry เก่านอกสเปก {removed} ไฟล์ (สเปก v8 = 11 entry)");
        }

        // ─────────────────────────────────────────────
        //  2. CodexManager ใน scene
        // ─────────────────────────────────────────────
        private static void SetupCodexManager(CodexEntry[] entries)
        {
            var go = GameObject.Find("CodexManager");
            if (go == null)
            {
                go = new GameObject("CodexManager");
                Debug.Log("[CodexSetup] สร้าง CodexManager ใน scene");
            }

            var mgr = go.GetComponent<CodexManager>() ?? go.AddComponent<CodexManager>();
            mgr.allCodexEntries = entries;
            EditorUtility.SetDirty(mgr);
        }

        // ─────────────────────────────────────────────
        //  3. หน้าจอ Codex (สเปก §6) — สร้างใหม่ทั้งแผง
        // ─────────────────────────────────────────────
        private static void SetupCodexUI()
        {
            var font = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null)
            {
                Debug.LogWarning("[CodexSetup] ไม่พบ HUDCanvas — รัน Setup HUD Canvas ก่อน แล้วรันเมนูนี้อีกครั้ง");
                return;
            }

            var codexGO = GameObject.Find("CodexUIController");
            if (codexGO == null) codexGO = new GameObject("CodexUIController");
            var ui = codexGO.GetComponent<CodexUIController>() ?? codexGO.AddComponent<CodexUIController>();

            // สกินโลหะ (นำเข้าโดย CodexUISetup — ต้องรัน 'Setup Codex UI Sprites' ก่อน)
            var frame = LoadSkin("frame_metal");
            ui.plateSprite = LoadSkin("plate_blank");
            ui.iconAtom = LoadSkin("icon_atom");
            ui.iconDroplet = LoadSkin("icon_droplet");
            ui.iconShield = LoadSkin("icon_shield");
            ui.iconPlant = LoadSkin("icon_plant");
            ui.iconLock = LoadSkin("icon_lock");
            if (frame == null)
                Debug.LogWarning("[CodexSetup] ไม่พบสไปรต์ frame_metal — รัน 'Setup Codex UI Sprites' ก่อนแล้วรันเมนูนี้ซ้ำ");

            // แผงเดิมทิ้ง — สร้างใหม่ตามสกินโลหะ (mockup)
            var old = hudCanvas.transform.Find("CodexPanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            const float PW = 1360f, PH = 880f;

            // ── Panel (กลางจอ · กรอบโลหะ 9-slice · ย่อ 0.9 ให้พอดี ref 1536×864) ──
            var panel = CreateOrGet("CodexPanel", hudCanvas.transform);
            {
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(PW, PH);
                rect.localScale = new Vector3(0.9f, 0.9f, 1f);
                SetSkin(panel, frame, sliced: true);
                panel.SetActive(false);
            }
            ui.codexPanel = panel;

            // ── Header: ไอคอนหนังสือ + ชื่อ (ฟ้า) + ความคืบหน้า (ทอง) + ปิด ──
            var icon = CreateOrGet("HeaderIcon", panel.transform);
            {
                var rect = icon.GetComponent<RectTransform>();
                TopLeft(rect); rect.anchoredPosition = new Vector2(30, -20); rect.sizeDelta = new Vector2(118, 118);
                SetSkin(icon, LoadSkin("header_book"), sliced: false);
            }

            var title = CreateText("CodexTitle", panel.transform, font, "คลังความรู้ (Codex)", 40,
                new Vector2(168, -24), new Vector2(760, 56), TextAnchor.MiddleLeft);
            TopLeft(title.rectTransform);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.47f, 0.84f, 1f);

            ui.progressText = CreateText("Progress", panel.transform, font, "ปลดแล้ว 0 / 11", 24,
                new Vector2(172, -96), new Vector2(900, 34), TextAnchor.MiddleLeft);
            TopLeft(ui.progressText.rectTransform);
            ui.progressText.color = new Color(1f, 0.82f, 0.35f);
            ui.progressText.fontStyle = FontStyle.Bold;

            var closeBtn = CreateButton("CloseBtn", panel.transform, font, "✕", 30,
                new Vector2(-28, -22), new Vector2(72, 72));
            TopRight(closeBtn.GetComponent<RectTransform>());
            var closeImg = EnsureImage(closeBtn.gameObject);
            var closeSprite = LoadSkin("close_x");
            if (closeSprite != null)
            {
                closeImg.sprite = closeSprite; closeImg.type = Image.Type.Simple; closeImg.color = Color.white;
                var cl = closeBtn.GetComponentInChildren<Text>(); if (cl != null) cl.text = ""; // ใช้สไปรต์ปุ่มปิดแทน ✕
            }
            else { closeImg.sprite = null; closeImg.type = Image.Type.Simple; closeImg.color = new Color(0.16f, 0.13f, 0.11f, 1f); }
            ui.closeButton = closeBtn; // wire runtime ใน CodexUIController.WireTabs

            // ── Tabs (5 แท็บโลหะ ข้อความ baked · เลือก = ขอบเรืองฟ้า) ──
            const float gap = 12f, margin = 30f, th = 74f;
            float tw = (PW - margin * 2 - gap * 4) / 5f;
            ui.tabAll = TabButton(panel.transform, "TabAll", "tab_all", 0, tw, th, gap, margin);
            ui.tabReactor = TabButton(panel.transform, "TabReactor", "tab_reactor", 1, tw, th, gap, margin);
            ui.tabMedical = TabButton(panel.transform, "TabMedical", "tab_medical", 2, tw, th, gap, margin);
            ui.tabAgriculture = TabButton(panel.transform, "TabAgri", "tab_agri", 3, tw, th, gap, margin);
            ui.tabEthics = TabButton(panel.transform, "TabEthics", "tab_ethics", 4, tw, th, gap, margin);

            // ── Left pane: entry list (กรอบโลหะ) ──
            var leftPane = CreateOrGet("EntryListPane", panel.transform);
            {
                var rect = leftPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.022f, 0.034f); rect.anchorMax = new Vector2(0.34f, 0.735f);
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                SetSkin(leftPane, frame, sliced: true);
            }
            var content = BuildScrollList(leftPane.transform, out var template, font, ui.plateSprite);
            ui.entryListParent = content.transform;
            ui.entryButtonPrefab = template;

            // ── Right pane: detail (กรอบโลหะ) ──
            var detailPane = CreateOrGet("DetailPane", panel.transform);
            {
                var rect = detailPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.36f, 0.034f); rect.anchorMax = new Vector2(0.978f, 0.735f);
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                SetSkin(detailPane, frame, sliced: true);
            }

            // pill หมวด + แหล่งปลด
            var pillGO = CreateOrGet("DetailPill", detailPane.transform);
            {
                var rect = pillGO.GetComponent<RectTransform>();
                TopLeft(rect); rect.anchoredPosition = new Vector2(36, -28); rect.sizeDelta = new Vector2(140, 40);
                ui.detailPill = EnsureImage(pillGO);
                ui.detailPill.color = new Color(0.3f, 0.55f, 0.9f);
                ui.detailPillLabel = CreateText("PillLabel", pillGO.transform, font, "หมวด", 18,
                    Vector2.zero, new Vector2(140, 40), TextAnchor.MiddleCenter);
                Stretch(ui.detailPillLabel.rectTransform);
                ui.detailPillLabel.fontStyle = FontStyle.Bold;
            }
            ui.detailUnlockFrom = CreateText("DetailUnlockFrom", detailPane.transform, font, "", 18,
                new Vector2(190, -28), new Vector2(340, 40), TextAnchor.MiddleLeft);
            TopLeft(ui.detailUnlockFrom.rectTransform);
            ui.detailUnlockFrom.color = new Color(0.62f, 0.7f, 0.76f);

            ui.detailTitle = CreateText("DetailTitle", detailPane.transform, font, "เลือกหัวข้อทางซ้าย", 34,
                new Vector2(36, -78), new Vector2(-72, 52), TextAnchor.MiddleLeft);
            TopStretch(ui.detailTitle.rectTransform);
            ui.detailTitle.fontStyle = FontStyle.Bold;

            ui.detailTitleEn = CreateText("DetailTitleEn", detailPane.transform, font, "", 16,
                new Vector2(36, -128), new Vector2(-72, 24), TextAnchor.MiddleLeft);
            TopStretch(ui.detailTitleEn.rectTransform);
            ui.detailTitleEn.color = new Color(0.55f, 0.62f, 0.68f);
            ui.detailTitleEn.fontStyle = FontStyle.Italic;

            // illustration (โชว์เมื่อ entry มีรูป — มุมขวาบน)
            var illustGO = CreateOrGet("DetailIllustration", detailPane.transform);
            {
                var rect = illustGO.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-20, -80); rect.sizeDelta = new Vector2(120, 96);
                ui.detailIllustration = EnsureImage(illustGO);
                ui.detailIllustration.color = Color.white;
                illustGO.SetActive(false);
            }

            // เนื้อหา (scroll)
            var dScroll = CreateOrGet("DetailScroll", detailPane.transform);
            {
                var rect = dScroll.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(36, 90); rect.offsetMax = new Vector2(-24, -156);
            }
            var dViewport = CreateOrGet("DViewport", dScroll.transform);
            {
                EnsureImage(dViewport).color = Color.clear;
                var mask = dViewport.GetComponent<Mask>() ?? dViewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                Stretch(dViewport.GetComponent<RectTransform>());
            }
            var dContent = CreateOrGet("DContent", dViewport.transform);
            {
                var rect = dContent.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 700);
                var dsr = dScroll.GetComponent<ScrollRect>() ?? dScroll.AddComponent<ScrollRect>();
                dsr.content = rect; dsr.viewport = dViewport.GetComponent<RectTransform>();
                dsr.horizontal = false; dsr.vertical = true;
                dsr.movementType = ScrollRect.MovementType.Clamped;
            }
            ui.detailContent = CreateText("DetailContent", dContent.transform, font, "", 22,
                Vector2.zero, new Vector2(0, 700), TextAnchor.UpperLeft);
            {
                var rect = ui.detailContent.rectTransform;
                Stretch(rect);
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            }
            ui.detailContent.color = new Color(0.9f, 0.9f, 0.85f);
            ui.detailContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            ui.detailContent.verticalOverflow = VerticalWrapMode.Overflow;
            ui.detailContent.lineSpacing = 1.4f;

            // แถบท้าย: "+2 Knowledge" (ไม่มี VESTA) — แถบมืดบนกรอบ
            var footerBar = CreateOrGet("FooterBar", detailPane.transform);
            {
                var rect = footerBar.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(0.5f, 0);
                rect.anchoredPosition = new Vector2(0, 22);
                rect.sizeDelta = new Vector2(-72, 52);
                var fimg = EnsureImage(footerBar);
                fimg.sprite = null; fimg.type = Image.Type.Simple;
                fimg.color = new Color(0.11f, 0.095f, 0.08f, 0.96f);
            }

            // ไอคอนหนังสือหน้า footer (ซ้าย) — โชว์เมื่อมีสไปรต์ footer_book
            var footerBook = LoadSkin("footer_book");
            float footerTextLeft = 20f;
            if (footerBook != null)
            {
                var bookGO = CreateOrGet("FooterBook", footerBar.transform);
                var brt = bookGO.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f); brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(18, 0); brt.sizeDelta = new Vector2(34, 34);
                var bimg = EnsureImage(bookGO);
                bimg.sprite = footerBook; bimg.type = Image.Type.Simple; bimg.color = Color.white;
                bimg.preserveAspect = true; bimg.raycastTarget = false;
                footerTextLeft = 62f; // เว้นที่ให้ไอคอน
            }
            ui.detailFooter = CreateText("DetailFooter", footerBar.transform, font, "", 18,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
            {
                var rect = ui.detailFooter.rectTransform;
                Stretch(rect); rect.offsetMin = new Vector2(footerTextLeft, 0); rect.offsetMax = new Vector2(-20, 0);
            }
            ui.detailFooter.color = new Color(1f, 0.82f, 0.35f);
            ui.detailFooter.fontStyle = FontStyle.Bold;

            // ── ปุ่ม Codex บน HUD (ค่าเมตา เข้าได้ตลอด · คีย์ลัด C) ──
            var codexBtnGO = GameObject.Find("CodexToggleButton");
            if (codexBtnGO == null)
            {
                var btn = CreateButton("CodexToggleButton", hudCanvas.transform, font, "Codex (C)", 16,
                    new Vector2(20, 210), new Vector2(120, 36));
                EnsureImage(btn.gameObject).color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
                codexBtnGO = btn.gameObject;
            }
            ui.toggleButton = codexBtnGO.GetComponent<Button>();
            var btnRect = codexBtnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero; btnRect.anchorMax = Vector2.zero;
            btnRect.pivot = Vector2.zero;
            btnRect.anchoredPosition = new Vector2(20, 210);
            EditorUtility.SetDirty(codexBtnGO);

            EditorUtility.SetDirty(ui);
        }

        // แท็บโลหะ (สไปรต์ข้อความ baked) + Outline เรืองฟ้าตอนเลือก (ปิดไว้ก่อน)
        private static Button TabButton(Transform parent, string name, string spriteName,
            int index, float w, float h, float gap, float margin)
        {
            var go = CreateOrGet(name, parent);
            var rect = go.GetComponent<RectTransform>();
            TopLeft(rect);
            rect.anchoredPosition = new Vector2(margin + index * (w + gap), -150f);
            rect.sizeDelta = new Vector2(w, h);
            var img = EnsureImage(go);
            img.sprite = LoadSkin(spriteName); img.type = Image.Type.Simple; img.color = Color.white;
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.targetGraphic = img;
            var ol = go.GetComponent<UnityEngine.UI.Outline>() ?? go.AddComponent<UnityEngine.UI.Outline>();
            ol.effectColor = new Color(0.35f, 0.82f, 1f, 0.95f);
            ol.effectDistance = new Vector2(3, -3);
            ol.enabled = false;
            return btn;
        }

        private static GameObject BuildScrollList(Transform pane, out GameObject template, Font font, Sprite plate)
        {
            var scroll = CreateOrGet("EntryScroll", pane);
            {
                var rect = scroll.GetComponent<RectTransform>();
                Stretch(rect);
                rect.offsetMin = new Vector2(16, 16); rect.offsetMax = new Vector2(-16, -16);
            }
            var viewport = CreateOrGet("Viewport", scroll.transform);
            {
                EnsureImage(viewport).color = Color.clear;
                var mask = viewport.GetComponent<Mask>() ?? viewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                Stretch(viewport.GetComponent<RectTransform>());
            }
            var content = CreateOrGet("EntryContent", viewport.transform);
            {
                var rect = content.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 8f; vlg.padding = new RectOffset(2, 2, 2, 2);
                vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
                vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
                var csf = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var sr = scroll.GetComponent<ScrollRect>() ?? scroll.AddComponent<ScrollRect>();
                sr.content = rect; sr.viewport = viewport.GetComponent<RectTransform>();
                sr.horizontal = false; sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
            }

            // template แถว: แผ่นโลหะ + ไอคอนซ้าย + ชื่อ + Outline เรืองฟ้าตอนเลือก
            template = CreateOrGet("EntryButtonTemplate", content.transform);
            {
                var rect = template.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, 92);
                var le = template.GetComponent<LayoutElement>() ?? template.AddComponent<LayoutElement>();
                le.minHeight = 92; le.preferredHeight = 92;
                var img = EnsureImage(template);
                img.sprite = plate; img.type = Image.Type.Simple; img.color = Color.white;
                var btn = template.GetComponent<Button>() ?? template.AddComponent<Button>();
                btn.targetGraphic = img;
                var ol = template.GetComponent<UnityEngine.UI.Outline>() ?? template.AddComponent<UnityEngine.UI.Outline>();
                ol.effectColor = new Color(0.35f, 0.82f, 1f, 0.95f);
                ol.effectDistance = new Vector2(3, -3);
                ol.enabled = false;

                var iconGO = CreateOrGet("Icon", template.transform);
                var ir = iconGO.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f); ir.pivot = new Vector2(0, 0.5f);
                ir.anchoredPosition = new Vector2(16, 0); ir.sizeDelta = new Vector2(64, 64);
                EnsureImage(iconGO).color = Color.white; // sprite เซ็ตตอน runtime

                var label = CreateText("Label", template.transform, font, "Entry", 26,
                    Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 1);
                lr.offsetMin = new Vector2(104, 0); lr.offsetMax = new Vector2(-10, 0);
                label.fontStyle = FontStyle.Bold;

                template.SetActive(false);
            }
            return content;
        }

        // ── สกินโลหะ helpers ──
        private static Sprite LoadSkin(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/CodexUI/{name}.png");

        // เซ็ตสไปรต์กรอบ/ไอคอนให้ Image · sliced = กรอบ 9-slice (frame_metal) · null = fallback สีมืด
        private static void SetSkin(GameObject go, Sprite sprite, bool sliced)
        {
            var img = EnsureImage(go);
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = sprite != null ? Color.white : new Color(0.09f, 0.085f, 0.11f, 0.98f);
        }

        // ─────────────────────────────────────────────
        //  เนื้อหา 11 entry (Codex_Spec §3 + §7 — ก๊อปตรงจากสเปก)
        // ─────────────────────────────────────────────
        private static EntryDef[] BuildEntryDefs() => new[]
        {
            new EntryDef("codex_deuterium", "ดิวเทอเรียม", "Deuterium", QuizCategory.Reactor, "droplet", "ควิซ #1",
                "ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว " +
                "เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลยโดยไม่ต้องผลิตขึ้นใหม่"),

            new EntryDef("codex_plasma_confinement", "การกักพลาสมา", "Plasma Confinement", QuizCategory.Reactor, "flame", "ควิซ #2",
                "ในโทคาแมก พลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก " +
                "ถ้าสนามไม่นิ่งจนพลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย"),

            new EntryDef("codex_magnetic_confinement", "สนามแม่เหล็กคู่", "Magnetic Confinement", QuizCategory.Reactor, "magnet", "ควิซ #3",
                "สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง " +
                "เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก"),

            new EntryDef("codex_dt_fusion_fuel", "เชื้อเพลิงคู่ D–T", "D–T Fusion Fuel", QuizCategory.Reactor, "atom-2", "ควิซ #Tritium",
                "เชื้อเพลิงที่หลอมรวมได้ง่ายที่สุดคือคู่ดิวเทอเรียม–ทริเทียม (D–T) เพราะจุดติดที่อุณหภูมิต่ำกว่าคู่อื่น " +
                "ดิวเทอเรียมจากน้ำพาเตาขึ้นมาได้ระดับหนึ่ง แต่การจะดันถึงจุดติดเต็มร้อยต้องมีทริเทียมป้อนคู่ — " +
                "ทริเทียมหายากจึงต้องเปิด Zone B ผลิตเอง"),

            new EntryDef("codex_tritium_breeding", "การเพาะทริเทียม", "Tritium Breeding", QuizCategory.Reactor, "atom", "ควิซ #Tritium-2",
                "ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตได้ด้วยการนำนิวตรอนที่เกิดจากปฏิกิริยาฟิวชันไปยิงใส่ลิเทียม " +
                "(breeding blanket) ลิเทียมจะแตกตัวให้ทริเทียม — เตาฟิวชันจึงสามารถผลิตเชื้อเพลิงส่วนหนึ่งของตัวเองได้"),

            new EntryDef("codex_nuclear_medicine", "เวชศาสตร์นิวเคลียร์", "Nuclear Medicine", QuizCategory.Medical, "stethoscope", "ควิซ #4",
                "เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น — (1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ " +
                "(2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย"),

            new EntryDef("codex_alara", "หลัก ALARA", "ALARA", QuizCategory.Ethics, "shield", "ควิซ #5",
                "ALARA (As Low As Reasonably Achievable) คือ ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้ " +
                "และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ"),

            new EntryDef("codex_mutation_breeding", "ปรับปรุงพันธุ์ด้วยรังสี", "Mutation Breeding", QuizCategory.Agriculture, "seeding", "ควิซ #6",
                "การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์ นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร " +
                "เช่น ข้าว กข6 ของไทย เป็นการแก้ปัญหาที่ต้นเหตุ"),

            new EntryDef("codex_food_irradiation", "ฉายรังสีถนอมอาหาร", "Food Irradiation", QuizCategory.Agriculture, "meat", "ควิซ #7",
                "รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น " +
                "โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)"),

            new EntryDef("codex_nuclear_fusion", "ฟิวชันคืออะไร", "Nuclear Fusion", QuizCategory.Reactor, "atom-2", "ควิซ #8",
                "ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า แล้วปลดปล่อยพลังงานมหาศาล " +
                "ตรงข้ามกับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก"),

            new EntryDef("codex_clean_energy", "ทำไมฟิวชันสะอาด", "Clean Energy", QuizCategory.Reactor, "leaf", "ควิซ #9",
                "ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂ ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด) " +
                "และไม่มีกากรังสีอายุยืนแบบฟิชชัน — แต่สะอาดกว่าไม่ได้แปลว่าไม่มีรังสีเลย เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน"),
        };

        // ─────────────────────────────────────────────
        //  Helper utilities
        // ─────────────────────────────────────────────
        private static GameObject CreateOrGet(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image EnsureImage(GameObject go)
            => go.GetComponent<Image>() ?? go.AddComponent<Image>();

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        private static void TopLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        }
        private static void TopRight(RectTransform rt)
        {
            rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        }
        private static void TopStretch(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
        }

        private static Text CreateText(string name, Transform parent, Font font,
            string content, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
        {
            var go = CreateOrGet(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.color = Color.white;
            text.alignment = anchor; text.text = content;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font,
            string label, int fontSize, Vector2 anchoredPos, Vector2 size)
        {
            var go = CreateOrGet(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = CreateText(name + "Label", go.transform, font, label, fontSize,
                Vector2.zero, size, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return btn;
        }

        private readonly struct EntryDef
        {
            public readonly string id, titleTh, titleEn, icon, unlockedFrom, body;
            public readonly QuizCategory category;
            public EntryDef(string id, string titleTh, string titleEn, QuizCategory category,
                string icon, string unlockedFrom, string body)
            {
                this.id = id; this.titleTh = titleTh; this.titleEn = titleEn;
                this.category = category; this.icon = icon; this.unlockedFrom = unlockedFrom; this.body = body;
            }
        }
    }
}
