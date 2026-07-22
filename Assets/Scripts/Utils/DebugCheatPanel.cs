// Assets/Scripts/Utils/DebugCheatPanel.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แผงโกง/บายพาสสำหรับ "ทดสอบเกม" เท่านั้น (คอมไพล์ทิ้งอัตโนมัติใน release build)
    ///
    /// - Spawn ตัวเองอัตโนมัติทุกครั้งที่กด Play — ไม่ต้องวางในซีน ไม่ต้องผูก UI ใด ๆ
    /// - กด F10 เพื่อเปิด/ปิดแผง (IMGUI overlay มุมซ้าย)
    /// - สั่งงานผ่าน event/public API ที่มีอยู่แล้ว (ResourceDelta, RequestEndDay, Overclock ฯลฯ)
    ///   + hook debug ที่ทำเครื่องหมายไว้ใน CoreTowerManager/PopulationManager
    ///
    /// ครอบคลุม: ข้ามวัน · เร่ง/หยุดเวลา · เติม/ลดทรัพยากรทุกชนิด · คุม CORE%/HEAT/เชื้อเพลิง/Coils/SCRAM
    ///           · ประชากร/Hope/ความรู้ · บังคับจบเกม (ชนะ/หลอมละลาย) · รีสตาร์ท
    /// </summary>
    public class DebugCheatPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<DebugCheatPanel>() != null) return;
            var go = new GameObject("[DebugCheatPanel]");
            go.AddComponent<DebugCheatPanel>();
            DontDestroyOnLoad(go);
        }

        [Tooltip("ปุ่มเปิด/ปิดแผง")]
        public KeyCode toggleKey = KeyCode.F10;

        private bool _open;
        private bool _placed;
        private Vector2 _scroll;
        private Rect _window;
        private GUIStyle _hdr, _hint;
        private GUISkin _skin;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _open = !_open;
                if (_open) _placed = false; // เปิดใหม่ทุกครั้ง → จัดกลางจอ
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!_open)
            {
                GUI.Label(new Rect(14, 10, 500, 30), $"[{toggleKey}] เปิดแผงทดสอบ (Debug)", _hint);
                return;
            }

            // ขนาดใหญ่ + จัดกลางจอ (คำนวณเมื่อเพิ่งเปิด — หลังจากนั้นลากย้ายได้)
            float w = Mathf.Min(Screen.width - 40, 900f);
            float h = Mathf.Min(Screen.height - 40, 1200f);
            if (!_placed)
            {
                _window = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
                _placed = true;
            }
            _window.width = w;
            _window.height = h;

            var prevSkin = GUI.skin;
            GUI.skin = _skin;
            _window = GUILayout.Window(918273, _window, DrawWindow, "แผงทดสอบเกม (Debug / Bypass)");
            GUI.skin = prevSkin;
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStatus();
            DrawTimeSection();
            DrawResourceSection();
            DrawCoreSection();
            DrawPopulationSection();
            DrawSicknessSection();
            DrawEndingSection();

            GUILayout.Space(6);
            if (GUILayout.Button("ปิดแผง (F10)")) _open = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        // ───────────────────────── สถานะปัจจุบัน ─────────────────────────
        private void DrawStatus()
        {
            var gm = GameManager.Instance;
            var rm = ResourceManager.Instance;
            var ct = CoreTowerManager.Instance;
            var pop = PopulationManager.Instance;

            GUILayout.Label("สถานะปัจจุบัน", _hdr);
            if (gm != null)
                GUILayout.Label($"วัน {gm.CurrentDay}/{GameManager.MaxDay}  ·  เฟส {gm.CurrentDayPhase}  ·  {gm.CurrentState}  ·  x{gm.GameSpeed:0.#}");
            if (ct != null)
            {
                var t = ct.Current;
                string lockTxt = t.isUnlocked ? "" : " (ยังล็อก)";
                GUILayout.Label($"CORE {t.corePercent:0.#}%  ·  HEAT {t.coreHeat:0.#}  ·  โหมด {t.overclockMode}{lockTxt}");
            }
            if (pop != null)
            {
                var p = pop.Current;
                GUILayout.Label($"ประชากร {p.total} (W{p.workers}/E{p.engineers}/M{p.medics}/F{p.farmers})  ·  Hope {p.hope:0}");
            }
            if (rm != null)
            {
                var c = rm.Current;
                GUILayout.Label($"E{c.energy:0} W{c.water:0} F{c.food:0} Fe{c.iron:0} D{c.deuterium:0} T{c.tritium:0} K{c.knowledge:0}");
            }
        }

        // ───────────────────────── เวลา / วัน ─────────────────────────
        private void DrawTimeSection()
        {
            GUILayout.Label("เวลา / วัน", _hdr);
            var gm = GameManager.Instance;
            if (gm == null) { GUILayout.Label("(ยังไม่มี GameManager)"); return; }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⏭ ข้ามวัน")) gm.RequestEndDay();
            if (GUILayout.Button("⏭ ข้าม 5 วัน"))
                for (int i = 0; i < 5; i++) gm.RequestEndDay();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⏸ หยุด")) Raise(e => e.RaiseSpeedChangeRequested(0f));
            if (GUILayout.Button("▶ 1x")) Raise(e => e.RaiseSpeedChangeRequested(1f));
            if (GUILayout.Button("⏩ 2x")) Raise(e => e.RaiseSpeedChangeRequested(2f));
            if (GUILayout.Button("⏩⏩ 4x")) Raise(e => e.RaiseSpeedChangeRequested(4f));
            GUILayout.EndHorizontal();
        }

        // ───────────────────────── ทรัพยากร ─────────────────────────
        private void DrawResourceSection()
        {
            GUILayout.Label("ทรัพยากร (−/+)", _hdr);
            ResRow("พลังงาน (E)", ResourceType.Energy);
            ResRow("น้ำ (W)", ResourceType.Water);
            ResRow("อาหาร (F)", ResourceType.Food);
            ResRow("เหล็ก (Fe)", ResourceType.Iron);
            ResRow("ดิวเทอเรียม (D)", ResourceType.Deuterium);
            ResRow("ทริเทียม (T)", ResourceType.Tritium);

            GUILayout.BeginHorizontal();
            GUILayout.Label("ความรู้ (K)", GUILayout.Width(220));
            if (GUILayout.Button("−10")) Delta(ResourceType.Knowledge, -10);
            if (GUILayout.Button("+10")) Delta(ResourceType.Knowledge, 10);
            if (GUILayout.Button("100")) Delta(ResourceType.Knowledge, 100); // clamp ที่ 100 อยู่แล้ว
            GUILayout.EndHorizontal();

            if (GUILayout.Button("เติมทุกอย่าง +1000 (ยกเว้นอาหารเต็ม 500)"))
            {
                Delta(ResourceType.Energy, 1000);
                Delta(ResourceType.Water, 1000);
                Delta(ResourceType.Food, 1000);
                Delta(ResourceType.Iron, 1000);
                Delta(ResourceType.Deuterium, 1000);
                Delta(ResourceType.Tritium, 1000);
                Delta(ResourceType.Knowledge, 100);
            }
        }

        private void ResRow(string label, ResourceType type)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(220));
            if (GUILayout.Button("−100")) Delta(type, -100);
            if (GUILayout.Button("+100")) Delta(type, 100);
            if (GUILayout.Button("+1000")) Delta(type, 1000);
            GUILayout.EndHorizontal();
        }

        // ───────────────────────── CORE TOWER ─────────────────────────
        private void DrawCoreSection()
        {
            GUILayout.Label("CORE TOWER", _hdr);
            var ct = CoreTowerManager.Instance;
            if (ct == null) { GUILayout.Label("(ยังไม่มี CoreTowerManager)"); return; }

            if (GUILayout.Button("[ปลด] ปลดล็อกเตาทันที (CORE 30%)")) ct.DebugUnlockNow();

            GUILayout.Label("ตั้ง CORE%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−10")) ct.DebugSetCore(ct.Current.corePercent - 10f);
            if (GUILayout.Button("+10")) ct.DebugSetCore(ct.Current.corePercent + 10f);
            if (GUILayout.Button("79")) ct.DebugSetCore(79f);
            if (GUILayout.Button("99")) ct.DebugSetCore(99f);
            GUILayout.EndHorizontal();

            GUILayout.Label("ตั้ง HEAT");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0")) ct.DebugSetHeat(0f);
            if (GUILayout.Button("+20")) ct.DebugSetHeat(ct.Current.coreHeat + 20f);
            if (GUILayout.Button("79")) ct.DebugSetHeat(79f);
            if (GUILayout.Button("99")) ct.DebugSetHeat(99f);
            GUILayout.EndHorizontal();

            GUILayout.Label("โหมดเร่งเครื่อง (Overclock)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle")) Raise(e => e.RaiseOverclockModeRequested(0));
            if (GUILayout.Button("Normal")) Raise(e => e.RaiseOverclockModeRequested(1));
            if (GUILayout.Button("Boost")) Raise(e => e.RaiseOverclockModeRequested(2));
            if (GUILayout.Button("OverDr")) Raise(e => e.RaiseOverclockModeRequested(3));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SCRAM")) Raise(e => e.RaiseScramRequested());
            if (GUILayout.Button("Toroidal +1")) Raise(e => e.RaiseUpgradeToroidalRequested());
            if (GUILayout.Button("Poloidal")) Raise(e => e.RaiseInstallPoloidalRequested());
            GUILayout.EndHorizontal();
        }

        // ───────────────────────── ประชากร ─────────────────────────
        private void DrawPopulationSection()
        {
            GUILayout.Label("ประชากร / ขวัญ", _hdr);
            var pop = PopulationManager.Instance;
            if (pop == null) { GUILayout.Label("(ยังไม่มี PopulationManager)"); return; }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+5 Worker")) pop.DebugAddPopulation(WorkerClass.Worker, 5);
            if (GUILayout.Button("+2 วิศวกร")) pop.DebugAddPopulation(WorkerClass.Engineer, 2);
            if (GUILayout.Button("+1 แพทย์")) pop.DebugAddPopulation(WorkerClass.Medic, 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hope 100")) pop.DebugSetHope(100f);
            if (GUILayout.Button("Hope 50")) pop.DebugSetHope(50f);
            if (GUILayout.Button("Hope 1")) pop.DebugSetHope(1f);
            if (GUILayout.Button("ปลดฝึกทุกคลาส")) pop.DebugUnlockAllTraining();
            GUILayout.EndHorizontal();
        }

        // ───────────────────────── สถานะป่วย / รังสี ─────────────────────────
        // Bypass for the radiation-status table (CONFIG: rad > sickThreshold = Sick 0% eff ·
        // rad > dyingThreshold = Dying + 20%/day death roll). Numbers come from GameConfigSO,
        // never hardcoded — the buttons push a hair past each threshold.
        private void DrawSicknessSection()
        {
            GUILayout.Label("สถานะป่วย / รังสี (Bypass)", _hdr);
            var wm = WorkerManager.Instance;
            var cfg = GameConfigSO.Instance;
            if (wm == null || cfg == null) { GUILayout.Label("(ยังไม่มี WorkerManager v6.3 — เข้า Gamescene ก่อน)"); return; }

            float sickRad = cfg.sickThreshold + 5f;   // เกณฑ์ +5 ให้เกินชัวร์
            float dyingRad = cfg.dyingThreshold + 5f;
            GUILayout.Label($"ตอนนี้: ☣ ป่วย {wm.SickCount} · ☠ ใกล้ตาย {wm.DyingCount}   (เกณฑ์: รังสี >{cfg.sickThreshold:0} ป่วย · >{cfg.dyingThreshold:0} ใกล้ตาย)");

            GUILayout.Label($"ยิงรังสี {sickRad:0} → ป่วยทันที (เลือกคนโดสต่ำสุดก่อน)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 คน")) wm.DebugSetRadiation(1, sickRad);
            if (GUILayout.Button("3 คน (การ์ด sick)")) wm.DebugSetRadiation(3, sickRad);
            if (GUILayout.Button("5 คน (การ์ด triage)")) wm.DebugSetRadiation(5, sickRad);
            GUILayout.EndHorizontal();

            GUILayout.Label($"ยิงรังสี {dyingRad:0} → ใกล้ตายทันที (เสี่ยงตาย {cfg.deathChance:P0}/วัน)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 คน")) wm.DebugSetRadiation(1, dyingRad);
            if (GUILayout.Button("3 คน")) wm.DebugSetRadiation(3, dyingRad);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("รักษาทุกคน (รังสี/หิว/ล้า → 0)")) wm.DebugHealAll();
            GUILayout.Label("การ์ดวิกฤตประเมินตอนจบวัน — ยิงรังสีแล้วกด '⏭ ข้ามวัน' เพื่อดูการ์ด sick/triage");
        }

        // ───────────────────────── บังคับจบเกม ─────────────────────────
        private void DrawEndingSection()
        {
            GUILayout.Label("บังคับจบเกม / รีเซ็ต", _hdr);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ชนะ (True)")) Raise(e => e.RaiseTowerComplete());
            if (GUILayout.Button("☢ หลอมละลาย")) Raise(e => e.RaiseGameOver(GameEndType.Meltdown));
            GUILayout.EndHorizontal();

            if (GameManager.Instance != null && GUILayout.Button("เริ่มเกมใหม่ (Restart)"))
                GameManager.Instance.Restart();
        }

        // ───────────────────────── helpers ─────────────────────────
        private static void Delta(ResourceType type, float amount) =>
            EventManager.Instance?.RaiseResourceDelta(type, amount);

        private static void Raise(System.Action<EventManager> act)
        {
            if (EventManager.Instance != null) act(EventManager.Instance);
        }

        private void EnsureStyles()
        {
            if (_skin == null)
            {
                // สกินขยายเฉพาะแผงนี้ (clone จาก skin ปัจจุบัน — ไม่กระทบ IMGUI อื่น)
                _skin = Instantiate(GUI.skin);
                _skin.button.fontSize = 22;
                _skin.button.fixedHeight = 46;
                _skin.button.margin = new RectOffset(4, 4, 4, 4);
                _skin.label.fontSize = 21;
                _skin.window.fontSize = 26;
                _skin.window.fontStyle = FontStyle.Bold;
                _skin.window.padding.top = 38;
                _skin.horizontalSlider.fixedHeight = 0;
            }
            if (_hdr == null)
            {
                _hdr = new GUIStyle(_skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.85f, 0.3f) },
                };
                _hdr.margin.top = 12;
            }
            if (_hint == null)
            {
                _hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.7f) },
                };
            }
        }
    }
}
#endif
