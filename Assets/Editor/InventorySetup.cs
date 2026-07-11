using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Setup ระบบ Inventory (GDD §13 — ไอเทมคราฟต์) รันผ่าน NuclearReMind / Setup Inventory (Items GDD §13)
    /// idempotent (find-or-create · asset = source of truth แบบ Apply Building Balance):
    ///   1. สร้าง/อัปเดต ItemSO 6 ชนิดตามตาราง §13 ที่ Assets/ScriptableObjects/Items/
    ///      (Deuterium/Tritium คงเป็นทรัพยากรใน ResourceManager · โล่พลาสมา = เงื่อนไขชนะที่ CoreTowerManager ไม่ใช่ไอเทม)
    ///   2. InventoryManager GO + wire catalog allItems
    ///   3. InventoryCanvas แยกจาก HUDCanvas (แบบ StoryCanvas) — แผงไอเทมกลางจอ + ปุ่ม toggle ซ้ายล่าง
    ///      (เหนือปุ่ม Records ที่ y=298) + row template ให้ controller instantiate ตอน runtime
    /// ปุ่มทุกปุ่ม wire onClick ตอน runtime ใน controller.Start — รัน setup ซ้ำได้ไม่มี listener ซ้ำ
    ///
    /// หมายเหตุ §13: GDD บอกโคบอลต์-60 ผลิตที่ "โรงงาน" แต่เกมไม่มีอาคารโรงงาน — ใช้ห้องวิจัยตามหัวข้อ §13
    /// ("ผลิตที่ห้องวิจัย/โรงพยาบาล") · น้ำหล่อเย็น "จากน้ำสะอาด" → โรงกรองน้ำ (WaterPlant)
    /// </summary>
    public static class InventorySetup
    {
        private const string ItemFolder = "Assets/ScriptableObjects/Items";

        [MenuItem("NuclearReMind/Setup Inventory (Items GDD §13)")]
        public static void Setup()
        {
            var items = CreateItemAssets();
            SetupManager(items);
            SetupUI();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[InventorySetup] สร้าง ItemSO 6 ชนิด + InventoryManager + แผง Inventory สำเร็จ — กด Save Scene (Ctrl+S)");
        }

        // ─────────────────────────────────────────────
        //  1. ItemSO assets (ค่าเกมทั้งหมดอยู่ที่นี่ → asset — rule 4)
        // ─────────────────────────────────────────────
        private static ItemSO[] CreateItemAssets()
        {
            if (!Directory.Exists(ItemFolder))
            {
                Directory.CreateDirectory(ItemFolder);
                AssetDatabase.Refresh();
            }

            return new[]
            {
                // Rad-Gear — อุปกรณ์ · Iron + วิจัย · กันคนป่วย/ตายเมื่อเข้าพื้นที่รังสี (ถือไว้มีผล — passive)
                Item("RadGear", so =>
                {
                    so.id = "rad_gear";
                    so.displayName = "Rad-Gear";
                    so.description = "ชุดป้องกันรังสี — กันคนป่วย/ตายเมื่อส่งเข้าพื้นที่รังสี (โซน B) ตามหลัก ALARA: ลดการรับรังสีให้ต่ำที่สุดเท่าที่ทำได้";
                    so.category = ItemCategory.Equipment;
                    so.craftCost = Costs((ResourceType.Iron, 150f));
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 6;
                    so.requiresResearch = true;
                    so.maxStack = 5;
                    so.isPassive = true;
                }),

                // เครื่องสแกน PET/SPECT — การแพทย์ · โรงพยาบาล + พลังงาน · วินิจฉัย (วิกฤต 2·A — เช็ก HasItem)
                Item("PetScanner", so =>
                {
                    so.id = "pet_scanner";
                    so.displayName = "เครื่องสแกน PET/SPECT";
                    so.description = "เครื่องถ่ายภาพเวชศาสตร์นิวเคลียร์ — ใช้ไอโซโทปรังสีตามหาเซลล์ผิดปกติในร่างกาย ใช้วินิจฉัยโรคกลายพันธุ์ (วิกฤต 2·A)";
                    so.category = ItemCategory.Medical;
                    so.craftCost = Costs((ResourceType.Energy, 200f));
                    so.craftedAt = BuildingType.Hospital;
                    so.craftTicks = 8;
                    so.requiresResearch = false;
                    so.maxStack = 1;
                    so.isPassive = true;
                }),

                // ไอโซโทปการแพทย์ — วัสดุแล็บ + ฟลักซ์นิวตรอน (18 tick ≈ 1 วันเกม = "เตา Idle 1 วัน") · รักษาหายสนิท (2·B)
                Item("MedicalIsotope", so =>
                {
                    so.id = "medical_isotope";
                    so.displayName = "ไอโซโทปการแพทย์";
                    so.description = "ยาเฉพาะจุดจากการอาบนิวตรอน (เช่น Tc-99m, Lu-177, I-131) — ฟลักซ์นิวตรอนจากเตาฟิวชันผลิตยารักษาผู้ป่วยหายสนิท (วิกฤต 2·B) ใช้เวลา ~1 วันเกม";
                    so.category = ItemCategory.Medical;
                    so.craftCost = Costs((ResourceType.Iron, 50f), (ResourceType.Energy, 50f));
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 18; // 90s/วัน ÷ 5s/tick = 18 tick ≈ 1 วันเกม (GDD: เตา Idle 1 วัน)
                    so.requiresResearch = true;
                    so.maxStack = 3;
                    so.curesAllSick = true;
                }),

                // เมล็ดพันธุ์ฉายรังสี — เกษตร · ห้องวิจัย + Iron 250 (GDD ระบุ) · เพิ่มผลผลิตอาหารยาว (3·A)
                Item("IrradiatedSeeds", so =>
                {
                    so.id = "irradiated_seeds";
                    so.displayName = "เมล็ดพันธุ์ฉายรังสี";
                    so.description = "เมล็ดพันธุ์ปรับปรุงด้วยการฉายรังสี (mutation breeding — เทคนิคเกษตรนิวเคลียร์จริง) เพิ่มผลผลิตอาหารถาวร (วิกฤต 3·A)";
                    so.category = ItemCategory.Agri;
                    so.craftCost = Costs((ResourceType.Iron, 250f)); // GDD §13 ระบุตรง
                    so.craftedAt = BuildingType.Laboratory;
                    so.craftTicks = 8;
                    so.requiresResearch = true;
                    so.maxStack = 2;
                    so.foodYieldBonus = 0.5f; // ผลผลิตอาหาร +50% ต่อการใช้ (จูนเฟส 8)
                }),

                // เครื่องฉายโคบอลต์-60 — ถนอมอาหาร · Energy 300 (GDD ระบุ) · อาหารเน่า → 0% (3·B)
                Item("Cobalt60Irradiator", so =>
                {
                    so.id = "cobalt60_irradiator";
                    so.displayName = "เครื่องฉายโคบอลต์-60";
                    so.description = "เครื่องฉายรังสีแกมมาจาก Co-60 ฆ่าเชื้อ/ถนอมอาหาร (food irradiation จริง) — หยุดอาหารเน่าเสียถาวร (วิกฤต 3·B)";
                    so.category = ItemCategory.Agri;
                    so.craftCost = Costs((ResourceType.Energy, 300f)); // GDD §13 ระบุตรง
                    so.craftedAt = BuildingType.Laboratory; // §13: ผลิตที่ห้องวิจัย/รพ. (เกมไม่มีอาคาร "โรงงาน")
                    so.craftTicks = 10;
                    so.requiresResearch = false;
                    so.maxStack = 1;
                    so.stopsFoodSpoilage = true;
                }),

                // น้ำหล่อเย็นฉุกเฉิน — ฉุกเฉิน · น้ำสะอาด (โรงกรองน้ำ) · ได้ทันที · ลด HEAT เตาเร่งด่วน (1·C)
                Item("EmergencyCoolant", so =>
                {
                    so.id = "emergency_coolant";
                    so.displayName = "น้ำหล่อเย็นฉุกเฉิน";
                    so.description = "น้ำสะอาดสำรองสำหรับหล่อเย็นเตาเร่งด่วน — ใช้แล้วลด HEAT ทันที (วิกฤต 1·C) การหล่อเย็นคือหัวใจความปลอดภัยของเตาปฏิกรณ์";
                    so.category = ItemCategory.Emergency;
                    so.craftCost = Costs((ResourceType.Water, 100f));
                    so.craftedAt = BuildingType.WaterPlant;
                    so.craftTicks = 0; // ของฉุกเฉิน — ได้ทันที
                    so.requiresResearch = false;
                    so.maxStack = 3;
                    so.coreHeatReduction = 30f; // อ่อนกว่า SCRAM (−40) แต่ไม่มี penalty/cooldown
                }),
            };
        }

        /// <summary>find-or-create ItemSO ที่ path แล้วตั้งค่า (setup = source of truth — รันซ้ำค่ากลับเป๊ะ)</summary>
        private static ItemSO Item(string fileName, System.Action<ItemSO> configure)
        {
            string path = $"{ItemFolder}/{fileName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ItemSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            configure(so);
            EditorUtility.SetDirty(so);
            return so;
        }

        private static ResourceCost[] Costs(params (ResourceType type, float amount)[] costs)
        {
            var result = new ResourceCost[costs.Length];
            for (int i = 0; i < costs.Length; i++)
                result[i] = new ResourceCost { type = costs[i].type, amount = costs[i].amount };
            return result;
        }

        // ─────────────────────────────────────────────
        //  2. InventoryManager + wire catalog
        // ─────────────────────────────────────────────
        private static void SetupManager(ItemSO[] items)
        {
            var go = GameObject.Find("InventoryManager") ?? new GameObject("InventoryManager");
            var mgr = go.GetComponent<InventoryManager>() ?? go.AddComponent<InventoryManager>();
            mgr.allItems = items;
            EditorUtility.SetDirty(go);
        }

        // ─────────────────────────────────────────────
        //  3. UI — InventoryCanvas (แยกจาก HUDCanvas แบบ StoryCanvas → รัน Setup HUD ซ้ำแล้วไม่โดนลบ)
        // ─────────────────────────────────────────────
        private static void SetupUI()
        {
            var font = LoadFont();

            var existing = GameObject.Find("InventoryCanvas");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[InventorySetup] ลบ InventoryCanvas เก่าออก (Ctrl+Z เพื่อคืน)");
            }

            // เหนือ HUDCanvas(0) ใต้ StoryCanvas(60)/PauseCanvas(100)
            var canvasGO = new GameObject("InventoryCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── แผงหลักกลางจอ (light theme — Palette) ──
            var panel = CreatePanel("InventoryPanel", canvasGO.transform);
            Center(panel, new Vector2(560, 470));
            panel.AddComponent<Image>().color = Palette.PanelBg;

            // header
            var header = CreatePanel("Header", panel.transform);
            var hRect = header.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 1); hRect.anchorMax = new Vector2(1, 1);
            hRect.pivot = new Vector2(0.5f, 1); hRect.anchoredPosition = Vector2.zero;
            hRect.sizeDelta = new Vector2(0, 46);
            header.AddComponent<Image>().color = Palette.HeaderBg;

            var title = CreateText("Title", header.transform, font, "🎒 คลังไอเทม (GDD §13)", 20, TextAnchor.MiddleLeft);
            title.color = Palette.TextPrimary;
            title.fontStyle = FontStyle.Bold;
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(14, 0); tRect.offsetMax = new Vector2(-50, 0);

            var closeBtn = CreateButton("CloseBtn", header.transform, font, "✕", 20, new Vector2(38, 38));
            var cRect = closeBtn.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1, 0.5f); cRect.anchorMax = new Vector2(1, 0.5f);
            cRect.pivot = new Vector2(1, 0.5f);
            cRect.anchoredPosition = new Vector2(-4, 0);

            // content — แถวไอเทมเรียงลง (controller instantiate จาก template ตอน runtime)
            var content = CreatePanel("Content", panel.transform);
            var ctRect = content.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(0, 0); ctRect.anchorMax = new Vector2(1, 1);
            ctRect.offsetMin = new Vector2(10, 10); ctRect.offsetMax = new Vector2(-10, -52);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rowTemplate = CreateRowTemplate(content.transform, font);

            // ── ปุ่ม toggle ซ้ายล่าง — เหนือปุ่ม Records (20, 298) ──
            var toggleBtn = CreateButton("InventoryToggleButton", canvasGO.transform, font, "🎒 ไอเทม (I)", 15, new Vector2(120, 36));
            var tgRect = toggleBtn.GetComponent<RectTransform>();
            tgRect.anchorMin = Vector2.zero; tgRect.anchorMax = Vector2.zero;
            tgRect.pivot = new Vector2(0, 0);
            tgRect.anchoredPosition = new Vector2(20, 340);

            // ── controller GO อยู่นอก canvas (รอดตอนรัน setup ซ้ำ) แล้ว re-wire ──
            var ctrlGO = GameObject.Find("InventoryPanelController") ?? new GameObject("InventoryPanelController");
            var ctrl = ctrlGO.GetComponent<InventoryPanelController>() ?? ctrlGO.AddComponent<InventoryPanelController>();
            ctrl.panel = panel;
            ctrl.rowsParent = content.transform;
            ctrl.rowTemplate = rowTemplate;
            ctrl.toggleButton = toggleBtn;
            ctrl.closeButton = closeBtn;
            EditorUtility.SetDirty(ctrlGO);

            panel.SetActive(false); // เริ่มปิด — controller.Toggle เปิด
        }

        /// <summary>แถวต้นแบบ (inactive): Icon / Name / Info / CraftBtn / UseBtn — ชื่อลูกต้องตรงกับ controller</summary>
        private static GameObject CreateRowTemplate(Transform parent, Font font)
        {
            var row = CreatePanel("RowTemplate", parent);
            var rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 58);
            row.AddComponent<Image>().color = Palette.PanelBgAlt;
            var layoutElem = row.AddComponent<LayoutElement>();
            layoutElem.minHeight = 58;
            layoutElem.preferredHeight = 58;

            var icon = CreateText("Icon", row.transform, font, "⚛", 24, TextAnchor.MiddleCenter);
            icon.color = Palette.TextPrimary;
            var iRect = icon.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 0); iRect.anchorMax = new Vector2(0, 1);
            iRect.pivot = new Vector2(0, 0.5f);
            iRect.anchoredPosition = new Vector2(6, 0);
            iRect.sizeDelta = new Vector2(36, 0);

            var name = CreateText("Name", row.transform, font, "ไอเทม ×0", 16, TextAnchor.UpperLeft);
            name.color = Palette.TextPrimary;
            name.fontStyle = FontStyle.Bold;
            var nRect = name.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0, 1); nRect.anchorMax = new Vector2(0, 1);
            nRect.pivot = new Vector2(0, 1);
            nRect.anchoredPosition = new Vector2(48, -6);
            nRect.sizeDelta = new Vector2(300, 24);

            var info = CreateText("Info", row.transform, font, "ต้นทุน: —", 13, TextAnchor.LowerLeft);
            info.color = Palette.TextMuted;
            var fRect = info.GetComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0, 0); fRect.anchorMax = new Vector2(0, 0);
            fRect.pivot = new Vector2(0, 0);
            fRect.anchoredPosition = new Vector2(48, 6);
            fRect.sizeDelta = new Vector2(320, 22);

            var craft = CreateButton("CraftBtn", row.transform, font, "⚒ ผลิต", 14, new Vector2(92, 40));
            var crRect = craft.GetComponent<RectTransform>();
            crRect.anchorMin = new Vector2(1, 0.5f); crRect.anchorMax = new Vector2(1, 0.5f);
            crRect.pivot = new Vector2(1, 0.5f);
            crRect.anchoredPosition = new Vector2(-86, 0);

            var use = CreateButton("UseBtn", row.transform, font, "ใช้", 14, new Vector2(72, 40));
            var uRect = use.GetComponent<RectTransform>();
            uRect.anchorMin = new Vector2(1, 0.5f); uRect.anchorMax = new Vector2(1, 0.5f);
            uRect.pivot = new Vector2(1, 0.5f);
            uRect.anchoredPosition = new Vector2(-8, 0);

            row.SetActive(false); // template — controller ใช้ Instantiate
            return row;
        }

        // ───────────────────────── helpers (แบบ StoryUISetup) ─────────────────────────

        private static Font LoadFont()
        {
            var kanit = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Kanit-Regular.ttf");
            if (kanit != null) return kanit;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Center(GameObject go, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;
            text.verticalOverflow = VerticalWrapMode.Overflow; // Kanit line height สูง — กัน truncate ทั้งบรรทัด
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(4, 0);
            tRect.offsetMax = new Vector2(-4, 0);
            var t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return btn;
        }
    }
}
