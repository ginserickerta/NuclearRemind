using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// แผงคลัง Inventory แบบกริด (ตามภาพต้นแบบ) — สร้าง runtime ทั้งแผง + spawn ตัวเองใต้ HUDCanvas
    /// เปิด/ปิดด้วยปุ่ม I · Esc / ✕ / คลิกนอกแผง = ปิด
    ///
    /// รวม "ทุกอย่าง" ไว้ในกริดเดียว (1 ชนิด = 1 ช่อง):
    ///   • ทรัพยากรบัลก์ จาก ResourceManager (Energy/Water/Iron/Food/Deuterium/Tritium)
    ///   • ไอเทมคราฟต์ จาก InventoryManager (Rad-Gear/PET/ยา/เมล็ด/โคบอลต์ ที่ถือครอง > 0)
    /// คลิกช่อง → แสดงรายละเอียด + รูปไอเทมทางขวา · ปุ่ม "ทิ้งไอเทม" ทิ้งไอเทมคราฟต์ที่เลือก (ทรัพยากรทิ้งไม่ได้)
    /// แท็บหมวด: ทั้งหมด / ทรัพยากร / เชื้อเพลิง / อาหาร / การแพทย์ / เกษตร
    /// อ่านสถานะ read-only (ResourceManager/InventoryManager) · สั่งทิ้งผ่าน event OnDiscardItemRequested
    /// </summary>
    public class InventoryGridUI : MonoBehaviour, GameUIStack.IPanel
    {
        // ── theme (เหล็ก-ทองแดงอุตสาหกรรม เข้มอุ่น — ตามภาพ) ──
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.80f);
        static readonly Color CPanel    = new Color(0.13f, 0.115f, 0.09f, 1f);
        static readonly Color CInset    = new Color(0.10f, 0.09f, 0.07f, 1f);
        static readonly Color CSlot     = new Color(0.11f, 0.10f, 0.08f, 1f);
        static readonly Color CSlotSel  = new Color(0.24f, 0.19f, 0.10f, 1f);
        static readonly Color CBorder   = new Color(0.42f, 0.34f, 0.22f, 1f);
        static readonly Color CBorderLt = new Color(0.58f, 0.47f, 0.30f, 1f);
        static readonly Color CText     = new Color(0.87f, 0.82f, 0.70f, 1f);
        static readonly Color CMuted    = new Color(0.55f, 0.50f, 0.40f, 1f);
        static readonly Color CGold     = new Color(0.84f, 0.66f, 0.33f, 1f);
        static readonly Color CGoldDim  = new Color(0.30f, 0.25f, 0.14f, 1f);
        static readonly Color CTabOff   = new Color(0.14f, 0.12f, 0.10f, 1f);
        static readonly Color CRed      = new Color(0.60f, 0.22f, 0.18f, 1f);
        static readonly Color CRedBright= new Color(0.78f, 0.30f, 0.24f, 1f);

        const int Cols = 8, Rows = 6;          // กริด 8×6 = 48 ช่อง (ตามภาพ)
        const int SlotCap = 999;               // เพดานที่โชว์บนหัวกริด "xx / 999"
        public Vector2 panelSize = new Vector2(1300f, 840f);
        public KeyCode toggleKey = KeyCode.I;

        // ── auto-spawn (ไม่ต้องวาง component ในซีน) ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<InventoryGridUI>() != null) return;
            var canvas = FindBestCanvas();
            var go = new GameObject("InventoryGridUI (auto)");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            go.AddComponent<InventoryGridUI>();
        }

        private static Canvas FindBestCanvas()
        {
            Canvas fallback = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                var root = c.rootCanvas != null ? c.rootCanvas : c;
                if (root.name == "HUDCanvas") return root;
                if (fallback == null) fallback = root;
            }
            return fallback;
        }

        // ── หมวดแท็บ ──
        private enum Tab { All, Resource, Fuel, Food, Medical, Agri }

        // ── entry รวม (ทรัพยากร + ไอเทม) 1 ชนิด = 1 ช่อง ──
        private class Entry
        {
            public string id;         // "res_energy" ... หรือ item.id
            public string name;
            public Tab tab;
            public int count;
            public Sprite sprite;     // null = ใช้ emoji
            public string emoji;
            public string desc;
            public string stackNote;  // บรรทัดสแต็ก/เพดาน (flavor)
            public bool isResource;   // ทรัพยากร → ทิ้งไม่ได้
        }

        private Font _font;
        private bool _shown;
        private Tab _tab = Tab.All;
        private string _selectedId;

        private readonly List<Entry> _all = new List<Entry>();   // ทุกช่องที่มีของ (ไม่กรอง — ใช้ตัวนับหัว)
        private readonly List<Entry> _view = new List<Entry>();  // กรองตามแท็บปัจจุบัน (โชว์ในกริด)

        // built refs
        private GameObject _backdrop, _root;
        private Text _countTxt;
        private SlotCell[] _cells;
        private TabBtn[] _tabs;
        private Button _discardBtn; private Text _discardTxt;
        // detail
        private Image _dIcon; private Text _dIconTxt, _dName, _dCat, _dCount, _dDesc, _dStack, _dHint;

        private class SlotCell { public Button btn; public Image bg; public Image icon; public Text iconTxt; public Text count; public string id; }
        private class TabBtn { public Button btn; public Image bg; public Text label; public Tab tab; }

        private void Awake() => _font = LoadFont();

        private bool _subscribed;

        // auto-spawn AfterSceneLoad → OnEnable อาจรันตอน EventManager.Instance ยัง null (build) → guard กัน NullRef ที่ทำให้ AutoSpawn ล้ม
        private void OnEnable() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnInventoryChanged += HandleChanged;
            EventManager.Instance.OnResourceChanged  += HandleResChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnInventoryChanged -= HandleChanged;
            EventManager.Instance.OnResourceChanged  -= HandleResChanged;
            _subscribed = false;
        }

        private void Start()
        {
            TrySubscribe(); // OnEnable อาจข้าม subscribe ถ้า EventManager ยังไม่พร้อม → ผูกที่นี่ (หลัง Awake ทุกตัว)
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            // Esc จัดการรวมที่ GameUIStack (ผ่าน PauseMenuController) — ไม่เช็คเองแล้ว
        }

        private void HandleChanged()               { if (_shown) Refresh(); }
        private void HandleResChanged(ResourceData _) { if (_shown) Refresh(); }

        // ═══════════════════════════ open / close ═══════════════════════════
        public void Toggle() { if (_shown) Hide(); else Open(); }

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) { UIPopIn.Ensure(_backdrop); _backdrop.SetActive(true); }
            GameUIStack.Push(this); // ขึ้นบนสุด + ลงทะเบียน (บล็อก Pause / Esc=ปิด)
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            GameUIStack.Pop(this);
            UIPopIn.PlayClose(_backdrop); // หุบออก (Windows 11) แล้วปิดเอง
        }

        // ── GameUIStack (แผงปิดได้: Esc=ปิดเหมือน ✕ · กติกากลางใน PauseMenuController) ──
        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // ═══════════════════════════ data ═══════════════════════════
        private void RebuildEntries()
        {
            _all.Clear();

            // ทรัพยากรบัลก์ (โชว์เมื่อ > 0)
            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                var c = rm.Current;
                AddRes("res_energy",   "พลังงาน (Energy)",  Tab.Resource, c.energy,    "⚡",
                       "พลังงานสำหรับเดินเครื่องอาคารและเตา CORE",       ResIcon("Energy"));
                AddRes("res_water",    "น้ำ (Water)",        Tab.Resource, c.water,     "💧",
                       "ใช้บริโภค · หล่อเย็นเตา · สกัดดิวเทอเรียม",        ResIcon("Water"));
                AddRes("res_iron",     "เหล็ก (Iron)",       Tab.Resource, c.iron,      "⛏",
                       "วัสดุก่อสร้างและอัปเกรดอาคาร",                     ResIcon("Iron"));
                AddRes("res_food",     "อาหาร (Food)",       Tab.Food,     c.food,      "🌿",
                       "เลี้ยงประชากร (บริโภค 2/คน/วัน) · เพดาน 500",      ResIcon("Food"));
                AddRes("res_deuterium","ดิวเทอเรียม",        Tab.Fuel,     c.deuterium, "D",
                       "เชื้อเพลิงป้อนเตา — ดัน CORE% ตามโหมด Overclock", ResIcon("Deuterium"));
                AddRes("res_tritium",  "ทริเทียม",           Tab.Fuel,     c.tritium,   "⚛",
                       "เชื้อเพลิงเปิดประตูช่วง CORE ≥ 80 (ใช้ช่วงพายุ Day 25+)", ResIcon("Tritium"));
            }

            // ไอเทมคราฟต์ที่ถือครอง (> 0)
            var inv = InventoryManager.Instance;
            if (inv != null && inv.allItems != null)
            {
                foreach (var item in inv.allItems)
                {
                    if (item == null) continue;
                    int n = inv.GetCount(item.id);
                    if (n <= 0) continue;
                    _all.Add(new Entry
                    {
                        id = item.id,
                        name = string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName,
                        tab = TabOfItem(item.category),
                        count = n,
                        sprite = item.icon,
                        emoji = CategoryEmoji(item.category),
                        desc = string.IsNullOrEmpty(item.description) ? "ไอเทมคราฟต์" : item.description,
                        stackNote = item.maxStack > 0 ? $"ถือสูงสุด {item.maxStack}/ช่อง" : "ถือได้ไม่จำกัด",
                        isResource = false,
                    });
                }
            }

            // กรองตามแท็บ
            _view.Clear();
            foreach (var e in _all)
                if (_tab == Tab.All || e.tab == _tab)
                    _view.Add(e);
        }

        private void AddRes(string id, string name, Tab tab, float amount, string emoji, string desc, Sprite icon = null)
        {
            int n = Mathf.RoundToInt(amount);
            if (n <= 0) return;
            _all.Add(new Entry
            {
                id = id, name = name, tab = tab, count = n,
                sprite = icon, emoji = emoji, desc = desc,   // มี icon จริง → ใช้ sprite · ไม่มี → fallback emoji
                stackNote = "ทรัพยากรบัลก์ (สแต็กใหญ่)", isResource = true,
            });
        }

        // icon ทรัพยากรจาก Resources/Icons/<name>.png (cache ครั้งเดียว · null ได้ → ตกไป emoji)
        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
        private Sprite ResIcon(string name)
        {
            if (!_iconCache.TryGetValue(name, out var s)) { s = Resources.Load<Sprite>("Icons/" + name); _iconCache[name] = s; }
            return s;
        }

        private static Tab TabOfItem(ItemCategory cat)
        {
            switch (cat)
            {
                case ItemCategory.Agri:      return Tab.Agri;
                case ItemCategory.Fuel:      return Tab.Fuel;
                case ItemCategory.Emergency: return Tab.Fuel;   // น้ำหล่อเย็นฉุกเฉิน — เกี่ยวเตา
                default:                     return Tab.Medical; // Medical + Equipment (Rad-Gear/PET/ยา)
            }
        }

        // ═══════════════════════════ refresh ═══════════════════════════
        private void Refresh()
        {
            if (_root == null) return;
            RebuildEntries();

            // ถ้าไอเทมที่เลือกหายไป (ถูกทิ้ง/หมด) → ล้างการเลือก
            if (_selectedId != null && _all.TrueForAll(e => e.id != _selectedId))
                _selectedId = null;

            // ตัวนับหัวกริด = จำนวนช่องที่มีของ (ทุกหมวด) / เพดาน
            if (_countTxt != null) _countTxt.text = $"{_all.Count:00} / {SlotCap} 🔒";

            // เติมกริด
            for (int i = 0; i < _cells.Length; i++)
            {
                var cell = _cells[i];
                if (i < _view.Count) FillCell(cell, _view[i]);
                else ClearCell(cell);
            }

            // แท็บไฮไลต์
            foreach (var t in _tabs)
            {
                if (t == null) continue;
                bool on = t.tab == _tab;
                if (t.bg != null) t.bg.color = on ? CGoldDim : CTabOff;
                if (t.label != null) t.label.color = on ? CGold : CMuted;
            }

            RefreshDetail();
        }

        private void FillCell(SlotCell cell, Entry e)
        {
            cell.id = e.id;
            bool sel = e.id == _selectedId;
            if (cell.bg != null) cell.bg.color = sel ? CSlotSel : CSlot;
            if (cell.btn != null) { cell.btn.interactable = true; var ol = cell.bg.GetComponent<Outline>(); if (ol != null) ol.effectColor = sel ? CGold : CBorder; }

            if (e.sprite != null)
            {
                if (cell.icon != null) { cell.icon.enabled = true; cell.icon.sprite = e.sprite; }
                if (cell.iconTxt != null) cell.iconTxt.enabled = false;
            }
            else
            {
                if (cell.icon != null) cell.icon.enabled = false;
                if (cell.iconTxt != null) { cell.iconTxt.enabled = true; cell.iconTxt.text = e.emoji; }
            }
            if (cell.count != null) { cell.count.enabled = true; cell.count.text = e.count > 999 ? "999+" : e.count.ToString(); }
        }

        private void ClearCell(SlotCell cell)
        {
            cell.id = null;
            if (cell.bg != null) { cell.bg.color = CSlot; var ol = cell.bg.GetComponent<Outline>(); if (ol != null) ol.effectColor = CBorder; }
            if (cell.btn != null) cell.btn.interactable = false;
            if (cell.icon != null) cell.icon.enabled = false;
            if (cell.iconTxt != null) cell.iconTxt.enabled = false;
            if (cell.count != null) cell.count.enabled = false;
        }

        private void RefreshDetail()
        {
            Entry sel = null;
            if (_selectedId != null)
                foreach (var e in _all) if (e.id == _selectedId) { sel = e; break; }

            bool has = sel != null;

            // รูป/emoji
            if (_dIcon != null)    _dIcon.enabled    = has && sel.sprite != null;
            if (_dIconTxt != null) { _dIconTxt.enabled = !has || sel.sprite == null; _dIconTxt.text = has ? sel.emoji : "🎒"; _dIconTxt.color = has ? CText : CMuted; }
            if (has && sel.sprite != null && _dIcon != null) _dIcon.sprite = sel.sprite;

            if (_dName != null)  { _dName.text = has ? sel.name : "เลือกไอเทม"; _dName.color = has ? CGold : CText; }
            if (_dHint != null)  { _dHint.gameObject.SetActive(!has); }
            if (_dCat != null)   { _dCat.gameObject.SetActive(has);   if (has) _dCat.text = TabLabel(sel.tab); }
            if (_dCount != null) { _dCount.gameObject.SetActive(has); if (has) _dCount.text = $"× {sel.count}"; }
            if (_dStack != null) { _dStack.gameObject.SetActive(has); if (has) _dStack.text = sel.stackNote; }
            if (_dDesc != null)  { _dDesc.gameObject.SetActive(has);  if (has) _dDesc.text = sel.desc; }

            // ปุ่มทิ้ง: ทิ้งได้เฉพาะไอเทมคราฟต์ที่เลือก (ทรัพยากรทิ้งไม่ได้)
            bool canDiscard = has && !sel.isResource;
            if (_discardBtn != null) _discardBtn.interactable = canDiscard;
            if (_discardTxt != null) _discardTxt.text = (has && sel.isResource) ? "🗑 ทิ้งทรัพยากรไม่ได้" : "🗑 ทิ้งไอเทม";
        }

        private void SelectSlot(int cellIndex)
        {
            var cell = _cells[cellIndex];
            if (string.IsNullOrEmpty(cell.id)) return;
            _selectedId = cell.id;
            Refresh();
        }

        private void SetTab(Tab t) { _tab = t; _selectedId = null; Refresh(); }

        private void DoDiscard()
        {
            if (_selectedId == null) return;
            var e = _all.Find(x => x.id == _selectedId);
            if (e == null || e.isResource) return;
            EventManager.Instance.RaiseDiscardItemRequested(_selectedId);
            _selectedId = null;
            // Refresh จะถูกเรียกผ่าน OnInventoryChanged อยู่แล้ว แต่เรียกซ้ำกันพลาด
            Refresh();
        }

        // ═══════════════════════════ build (runtime) ═══════════════════════════
        private void BuildPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _backdrop = Panel("InvBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>(); bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            _root = Panel("InvPanel", _backdrop.transform, CPanel);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = panelSize;
            rr.anchoredPosition = Vector2.zero;
            AddBorder(_root, CBorderLt, 3f);

            float W = panelSize.x, pad = 30f;

            // ===== HEADER =====
            var iconBox = Panel("HeaderIcon", _root.transform, CInset);
            SetRect(iconBox.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad,-24), new Vector2(64,64));
            AddBorder(iconBox, CBorder, 2f);
            var hIco = Txt("hi", iconBox.transform, "🎒", 34, CGold, TextAnchor.MiddleCenter);
            Stretch(hIco.rectTransform); hIco.raycastTarget = false;

            var title = Txt("Title", _root.transform, "INVENTORY", 36, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad+80,-24), new Vector2(600,44));
            var sub = Txt("Sub", _root.transform, "คลังของคุณ", 20, CMuted, TextAnchor.UpperLeft);
            SetRect(sub.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad+82,-66), new Vector2(600,26));

            var close = Btn("Close", _root.transform, "✕", 26, CRed);
            SetRect((RectTransform)close.transform, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-pad,-24), new Vector2(52,52));
            AddBorder(close.gameObject, CRedBright, 2f);
            close.onClick.AddListener(Hide);

            // ===== LAYOUT MATH =====
            float contentTop = -110f;
            float leftW = 760f;
            float gridX = pad;
            float rightX = pad + leftW + 24f;
            float rightW = W - rightX - pad;
            float rowY = -(panelSize.y - 40f - 56f); // แถวแท็บ/ปุ่มล่าง (อ้างจาก top)

            // ===== COUNTER (หัวกริด ขวาบน) =====
            _countTxt = Txt("Count", _root.transform, "00 / 999 🔒", 20, CMuted, TextAnchor.UpperRight, FontStyle.Bold);
            SetRect(_countTxt.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(1,1), new Vector2(gridX+leftW,-84), new Vector2(360,26));

            // ===== GRID =====
            float gap = 8f;
            float cell = (leftW - (Cols - 1) * gap) / Cols; // ≈ 88
            float gridTop = contentTop - 6f;
            _cells = new SlotCell[Cols * Rows];
            for (int r = 0; r < Rows; r++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int idx = r * Cols + col;
                    float cx = gridX + col * (cell + gap);
                    float cy = gridTop - r * (cell + gap);
                    _cells[idx] = MakeSlot(idx, cx, cy, cell);
                }
            }

            // ===== DETAIL (ขวา) =====
            float detailTop = contentTop - 6f;
            float detailH = -(rowY) - (-detailTop) - 24f; // สูงจาก detailTop ถึงเหนือแถวปุ่ม
            var detail = Panel("Detail", _root.transform, CInset);
            SetRect(detail.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(rightX,detailTop), new Vector2(rightW, detailH));
            AddBorder(detail, CBorder, 2f);
            BuildDetail(detail.transform, rightW, detailH);

            // ===== TABS (ล่างซ้าย) =====
            var tabDefs = new (Tab t, string label)[] {
                (Tab.All, "▦ ทั้งหมด"), (Tab.Resource, "ทรัพยากร"), (Tab.Fuel, "เชื้อเพลิง"),
                (Tab.Food, "อาหาร"), (Tab.Medical, "การแพทย์"), (Tab.Agri, "เกษตร"),
            };
            _tabs = new TabBtn[tabDefs.Length];
            float tabsW = leftW;
            float tw = (tabsW - (tabDefs.Length - 1) * 8f) / tabDefs.Length; // ≈ 120
            for (int i = 0; i < tabDefs.Length; i++)
            {
                float tx = gridX + i * (tw + 8f);
                _tabs[i] = MakeTab(tabDefs[i].t, tabDefs[i].label, tx, rowY, tw);
            }

            // ===== DISCARD (ล่างขวา) =====
            _discardBtn = Btn("Discard", _root.transform, "🗑 ทิ้งไอเทม", 22, CRed);
            SetRect((RectTransform)_discardBtn.transform, new Vector2(0,1), new Vector2(0,1), new Vector2(1,1), new Vector2(W-pad,rowY), new Vector2(rightW, 56));
            AddBorder(_discardBtn.gameObject, CRedBright, 2f);
            _discardTxt = _discardBtn.GetComponentInChildren<Text>();
            _discardBtn.onClick.AddListener(DoDiscard);
        }

        private SlotCell MakeSlot(int idx, float x, float y, float size)
        {
            var go = new GameObject($"Slot{idx}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var bg = go.GetComponent<Image>(); bg.color = CSlot;
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(size,size));
            AddBorder(go, CBorder, 1.5f);

            var icon = Img("icon", go.transform, Color.white);
            var irt = icon.rectTransform; irt.anchorMin = new Vector2(0.5f,0.5f); irt.anchorMax = new Vector2(0.5f,0.5f); irt.pivot = new Vector2(0.5f,0.5f);
            irt.anchoredPosition = new Vector2(0,4); irt.sizeDelta = new Vector2(size*0.56f, size*0.56f);
            icon.raycastTarget = false; icon.preserveAspect = true; icon.enabled = false;

            var iconTxt = Txt("emoji", go.transform, "", Mathf.RoundToInt(size*0.42f), CText, TextAnchor.MiddleCenter);
            SetRect(iconTxt.rectTransform, new Vector2(0,0), new Vector2(1,1), new Vector2(0.5f,0.5f), new Vector2(0,4), new Vector2(0,0));
            iconTxt.raycastTarget = false; iconTxt.enabled = false;

            var count = Txt("cnt", go.transform, "", 16, CText, TextAnchor.LowerRight, FontStyle.Bold);
            SetRect(count.rectTransform, new Vector2(0,0), new Vector2(1,0), new Vector2(1,0), new Vector2(-6,4), new Vector2(0,22));
            count.raycastTarget = false; count.enabled = false;

            var b = go.GetComponent<Button>();
            int captured = idx;
            b.onClick.AddListener(() => SelectSlot(captured));
            b.interactable = false;
            return new SlotCell { btn = b, bg = bg, icon = icon, iconTxt = iconTxt, count = count, id = null };
        }

        private TabBtn MakeTab(Tab t, string label, float x, float y, float w)
        {
            var go = new GameObject($"Tab_{t}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var bg = go.GetComponent<Image>(); bg.color = CTabOff;
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(w,56));
            AddBorder(go, CBorder, 1.5f);
            var lbl = Txt("l", go.transform, label, 19, CMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(lbl.rectTransform); lbl.raycastTarget = false; lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = go.GetComponent<Button>();
            var tab = t;
            b.onClick.AddListener(() => SetTab(tab));
            return new TabBtn { btn = b, bg = bg, label = lbl, tab = t };
        }

        // การ์ดรายละเอียดขวา
        private void BuildDetail(Transform parent, float w, float h)
        {
            // กล่องรูปไอเทม (บนสุด กลาง)
            float boxS = 200f;
            var iconBox = Panel("DIconBox", parent, new Color(0.09f,0.08f,0.06f,1f));
            SetRect(iconBox.GetComponent<RectTransform>(), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0,-28), new Vector2(boxS,boxS));
            AddBorderDashed(iconBox, CBorder);

            _dIcon = Img("dicon", iconBox.transform, Color.white);
            var drt = _dIcon.rectTransform; drt.anchorMin = new Vector2(0.5f,0.5f); drt.anchorMax = new Vector2(0.5f,0.5f); drt.pivot = new Vector2(0.5f,0.5f);
            drt.anchoredPosition = Vector2.zero; drt.sizeDelta = new Vector2(boxS*0.7f, boxS*0.7f);
            _dIcon.raycastTarget = false; _dIcon.preserveAspect = true; _dIcon.enabled = false;

            _dIconTxt = Txt("diconT", iconBox.transform, "🎒", 96, CMuted, TextAnchor.MiddleCenter);
            Stretch(_dIconTxt.rectTransform); _dIconTxt.raycastTarget = false;

            // ชื่อ
            _dName = Txt("DName", parent, "เลือกไอเทม", 26, CText, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(_dName.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-244), new Vector2(-24,40));

            // หมวด (chip กลาง)
            _dCat = Txt("DCat", parent, "", 17, CGold, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(_dCat.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-288), new Vector2(-24,24));

            // เส้นคั่น
            var div = Img("Div", parent, CBorder);
            SetRect(div.rectTransform, new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0,-320), new Vector2(w-56,2));

            // จำนวน
            _dCount = Txt("DCount", parent, "", 40, CGold, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(_dCount.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-338), new Vector2(-24,52));

            // สแต็ก/เพดาน
            _dStack = Txt("DStack", parent, "", 16, CMuted, TextAnchor.UpperCenter);
            SetRect(_dStack.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-392), new Vector2(-24,24));

            // คำอธิบาย
            _dDesc = Txt("DDesc", parent, "", 18, CText, TextAnchor.UpperCenter);
            SetRect(_dDesc.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-428), new Vector2(-40,140));

            // hint ตอนยังไม่เลือก
            _dHint = Txt("DHint", parent, "กรุณาเลือกไอเทมจากคลัง\nเพื่อดูรายละเอียด", 18, CMuted, TextAnchor.UpperCenter);
            SetRect(_dHint.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-296), new Vector2(-40,80));
        }

        // ─────────── helpers ───────────
        private GameObject Panel(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            return go;
        }
        private Image Img(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col;
            return img;
        }
        private Text Txt(string name, Transform parent, string text, int size, Color col, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        private Button Btn(string name, Transform parent, string label, int size, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = col;
            var t = Txt("T", go.transform, label, size, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform); t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = go.GetComponent<Button>();
            var cb = b.colors; cb.disabledColor = new Color(0.35f,0.33f,0.30f,0.5f); b.colors = cb;
            return b;
        }
        private Outline AddBorder(GameObject target, Color col, float w)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = col; ol.effectDistance = new Vector2(w, w); ol.useGraphicAlpha = false;
            return ol;
        }
        // กรอบประ (จำลอง — ใช้ Outline บาง 2 ชั้นให้ดูเป็นขอบ placeholder)
        private void AddBorderDashed(GameObject target, Color col)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = new Color(col.r, col.g, col.b, 0.6f); ol.effectDistance = new Vector2(1.5f, -1.5f); ol.useGraphicAlpha = false;
        }
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        private static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("HUD/Fonts/Kanit-Regular");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        private static string TabLabel(Tab t)
        {
            switch (t)
            {
                case Tab.Resource: return "หมวด: ทรัพยากร";
                case Tab.Fuel:     return "หมวด: เชื้อเพลิง";
                case Tab.Food:     return "หมวด: อาหาร";
                case Tab.Medical:  return "หมวด: การแพทย์";
                case Tab.Agri:     return "หมวด: เกษตร";
                default:           return "หมวด: ทั่วไป";
            }
        }

        private static string CategoryEmoji(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Equipment: return "🛡";
                case ItemCategory.Medical:   return "⚕";
                case ItemCategory.Agri:      return "🌾";
                case ItemCategory.Emergency: return "🧯";
                default:                     return "⚛";
            }
        }
    }
}
