using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: แผงจัดคนงานลงงาน (ฟาร์ม/น้ำ/ไฟ/เหมือง/แล็บ/หล่อเย็น) + แถว Zone B และคราฟต์ Rad Suit
    /// [TH] สร้าง UI เองทั้งหมดด้วยโค้ด (ไม่ใช้ prefab) — ปัจจุบัน "ปลดระวาง" แล้ว: จัดคนผ่านการคลิกอาคารแทน
    /// [TH] เก็บไฟล์ไว้เพราะตรรกะเปิด Zone B / คราฟต์ชุดจะถูกยกไปใช้ใน UI ปลายเกมภายหลัง
    /// v6.3 worker-assignment panel (GDD §17) — the live in-game way to move workers between JOBS
    /// (farm/power/water/mine/lab/cool), replacing the legacy per-building cell assignment. Self-contained:
    /// auto-spawns onto HUDCanvas and builds its own uGUI (same pattern as ResearchQueuePanel). Toggled by a
    /// key (default J) since the v6.3 job pool is global, not per-building. Pauses the day clock while open.
    ///
    /// ★ cutover (slice 2 Workers): production reads Σ efficiency per job (ResourceManager.ComputeV63…),
    /// so assigning workers here is what makes food/water/iron/power flow once WorkerManager is live.
    /// </summary>
    public class WorkerAssignPanel : MonoBehaviour, GameUIStack.IPanel
    {
        static readonly Color CBackdrop = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color CPanel    = new Color(0.10f, 0.10f, 0.09f, 0.98f);
        static readonly Color CBorder   = new Color(0.32f, 0.30f, 0.26f, 1f);
        static readonly Color CText      = new Color(0.93f, 0.92f, 0.86f, 1f);
        static readonly Color CMuted     = new Color(0.62f, 0.62f, 0.56f, 1f);
        static readonly Color CGold      = new Color(0.96f, 0.80f, 0.35f, 1f);
        static readonly Color CPlus      = new Color(0.20f, 0.34f, 0.24f, 1f);
        static readonly Color CMinus     = new Color(0.36f, 0.20f, 0.20f, 1f);
        static readonly Color CBtnDim    = new Color(0.15f, 0.15f, 0.13f, 1f);

        public KeyCode toggleKey = KeyCode.J;

        // job id → display label (GDD §17 jobs the player can staff)
        private static readonly (string job, string label)[] Jobs =
        {
            (WorkerJobs.Farm,  "ฟาร์ม (อาหาร)"),
            (WorkerJobs.Water, "น้ำ"),
            (WorkerJobs.Power, "⚡ โรงไฟ (พลังงาน)"),
            (WorkerJobs.Mine,  "⛏ เหมือง (เหล็ก)"),
            (WorkerJobs.Lab,   "ห้องวิจัย"),
            (WorkerJobs.Cool,  "❄ ระบายความร้อนเตา"),
        };

        private Font _font;
        private bool _shown;
        private GameObject _backdrop, _root;
        private Text _header;
        private readonly Dictionary<string, Text> _countTexts = new Dictionary<string, Text>();

        // ★ v6.3 cutover (slice 5 Reactor): Zone B row — the tritium breeder (GDD §22, Method B).
        //   Locked states are SHOWN (🔒 + what unlocks them — rule #6 spirit), never hidden.
        private Text _zoneLabel, _zoneInfo, _zoneCount;
        private Button _zoneMinus, _zonePlus, _zoneOpenBtn;

        // ★ slice 6: Rad Suit row — the Death-Spiral brake (rad ×0.4 in Zone B, CONFIG 🔒 RADIATION)
        private Text _suitLabel;
        private Button _suitCraftBtn;
        private Text _suitCraftLabel;

        // ★ v6.3 cutover (worker-click fix): the J panel is RETIRED — worker assignment now happens by
        //   clicking the building (BuildingUpgradeUI/LabPanelUI +/− → job pool) and cooling by clicking the
        //   CORE TOWER. Auto-spawn is disabled so no panel appears and J does nothing. The file is kept
        //   (not deleted) because its Zone B open/staff + Rad Suit craft logic is the salvage source for a
        //   later end-game building UI (deferred per the design decision — Zone B is Phase-4-only).
        //   To re-enable temporarily for debugging, restore the [RuntimeInitializeOnLoadMethod] hook below.
        //
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        private static void AutoSpawn()
        {
            try { AutoSpawnUnsafe(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorkerAssignPanel] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<WorkerAssignPanel>() != null) return;
            var canvas = FindBestCanvas();
            if (canvas == null) return;
            var go = new GameObject("WorkerAssignPanel (auto)");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<WorkerAssignPanel>();
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

        private void Awake() => _font = LoadFont();

        private static Font LoadFont()
        {
            return UIFonts.Body;
        }

        private void Start()
        {
            BuildPanel();
            Hide();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (_shown) Hide(); else Open();
            }
        }

        // [TH] เปิดแผง: หยุดเวลาเกมชั่วคราวแล้วรีเฟรชตัวเลขทั้งหมด
        private void Open()
        {
            _shown = true;
            if (_backdrop != null) _backdrop.SetActive(true);
            GameUIStack.Push(this);
            TimeManager.Instance?.Pause(PauseReason.LabPopup);
            Refresh();
        }

        // [TH] ปิดแผงและปล่อยเวลาเกมเดินต่อ
        private void Hide()
        {
            _shown = false;
            if (_backdrop != null) _backdrop.SetActive(false);
            GameUIStack.Pop(this);
            TimeManager.Instance?.Resume(PauseReason.LabPopup);
        }

        bool GameUIStack.IPanel.ClosableByEscape => true;
        void GameUIStack.IPanel.BringToFront() => GameUIStack.RaiseToTop(_backdrop);
        GameObject GameUIStack.IPanel.PanelRoot => _backdrop;
        void GameUIStack.IPanel.CloseFromStack() => Hide();

        // [TH] ปุ่ม +/− ของแต่ละงาน: + ดึงคนว่างมาลงงาน · − ถอนคนออกไปว่าง (Zone B รับเฉพาะคนรังสีต่ำพอ)
        private void Move(string job, int dir)
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;

            if (dir > 0)
            {
                // §22: Zone B takes only workers with radiation < zoneb_send_rad_max, lowest first
                if (job == WorkerJobs.ZoneB)
                {
                    var zb = ZoneBController.Instance;
                    if (zb == null || !zb.IsOpen) return;
                    Worker pick = null;
                    float radMax = GameConfigSO.Instance.zoneBSendRadMax;
                    foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
                    {
                        if (!w.alive || w.resting || w.strikeDaysLeft != 0) continue;
                        if (w.radiation >= radMax) continue;
                        if (pick == null || w.radiation < pick.radiation) pick = w;
                    }
                    if (pick != null) wm.AssignJob(pick, WorkerJobs.ZoneB);
                    else EventManager.Instance.RaiseNotice($"ไม่มีคนว่างที่รังสีต่ำพอ (ต้อง < {radMax:0}) จะส่งเข้า Zone B");
                    Refresh();
                    return;
                }

                foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
                    if (w.alive && !w.resting && w.strikeDaysLeft == 0) { wm.AssignJob(w, job); break; }
            }
            else
            {
                var inJob = wm.GetWorkers(job);
                if (inJob.Count > 0) wm.AssignJob(inJob[0], WorkerJobs.Idle);
            }
            Refresh();
        }

        // ★ Open Zone B (GDD §6/§7): needs the tritium note + Phase 4 · costs Iron 150 + Power 200
        //   (no free choices — rule #5). ZoneBController.Open() flips storm pressure & production on.
        // [TH] เปิด Zone B: ต้องวิจัย Note tritium + ถึง Phase 4 และจ่ายเหล็ก+พลังงาน (ไม่มีของฟรี)
        private void OpenZoneB()
        {
            var zb = ZoneBController.Instance;
            var cfg = GameConfigSO.Instance;
            if (zb == null || zb.IsOpen) return;
            if (!KnowledgeDB.Instance.HasNote("tritium") || !PhaseManager.IsPhaseUnlocked(4)) return;

            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                if (rm.Current.iron < cfg.zoneBBuildIron || rm.Current.energy < cfg.zoneBBuildPower)
                {
                    EventManager.Instance.RaiseNotice(
                        $"ทรัพยากรไม่พอ — Zone B ต้องใช้เหล็ก {cfg.zoneBBuildIron:0} + พลังงาน {cfg.zoneBBuildPower:0}");
                    return;
                }
                EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -cfg.zoneBBuildIron);
                EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -cfg.zoneBBuildPower);
            }
            zb.Open();
            Refresh();
        }

        // [TH] อัปเดตตัวเลขทุกแถวให้ตรงสถานะจริง (จำนวนคนต่องาน, Zone B, Rad Suit)
        private void Refresh()
        {
            var wm = WorkerManager.Instance;
            if (_header != null)
            {
                int idle = wm != null ? wm.GetWorkers(WorkerJobs.Idle).Count : 0;
                int alive = wm != null ? wm.AliveCount : 0;
                _header.text = wm == null
                    ? "จัดคนงาน — ระบบยังไม่พร้อม"
                    : $"จัดคนงาน (v6.3) · ว่าง {idle} / ทั้งหมด {alive} คน";
            }
            foreach (var kv in _countTexts)
            {
                int n = wm != null ? wm.GetWorkers(kv.Key).Count : 0;
                kv.Value.text = n.ToString();
            }
            RefreshZoneB(wm);
            RefreshRadSuit(wm);
        }

        // Rad Suit row states: 🔒 needs nuclear_medicine → craftable (labMat 30, up to 5).
        // [TH] แถว Rad Suit: ยังไม่วิจัย = โชว์ว่าล็อกอยู่ (ห้ามซ่อน) · วิจัยแล้ว = คราฟต์ได้ตามเป้า
        private void RefreshRadSuit(WorkerManager wm)
        {
            if (_suitLabel == null) return;
            var rs = RadSuitManager.Instance;
            var cfg = GameConfigSO.Instance;
            bool hasNote = KnowledgeDB.Instance.HasNote("nuclear_medicine");

            if (rs == null)
            {
                _suitLabel.text = "Rad Suit — ระบบยังไม่พร้อม";
                if (_suitCraftBtn != null) _suitCraftBtn.gameObject.SetActive(false);
                return;
            }

            if (!hasNote)
            {
                _suitLabel.text = "Rad Suit — [ล็อก] ต้องวิจัย Note 'nuclear_medicine'";
                if (_suitCraftBtn != null) _suitCraftBtn.gameObject.SetActive(false);
                return;
            }

            int inZone = wm != null ? wm.GetWorkers(WorkerJobs.ZoneB).Count : 0;
            int worn = Mathf.Min(rs.SuitsMade, inZone);
            _suitLabel.text = $"Rad Suit · มี {rs.SuitsMade}/{cfg.suitTargetCount} · สวมใน Zone B {worn} คน (รังสี ×{cfg.radSuitMult:0.0#})";

            if (_suitCraftBtn != null)
            {
                _suitCraftBtn.gameObject.SetActive(true);
                _suitCraftBtn.interactable = rs.CanCraft;
                if (_suitCraftLabel != null)
                    _suitCraftLabel.text = rs.SuitsMade >= cfg.suitTargetCount
                        ? "ครบเป้าแล้ว"
                        : $"คราฟต์ (labMat {cfg.suitCostLabMat})";
            }
        }

        // Zone B row states: 🔒 no note → 🔒 phase < 4 → "open" button → live staffing row.
        // [TH] แถว Zone B ไล่สถานะ: ล็อกเพราะยังไม่วิจัย → ล็อกเพราะเฟสไม่ถึง → ปุ่มเปิด → จัดคนได้จริง
        private void RefreshZoneB(WorkerManager wm)
        {
            if (_zoneLabel == null) return;
            var zb = ZoneBController.Instance;
            var cfg = GameConfigSO.Instance;
            bool hasNote = KnowledgeDB.Instance.HasNote("tritium");
            bool phaseOk = PhaseManager.IsPhaseUnlocked(4);
            bool open = zb != null && zb.IsOpen;
            bool canStaff = open && wm != null;

            if (_zoneCount != null)
            {
                _zoneCount.gameObject.SetActive(canStaff);
                _zoneCount.text = (wm != null ? wm.GetWorkers(WorkerJobs.ZoneB).Count : 0).ToString();
            }
            if (_zoneMinus != null) _zoneMinus.gameObject.SetActive(canStaff);
            if (_zonePlus != null) _zonePlus.gameObject.SetActive(canStaff);
            if (_zoneOpenBtn != null) _zoneOpenBtn.gameObject.SetActive(!open && hasNote && phaseOk && zb != null);

            if (!hasNote)
                _zoneLabel.text = "☢ Zone B — [ล็อก] ต้องวิจัย Note 'tritium'";
            else if (!phaseOk)
                _zoneLabel.text = "☢ Zone B — [ล็อก] ปลดที่ Phase 4 (CORE ≥ 80)";
            else
                _zoneLabel.text = "☢ Zone B (Tritium)";

            if (_zoneInfo != null)
            {
                if (open)
                {
                    float rate = MasteryRegistry.Instance.ZoneBTritiumPerDay();
                    _zoneInfo.text = $"คลัง Tritium {zb.TritiumStock:0.0} · ผลิต {rate:0.0}/วัน (คน ≥ {cfg.zoneBMinStaff}) · รังสี +{cfg.radZoneB:0}/วัน";
                }
                else
                {
                    _zoneInfo.text = $"สร้าง: เหล็ก {cfg.zoneBBuildIron:0} + พลังงาน {cfg.zoneBBuildPower:0}";
                }
            }
        }

        // ═══════════════ BUILD ═══════════════
        // [TH] ประกอบหน้าตาแผงทั้งหมดด้วยโค้ด: ฉากหลัง + กรอบ + หัวเรื่อง + แถวงานทีละแถว
        private void BuildPanel()
        {
            _backdrop = NewUI("Backdrop", transform, CBackdrop);
            Stretch(_backdrop, Vector2.zero, Vector2.one);
            var bdBtn = _backdrop.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Hide);

            _root = NewUI("Panel", _backdrop.transform, CPanel);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 700f); // ★ slice 5/6: + Zone B row (2-line) + Rad Suit row
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = CBorder; outline.effectDistance = new Vector2(2f, -2f);

            _header = MakeText("Header", _root.transform, "", 24, CGold, TextAnchor.UpperLeft);
            Anchor(_header.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -20f), new Vector2(-24f, -70f));

            var close = MakeButton("Close", _root.transform, "✕", CBtnDim, Hide);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-16f, -16f); crt.sizeDelta = new Vector2(48f, 48f);

            var hint = MakeText("Hint", _root.transform, $"กด [{toggleKey}] เพื่อปิด · คลิกนอกกรอบก็ปิด", 15, CMuted, TextAnchor.UpperLeft);
            Anchor(hint.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -74f), new Vector2(-24f, -100f));

            // job rows
            var list = NewUI("List", _root.transform, new Color(0f, 0f, 0f, 0.25f));
            Anchor(list, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 24f), new Vector2(-24f, -104f));
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            foreach (var (job, label) in Jobs) BuildJobRow(list.transform, job, label);
            BuildZoneBRow(list.transform);   // ★ slice 5: Zone B — locked states shown, never hidden
            BuildRadSuitRow(list.transform); // ★ slice 6: craft Rad Suits (labMat 30 · target 5)
        }

        // Rad Suit row: label (state + count) + craft button. Locked state SHOWN with 🔒 (rule #6 spirit).
        private void BuildRadSuitRow(Transform parent)
        {
            var row = NewUI("Row_radsuit", parent, new Color(0.75f, 0.65f, 0.35f, 0.06f));
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 54f; le.preferredHeight = 54f;

            _suitLabel = MakeText("Name", row.transform, "Rad Suit", 18, CText, TextAnchor.MiddleLeft);
            Anchor(_suitLabel.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-220f, 0f));

            _suitCraftBtn = MakeButton("Craft", row.transform, "คราฟต์", new Color(0.30f, 0.27f, 0.14f, 1f), CraftSuit);
            var srt = _suitCraftBtn.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f); srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-12f, 0f); srt.sizeDelta = new Vector2(200f, 44f);
            _suitCraftLabel = _suitCraftBtn.GetComponentInChildren<Text>();
            if (_suitCraftLabel != null) _suitCraftLabel.fontSize = 17;
        }

        private void CraftSuit()
        {
            var rs = RadSuitManager.Instance;
            if (rs == null) return;
            if (!rs.CraftSuit())
                EventManager.Instance.RaiseNotice(
                    $"คราฟต์ไม่ได้ — ต้องมี labMat ≥ {GameConfigSO.Instance.suitCostLabMat} และยังไม่ครบเป้า {GameConfigSO.Instance.suitTargetCount} ชุด");
            Refresh();
        }

        // Zone B row (GDD §22): 2-line row — label + info line; right side is either the "open"
        // button (pay Iron+Power once) or the −/count/+ staffing controls once open.
        private void BuildZoneBRow(Transform parent)
        {
            var row = NewUI("Row_zoneb", parent, new Color(0.35f, 0.75f, 0.55f, 0.06f));
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 74f; le.preferredHeight = 74f;

            _zoneLabel = MakeText("Name", row.transform, "☢ Zone B", 20, CText, TextAnchor.UpperLeft);
            Anchor(_zoneLabel.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-220f, -8f));

            _zoneInfo = MakeText("Info", row.transform, "", 14, CMuted, TextAnchor.LowerLeft);
            Anchor(_zoneInfo.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 6f), new Vector2(-220f, 28f));

            _zoneOpenBtn = MakeButton("Open", row.transform, "เปิด Zone B", new Color(0.16f, 0.32f, 0.24f, 1f), OpenZoneB);
            var ort = _zoneOpenBtn.GetComponent<RectTransform>();
            ort.anchorMin = ort.anchorMax = new Vector2(1f, 0.5f); ort.pivot = new Vector2(1f, 0.5f);
            ort.anchoredPosition = new Vector2(-12f, 0f); ort.sizeDelta = new Vector2(180f, 48f);

            _zoneMinus = MakeButton("Minus", row.transform, "−", CMinus, () => Move(WorkerJobs.ZoneB, -1));
            PlaceRight(_zoneMinus, -140f, 44f);

            _zoneCount = MakeText("Count", row.transform, "0", 22, CGold, TextAnchor.MiddleCenter);
            var crt2 = _zoneCount.GetComponent<RectTransform>();
            crt2.anchorMin = crt2.anchorMax = new Vector2(1f, 0.5f); crt2.pivot = new Vector2(1f, 0.5f);
            crt2.anchoredPosition = new Vector2(-90f, 0f); crt2.sizeDelta = new Vector2(44f, 44f);

            _zonePlus = MakeButton("Plus", row.transform, "+", CPlus, () => Move(WorkerJobs.ZoneB, +1));
            PlaceRight(_zonePlus, -12f, 44f);
        }

        private void BuildJobRow(Transform parent, string job, string label)
        {
            var row = NewUI($"Row_{job}", parent, new Color(1f, 1f, 1f, 0.04f));
            var le = row.AddComponent<LayoutElement>(); le.minHeight = 54f; le.preferredHeight = 54f;

            var name = MakeText("Name", row.transform, label, 20, CText, TextAnchor.MiddleLeft);
            Anchor(name.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-200f, 0f));

            var minus = MakeButton("Minus", row.transform, "−", CMinus, () => Move(job, -1));
            PlaceRight(minus, -140f, 44f);

            var count = MakeText("Count", row.transform, "0", 22, CGold, TextAnchor.MiddleCenter);
            var crt = count.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f); crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-90f, 0f); crt.sizeDelta = new Vector2(44f, 44f);
            _countTexts[job] = count;

            var plus = MakeButton("Plus", row.transform, "+", CPlus, () => Move(job, +1));
            PlaceRight(plus, -12f, 44f);
        }

        private static void PlaceRight(Button b, float x, float size)
        {
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(size, size);
        }

        // ── uGUI helpers (same as ResearchQueuePanel) ──
        private static GameObject NewUI(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            return go;
        }

        private static void Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private Text MakeText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = anchor; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            return t;
        }

        private Button MakeButton(string name, Transform parent, string text, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewUI(name, parent, bg);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = MakeText("Label", go.transform, text, 22, CText, TextAnchor.MiddleCenter);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            return btn;
        }
    }
}
