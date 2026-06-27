using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup ครบชุดสำหรับ Day 10 — รันผ่าน NuclearReMind / Setup Codex System
    /// ขั้นตอน:
    ///   1. สร้าง CodexEntry assets ทั้ง 5 ใน ScriptableObjects/CodexEntries/
    ///   2. เพิ่ม CodexManager ใน scene + wire allCodexEntries
    ///   3. สร้าง Codex Canvas (panel + list + detail) และ wire CodexUIController
    ///   4. เพิ่มปุ่ม Codex เข้า HUDCanvas
    /// </summary>
    public static class CodexSetup
    {
        private const string ScenePath = "Assets/Scenes/Gamescene.unity";
        private const string EntriesPath = "Assets/ScriptableObjects/CodexEntries";

        [MenuItem("NuclearReMind/Setup Codex System")]
        public static void SetupAll()
        {
            // เปิด scene ถ้ายังไม่เปิด
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            System.IO.Directory.CreateDirectory(EntriesPath);

            var entries = CreateOrLoadEntries();
            SetupCodexManager(entries);
            SetupCodexUI(entries);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[CodexSetup] เสร็จแล้ว! กด Save Scene (Ctrl+S) เพื่อบันทึก");
        }

        // ─────────────────────────────────────────────
        //  1. สร้าง / โหลด CodexEntry assets
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

                asset.entryId           = defs[i].id;
                asset.title             = defs[i].title;
                asset.branch            = defs[i].branch;
                asset.content           = defs[i].content;
                asset.researchPointCost = defs[i].cost;
                asset.unlockedByEvent   = defs[i].unlockedBy;

                EditorUtility.SetDirty(asset);
                result[i] = asset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        // ─────────────────────────────────────────────
        //  2. เพิ่ม CodexManager ใน scene + wire entries
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
        //  3. สร้าง Codex Canvas + wire CodexUIController
        // ─────────────────────────────────────────────
        private static void SetupCodexUI(CodexEntry[] entries)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // หา HUDCanvas ที่มีอยู่แล้ว (สร้างโดย HUDCanvasSetup)
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null)
            {
                Debug.LogWarning("[CodexSetup] ไม่พบ HUDCanvas — รัน NuclearReMind/Setup HUD Canvas ก่อน แล้วรัน Codex System อีกครั้ง");
                return;
            }

            // ── CodexUIController component ──
            var codexGO = GameObject.Find("CodexUIController");
            if (codexGO == null) codexGO = new GameObject("CodexUIController");
            var ui = codexGO.GetComponent<CodexUIController>() ?? codexGO.AddComponent<CodexUIController>();

            // ── Codex Panel (ขวา เปิดทับ screen) ──
            var panel = CreateOrGet("CodexPanel", hudCanvas.transform);
            {
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot     = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(0, 0);
                rect.sizeDelta        = new Vector2(720, 0);
                EnsureImage(panel).color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
                panel.SetActive(false);
            }
            ui.codexPanel = panel;

            // ── Header bar ──
            var header = CreateOrGet("CodexHeader", panel.transform);
            {
                var rect = header.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 50);
                EnsureImage(header).color = new Color(0.04f, 0.04f, 0.08f, 1f);

                var title = CreateText("CodexTitle", header.transform, font, "LEARNING CODEX", 22,
                    new Vector2(-60, 0), new Vector2(580, 50), TextAnchor.MiddleLeft);
                title.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
                title.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
                title.fontStyle = FontStyle.Bold;
                title.color = new Color(0.4f, 0.9f, 1f);

                var rpText = CreateText("RPText", header.transform, font, "RP: 0", 18,
                    new Vector2(-10, 0), new Vector2(100, 50), TextAnchor.MiddleRight);
                rpText.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
                rpText.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
                rpText.color = new Color(1f, 0.85f, 0.2f);
                ui.researchPointText = rpText;

                var closeBtn = CreateButton("CloseBtn", header.transform, font, "✕", 20,
                    new Vector2(-5, 0), new Vector2(40, 40));
                closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0.5f);
                closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.5f);
                closeBtn.onClick.AddListener(() => ui.Toggle());
            }

            // ── Left pane: Entry list + RP counter ──
            var leftPane = CreateOrGet("EntryListPane", panel.transform);
            {
                var rect = leftPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(0, -50);
                rect.sizeDelta = new Vector2(260, -50);
                EnsureImage(leftPane).color = new Color(0.06f, 0.06f, 0.10f, 1f);
            }

            // scroll view inside left pane
            var scroll = CreateOrGet("EntryScroll", leftPane.transform);
            {
                var rect = scroll.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            }
            var viewport = CreateOrGet("Viewport", scroll.transform);
            {
                EnsureImage(viewport).color = Color.clear;
                var mask = viewport.GetComponent<Mask>() ?? viewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var rect = viewport.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            }
            var content = CreateOrGet("EntryContent", viewport.transform);
            {
                var rect = content.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 0);
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
            ui.entryListParent = content.transform;

            // สร้าง entry button prefab (stored as child template — ซ่อนไว้)
            var btnTemplate = CreateOrGet("EntryButtonTemplate", content.transform);
            {
                var rect = btnTemplate.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(248, 40);
                EnsureImage(btnTemplate).color = new Color(0.15f, 0.15f, 0.22f, 1f);
                var btn = btnTemplate.GetComponent<Button>() ?? btnTemplate.AddComponent<Button>();
                btn.targetGraphic = btnTemplate.GetComponent<Image>();
                var cb = btn.colors;
                cb.normalColor      = new Color(0.15f, 0.15f, 0.22f, 1f);
                cb.highlightedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
                cb.pressedColor     = new Color(0.1f, 0.2f, 0.4f, 1f);
                btn.colors = cb;
                CreateText("Label", btnTemplate.transform, font, "Entry", 15,
                    new Vector2(8, 0), new Vector2(232, 40), TextAnchor.MiddleLeft);
                btnTemplate.SetActive(false);
            }
            ui.entryButtonPrefab = btnTemplate;

            // ── Right pane: Detail view ──
            var detailPane = CreateOrGet("DetailPane", panel.transform);
            {
                var rect = detailPane.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.offsetMin = new Vector2(264, 0);
                rect.offsetMax = new Vector2(0, -50);
                EnsureImage(detailPane).color = new Color(0.05f, 0.05f, 0.09f, 1f);
            }

            ui.detailBranch = CreateText("DetailBranch", detailPane.transform, font, "BRANCH", 14,
                new Vector2(12, -14), new Vector2(400, 20), TextAnchor.UpperLeft);
            ui.detailBranch.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            ui.detailBranch.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            ui.detailBranch.color = new Color(0.4f, 0.9f, 1f, 0.7f);
            ui.detailBranch.fontStyle = FontStyle.Bold;

            ui.detailTitle = CreateText("DetailTitle", detailPane.transform, font, "เลือก entry ทางซ้าย", 22,
                new Vector2(12, -36), new Vector2(-24, 36), TextAnchor.UpperLeft);
            ui.detailTitle.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            ui.detailTitle.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            ui.detailTitle.fontStyle = FontStyle.Bold;
            ui.detailTitle.color = Color.white;

            // illustration placeholder (shown when CodexEntry.illustration != null)
            var illustGO = CreateOrGet("DetailIllustration", detailPane.transform);
            {
                var rect = illustGO.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-12, -36);
                rect.sizeDelta = new Vector2(100, 80);
                var img = illustGO.GetComponent<Image>() ?? illustGO.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.15f);
                ui.detailIllustration = img;
            }

            // scrollable content area
            var detailScroll = CreateOrGet("DetailScroll", detailPane.transform);
            {
                var rect = detailScroll.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(8, 8); rect.offsetMax = new Vector2(-8, -80);
            }
            var dViewport = CreateOrGet("DViewport", detailScroll.transform);
            {
                EnsureImage(dViewport).color = Color.clear;
                var mask = dViewport.GetComponent<Mask>() ?? dViewport.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var rect = dViewport.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            }
            var dContent = CreateOrGet("DContent", dViewport.transform);
            {
                var rect = dContent.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 400);
                var dsr = detailScroll.GetComponent<ScrollRect>() ?? detailScroll.AddComponent<ScrollRect>();
                dsr.content = rect; dsr.viewport = dViewport.GetComponent<RectTransform>();
                dsr.horizontal = false; dsr.vertical = true;
                dsr.movementType = ScrollRect.MovementType.Clamped;
            }

            ui.detailContent = CreateText("DetailContent", dContent.transform, font, "", 15,
                Vector2.zero, new Vector2(0, 400), TextAnchor.UpperLeft);
            {
                var rect = ui.detailContent.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            }
            ui.detailContent.color = new Color(0.9f, 0.9f, 0.85f);
            ui.detailContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            ui.detailContent.verticalOverflow = VerticalWrapMode.Overflow;
            ui.detailContent.lineSpacing = 1.3f;

            // ── ปุ่ม Codex ใน HUD (bottom-left) ──
            var codexBtnGO = GameObject.Find("CodexToggleButton");
            if (codexBtnGO == null)
            {
                var btn = CreateButton("CodexToggleButton", hudCanvas.transform, font, "📖 Codex", 16,
                    new Vector2(20, 20), new Vector2(120, 36));
                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(0, 0);
                rect.pivot = new Vector2(0, 0);
                btn.onClick.AddListener(() => ui.Toggle());
                EnsureImage(btn.gameObject).color = new Color(0.1f, 0.15f, 0.28f, 0.9f);
            }

            EditorUtility.SetDirty(ui);
        }

        // ─────────────────────────────────────────────
        //  เนื้อหา 5 entries ภาษาไทย (ระดับมัธยม)
        // ─────────────────────────────────────────────
        private static EntryDef[] BuildEntryDefs()
        {
            var core = new[]
            {
            new EntryDef(
                "Core_01",
                "ฟิชชัน (Nuclear Fission)",
                "Core", 0, "phase_1_complete",
@"ฟิชชัน (Nuclear Fission) คือปฏิกิริยาที่นิวเคลียสของอะตอมหนักแตกออกเป็นนิวเคลียสที่เบากว่า
สองชิ้น พร้อมปล่อยนิวตรอนและพลังงานจำนวนมหาศาล

⚛️ กระบวนการ (ยูเรเนียม-235 เป็นตัวอย่าง)
  นิวตรอนพลังงานต่ำ + U-235  →  U-236* (ไม่เสถียร)
  U-236*  →  Kr-92 + Ba-141 + 3 นิวตรอน + ~200 MeV

  200 MeV ต่อหนึ่งฟิชชัน ฟังดูน้อย แต่ถ้านำ U-235 หนัก 1 กรัม
  (~2.56 × 10²¹ อะตอม) มาทำฟิชชันทั้งหมด จะได้พลังงานเทียบเท่า
  น้ำมันเตาประมาณ 2,700 ลิตร — นี่คือเหตุผลที่นิวเคลียร์ประหยัดเชื้อเพลิงมาก

⚡ การนำไปใช้ในโรงไฟฟ้า
  ความร้อนจากฟิชชัน → ต้มน้ำให้เป็นไอ → ไอหมุนกังหัน → ผลิตไฟฟ้า
  กระบวนการนี้ไม่ปล่อย CO₂ โดยตรง ต่างจากการเผาถ่านหิน

🔬 ฟิชชันกับฟิวชัน ต่างกันอย่างไร?
  • ฟิชชัน  = นิวเคลียสใหญ่แตกออก → ใช้ในโรงไฟฟ้านิวเคลียร์ปัจจุบัน
  • ฟิวชัน  = นิวเคลียสเล็กรวมกัน → CORE TOWER ของ Veltara คือฝันของเทคโนโลยีนี้"
            ),
            new EntryDef(
                "Core_02",
                "ปฏิกิริยาลูกโซ่ (Chain Reaction)",
                "Core", 0, "phase_1_complete",
@"ปฏิกิริยาลูกโซ่ (Chain Reaction) เกิดขึ้นเมื่อนิวตรอนจากฟิชชันหนึ่งครั้งไปก่อให้เกิดฟิชชัน
ครั้งถัดไป ทำให้ปฏิกิริยาดำเนินต่อเนื่องด้วยตัวเองโดยไม่ต้องป้อนพลังงานจากภายนอก

⛓️ ค่า k (Multiplication Factor) — หัวใจของการควบคุม
  k < 1 : Sub-critical — จำนวนฟิชชันลดลงทุกรุ่น ปฏิกิริยาดับเอง
  k = 1 : Critical     — จำนวนฟิชชันคงที่ ← สถานะที่โรงไฟฟ้าต้องรักษา
  k > 1 : Super-critical — จำนวนฟิชชันระเบิดพุ่ง อันตราย

🎛️ แท่งควบคุม (Control Rods)
  ทำจาก โบรอน (B) หรือ แฮฟเนียม (Hf) ซึ่งดูดซับนิวตรอนได้ดี
  กดแท่งลึก → ดูดนิวตรอนมากขึ้น → k ลดลง → กำลังไฟฟ้าลด
  ยกแท่งขึ้น → ดูดนิวตรอนน้อยลง → k เพิ่มขึ้น → กำลังไฟฟ้าเพิ่ม

🛡️ ระบบฉุกเฉิน (SCRAM)
  ถ้าเซ็นเซอร์ตรวจพบความผิดปกติ แท่งควบคุมทุกอันจะถูกปล่อยให้ตกลงสู่แกนกลาง
  ทันที กด k ให้ต่ำกว่า 1 ภายในไม่กี่วินาที หยุดปฏิกิริยาทั้งหมด"
            ),
            new EntryDef(
                "Core_03",
                "ครึ่งชีวิต (Half-Life)",
                "Core", 50, "",
@"ครึ่งชีวิต (Half-Life, t½) คือเวลาที่ใช้เพื่อให้ปริมาณสารกัมมันตรังสีลดลงเหลือครึ่งหนึ่ง
เป็นค่าเฉพาะของแต่ละไอโซโทป ไม่ขึ้นกับอุณหภูมิหรือความดัน

📐 สมการการสลาย
  N(t) = N₀ × (1/2)^(t / t½)
  N₀ = ปริมาณเริ่มต้น | t = เวลาที่ผ่านไป | N(t) = ปริมาณที่เหลือ

  ตัวอย่าง: ไอโอดีน-131 มี t½ = 8 วัน
  วันที่  0: 100 หน่วย
  วันที่  8:  50 หน่วย
  วันที่ 16:  25 หน่วย
  วันที่ 24:  12.5 หน่วย

⏱️ ครึ่งชีวิตของไอโซโทปสำคัญ
  ไอโซโทป         ครึ่งชีวิต      การใช้งาน
  ───────────────────────────────────────────────
  ไอโอดีน-131     8 วัน          รักษามะเร็งต่อมไทรอยด์
  เทคนีเชียม-99m   6 ชั่วโมง      ตรวจวินิจฉัยทางการแพทย์
  ซีเซียม-137     30 ปี          กากนิวเคลียร์จากโรงไฟฟ้า
  คาร์บอน-14      5,730 ปี       กำหนดอายุทางโบราณคดี
  ยูเรเนียม-238   4,470 ล้านปี   เชื้อเพลิงนิวเคลียร์

🏭 ความสำคัญต่อ Veltara
  ครึ่งชีวิตบอกว่ากากนิวเคลียร์จากโรงไฟฟ้าต้องถูกเก็บนานแค่ไหน
  ซีเซียม-137 ต้อง 10 ครึ่งชีวิต (~300 ปี) จึงจะลดลงเหลือน้อยกว่า 0.1%"
            ),
            new EntryDef(
                "Core_04",
                "สารหล่อเย็นในเครื่องปฏิกรณ์ (Coolant)",
                "Core", 50, "",
@"สารหล่อเย็น (Coolant) ไหลผ่านแกนปฏิกรณ์เพื่อดูดซับความร้อนจากฟิชชัน แล้วนำไปผลิตไอน้ำ
หมุนกังหันผลิตไฟฟ้า — การเลือกสารหล่อเย็นกำหนดชนิดและประสิทธิภาพของเครื่องปฏิกรณ์

💧 ชนิดสารหล่อเย็นและเครื่องปฏิกรณ์ที่ใช้
  น้ำธรรมดา (H₂O)
    • PWR (Pressurized Water Reactor) — แรงดันสูงป้องกันการเดือด
    • BWR (Boiling Water Reactor)  — น้ำเดือดในแกน ไอตรงไปหมุนกังหัน
    • พบมากที่สุดในโลก (>70% ของโรงไฟฟ้านิวเคลียร์ทั้งหมด)

  น้ำหนัก (D₂O, Heavy Water)
    • CANDU reactor — ใช้ยูเรเนียมธรรมชาติ ไม่ต้องเสริมสมรรถนะ

  ก๊าซ (CO₂ หรือ He)
    • HTGR (High Temperature Gas Reactor) — อุณหภูมิสูงมาก ประสิทธิภาพดี

  โลหะเหลว (โซเดียม Na)
    • Fast Breeder Reactor — ผลิตเชื้อเพลิงใหม่ขณะทำงาน

🌡️ วงจรความร้อนแบบ PWR (สองวงจร)
  วงจร 1: น้ำอัดแรงดัน → ดูดความร้อนจากแกน → เครื่องกำเนิดไอน้ำ
  วงจร 2: น้ำวงจรสอง → รับความร้อน → กลายเป็นไอ → หมุนกังหัน → ผลิตไฟฟ้า
  (น้ำวงจร 1 ไม่สัมผัสกังหัน ลดการปนเปื้อนกัมมันตรังสี)"
            ),
            new EntryDef(
                "Core_05",
                "รังสีและการป้องกัน (Radiation & Shielding)",
                "Core", 75, "",
@"รังสีนิวเคลียร์คือพลังงานหรืออนุภาคที่ปล่อยออกมาจากนิวเคลียสที่ไม่เสถียร
แต่ละชนิดมีอำนาจทะลุทะลวงและอันตรายต่างกัน

☢️ รังสีสามชนิดหลัก
  รังสี  สิ่งที่ปล่อย         ทะลุทะลวง            การป้องกัน
  ─────────────────────────────────────────────────────────────
  แอลฟา (α) อนุภาค (2p+2n)   ต่ำ (ถูกผิวหนังหยุด)  กระดาษหนึ่งแผ่น
  บีตา  (β) อิเล็กตรอน       กลาง                 แผ่นอะลูมิเนียม 1-2 ซม.
  แกมมา (γ) คลื่นแม่เหล็กไฟฟ้า สูงมาก (ทะลุร่างกาย) ตะกั่วหนา หรือคอนกรีต

  ⚠️ รังสีแอลฟา แม้ทะลุทะลวงน้อย แต่อันตรายมากถ้าสูดหรือกลืนเข้าร่างกาย
     เพราะจะทำลายเซลล์ใกล้เคียงได้โดยตรง

📏 หน่วยวัดรังสีที่ควรรู้
  เบกเคอเรล (Bq)  : จำนวนนิวเคลียสที่สลายตัวต่อวินาที (วัดความแรงของแหล่งกำเนิด)
  เกรย์ (Gy)      : พลังงานรังสีที่ร่างกายดูดซับ (J/kg)
  ซีเวิร์ต (Sv)   : ผลกระทบทางชีวภาพที่ร่างกายได้รับ (คำนึงถึงชนิดรังสี)
  ขีดจำกัดปลอดภัย : < 1 mSv/ปี สำหรับประชาชนทั่วไป (ICRP แนะนำ)
  คนทำงานนิวเคลียร์: < 20 mSv/ปี (เฉลี่ย 5 ปี)

🌟 รังสีในชีวิตประจำวัน
  ทุกคนได้รับรังสีตามธรรมชาติ ~2.4 mSv/ปี จากพื้นดิน อาหาร และรังสีคอสมิก
  X-Ray ทรวงอก ≈ 0.02 mSv | เที่ยวบินข้ามทวีป ≈ 0.1 mSv
  รังสีในระดับต่ำเหล่านี้ร่างกายซ่อมแซมตัวเองได้ตามปกติ"
            ),
            };

            // เพิ่ม 15 entries ของสาขา Agriculture / Medical / Environment (CodexBranchContent.cs)
            var all = new List<EntryDef>(core);
            foreach (var e in CodexBranchContent.Entries())
                all.Add(new EntryDef(e.id, e.title, e.branch, e.cost, e.unlockedBy, e.content));
            return all.ToArray();
        }

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
            CreateText(name + "Label", go.transform, font, label, fontSize,
                Vector2.zero, size, TextAnchor.MiddleCenter);
            return btn;
        }

        private readonly struct EntryDef
        {
            public readonly string id, title, branch, unlockedBy, content;
            public readonly int cost;
            public EntryDef(string id, string title, string branch, int cost, string unlockedBy, string content)
            { this.id = id; this.title = title; this.branch = branch;
              this.cost = cost; this.unlockedBy = unlockedBy; this.content = content; }
        }
    }
}
