using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Hotbar ด้านล่างจอ: แสดงปุ่มสำหรับ building แต่ละประเภท (สูงสุด 8 ช่อง)
    /// คลิกปุ่ม → raise OnBuildingSelectRequested → PlacementController.BeginPlacement
    /// ปุ่มหรี่ลง (dimmed) เมื่อ resource ไม่พอ, highlight เมื่อถูกเลือก
    /// แป้น 1-9 ยังใช้ได้เหมือนเดิม และ sync highlight กับ UI ด้วย
    /// </summary>
    public class BuildingSelectionUI : MonoBehaviour
    {
        public static BuildingSelectionUI Instance { get; private set; }

        [Header("Building list (ลำดับตรงกับ hotbar 1-8)")]
        public BuildingData[] buildings;

        [Header("ไอคอนกรอบ Build Menu (ลำดับตรงกับ buildings) — ถ้า null ใช้ data.sprite")]
        public Sprite[] menuIcons;      // wire โดย AtlasUISetup (map ตามชื่อ asset)
        public Sprite hammerIcon;       // ไอคอนค้อนของปุ่มทุบอาคาร (ถ้า null ใช้ emoji 🔨)

        [Header("Runtime — wire โดย setup script")]
        public Transform buttonContainer;

        [Header("พับ/กางแถบอาคาร (ปุ่มหูจับ + แป้น B)")]
        public GameObject panelRoot;   // BuildingSelectionPanel (ตัวที่ซ่อน/โชว์)
        public Button toggleButton;    // ปุ่มหูจับ (สังกัด HUD ไม่ใช่ panel จึงเห็นตอนพับ)
        public Text toggleLabel;
        private bool _collapsed;

        [Header("Hotbar หน้าตา (ปรับได้ใน Inspector — เห็นผลรอบ Play ถัดไป)")]
        [Tooltip("ขนาดช่องอาคาร (กว้าง×สูง px)")]
        public Vector2 slotSize = new Vector2(90f, 110f);
        [Tooltip("ฟอนต์ hotbar (ว่าง = Kanit)")]
        public Font hotbarFont;
        [Tooltip("กรอบไอคอนในช่อง (anchor 0-1): min=ล่างซ้าย, max=บนขวา — ยิ่งห่างยิ่งไอคอนโต")]
        public Vector2 iconAnchorMin = new Vector2(0.1f, 0.35f);
        public Vector2 iconAnchorMax = new Vector2(0.9f, 0.90f);
        [Space(4)]
        [Tooltip("ชื่ออาคาร")] public int nameFontSize = 12;
        public Color nameColor = Color.white;
        [Tooltip("ราคา")] public int costFontSize = 11;
        public Color costColor = new Color(1f, 0.85f, 0.3f);
        [Tooltip("เลขคีย์ลัดมุมช่อง")] public int keyFontSize = 13;
        [Tooltip("ป้ายล็อกเฟส 🔒")] public int lockFontSize = 13;
        public Color lockColor = new Color(1f, 0.85f, 0.3f);

        [Header("Slot template (แก้ layout ช่องด้วยตา — ว่าง = สร้างสดตามฟิลด์ข้างบน)")]
        [Tooltip("object ต้นแบบช่อง (inactive) ที่ code จะ clone แทนการสร้างสด\n" +
                 "สร้างด้วยเมนู NuclearReMind → UI → Bake Hotbar Slot Template แล้วแก้ layout/สี/ฟอนต์ใน Scene ได้เลย\n" +
                 "ต้องมีลูกชื่อ: Icon(Image), NameLabel, CostLabel, KeyLabel, LockLabel + root มี Image+Button")]
        public GameObject slotTemplate;

        [Header("Demolish template (แก้ปุ่มทุบด้วยตา — ว่าง = สร้างสด/ใช้ hammerIcon)")]
        [Tooltip("object ต้นแบบปุ่มทุบ (inactive) ที่ code จะ clone แทนการสร้างสด\n" +
                 "สร้างด้วยเมนู NuclearReMind → UI → Bake Demolish Button Template แล้วแก้ layout/ไอคอน/สีใน Scene ได้เลย\n" +
                 "ต้องมีลูกชื่อ: Icon(Image) และ/หรือ IconEmoji(Text) + root มี Image+Button")]
        public GameObject demolishTemplate;

        // state
        private Button[] _buttons;
        private Image[]  _buttonImages;
        private Text[]   _lockLabels; // "🔒 เฟส N" ต่อช่อง — โชว์เมื่อยังไม่ถึงเฟสปลดล็อก (GDD §6)
        private BuildingData _selected;
        private ResourceData _resources;
        private bool _isDemolishing;
        private int _currentPhase = 1; // cache จาก OnDayStarted (GamePhase.FromDay)

        // demolish button refs (สร้างแยกจาก building slots)
        private Image _demolishImage;

        // สีสถานะ
        private static readonly Color ColNormal      = new Color(0.15f, 0.15f, 0.22f, 1f);
        private static readonly Color ColSelected    = new Color(0.25f, 0.55f, 0.85f, 1f);
        private static readonly Color ColCantAfford  = new Color(0.35f, 0.15f, 0.15f, 1f);
        private static readonly Color ColLocked      = new Color(0.10f, 0.10f, 0.13f, 1f); // ล็อกเฟส — เข้มกว่าปกติ
        private static readonly Color ColDemolish    = new Color(0.35f, 0.10f, 0.10f, 1f);
        private static readonly Color ColDemolishOn  = new Color(0.85f, 0.20f, 0.20f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingSelected    += HandleBuildingSelected;
            EventManager.Instance.OnResourceChanged     += HandleResourceChanged;
            EventManager.Instance.OnDemolishModeToggled += HandleDemolishModeToggled;
            EventManager.Instance.OnDayStarted          += HandleDayStarted;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingSelected    -= HandleBuildingSelected;
            EventManager.Instance.OnResourceChanged     -= HandleResourceChanged;
            EventManager.Instance.OnDemolishModeToggled -= HandleDemolishModeToggled;
            EventManager.Instance.OnDayStarted          -= HandleDayStarted;
        }

        private void Start()
        {
            BuildButtons();

            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleCollapsed);
            ApplyCollapse(); // ตั้งสถานะเริ่ม (กางอยู่)
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) ToggleCollapsed();
        }

        // ───────────── พับ/กางแถบอาคาร ─────────────
        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            ApplyCollapse();
        }

        private void ApplyCollapse()
        {
            if (panelRoot != null) panelRoot.SetActive(!_collapsed);
            if (toggleLabel != null) toggleLabel.text = _collapsed ? "▲ อาคาร" : "▼ อาคาร";
        }

        // ─────────────────────────────────────────
        //  Build UI
        // ─────────────────────────────────────────

        private void BuildButtons()
        {
            if (buttonContainer == null || buildings == null) return;

            // เฟสปัจจุบันตอนสร้างปุ่ม (read-only query — ครอบกรณี UI สร้างหลัง Day เริ่ม/หลังโหลดเซฟ)
            if (GameManager.Instance != null)
                _currentPhase = GameManager.Instance.CurrentPhase;

            _buttons      = new Button[buildings.Length];
            _buttonImages = new Image[buildings.Length];
            _lockLabels   = new Text[buildings.Length];

            // ★ แผงถูก bake เป็น prefab เต็ม (มี Slot_1 อยู่แล้ว) → เติมข้อมูลลง slot เดิม ไม่สร้างใหม่
            //   → แก้ทั้งแผงด้วยตาใน BuildingSelectionPanel.prefab ได้ · runtime ไม่ทำลาย layout/สี/ฟอนต์ที่จัดไว้
            //   ไม่ได้ bake → path เดิม (สร้าง slot สดจาก template/โครง)
            if (HasBakedSlots())
                PopulateExistingSlots();
            else
                GenerateSlots();

            RefreshButtonColors(); // สถานะล็อกเฟสเริ่มต้น (Day 1 = เฟส 1)
        }

        // แผงถูก bake ไว้แล้วหรือยัง (มีช่องแรกจริงในคอนเทนเนอร์) — ตัดสินตอน Start ก่อน generate
        private bool HasBakedSlots() =>
            buttonContainer != null && buttonContainer.Find("Slot_1") != null;

        // path เดิม: ล้างแล้วสร้าง slot สดจาก slotTemplate/โครงตามฟิลด์ Inspector (ไม่มี prefab แผงเต็ม)
        private void GenerateSlots()
        {
            foreach (Transform child in buttonContainer) Destroy(child.gameObject);

            for (int i = 0; i < buildings.Length; i++)
            {
                var data = buildings[i];
                if (data == null) continue;

                var slot = CreateSlot(i, data);
                _buttons[i]      = slot.GetComponent<Button>();
                _buttonImages[i] = slot.GetComponent<Image>();
            }

            BuildDemolishButton();
        }

        // เชื่อม slot ที่ bake ไว้ใน prefab (Slot_1..Slot_N + Slot_Demolish)
        // ★ ผูกเฉพาะสิ่งที่จำเป็น (ปุ่มกด/ลาก/ref ป้ายล็อก) — "ไม่เขียนทับ" text/ไอคอนที่ผู้ใช้แก้มือใน prefab
        //   (ชื่อ/ราคา/คีย์เป็นค่าคงที่ต่ออาคาร bake ลง prefab ไปแล้ว · อยากซิงก์ค่าใหม่จากข้อมูล = bake ใหม่)
        private void PopulateExistingSlots()
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                var data = buildings[i];
                if (data == null) continue;

                var tf = buttonContainer.Find($"Slot_{i + 1}");
                if (tf == null)
                {
                    Debug.LogWarning($"[BuildingSelectionUI] prefab ไม่มี Slot_{i + 1} — bake ใหม่ถ้าจำนวนอาคารเปลี่ยน");
                    continue;
                }
                tf.gameObject.SetActive(true);
                WireExistingSlot(tf.gameObject, i, data);
                _buttons[i]      = tf.GetComponent<Button>();
                _buttonImages[i] = tf.GetComponent<Image>();
            }

            // ซ่อนช่องส่วนเกินที่ bake ไว้ (กรณีรายการอาคารสั้นกว่าที่ bake) — ไม่หลุด layout
            for (int extra = buildings.Length + 1; ; extra++)
            {
                var tf = buttonContainer.Find($"Slot_{extra}");
                if (tf == null) break;
                tf.gameObject.SetActive(false);
            }

            var demo = buttonContainer.Find("Slot_Demolish");
            if (demo != null) { demo.gameObject.SetActive(true); WireDemolish(demo.gameObject); }
            else BuildDemolishButton();
        }

        /// <summary>
        /// [Editor เท่านั้น] สร้าง+เติมข้อมูลทุก slot ลง buttonContainer (อาคาร Slot_1..N + Slot_Demolish)
        /// เพื่อ bake เป็น prefab แผงเต็ม (BuildingSelectionPanelBaker) — เห็นไอคอน/ชื่อ/ราคาจริงตอนแก้ prefab
        /// </summary>
        public void BakePopulateAllSlots()
        {
            if (buttonContainer == null || buildings == null) return;

            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in buttonContainer) kids.Add(c);
            foreach (var c in kids)
            {
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _buttons      = new Button[buildings.Length];
            _buttonImages = new Image[buildings.Length];
            _lockLabels   = new Text[buildings.Length];

            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null) continue;
                var slot = BuildSlotStructure(buttonContainer, $"Slot_{i + 1}");
                slot.SetActive(true);
                PopulateSlot(slot, i, buildings[i]);
            }

            var demo = BuildDemolishStructure(buttonContainer, "Slot_Demolish");
            demo.SetActive(true);
            WireDemolish(demo);
        }

        private static Font LoadKanitFont()
        {
            var f = Resources.Load<Font>("HUD/Fonts/Kanit-Regular");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ฟอนต์ที่ใช้จริง — hotbarFont ถ้าตั้งไว้ ไม่งั้น Kanit
        private Font ResolveFont() => hotbarFont != null ? hotbarFont : LoadKanitFont();

        private void BuildDemolishButton()
        {
            // มี template → clone (แก้ layout/ไอคอนด้วยตาใน object นั้น) · ไม่มี → สร้างสด
            GameObject slot = demolishTemplate != null
                ? Instantiate(demolishTemplate, buttonContainer)
                : BuildDemolishStructure(buttonContainer, "Slot_Demolish");
            slot.name = "Slot_Demolish";
            slot.SetActive(true);
            WireDemolish(slot);
        }

        /// <summary>
        /// สร้าง "โครงปุ่มทุบ" (bg+ปุ่ม, Icon(Image), IconEmoji(Text 🔨), Label, SubLabel)
        /// ใช้ทั้ง runtime (ไม่มี template) และเมนู Bake Demolish Button Template
        /// ไม่ผูก listener/เลือกไอคอน (WireDemolish ทำ) เพื่อให้ template คุม layout/ไอคอนเองได้
        /// </summary>
        public GameObject BuildDemolishStructure(Transform parent, string name)
        {
            var font = ResolveFont();

            var slot = new GameObject(name, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            slot.GetComponent<RectTransform>().sizeDelta = slotSize;

            var bg = slot.AddComponent<Image>();
            bg.color = ColDemolish;

            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.6f, 0.2f, 0.2f, 1f);
            colors.pressedColor     = new Color(0.9f, 0.1f, 0.1f, 1f);
            btn.colors = colors;

            // Icon(Image) — สร้างเสมอ (WireDemolish ใส่ hammerIcon/เปิด-ปิดทีหลัง) · ลาก sprite เองใน Scene ได้
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(slot.transform, false);
            var ir = iconGO.GetComponent<RectTransform>();
            ir.anchorMin = iconAnchorMin;
            ir.anchorMax = iconAnchorMax;
            ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = hammerIcon;
            iconImg.enabled = hammerIcon != null;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // IconEmoji(Text 🔨) — fallback เมื่อไม่มี sprite
            var emoji = MakeText("IconEmoji", slot.transform, font, "🔨", 30,
                new Vector2(0, 20), new Vector2(0, 48), TextAnchor.MiddleCenter);
            emoji.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.3f);
            emoji.gameObject.SetActive(hammerIcon == null);

            MakeText("Label", slot.transform, font, "ทุบอาคาร", 11,
                new Vector2(0, 24), new Vector2(0, 20), TextAnchor.LowerCenter)
                .GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            var subLbl = MakeText("SubLabel", slot.transform, font, "คลิกขวายกเลิก", 9,
                new Vector2(0, 6), new Vector2(0, 16), TextAnchor.LowerCenter);
            subLbl.color = new Color(0.8f, 0.6f, 0.6f);
            subLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            subLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);

            return slot;
        }

        /// <summary>ผูกปุ่ม + เลือกไอคอน (hammerIcon override) ให้ปุ่มทุบ — ใช้ทั้ง clone จาก template และโครงสด</summary>
        private void WireDemolish(GameObject slot)
        {
            _demolishImage = slot.GetComponent<Image>();
            if (_demolishImage != null) _demolishImage.color = ColDemolish;

            // hammerIcon (Inspector) override รูปใน template · ไม่มี = คงรูป/emoji ที่ template ตั้งไว้
            if (hammerIcon != null)
            {
                var iconImg = FindDeep(slot.transform, "Icon")?.GetComponent<Image>();
                if (iconImg != null) { iconImg.sprite = hammerIcon; iconImg.enabled = true; }
                var emoji = FindDeep(slot.transform, "IconEmoji");
                if (emoji != null) emoji.gameObject.SetActive(false);
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                btn.targetGraphic = _demolishImage;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleDemolish);
            }
        }

        private GameObject CreateSlot(int index, BuildingData data)
        {
            // มี template → clone (แก้ layout ด้วยตาใน object นั้น) · ไม่มี → สร้างสดตามฟิลด์
            GameObject slot = slotTemplate != null
                ? Instantiate(slotTemplate, buttonContainer)
                : BuildSlotStructure(buttonContainer, $"Slot_{index + 1}");
            slot.name = $"Slot_{index + 1}";
            slot.SetActive(true);
            PopulateSlot(slot, index, data);
            return slot;
        }

        /// <summary>
        /// สร้าง "โครงช่อง" (bg+ปุ่ม, Icon, NameLabel, CostLabel, KeyLabel, LockLabel) ตามสไตล์ฟิลด์ Inspector
        /// ใช้ทั้ง runtime (ไม่มี template) และเมนู Bake Hotbar Slot Template · ไม่ใส่ข้อมูลอาคาร (PopulateSlot ทำ)
        /// </summary>
        public GameObject BuildSlotStructure(Transform parent, string name)
        {
            var font = ResolveFont();

            var slot = new GameObject(name, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            slot.GetComponent<RectTransform>().sizeDelta = slotSize;

            var bg = slot.AddComponent<Image>();
            bg.color = ColNormal;

            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.45f, 0.65f, 1f);
            colors.pressedColor     = new Color(0.1f, 0.3f, 0.6f, 1f);
            btn.colors = colors;

            var keyLbl = MakeText("KeyLabel", slot.transform, font, "1", keyFontSize,
                new Vector2(4, -4), new Vector2(20, 18), TextAnchor.UpperLeft);
            keyLbl.color = new Color(0.7f, 0.7f, 0.7f);

            // Icon — สร้างเสมอ (PopulateSlot ใส่ sprite/เปิด-ปิดทีหลัง)
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(slot.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = iconAnchorMin;
            iconRect.anchorMax = iconAnchorMax;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var nameLbl = MakeText("NameLabel", slot.transform, font, "ชื่ออาคาร", nameFontSize,
                new Vector2(0, 24), new Vector2(0, 20), TextAnchor.LowerCenter);
            nameLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            nameLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            nameLbl.color = nameColor;
            nameLbl.horizontalOverflow = HorizontalWrapMode.Wrap;

            var costLbl = MakeText("CostLabel", slot.transform, font, "", costFontSize,
                new Vector2(0, 6), new Vector2(0, 18), TextAnchor.LowerCenter);
            costLbl.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            costLbl.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            costLbl.color = costColor;

            var lockLbl = MakeText("LockLabel", slot.transform, font, "🔒 เฟส 1", lockFontSize,
                new Vector2(0, 0), new Vector2(0, 24), TextAnchor.MiddleCenter);
            var lockRect = lockLbl.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0, 0.35f);
            lockRect.anchorMax = new Vector2(1, 0.65f);
            lockLbl.color = lockColor;
            lockLbl.fontStyle = FontStyle.Bold;
            lockLbl.gameObject.SetActive(false);

            return slot;
        }

        /// <summary>
        /// โหมด prefab แผงเต็ม: ผูกเฉพาะสิ่งที่ serialize ไม่ได้/ต้องมีตอนรัน — ปุ่มกด, ลากวาง, ref ป้ายล็อก
        /// "ไม่แตะ" text/ไอคอน (ชื่อ/ราคา/คีย์/รูป) ที่ผู้ใช้แก้มือใน Prefab Mode → คงค่าที่จัดไว้ทุกครั้งที่ Play
        /// </summary>
        private void WireExistingSlot(GameObject slot, int index, BuildingData data)
        {
            // ref ป้ายล็อก (RefreshButtonColors คุมโชว์/ซ่อน) — ไม่แก้ text ที่ bake ไว้
            var lockTxt = FindDeep(slot.transform, "LockLabel")?.GetComponent<Text>();
            if (lockTxt != null) { _lockLabels[index] = lockTxt; lockTxt.gameObject.SetActive(false); }

            // click: lambda ไม่ถูก serialize ใน prefab → ต้องผูกใหม่ทุกครั้งที่รัน
            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                var captured = data;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => EventManager.Instance.RaiseBuildingSelectRequested(captured));
            }

            // drag & drop: ผูกข้อมูลอาคารให้ตัวลาก (เผื่อ prefab เก่าไม่มี component)
            var drag = slot.GetComponent<HotbarSlotDrag>();
            if (drag == null) drag = slot.AddComponent<HotbarSlotDrag>();
            drag.data = data;
        }

        /// <summary>ใส่ข้อมูล/ผูกปุ่มลงช่อง (ทั้ง clone จาก template และโครงสร้างสด) — ไม่แตะ font/สี/layout (ให้ template คุมเอง)</summary>
        private void PopulateSlot(GameObject slot, int index, BuildingData data)
        {
            var slotIcon = (menuIcons != null && index < menuIcons.Length && menuIcons[index] != null)
                ? menuIcons[index] : data.sprite;
            var iconImg = FindDeep(slot.transform, "Icon")?.GetComponent<Image>();
            if (iconImg != null) { iconImg.sprite = slotIcon; iconImg.enabled = slotIcon != null; }

            var nameTxt = FindDeep(slot.transform, "NameLabel")?.GetComponent<Text>();
            if (nameTxt != null) nameTxt.text = data.buildingName;

            var costTxt = FindDeep(slot.transform, "CostLabel")?.GetComponent<Text>();
            if (costTxt != null) costTxt.text = BuildCostString(data);

            var keyTxt = FindDeep(slot.transform, "KeyLabel")?.GetComponent<Text>();
            if (keyTxt != null) keyTxt.text = $"{index + 1}";

            var lockTxt = FindDeep(slot.transform, "LockLabel")?.GetComponent<Text>();
            if (lockTxt != null)
            {
                lockTxt.text = $"🔒 เฟส {data.unlockPhase}";
                lockTxt.gameObject.SetActive(false); // RefreshButtonColors คุมการโชว์
                _lockLabels[index] = lockTxt;
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                var captured = data;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => EventManager.Instance.RaiseBuildingSelectRequested(captured));
            }

            // drag & drop: ลากช่องไปวางบน grid ได้ (แตะสั้น ๆ ยังคลิกเลือกได้เหมือนเดิม)
            var drag = slot.GetComponent<HotbarSlotDrag>();
            if (drag == null) drag = slot.AddComponent<HotbarSlotDrag>();
            drag.data = data;
        }

        // หา child ตามชื่อแบบลึก (รองรับ template ที่ผู้ใช้จัด nested)
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        // ─────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────

        private void HandleBuildingSelected(BuildingData data)
        {
            _selected = data;
            // การเลือกอาคารจะยกเลิก demolish mode อัตโนมัติ
            if (data != null && _isDemolishing)
                EventManager.Instance.RaiseDemolishModeToggled(false);
            RefreshButtonColors();
        }

        private void HandleResourceChanged(ResourceData res)
        {
            _resources = res;
            RefreshButtonColors();
        }

        private void HandleDemolishModeToggled(bool active)
        {
            _isDemolishing = active;
            if (_demolishImage != null)
                _demolishImage.color = active ? ColDemolishOn : ColDemolish;
        }

        // เฟสเกมเปลี่ยนได้เฉพาะตอนขึ้นวันใหม่ (GamePhase.FromDay) → re-render สถานะล็อก
        private void HandleDayStarted(int day, bool timed)
        {
            _currentPhase = GamePhase.FromDay(day);
            RefreshButtonColors();
        }

        private void ToggleDemolish()
        {
            // PlacementController.HandleDemolishModeToggled ดูแล cancel placement แล้ว
            EventManager.Instance.RaiseDemolishModeToggled(!_isDemolishing);
        }

        private void RefreshButtonColors()
        {
            if (_buttons == null) return;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null || buildings[i] == null) continue;

                bool locked     = buildings[i].unlockPhase > _currentPhase; // ยังไม่ถึงเฟส (GDD §6)
                bool isSelected = buildings[i] == _selected && !_isDemolishing;
                bool canAfford  = CanAfford(buildings[i]);

                // ล็อกเฟสชนะทุกสถานะ: ปุ่มหรี่ + กดไม่ได้ + ป้าย 🔒
                _buttons[i].interactable = !locked;
                if (_lockLabels[i] != null) _lockLabels[i].gameObject.SetActive(locked);

                _buttonImages[i].color = locked      ? ColLocked
                                       : isSelected  ? ColSelected
                                       : !canAfford  ? ColCantAfford
                                                     : ColNormal;
            }

            if (_demolishImage != null)
                _demolishImage.color = _isDemolishing ? ColDemolishOn : ColDemolish;
        }

        // ─────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────

        private bool CanAfford(BuildingData data) =>
            _resources.energy >= data.energyCost &&
            _resources.iron   >= data.ironCost &&
            (PopulationManager.Instance == null ||
             PopulationManager.Instance.Current.total >= data.workerRequired);

        private static string BuildCostString(BuildingData data)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (data.energyCost     > 0) parts.Add($"⚡{data.energyCost}");
            if (data.ironCost       > 0) parts.Add($"⛏{data.ironCost}");
            if (data.workerRequired > 0) parts.Add($"👷{data.workerRequired}");
            return parts.Count > 0 ? string.Join(" ", parts) : "ฟรี";
        }

        private static Text MakeText(string name, Transform parent, Font font,
            string content, int size, Vector2 offset, Vector2 sizeDelta, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = offset;
            rect.sizeDelta        = sizeDelta;
            var txt = go.AddComponent<Text>();
            txt.font = font; txt.fontSize = size;
            txt.alignment = anchor; txt.text = content;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            return txt;
        }
    }
}
