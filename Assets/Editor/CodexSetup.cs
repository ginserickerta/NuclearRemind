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

            // แผงเดิม (โครงเก่า) ทิ้ง — สร้างใหม่ให้ตรงสเปก (มีแถบกรอง/หัวใหม่)
            var old = hudCanvas.transform.Find("CodexPanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // ── Panel (ขวา 720px เต็มสูง) ──
            var panel = CreateOrGet("CodexPanel", hudCanvas.transform);
            {
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(720, 0);
                EnsureImage(panel).color = new Color(0.07f, 0.075f, 0.11f, 0.97f);
                panel.SetActive(false);
            }
            ui.codexPanel = panel;

            // ── Header (h 88): ชื่อ + ความคืบหน้า + ย้ำถาวร + ปิด ──
            var header = CreateOrGet("CodexHeader", panel.transform);
            {
                var rect = header.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 88);
                EnsureImage(header).color = new Color(0.045f, 0.05f, 0.08f, 1f);

                var title = CreateText("CodexTitle", header.transform, font, "คลังความรู้ (Codex)", 24,
                    new Vector2(16, -8), new Vector2(400, 34), TextAnchor.MiddleLeft);
                TopLeft(title.rectTransform);
                title.fontStyle = FontStyle.Bold;
                title.color = new Color(0.45f, 0.85f, 1f);

                ui.progressText = CreateText("Progress", header.transform, font, "ปลดแล้ว 0 / 11", 20,
                    new Vector2(-70, -12), new Vector2(240, 30), TextAnchor.MiddleRight);
                TopRight(ui.progressText.rectTransform);
                ui.progressText.color = new Color(1f, 0.85f, 0.35f);
                ui.progressText.fontStyle = FontStyle.Bold;

                var note = CreateText("PersistNote", header.transform, font,
                    "ความรู้บันทึกถาวร — ไม่รีเซ็ตแม้เริ่มเกมใหม่", 14,
                    new Vector2(16, -48), new Vector2(500, 22), TextAnchor.MiddleLeft);
                TopLeft(note.rectTransform);
                note.color = new Color(0.55f, 0.62f, 0.68f);

                var closeBtn = CreateButton("CloseBtn", header.transform, font, "✕", 20,
                    new Vector2(-8, -8), new Vector2(40, 40));
                TopRight(closeBtn.GetComponent<RectTransform>());
                EnsureImage(closeBtn.gameObject).color = new Color(0.45f, 0.18f, 0.16f, 1f);
                ui.closeButton = closeBtn; // wire ตอน runtime ใน CodexUIController.WireTabs (lambda จาก editor ไม่ persist)
            }

            // ── Tabs (h 40): ทั้งหมด / เตา-ฟิวชัน / แพทย์-รังสี / เกษตร / จริยธรรม ──
            var tabs = CreateOrGet("CodexTabs", panel.transform);
            {
                var rect = tabs.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -88);
                rect.sizeDelta = new Vector2(0, 40);
                EnsureImage(tabs).color = new Color(0.06f, 0.065f, 0.095f, 1f);

                float w = (720f - 24f) / 5f;
                ui.tabAll = TabButton(tabs.transform, font, "TabAll", "ทั้งหมด", 0, w);
                ui.tabReactor = TabButton(tabs.transform, font, "TabReactor", "เตา/ฟิวชัน", 1, w);
                ui.tabMedical = TabButton(tabs.transform, font, "TabMedical", "แพทย์/รังสี", 2, w);
                ui.tabAgriculture = TabButton(tabs.transform, font, "TabAgri", "เกษตร", 3, w);
                ui.tabEthics = TabButton(tabs.transform, font, "TabEthics", "จริยธรรม", 4, w);
            }

            // ── Left pane: entry list (w 260) ──
            var leftPane = CreateOrGet("EntryListPane", panel.transform);
            {
                var rect = leftPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(0, -64);
                rect.sizeDelta = new Vector2(260, -128);
                EnsureImage(leftPane).color = new Color(0.055f, 0.06f, 0.09f, 1f);
            }
            var content = BuildScrollList(leftPane.transform, out var template, font);
            ui.entryListParent = content.transform;
            ui.entryButtonPrefab = template;

            // ── Right pane: detail ──
            var detailPane = CreateOrGet("DetailPane", panel.transform);
            {
                var rect = detailPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.offsetMin = new Vector2(264, 0);
                rect.offsetMax = new Vector2(0, -128);
                EnsureImage(detailPane).color = new Color(0.05f, 0.052f, 0.085f, 1f);
            }

            // pill หมวด + แหล่งปลด (แถวบนของแผงขวา)
            var pillGO = CreateOrGet("DetailPill", detailPane.transform);
            {
                var rect = pillGO.GetComponent<RectTransform>();
                TopLeft(rect);
                rect.anchoredPosition = new Vector2(12, -12);
                rect.sizeDelta = new Vector2(120, 26);
                ui.detailPill = EnsureImage(pillGO);
                ui.detailPill.color = new Color(0.3f, 0.55f, 0.9f);
                ui.detailPillLabel = CreateText("PillLabel", pillGO.transform, font, "หมวด", 14,
                    Vector2.zero, new Vector2(120, 26), TextAnchor.MiddleCenter);
                Stretch(ui.detailPillLabel.rectTransform);
                ui.detailPillLabel.fontStyle = FontStyle.Bold;
            }
            ui.detailUnlockFrom = CreateText("DetailUnlockFrom", detailPane.transform, font, "", 14,
                new Vector2(142, -12), new Vector2(280, 26), TextAnchor.MiddleLeft);
            TopLeft(ui.detailUnlockFrom.rectTransform);
            ui.detailUnlockFrom.color = new Color(0.6f, 0.68f, 0.74f);

            ui.detailTitle = CreateText("DetailTitle", detailPane.transform, font, "เลือกหัวข้อทางซ้าย", 22,
                new Vector2(12, -46), new Vector2(-24, 32), TextAnchor.MiddleLeft);
            TopStretch(ui.detailTitle.rectTransform);
            ui.detailTitle.fontStyle = FontStyle.Bold;

            ui.detailTitleEn = CreateText("DetailTitleEn", detailPane.transform, font, "", 15,
                new Vector2(12, -80), new Vector2(-24, 22), TextAnchor.MiddleLeft);
            TopStretch(ui.detailTitleEn.rectTransform);
            ui.detailTitleEn.color = new Color(0.55f, 0.62f, 0.68f);
            ui.detailTitleEn.fontStyle = FontStyle.Italic;

            // illustration (โชว์เมื่อ entry มีรูป)
            var illustGO = CreateOrGet("DetailIllustration", detailPane.transform);
            {
                var rect = illustGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-12, -46);
                rect.sizeDelta = new Vector2(100, 80);
                ui.detailIllustration = EnsureImage(illustGO);
                ui.detailIllustration.color = Color.white;
                illustGO.SetActive(false);
            }

            // เนื้อหา (scroll)
            var dScroll = CreateOrGet("DetailScroll", detailPane.transform);
            {
                var rect = dScroll.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(12, 44); rect.offsetMax = new Vector2(-12, -108);
            }
            var dViewport = CreateOrGet("DViewport", dScroll.transform);
            {
                EnsureImage(dViewport).color = Color.clear;
                var mask = dViewport.GetComponent<Mask>() ?? dViewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var rect = dViewport.GetComponent<RectTransform>();
                Stretch(rect);
            }
            var dContent = CreateOrGet("DContent", dViewport.transform);
            {
                var rect = dContent.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 600);
                var dsr = dScroll.GetComponent<ScrollRect>() ?? dScroll.AddComponent<ScrollRect>();
                dsr.content = rect; dsr.viewport = dViewport.GetComponent<RectTransform>();
                dsr.horizontal = false; dsr.vertical = true;
                dsr.movementType = ScrollRect.MovementType.Clamped;
            }
            ui.detailContent = CreateText("DetailContent", dContent.transform, font, "", 17,
                Vector2.zero, new Vector2(0, 600), TextAnchor.UpperLeft);
            {
                var rect = ui.detailContent.rectTransform;
                Stretch(rect);
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            }
            ui.detailContent.color = new Color(0.92f, 0.92f, 0.88f);
            ui.detailContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            ui.detailContent.verticalOverflow = VerticalWrapMode.Overflow;
            ui.detailContent.lineSpacing = 1.35f;

            // แถบท้าย: "+2 Knowledge" (ไม่มีผู้บรรยาย VESTA — ตัดตาม v8)
            ui.detailFooter = CreateText("DetailFooter", detailPane.transform, font, "", 14,
                new Vector2(12, 12), new Vector2(-24, 24), TextAnchor.MiddleLeft);
            {
                var rect = ui.detailFooter.rectTransform;
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(0.5f, 0);
            }
            ui.detailFooter.color = new Color(1f, 0.85f, 0.35f, 0.85f);

            // ── ปุ่ม Codex บน HUD (ตำแหน่งเดิม — ค่าเมตา เข้าได้ตลอด) ──
            var codexBtnGO = GameObject.Find("CodexToggleButton");
            if (codexBtnGO == null)
            {
                var btn = CreateButton("CodexToggleButton", hudCanvas.transform, font, "Codex (C)", 16,
                    new Vector2(20, 210), new Vector2(120, 36));
                EnsureImage(btn.gameObject).color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
                codexBtnGO = btn.gameObject;
            }
            var toggleBtn = codexBtnGO.GetComponent<Button>();
            ui.toggleButton = toggleBtn; // wire ตอน runtime ใน CodexUIController.WireTabs (lambda จาก editor ไม่ persist)
            var btnRect = codexBtnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero; btnRect.anchorMax = Vector2.zero;
            btnRect.pivot = Vector2.zero;
            btnRect.anchoredPosition = new Vector2(20, 210);
            EditorUtility.SetDirty(codexBtnGO);

            EditorUtility.SetDirty(ui);
        }

        private static Button TabButton(Transform parent, Font font, string name, string label, int index, float w)
        {
            var btn = CreateButton(name, parent, font, label, 15,
                new Vector2(12 + index * w, -4), new Vector2(w - 4, 32));
            var rect = btn.GetComponent<RectTransform>();
            TopLeft(rect);
            rect.anchoredPosition = new Vector2(12 + index * w, -4);
            EnsureImage(btn.gameObject).color = new Color(0.13f, 0.14f, 0.18f, 1f);
            return btn;
        }

        private static GameObject BuildScrollList(Transform pane, out GameObject template, Font font)
        {
            var scroll = CreateOrGet("EntryScroll", pane);
            {
                var rect = scroll.GetComponent<RectTransform>();
                Stretch(rect);
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
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
                vlg.spacing = 4f; vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
                var csf = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var sr = scroll.GetComponent<ScrollRect>() ?? scroll.AddComponent<ScrollRect>();
                sr.content = rect; sr.viewport = viewport.GetComponent<RectTransform>();
                sr.horizontal = false; sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
            }

            template = CreateOrGet("EntryButtonTemplate", content.transform);
            {
                var rect = template.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(244, 42);
                EnsureImage(template).color = new Color(0.14f, 0.15f, 0.21f, 1f);
                var btn = template.GetComponent<Button>() ?? template.AddComponent<Button>();
                btn.targetGraphic = template.GetComponent<Image>();
                var cb = btn.colors;
                cb.normalColor = new Color(0.14f, 0.15f, 0.21f, 1f);
                cb.highlightedColor = new Color(0.24f, 0.34f, 0.52f, 1f);
                cb.pressedColor = new Color(0.10f, 0.20f, 0.38f, 1f);
                btn.colors = cb;
                var label = CreateText("Label", template.transform, font, "Entry", 15,
                    new Vector2(10, 0), new Vector2(228, 42), TextAnchor.MiddleLeft);
                TopLeft(label.rectTransform);
                label.rectTransform.anchorMin = new Vector2(0, 0);
                label.rectTransform.anchorMax = new Vector2(1, 1);
                label.rectTransform.offsetMin = new Vector2(10, 0);
                label.rectTransform.offsetMax = new Vector2(-6, 0);
                template.SetActive(false);
            }
            return content;
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
