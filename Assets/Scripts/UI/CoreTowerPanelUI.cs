using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// แผงควบคุม CORE TOWER — หน้าตาจาก sprite ทีม (Resources/CoreTowerUI) จัดวางตาม mockup portrait
    /// สร้าง runtime ทั้งแผง (ไม่ต้อง wire scene) · เปิดด้วยคลิก CORE TOWER บนแมพ · Esc/✕/คลิกนอกแผง = ปิด
    ///
    /// องค์ประกอบ (sprite): title · gauge_core/heat (หลอดยาว) · stat_integrity/heat · mode_×4 · btn_plus/minus ·
    ///   fuel_d2/t3 · icon_engineer · panel_engineer/cooling · btn_addcool/scram/confirm/close
    ///   + procedural: พื้นหลังโปร่งแสงเข้ม (สไตล์ Frostpunk) · ไอคอนเตาหัวมุม
    /// ฟอนต์ = Chakra Petch (เฉพาะแผงนี้) · ตำแหน่งตัวเลขวัดจาก pixel ของ glyph ที่ฝังในรูป (%, ช่องค่า)
    /// ทุกปุ่มมี UIClickPop (เด้งขยายตอนคลิก) · เปิดแผง = pop-in ทั้งแผง
    ///
    /// ตรรกะ/ค่าทั้งหมดคงเดิม: อ่านจาก OnTowerProgressChanged/OnResourceChanged + CoreTowerManager.Instance (query อย่างเดียว)
    /// ค่าที่แสดง: สมบูรณ์/CORE% = corePercent · ความร้อน = coreHeat/HeatMeltdown · เชื้อเพลิง/วิศวกร/หล่อเย็นผ่าน Adjust
    /// </summary>
    public class CoreTowerPanelUI : MonoBehaviour
    {
        const string SpriteDir = "CoreTowerUI/";

        // ── theme ──
        static readonly Color CBackdrop = new Color(0f, 0.006f, 0.012f, 0.46f);         // หรี่จอ โทนเย็นเล็กน้อย
        static readonly Color CPanelTop = new Color(0.013f, 0.028f, 0.042f, 0.82f);     // dark liquid โปร่งแสง (แก้ว-น้ำเข้ม)
        static readonly Color CPanelBot = new Color(0.070f, 0.078f, 0.094f, 1f);
        static readonly Color CGlassSheen = new Color(0.42f, 0.62f, 0.78f, 1f);          // แสงเหลือบผิวแก้ว (บนสุดของการ์ด)
        static readonly Color CBorder   = new Color(0.227f, 0.251f, 0.282f, 1f);
        static readonly Color CBorderLo = new Color(0.157f, 0.172f, 0.196f, 1f);
        static readonly Color CText     = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color CMuted    = new Color(0.59f, 0.66f, 0.71f, 1f);
        static readonly Color CGold     = new Color(0.957f, 0.769f, 0.251f, 1f);
        static readonly Color CHeat     = new Color(0.878f, 0.290f, 0.227f, 1f);
        static readonly Color CGreen    = new Color(0.471f, 0.784f, 0.353f, 1f);
        static readonly Color CAccent   = new Color(0.471f, 0.745f, 0.863f, 1f);
        static readonly Color CDeut     = new Color(0.275f, 0.510f, 0.863f, 1f);
        static readonly Color CTrit     = new Color(0.353f, 0.784f, 0.471f, 1f);
        static readonly Color CDim      = new Color(0.52f, 0.52f, 0.52f, 1f);   // ปุ่มโหมดที่ไม่ถูกเลือก

        // panel = 1000×1360 หน่วยออกแบบ (top-left origin) — anchoredPosition แปลงเป็น (x, −y) จากมุมบนซ้าย
        public Vector2 panelSize = new Vector2(1000f, 1360f);

        private Font _font;
        private bool _shown;
        private int _pendingMode = CoreTowerManager.ModeNormal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<CoreTowerPanelUI>() != null) return;
            var canvas = FindBestCanvas();
            var go = new GameObject("CoreTowerPanelUI (auto)");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            go.AddComponent<CoreTowerPanelUI>();
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

        // built refs
        private GameObject _backdrop, _root;
        private UIClickPop _rootPop;
        private Text _dayTxt, _integrityTxt, _statHeatTxt, _coreGaugeTxt, _heatGaugeTxt;
        private Text _deutTxt, _tritTxt, _engTxt, _coolPowerTxt, _coolWaterTxt;
        private RectTransform _coreFill, _heatFill;
        private Image _heatFillImg;
        private ModeBtn[] _modes;
        private Button _confirmBtn, _scramBtn, _addCoolBtn;
        private Button _deutMinus, _deutPlus, _engMinus, _engPlus, _tritMinus, _tritPlus;

        private class ModeBtn { public Button btn; public Image img; public int mode; }

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
            if (Input.GetMouseButtonDown(0) && !overUI && !_shown && ClickedCoreTower()) Open();
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
            if (_rootPop != null) _rootPop.PlayFrom(0.5f); // pop-in ทั้งแผง — เด้งจากเล็กไปใหญ่
        }

        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
        }

        private void HandleTowerProgress(TowerData _)    { if (_shown) Refresh(); }
        private void HandleModeChanged(int mode)         { _pendingMode = mode; if (_shown) Refresh(); }
        private void HandleResourceChanged(ResourceData _) { if (_shown) Refresh(); }

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

            if (_dayTxt != null)
                _dayTxt.text = d.isUnlocked ? $"Day {day}" : $"Day {day} · {LockReason(day)}";

            // สมบูรณ์/CORE% = corePercent · ความร้อน = coreHeat เทียบ meltdown เป็น %
            float heatPct = Mathf.Clamp(d.coreHeat / CoreTowerManager.HeatMeltdown * 100f, 0f, 100f);
            if (_integrityTxt != null) _integrityTxt.text = $"{d.corePercent:0}";
            if (_statHeatTxt  != null) _statHeatTxt.text  = $"{heatPct:0}";
            if (_coreGaugeTxt != null) _coreGaugeTxt.text = $"{d.corePercent:0}%";
            if (_heatGaugeTxt != null) _heatGaugeTxt.text = $"{heatPct:0}%";

            SetBarFill(_coreFill, d.corePercent / 100f);
            SetBarFill(_heatFill, d.coreHeat / CoreTowerManager.HeatMeltdown);
            bool hot = d.coreHeat >= CoreTowerManager.HeatWarnZone;
            if (_heatFillImg != null) _heatFillImg.color = hot ? CHeat : new Color(0.80f, 0.45f, 0.28f, 1f);

            // เชื้อเพลิง/วิศวกร — ล็อกจนกว่าเตาปลดล็อก (Day 11) · Tritium เพิ่มล็อกถึงวันพายุ (Day 25)
            bool tritUnlocked = d.isUnlocked && day >= CoreTowerManager.StormStartDay;
            SetInteractable(_deutMinus, d.isUnlocked); SetInteractable(_deutPlus, d.isUnlocked);
            SetInteractable(_engMinus, d.isUnlocked);  SetInteractable(_engPlus, d.isUnlocked);
            SetInteractable(_tritMinus, tritUnlocked);  SetInteractable(_tritPlus, tritUnlocked);

            if (_deutTxt != null) _deutTxt.text = $"{ct.PlannedDeuterium:0}";
            if (_tritTxt != null) _tritTxt.text = tritUnlocked ? $"{ct.PlannedTritium:0}" : "ล็อก";
            if (_engTxt  != null) _engTxt.text  = $"{ct.PlannedCoolingEngineers}/{ct.MaxCoolingEngineers}";

            if (_coolPowerTxt != null) _coolPowerTxt.text = $"{ct.PreviewCooling():0}";
            if (_coolWaterTxt != null) _coolWaterTxt.text = $"{ct.PreviewWaterUsed():0}";

            foreach (var m in _modes)
            {
                if (m == null) continue;
                if (m.img != null) m.img.color = (m.mode == _pendingMode) ? Color.white : CDim;
                if (m.btn != null) m.btn.interactable = d.isUnlocked;
            }
            if (_confirmBtn != null) _confirmBtn.interactable = d.isUnlocked && _pendingMode != d.overclockMode;
            if (_scramBtn != null)
                _scramBtn.interactable = d.isUnlocked && d.coreHeat >= ct.scramHeatThreshold && d.scramCooldown <= 0;
            if (_addCoolBtn != null) _addCoolBtn.interactable = d.isUnlocked;
        }

        // เหตุผลที่เตายังล็อกอยู่ — แยก "ยังไม่ถึงวัน" กับ "ถึงวันแล้วแต่ยังไม่วิจัย" (เดิมข้อความเดียวกันทั้งคู่
        // ทำให้ผู้เล่นเข้าใจผิดว่าปุ่ม Overclock บั๊ก ทั้งที่ต้องไปกดวิจัย "ปลดล็อก CORE TOWER" ที่ห้องวิจัยก่อน)
        private static string LockReason(int day)
        {
            if (day < CoreTowerManager.UnlockDay) return $"ล็อก (ปลด Day {CoreTowerManager.UnlockDay})";
            bool researched = ResearchManager.Instance == null || ResearchManager.Instance.CoreUnlockDone;
            return researched ? "ล็อก" : "ล็อก — ต้องวิจัย 'ปลดล็อก CORE TOWER' ที่ห้องวิจัยก่อน";
        }

        private static void SetInteractable(Button b, bool on) { if (b != null) b.interactable = on; }
        private static void SetBarFill(RectTransform fill, float frac)
        {
            if (fill != null) fill.anchorMax = new Vector2(1f, Mathf.Clamp01(frac));
        }

        // ═══════════════════════════ BUILD ═══════════════════════════
        private void BuildPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _backdrop = Panel("CoreTowerBackdrop", parent, CBackdrop);
            Stretch(_backdrop.GetComponent<RectTransform>());
            var bd = _backdrop.AddComponent<Button>(); bd.transition = Selectable.Transition.None;
            bd.onClick.AddListener(Hide);

            // พื้นหลังแผง (procedural กรอบเหล็กหมุด) — ไล่เฉดบน→ล่าง + ขอบสองชั้น + หมุด 4 มุม
            _root = new GameObject("CoreTowerPanel", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(_backdrop.transform, false);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = panelSize;
            var bgImg = _root.GetComponent<Image>();
            bgImg.color = CPanelTop;
            AddBorder(_root, CBorder, 4f);
            var inner = Img("InnerLine", _root.transform, new Color(0,0,0,0));
            SetRectTL(inner.rectTransform, 10, 10, panelSize.x - 20, panelSize.y - 20);
            AddBorder(inner.gameObject, CBorderLo, 2f); inner.raycastTarget = false;

            // แสงเหลือบผิวแก้ว (dark liquid) — ไล่จางจากขอบบนลงมา ให้การ์ดดูเป็นของเหลว/แก้วโปร่ง
            var sheen = Img("GlassSheen", _root.transform, Color.white);
            sheen.sprite = TopSheenSprite(); sheen.type = Image.Type.Simple; sheen.raycastTarget = false;
            var srt = sheen.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-12f, panelSize.y * 0.46f);  // x: inset 6 ต่อข้าง · y: สูง 46% ของการ์ด
            srt.anchoredPosition = new Vector2(0f, -6f);
            sheen.color = new Color(CGlassSheen.r, CGlassSheen.g, CGlassSheen.b, 0.11f);

            // ย่อทั้งแผงให้พอดีจอ (แผง portrait สูงเกินจอ 16:9) — ตั้ง scale ก่อนแนบ pop เพื่อให้ base ถูก
            var canvasRT = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            float ch = canvasRT != null ? canvasRT.rect.height : 0f;
            float fit = ch > 1f ? Mathf.Min(1f, ch * 0.94f / panelSize.y) : 1f;
            _root.transform.localScale = Vector3.one * fit;

            // pop-in ทั้งแผง: เด้งขยายจากเล็ก (0.5×) ไปใหญ่ · สปริงเกินนิดก่อนเข้าที่
            _rootPop = UIClickPop.Attach(_root); _rootPop.playOnClick = false;
            _rootPop.duration = 0.36f; _rootPop.overshoot = 2.2f;

            // ===== HEADER =====
            ReactorIcon(46, 40, 120, 120);
            var title = SpriteImg("title", 250, 44, 500, deriveH: false, boxH: 96);
            title.raycastTarget = false;
            _dayTxt = Label("Day", 500, 150, 26, CGold, TextAnchor.MiddleCenter, FontStyle.Bold, 400);
            var close = SpriteButtonBox("btn_close", panelSize.x - 118, 36, 78, 78);
            close.onClick.AddListener(Hide); Pop(close);

            // ===== STAT ROW =====
            // % ฝังในรูปที่ x 0.60–0.68, กลาง y 0.715 (วัดจาก pixel) → เลขชิดขวาจบที่ 0.58 แนวเดียวกับ %
            SpriteImg("stat_integrity", 44, 196, 430, deriveH: false, boxH: 232).raycastTarget = false;
            SpriteImg("stat_heat",      526, 196, 430, deriveH: false, boxH: 232).raycastTarget = false;
            _integrityTxt = Label("iv", 44 + 430*0.58f - 120, 196 + 232*0.715f, 46, CGreen, TextAnchor.MiddleRight, FontStyle.Bold, 240);
            _statHeatTxt  = Label("hv", 526 + 430*0.58f - 120, 196 + 232*0.715f, 46, CHeat,  TextAnchor.MiddleRight, FontStyle.Bold, 240);

            // ===== LEFT: mode buttons =====
            Label("ModeLabel", 40 + 298*0.5f, 452, 22, CMuted, TextAnchor.MiddleCenter, FontStyle.Bold, 320, "เลือกโหมดเตาวันนี้");
            _modes = new ModeBtn[4];
            _modes[0] = ModeButton("mode_overdrive", CoreTowerManager.ModeOverdrive, 40, 470, 298);
            _modes[1] = ModeButton("mode_boost",     CoreTowerManager.ModeBoost,     40, 650, 298);
            _modes[2] = ModeButton("mode_normal",    CoreTowerManager.ModeNormal,    40, 830, 298);
            _modes[3] = ModeButton("mode_idle",      CoreTowerManager.ModeIdle,      40, 1010, 298);

            // ===== CENTER: gauges (สไปรต์หลอดยาวรุ่นใหม่ — กว้างตาม aspect จริง ~0.154) =====
            float gy = 480, gh = 600;
            float gAsp = AspectOf("gauge_core"); if (gAsp > 0.5f) gAsp = 0.154f; // กันพลาดถ้าสไปรต์ไม่โหลด
            float gw = gh * gAsp;
            float cgx = 400, hgx = 400 + gw + 64;
            Gauge("gauge_core", cgx, gy, gw, gh, CGold, out _coreFill, out _);
            Gauge("gauge_heat", hgx, gy, gw, gh, CHeat, out _heatFill, out _heatFillImg);
            Label("cg", cgx + gw/2, 454, 18, CMuted, TextAnchor.MiddleCenter, FontStyle.Bold, 200, "ความสำเร็จ");
            Label("hg", hgx + gw/2, 454, 18, CMuted, TextAnchor.MiddleCenter, FontStyle.Bold, 200, "ความร้อน");
            _coreGaugeTxt = Label("cgv", cgx + gw/2, gy + gh + 26, 24, CGold, TextAnchor.MiddleCenter, FontStyle.Bold, 200);
            _heatGaugeTxt = Label("hgv", hgx + gw/2, gy + gh + 26, 24, CHeat, TextAnchor.MiddleCenter, FontStyle.Bold, 200);

            // ===== RIGHT: fuel + engineer + cooling =====
            float rx = 690, rw = 270;
            Label("FuelHdr", rx + rw/2, 452, 22, CGreen, TextAnchor.MiddleCenter, FontStyle.Bold, 260, "เชื้อเพลิง");
            // Deuterium — ถังสไปรต์ทีม (aspect ~0.52) + ป้าย/ปุ่มคนละแถว ไม่ทับถัง
            var d2Icon = SpriteImg("fuel_d2", rx, 478, 54, deriveH: true, boxH: 0); d2Icon.raycastTarget = false;
            UIGlowPulse.Attach(d2Icon.gameObject, CDeut, min: 0.10f, max: 0.55f, dist: 5f, spd: 2.1f);
            Label("DeutL", rx + 168, 496, 20, CText, TextAnchor.MiddleCenter, FontStyle.Bold, 200, "Deuterium");
            _deutMinus = SpriteButton("btn_minus", rx + 68, 522, 46, 52); _deutMinus.onClick.AddListener(() => Adjust(ReactorAllocation.Deuterium, -5)); Pop(_deutMinus);
            _deutTxt = Label("DeutV", rx + 155, 548, 26, CGold, TextAnchor.MiddleCenter, FontStyle.Bold, 90);
            _deutPlus  = SpriteButton("btn_plus", rx + 200, 522, 46, 52); _deutPlus.onClick.AddListener(() => Adjust(ReactorAllocation.Deuterium, 5)); Pop(_deutPlus);
            // Tritium
            var t3Icon = SpriteImg("fuel_t3", rx, 592, 54, deriveH: true, boxH: 0); t3Icon.raycastTarget = false;
            UIGlowPulse.Attach(t3Icon.gameObject, CTrit, min: 0.10f, max: 0.55f, dist: 5f, spd: 2.3f);
            Label("TritL", rx + 168, 610, 20, CText, TextAnchor.MiddleCenter, FontStyle.Bold, 200, "Tritium");
            _tritMinus = SpriteButton("btn_minus", rx + 68, 636, 46, 52); _tritMinus.onClick.AddListener(() => Adjust(ReactorAllocation.Tritium, -5)); Pop(_tritMinus);
            _tritTxt = Label("TritV", rx + 155, 662, 26, CGold, TextAnchor.MiddleCenter, FontStyle.Bold, 90);
            _tritPlus  = SpriteButton("btn_plus", rx + 200, 636, 46, 52); _tritPlus.onClick.AddListener(() => Adjust(ReactorAllocation.Tritium, 5)); Pop(_tritPlus);
            // engineer panel — ช่องค่าฝังที่ x 0.334–0.776 กลาง y 0.634 (วัดจาก pixel) + ไอคอนวิศวกรซ้ายสุด
            float engH = rw / AspectOf("panel_engineer");
            SpriteImg("panel_engineer", rx, 710, rw, deriveH: true, boxH: 0).raycastTarget = false;
            float engMidY = 710 + engH*0.634f;
            SpriteImg("icon_engineer", rx + 10, engMidY - 19, 52, deriveH: true, boxH: 0).raycastTarget = false;
            _engMinus = SpriteButton("btn_minus", rx + 64, engMidY - 23, 40, 46); _engMinus.onClick.AddListener(() => Adjust(ReactorAllocation.CoolingEngineer, -1)); Pop(_engMinus);
            _engTxt = Label("EngV", rx + rw*0.555f, engMidY, 26, CText, TextAnchor.MiddleCenter, FontStyle.Bold, 120);
            _engPlus = ClearButton("EngPlus", rx + rw*0.78f, engMidY - 24, rw*0.18f, 48); _engPlus.onClick.AddListener(() => Adjust(ReactorAllocation.CoolingEngineer, 1)); Pop(_engPlus);
            // cooling panel — % ฝังที่ x 0.562–0.636 กลาง y 0.434 · ช่องเลขน้ำกลาง x 0.573, y 0.80 (วัดจาก pixel)
            float coolY = 710 + engH + 16;
            float coolH = rw / AspectOf("panel_cooling");
            SpriteImg("panel_cooling", rx, coolY, rw, deriveH: true, boxH: 0).raycastTarget = false;
            _coolPowerTxt = Label("CoolP", rx + rw*0.545f - 60, coolY + coolH*0.434f, 26, CAccent, TextAnchor.MiddleRight, FontStyle.Bold, 120);
            _coolWaterTxt = Label("CoolW", rx + rw*0.573f, coolY + coolH*0.80f, 20, CAccent, TextAnchor.MiddleCenter, FontStyle.Bold, 100);

            // ===== BOTTOM BUTTONS =====
            float by = 1210, bw = (panelSize.x - 80 - 40) / 3f, bh = 120;
            _addCoolBtn = SpriteButtonBox("btn_addcool", 40, by, bw, bh); _addCoolBtn.onClick.AddListener(() => Adjust(ReactorAllocation.CoolingWater, 20)); Pop(_addCoolBtn);
            _scramBtn   = SpriteButtonBox("btn_scram",   40 + bw + 20, by, bw, bh); _scramBtn.onClick.AddListener(DoScram); Pop(_scramBtn);
            _confirmBtn = SpriteButtonBox("btn_confirm", 40 + (bw + 20)*2, by, bw, bh); _confirmBtn.onClick.AddListener(ConfirmMode); Pop(_confirmBtn);
        }

        // ─────────── element builders ───────────
        private ModeBtn ModeButton(string sprite, int mode, float x, float y, float w)
        {
            var img = SpriteImg(sprite, x, y, w, deriveH: true, boxH: 0);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None; // คุมสีเอง (เลือก=ขาว, ไม่เลือก=หรี่) ไม่ให้ ColorTint ทับ
            int m = mode; btn.onClick.AddListener(() => SelectMode(m)); Pop(btn);
            UIGlowPulse.Attach(img.gameObject, ModeGlowColor(mode), min: 0.07f, max: 0.42f, dist: 5f, spd: 1.9f);
            return new ModeBtn { btn = btn, img = img, mode = mode };
        }

        // สีเรืองแสงต่อโหมด — ล้อสีลูกศร/โทนของโหมด (overdrive/boost ร้อน · normal เขียว · idle ฟ้า)
        private static Color ModeGlowColor(int mode)
        {
            if (mode == CoreTowerManager.ModeOverdrive) return new Color(0.94f, 0.30f, 0.22f, 1f);
            if (mode == CoreTowerManager.ModeBoost)     return new Color(0.96f, 0.62f, 0.20f, 1f);
            if (mode == CoreTowerManager.ModeNormal)    return new Color(0.47f, 0.78f, 0.35f, 1f);
            return new Color(0.47f, 0.72f, 0.86f, 1f); // idle
        }

        // หลอด gauge: วาง sprite frame แล้ววาง fill (สี) ทับด้านในหลอด (anchor ล่าง ปรับ anchorMax.y)
        // ช่องในวัดจาก pixel ของสไปรต์รุ่นยาว: x 0.267–0.638 · y 0.117–0.958 → inset เผื่อขอบเล็กน้อย
        private void Gauge(string sprite, float x, float y, float w, float h, Color fillColor,
                           out RectTransform fill, out Image fillImg)
        {
            SpriteImg(sprite, x, y, w, deriveH: false, boxH: h).raycastTarget = false;
            var tube = new GameObject("tube", typeof(RectTransform)).GetComponent<RectTransform>();
            tube.SetParent(_root.transform, false);
            SetRectTL(tube, x + w*0.29f, y + h*0.135f, w*0.33f, h*0.805f);
            var f = Img("fill", tube, fillColor);
            var frt = f.rectTransform;
            frt.anchorMin = new Vector2(0,0); frt.anchorMax = new Vector2(1,0.3f); frt.pivot = new Vector2(0.5f,0f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            f.raycastTarget = false;
            fill = frt; fillImg = f;
        }

        private static readonly Color CReactorGlow = new Color(1f, 0.52f, 0.18f, 1f); // ส้ม-แกนเตา

        private void ReactorIcon(float x, float y, float w, float h)
        {
            var s = Resources.Load<Sprite>(SpriteDir + "reactor");
            if (s != null)
            {
                var img = Img("Reactor", _root.transform, Color.white); img.sprite = s; img.preserveAspect = true;
                SetRectTL(img.rectTransform, x, y, w, h); img.raycastTarget = false;
                UIGlowPulse.Attach(img.gameObject, CReactorGlow, min: 0.14f, max: 0.72f, dist: 7f, spd: 1.7f);
                return;
            }
            // procedural: กล่องเหล็ก + แกนเรืองแสง (แกนเต้นแสง)
            var box = Img("Reactor", _root.transform, new Color(0.157f,0.172f,0.196f,1f));
            SetRectTL(box.rectTransform, x, y, w, h); AddBorder(box.gameObject, CBorder, 3f); box.raycastTarget = false;
            var core = Img("rc", box.transform, new Color(0.90f,0.47f,0.16f,1f));
            SetRectTL(core.rectTransform, w*0.36f, h*0.30f, w*0.28f, h*0.44f); core.raycastTarget = false;
            UIGlowPulse.Attach(core.gameObject, CReactorGlow, min: 0.18f, max: 0.85f, dist: 8f, spd: 1.7f, brighten: true);
        }

        // ─────────── sprite/text helpers ───────────
        private float AspectOf(string name)
        {
            var s = Resources.Load<Sprite>(SpriteDir + name);
            return s != null ? s.rect.width / s.rect.height : 1f;
        }

        // สร้าง Image จาก sprite · deriveH=true → สูงตาม aspect(กว้าง w) · deriveH=false → กล่อง (w×boxH) preserveAspect
        private Image SpriteImg(string name, float x, float y, float w, bool deriveH, float boxH)
        {
            var img = Img(name, _root.transform, Color.white);
            var s = Resources.Load<Sprite>(SpriteDir + name);
            img.sprite = s; img.preserveAspect = true;
            float h = deriveH ? (s != null ? w / (s.rect.width / s.rect.height) : w) : boxH;
            SetRectTL(img.rectTransform, x, y, w, h);
            return img;
        }

        private Button SpriteButton(string name, float x, float y, float w, float h)
        {
            var img = Img(name, _root.transform, Color.white);
            img.sprite = Resources.Load<Sprite>(SpriteDir + name); img.preserveAspect = true;
            SetRectTL(img.rectTransform, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            SetDisabledFade(b);
            return b;
        }

        // ปุ่ม sprite แบบ contain ในกล่อง (สำหรับปุ่มล่าง — sprite กว้างกว่าสูง)
        private Button SpriteButtonBox(string name, float x, float y, float w, float h)
        {
            var img = SpriteImg(name, x, y, w, deriveH: false, boxH: h);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            SetDisabledFade(b);
            return b;
        }

        // ปุ่มโปร่งใส (overlay บนปุ่มที่ฝังในรูป เช่น + ของแผงวิศวกร)
        private Button ClearButton(string name, float x, float y, float w, float h)
        {
            var img = Img(name, _root.transform, new Color(1,1,1,0));
            SetRectTL(img.rectTransform, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            return b;
        }

        private void Pop(Component c) => UIClickPop.Attach(c.gameObject);

        private static void SetDisabledFade(Button b)
        {
            var cb = b.colors; cb.disabledColor = new Color(0.45f,0.45f,0.45f,0.5f); b.colors = cb;
        }

        // ─────────── low-level ───────────
        private GameObject Panel(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); go.GetComponent<Image>().color = col;
            return go;
        }
        private Image Img(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col; return img;
        }
        // ป้ายข้อความ วางด้วยพิกัดออกแบบ (จุดกึ่งกลางที่ x,y จากมุมบนซ้าย) — text ว่าง = ป้ายค่าที่เติมใน Refresh
        private Text Label(string name, float x, float y, int size, Color col, TextAnchor anchor, FontStyle style, float w, string text = "")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_root.transform, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = col; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0.5f,0.5f);
            rt.sizeDelta = new Vector2(w, size + 12);
            rt.anchoredPosition = new Vector2(x, -y);
            return t;
        }

        private Outline AddBorder(GameObject target, Color col, float w)
        {
            var ol = target.AddComponent<Outline>();
            ol.effectColor = col; ol.effectDistance = new Vector2(w, w); ol.useGraphicAlpha = false;
            return ol;
        }
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        // วางด้วยพิกัดออกแบบ top-left (x,y,w,h) → center pivot เพื่อ pop เด้งจากกึ่งกลาง
        private void SetRectTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0.5f,0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w/2f, -(y + h/2f));
        }
        // gradient แนวตั้งสำหรับแสงเหลือบผิวแก้ว — ขาวโปร่ง เข้มสุดขอบบน จางลงล่าง (cache ครั้งเดียว)
        private static Sprite _sheenSprite;
        private static Sprite TopSheenSprite()
        {
            if (_sheenSprite != null) return _sheenSprite;
            const int w = 4, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);   // 0 ล่าง → 1 บน
                float a = t * t;                 // เข้มสุดบนสุด จางแบบ ease ลงล่าง
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            _sheenSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _sheenSprite;
        }

        // ฟอนต์เฉพาะแผง CORE TOWER = Chakra Petch (มี Thai glyph) — fallback Kanit → builtin
        private static Font LoadFont()
        {
            var f = Resources.Load<Font>("Fonts/ChakraPetch-Regular");
            if (f == null) f = Resources.Load<Font>("Fonts/Kanit-Regular");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }
}
