using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// แผงควบคุม CORE TOWER (Prototype UI ตามภาพต้นแบบ) — สร้าง runtime ทั้งแผง (ไม่ต้อง wire scene)
    /// เปิดด้วยการคลิก CORE TOWER บนแมพ · Esc/✕/คลิกนอกแผง = ปิด
    ///
    /// องค์ประกอบ:
    ///   • กล่องสถานะ CORE% (Q) + HEAT (เตือนเมื่อ ≥ 80%)
    ///   • หลอดยาว = CORE% แนวตั้ง 0(ล่าง)→100(บน) · หลอดสั้น = เทอร์โมมิเตอร์ HEAT
    ///   • ปุ่มเลือกโหมด 4 โหมด (Overdrive/Boost/Normal/Idle) + ปุ่ม "ยืนยันโหมด"
    ///   • จัดสรร −/+ : Deuterium / Tritium / วิศวกร (ผ่าน OnReactorAllocationAdjust)
    ///   • ระบบหล่อเย็น (กำลัง + น้ำที่ใช้) + ปุ่ม "หล่อเย็นเพิ่ม" · ปุ่ม SCRAM
    /// อ่านสถานะจาก OnTowerProgressChanged/OnResourceChanged + CoreTowerManager.Instance (query อย่างเดียว)
    /// </summary>
    public class CoreTowerPanelUI : MonoBehaviour
    {
        // ── theme (เหล็ก-น้ำเงินอุตสาหกรรม) ──
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.72f);
        static readonly Color CPanel    = new Color(0.10f, 0.12f, 0.15f, 1f);
        static readonly Color CInset    = new Color(0.14f, 0.17f, 0.21f, 1f);
        static readonly Color CInset2   = new Color(0.18f, 0.22f, 0.27f, 1f);
        static readonly Color CBorder   = new Color(0.34f, 0.44f, 0.54f, 1f);
        static readonly Color CText     = new Color(0.90f, 0.93f, 0.96f, 1f);
        static readonly Color CMuted    = new Color(0.56f, 0.63f, 0.71f, 1f);
        static readonly Color CGold     = new Color(0.96f, 0.80f, 0.34f, 1f); // CORE%
        static readonly Color CHeat     = new Color(0.88f, 0.32f, 0.26f, 1f); // HEAT
        static readonly Color CWarn     = new Color(0.96f, 0.55f, 0.24f, 1f);
        static readonly Color CAccent   = new Color(0.46f, 0.76f, 0.95f, 1f);
        static readonly Color CBtn      = new Color(0.20f, 0.40f, 0.55f, 1f);
        static readonly Color CBtnDim   = new Color(0.17f, 0.21f, 0.26f, 1f);
        static readonly Color CScram    = new Color(0.62f, 0.22f, 0.20f, 1f);
        static readonly Color CSelected = new Color(0.28f, 0.52f, 0.72f, 1f);
        static readonly Color CClose    = new Color(0.55f, 0.24f, 0.22f, 1f);

        public Vector2 panelSize = new Vector2(1120f, 900f);
        public Vector2 anchoredPosition = Vector2.zero;

        private Font _font;
        private bool _shown;
        private int _pendingMode = CoreTowerManager.ModeNormal;

        // สร้างตัวเองอัตโนมัติหลังโหลดซีน — ไม่ต้องวาง component ในซีน (กันซ้ำถ้ามีอยู่แล้ว)
        // parent ใต้ HUDCanvas เพื่อให้ BuildPanel หา Canvas เจอ + เรนเดอร์ถูกเลเยอร์
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<CoreTowerPanelUI>() != null) return; // มีในซีนแล้ว → ไม่สร้างซ้ำ

            var canvas = FindBestCanvas();
            var go = new GameObject("CoreTowerPanelUI (auto)");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            go.AddComponent<CoreTowerPanelUI>();
        }

        // เลือก Canvas ที่เหมาะ: เจาะจง HUDCanvas ก่อน · fallback = Canvas ที่ active ตัวแรก
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

        // built refs
        private GameObject _backdrop, _root;
        private Text _dayTxt, _coreValTxt, _heatValTxt, _heatWarnTxt;
        private Text _deutTxt, _tritTxt, _engTxt, _coolPowerTxt, _coolWaterTxt;
        private RectTransform _coreFill, _heatFill;
        private Image _heatFillImg;
        private ModeBtn[] _modes;
        private Button _confirmBtn, _scramBtn, _addCoolBtn;
        private Button _deutMinus, _deutPlus, _engMinus, _engPlus; // ล็อกจัดสรรจนกว่าปลดล็อกเตา (Day 11)
        private Button _tritMinus, _tritPlus; // ล็อก Tritium จนถึงวันพายุ (Day 25) — §GDD

        private class ModeBtn { public Button btn; public Image bg; public int mode; }

        private void Awake() => _font = LoadFont();

        private void OnEnable()
        {
            EventManager.Instance.OnTowerProgressChanged += HandleTowerProgress;
            EventManager.Instance.OnOverclockModeChanged += HandleModeChanged;
            EventManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerProgressChanged -= HandleTowerProgress;
            EventManager.Instance.OnOverclockModeChanged -= HandleModeChanged;
            EventManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        private void Start()
        {
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Input.GetMouseButtonDown(0) && !overUI && !_shown)
            {
                if (ClickedCoreTower()) Open();
            }
            if (_shown && Input.GetKeyDown(KeyCode.Escape)) Hide();
        }

        private bool ClickedCoreTower()
        {
            var reg = BuildingRegistry.Instance;
            if (reg == null || InputManager.Instance == null) return false;
            var cell = InputManager.Instance.GetMouseGridPosition();
            return reg.TryGetBuildingAt(cell, out _, out var data) && data != null
                   && (data.buildingType == BuildingType.CoreTower || data.isCoreTowerPart);
        }

        private void Open()
        {
            _shown = true;
            if (_backdrop != null) { _backdrop.SetActive(true); _backdrop.transform.SetAsLastSibling(); }
            var ct = CoreTowerManager.Instance;
            _pendingMode = ct != null ? Mathf.Clamp(ct.Current.overclockMode, 0, 3) : CoreTowerManager.ModeNormal;
            Refresh();
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
        }

        // ── event handlers ──
        private void HandleTowerProgress(TowerData _)   { if (_shown) Refresh(); }
        private void HandleModeChanged(int mode)        { _pendingMode = mode; if (_shown) Refresh(); }
        private void HandleResourceChanged(ResourceData _) { if (_shown) Refresh(); }

        // ── button actions ──
        private void SelectMode(int mode) { _pendingMode = mode; Refresh(); }
        private void ConfirmMode()  => EventManager.Instance.RaiseOverclockModeRequested(_pendingMode);
        private void DoScram()      => EventManager.Instance.RaiseScramRequested();
        private void Adjust(ReactorAllocation k, int d) => EventManager.Instance.RaiseReactorAllocationAdjust(k, d);

        // ═══════════════════════════ POPULATE ═══════════════════════════
        private void Refresh()
        {
            if (_root == null) return;
            var ct = CoreTowerManager.Instance;
            if (ct == null) return;
            var d = ct.Current;
            var rm = ResourceManager.Instance;
            var res = rm != null ? rm.Current : default;

            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            if (_dayTxt != null) _dayTxt.text = d.isUnlocked ? $"Day {day}" : $"Day {day} · ล็อก (ปลดล็อก Day {CoreTowerManager.UnlockDay})";

            // สถานะ CORE% / HEAT
            if (_coreValTxt != null) _coreValTxt.text = $"{d.corePercent:0.#} %";
            SetBarFill(_coreFill, d.corePercent / 100f);

            if (_heatValTxt != null) _heatValTxt.text = $"{d.coreHeat:0}";
            SetBarFill(_heatFill, d.coreHeat / CoreTowerManager.HeatMeltdown);
            bool hot = d.coreHeat >= CoreTowerManager.HeatWarnZone;
            if (_heatFillImg != null) _heatFillImg.color = hot ? CHeat : new Color(0.80f, 0.45f, 0.28f, 1f);
            if (_heatWarnTxt != null)
            {
                _heatWarnTxt.gameObject.SetActive(hot);
                _heatWarnTxt.text = d.coreHeat >= CoreTowerManager.HeatMeltdown ? "☢ MELTDOWN" : "⚠ ความร้อนสูง! (≥80)";
            }

            // จัดสรรเชื้อเพลิง/วิศวกร — ทั้งระบบล็อกจนกว่าเตาปลดล็อก (Day 11)
            if (_deutMinus != null) _deutMinus.interactable = d.isUnlocked;
            if (_deutPlus  != null) _deutPlus.interactable  = d.isUnlocked;
            if (_engMinus  != null) _engMinus.interactable  = d.isUnlocked;
            if (_engPlus   != null) _engPlus.interactable   = d.isUnlocked;
            if (_deutTxt != null) _deutTxt.text = $"{ct.PlannedDeuterium:0} / ต้องการ {ct.DeuteriumNeed:0}   (คลัง {res.deuterium:0})";

            // Tritium: ใช้ได้เฉพาะช่วงพายุรังสี (Day 25–30) — ก่อนหน้านั้นล็อกไว้ (§GDD)
            bool tritUnlocked = d.isUnlocked && day >= CoreTowerManager.StormStartDay;
            if (_tritMinus != null) _tritMinus.interactable = tritUnlocked;
            if (_tritPlus  != null) _tritPlus.interactable  = tritUnlocked;
            if (_tritTxt != null)
                _tritTxt.text = tritUnlocked
                    ? $"{ct.PlannedTritium:0} / ต้องการ {ct.TritiumNeed:0}   (คลัง {res.tritium:0})"
                    : $"🔒 ปลดล็อกวันพายุ (Day {CoreTowerManager.StormStartDay}+)";

            if (_engTxt  != null) _engTxt.text  = $"{ct.PlannedCoolingEngineers} / มี {ct.MaxCoolingEngineers} คน";

            // หล่อเย็น
            if (_coolPowerTxt != null) _coolPowerTxt.text = $"ระบบหล่อเย็น : {ct.PreviewCooling():0}";
            if (_coolWaterTxt != null) _coolWaterTxt.text = $"( ใช้น้ำ : {ct.PreviewWaterUsed():0} )";

            // ปุ่มโหมด: ไฮไลต์ตัวที่กำลังจะยืนยัน (_pendingMode) · เปิดใช้ได้เมื่อปลดล็อกแล้ว
            foreach (var m in _modes)
            {
                if (m == null) continue;
                if (m.bg != null) m.bg.color = (m.mode == _pendingMode) ? CSelected : CInset2;
                if (m.btn != null) m.btn.interactable = d.isUnlocked;
            }
            if (_confirmBtn != null) _confirmBtn.interactable = d.isUnlocked && _pendingMode != d.overclockMode;

            // SCRAM: HEAT ≥ 90 และ cooldown หมด
            if (_scramBtn != null)
                _scramBtn.interactable = d.isUnlocked && d.coreHeat >= ct.scramHeatThreshold && d.scramCooldown <= 0;
            if (_addCoolBtn != null) _addCoolBtn.interactable = d.isUnlocked;
        }

        private static void SetBarFill(RectTransform fill, float frac)
        {
            if (fill != null) fill.anchorMax = new Vector2(1f, Mathf.Clamp01(frac));
        }

        // ═══════════════════════════ BUILD (runtime) ═══════════════════════════
        private void BuildPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>(); // fallback: หา Canvas ในซีน
            Transform parent = canvas != null ? canvas.transform : transform;

            _backdrop = Panel("CoreTowerBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>(); bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            _root = Panel("CoreTowerPanel", _backdrop.transform, CPanel);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = panelSize;
            rr.anchoredPosition = anchoredPosition;
            AddBorder(_root, CBorder, 3f);

            float W = panelSize.x, pad = 26f;

            // ===== HEADER =====
            var title = Txt("Title", _root.transform, "CORE TOWER", 38, CText, TextAnchor.UpperCenter, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,-16), new Vector2(-40,48));
            _dayTxt = Txt("Day", _root.transform, "Day —", 20, CMuted, TextAnchor.UpperLeft);
            SetRect(_dayTxt.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad,-70), new Vector2(560,28));
            var close = Btn("Close", _root.transform, "✕", 24, CClose);
            SetRect((RectTransform)close.transform, new Vector2(1,1), new Vector2(1,1), new Vector2(1,1), new Vector2(-pad,-pad), new Vector2(44,44));
            close.onClick.AddListener(Hide);

            // ===== STAT BOXES =====
            float boxW = (W - pad*2 - 16f) / 2f;
            _coreValTxt = StatBox("CORE% ( Q )", CGold, pad, -104, boxW, out _);
            _heatValTxt = StatBox("HEAT ( ความร้อน )", CHeat, pad + boxW + 16f, -104, boxW, out _heatWarnTxt);

            // ===== MODE SELECT (ซ้าย) =====
            var mLabel = Txt("ModeLabel", _root.transform, "เลือกโหมดเตาวันนี้ (ล็อคทั้งวัน)", 19, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(mLabel.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(pad,-214), new Vector2(320,44));

            _modes = new ModeBtn[4];
            _modes[0] = ModeButton("Overdrive (สุด)",  "×3 · เร็วสุด · ความร้อน +40 เสี่ยงพัง", CoreTowerManager.ModeOverdrive, pad, -262);
            _modes[1] = ModeButton("Boost (เร่ง)",      "×2 · เร็ว · ความร้อน +20 ต้องหล่อเย็น", CoreTowerManager.ModeBoost,     pad, -354);
            _modes[2] = ModeButton("Normal (ปกติ)",     "×1 · ปลอดภัย แต่ช้า · ความร้อน +5",     CoreTowerManager.ModeNormal,    pad, -446);
            _modes[3] = ModeButton("Idle (พัก)",        "×0 · ไม่ดัน % · พักเตาให้เย็นลง",       CoreTowerManager.ModeIdle,      pad, -538);

            // ===== TUBES =====
            // หลอดยาว = CORE% (0 ล่าง → 100 บน)
            _coreFill = VBar("CoreTube", 360f, -258f, 58f, 476f, CGold, out _);
            var c100 = Txt("C100", _root.transform, "100%", 15, CMuted, TextAnchor.MiddleCenter);
            SetRect(c100.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0.5f,1), new Vector2(360f+29f,-240f), new Vector2(70,20));
            var c0 = Txt("C0", _root.transform, "0%", 15, CMuted, TextAnchor.MiddleCenter);
            SetRect(c0.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0.5f,1), new Vector2(360f+29f,-742f), new Vector2(70,20));

            // หลอดสั้น = HEAT (เทอร์โมมิเตอร์)
            _heatFill = VBar("HeatTube", 452f, -300f, 46f, 400f, CHeat, out _heatFillImg);
            HeatMark(452f, -300f, 46f, 400f, 0.8f); // เส้นเตือน 80%

            // ===== RIGHT: allocation =====
            float rx = 540f, rw = W - pad - rx;
            _deutTxt = AllocRow("⚛ Deuterium", ReactorAllocation.Deuterium, 5, rx, -256f, rw, out _deutMinus, out _deutPlus);
            _tritTxt = AllocRow("☢ Tritium",   ReactorAllocation.Tritium,   5, rx, -330f, rw, out _tritMinus, out _tritPlus);
            _engTxt  = AllocRow("👷 วิศวกรหล่อเย็น", ReactorAllocation.CoolingEngineer, 1, rx, -404f, rw, out _engMinus, out _engPlus);

            _coolPowerTxt = Txt("CoolPower", _root.transform, "ระบบหล่อเย็น : —", 22, CAccent, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(_coolPowerTxt.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(rx,-500f), new Vector2(rw,32));
            _coolWaterTxt = Txt("CoolWater", _root.transform, "( ใช้น้ำ : — )", 18, CMuted, TextAnchor.UpperLeft);
            SetRect(_coolWaterTxt.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(rx,-540f), new Vector2(rw,28));

            // ===== BOTTOM BUTTONS =====
            float by = -pad, bh = 62f, bw = (W - pad*2 - 40f) / 3f;
            _addCoolBtn = Btn("AddCool", _root.transform, "💧 หล่อเย็นเพิ่ม", 22, CBtn);
            SetRect((RectTransform)_addCoolBtn.transform, new Vector2(0,0), new Vector2(0,0), new Vector2(0,0), new Vector2(pad,-by), new Vector2(bw,bh));
            _addCoolBtn.onClick.AddListener(() => Adjust(ReactorAllocation.CoolingWater, 20));

            _scramBtn = Btn("Scram", _root.transform, "⚠ SCRAM", 22, CScram);
            SetRect((RectTransform)_scramBtn.transform, new Vector2(0,0), new Vector2(0,0), new Vector2(0,0), new Vector2(pad+bw+20f,-by), new Vector2(bw,bh));
            _scramBtn.onClick.AddListener(DoScram);

            _confirmBtn = Btn("Confirm", _root.transform, "✔ ยืนยันโหมด", 22, CBtn);
            SetRect((RectTransform)_confirmBtn.transform, new Vector2(0,0), new Vector2(0,0), new Vector2(0,0), new Vector2(pad+(bw+20f)*2f,-by), new Vector2(bw,bh));
            _confirmBtn.onClick.AddListener(ConfirmMode);
        }

        // กล่องสถานะ (title บน + ค่าใหญ่ล่าง) — คืน Text ของค่า · out warn = Text เตือน (ใช้กับ HEAT)
        private Text StatBox(string title, Color valColor, float x, float y, float w, out Text warn)
        {
            var box = Panel("Stat", _root.transform, CInset);
            SetRect(box.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(w,92));
            AddBorder(box, CBorder, 2f);
            var t = Txt("t", box.transform, title, 20, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(t.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-10), new Vector2(-20,26));
            var v = Txt("v", box.transform, "—", 40, valColor, TextAnchor.LowerLeft, FontStyle.Bold);
            SetRect(v.rectTransform, new Vector2(0,0), new Vector2(0.6f,0), new Vector2(0,0), new Vector2(16,12), new Vector2(300,50));
            warn = Txt("warn", box.transform, "", 17, CWarn, TextAnchor.LowerRight, FontStyle.Bold);
            SetRect(warn.rectTransform, new Vector2(0.4f,0), new Vector2(1,0), new Vector2(1,0), new Vector2(-14,14), new Vector2(300,44));
            warn.gameObject.SetActive(false);
            return v;
        }

        // ปุ่มโหมด (ชื่อบน + คำอธิบายล่าง)
        private ModeBtn ModeButton(string name, string desc, int mode, float x, float y)
        {
            var go = new GameObject($"Mode{mode}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var bg = go.GetComponent<Image>(); bg.color = CInset2;
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(320,82));
            AddBorder(go, CBorder, 1.5f);
            var nm = Txt("n", go.transform, name, 22, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(nm.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-10), new Vector2(-16,28));
            var ds = Txt("d", go.transform, desc, 14, CMuted, TextAnchor.UpperLeft);
            SetRect(ds.rectTransform, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(16,-42), new Vector2(-16,34));
            nm.raycastTarget = false; ds.raycastTarget = false;
            var b = go.GetComponent<Button>();
            int m = mode;
            b.onClick.AddListener(() => SelectMode(m));
            return new ModeBtn { btn = b, bg = bg, mode = mode };
        }

        // แถวจัดสรร: ป้าย (ซ้าย) + [−] ค่า [+] (ขวา) + บรรทัดค่ารายละเอียด — คืน Text รายละเอียด + out ปุ่ม −/+
        private Text AllocRow(string label, ReactorAllocation kind, int step, float x, float y, float w,
            out Button minusBtn, out Button plusBtn)
        {
            var row = Panel("Alloc", _root.transform, CInset);
            SetRect(row.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(w,66));
            AddBorder(row, CBorder, 1.5f);
            var lbl = Txt("l", row.transform, label, 20, CText, TextAnchor.UpperLeft, FontStyle.Bold);
            SetRect(lbl.rectTransform, new Vector2(0,1), new Vector2(0.6f,1), new Vector2(0,1), new Vector2(14,-8), new Vector2(0,26));
            var val = Txt("v", row.transform, "—", 15, CAccent, TextAnchor.LowerLeft);
            SetRect(val.rectTransform, new Vector2(0,0), new Vector2(0.7f,0), new Vector2(0,0), new Vector2(14,8), new Vector2(0,24));

            var minus = Btn("−", row.transform, "−", 24, CBtnDim);
            SetRect((RectTransform)minus.transform, new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(-96,0), new Vector2(42,42));
            int s = step; var k = kind;
            minus.onClick.AddListener(() => Adjust(k, -s));
            var plus = Btn("+", row.transform, "+", 24, CBtn);
            SetRect((RectTransform)plus.transform, new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(1,0.5f), new Vector2(-14,0), new Vector2(42,42));
            plus.onClick.AddListener(() => Adjust(k, s));
            minusBtn = minus; plusBtn = plus;
            return val;
        }

        // หลอดแนวตั้ง: bg + fill (anchor ล่าง ปรับ anchorMax.y) — คืน fill RectTransform · out fillImg
        private RectTransform VBar(string name, float x, float y, float w, float h, Color fillColor, out Image fillImg)
        {
            var bg = Panel(name, _root.transform, new Color(0.08f,0.10f,0.13f,1f));
            SetRect(bg.GetComponent<RectTransform>(), new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y), new Vector2(w,h));
            AddBorder(bg, CBorder, 2f);
            var fill = Img("fill", bg.transform, fillColor);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0,0); frt.anchorMax = new Vector2(1,0.3f); frt.pivot = new Vector2(0.5f,0f);
            frt.offsetMin = new Vector2(3,3); frt.offsetMax = new Vector2(-3,0);
            fill.raycastTarget = false;
            fillImg = fill;
            return frt;
        }

        private void HeatMark(float x, float y, float w, float h, float frac)
        {
            var mark = Img("HeatWarnMark", _root.transform, CWarn);
            SetRect(mark.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(0,1), new Vector2(x,y - h*(1f-frac)), new Vector2(w,3));
            mark.raycastTarget = false;
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
            var cb = b.colors; cb.disabledColor = new Color(0.4f,0.4f,0.4f,0.55f); b.colors = cb;
            return b;
        }
        private Outline AddBorder(GameObject target, Color col, float w)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = col; ol.effectDistance = new Vector2(w, w); ol.useGraphicAlpha = false;
            return ol;
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
            var f = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }
}
