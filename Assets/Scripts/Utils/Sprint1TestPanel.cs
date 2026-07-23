// Assets/Scripts/Utils/Sprint1TestPanel.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// [TH] หน้าที่: แผงทดสอบระบบ Sprint 1 (คนงาน/กะ/Hope/คลัง) ตอน Play — เครื่องมือ dev เท่านั้น
    /// กด F9 เปิด/ปิด · จัดงานคนงาน + ชุด preset · ปุ่มกดดันคนงาน (หิว/ล้า/รังสี) · ข้ามวัน · ดู Hope breakdown สด
    /// ถูกคอมไพล์ทิ้งใน release build (เหมือน DebugCheatPanel)
    ///
    /// Play-mode test panel for the Sprint 1 systems (Worker/Shift/HopeLedger/Inventory view).
    /// Dev-only — compiled out of release builds (same pattern as DebugCheatPanel).
    ///
    /// - Auto-spawns on Play, and spawns WorkerManager if the scene doesn't have one yet
    ///   (spawning it flips the v6.3 guards: PopulationManager hope + ResourceManager
    ///   per-person consumption defer to the new systems)
    /// - F9 toggles the panel
    /// - Covers: job assignment (+ 3 GDD presets), worker stress tools (starve/fatigue/irradiate),
    ///   hope report/commit, day skip, live hope breakdown + inventory delta view
    /// </summary>
    public class Sprint1TestPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<Sprint1TestPanel>() != null) return;
            var go = new GameObject("[Sprint1TestPanel]");
            go.AddComponent<Sprint1TestPanel>();
            DontDestroyOnLoad(go);
        }

        public KeyCode toggleKey = KeyCode.F9;

        private bool _open;
        private bool _placed;
        private bool _resizing;
        private Vector2 _scroll;
        private Rect _window;
        private GUIStyle _hdr, _hint, _gain, _loss;

        private const float MinWidth = 360f;   // clamp floor only — kept small so it never exceeds tiny screens
        private const float MinHeight = 260f;
        private const float GripSize = 26f;
        private const float FullScreenMargin = 6f; // near-fullscreen inset (leave a sliver so the grip is grabbable)
        private const int FontSize = 18;       // ★ body font (label / button / textfield) — bump to taste
        private const int HeaderSize = 22;     // ★ section headers + window title

        private static readonly string[] AssignableJobs =
            { WorkerJobs.Farm, WorkerJobs.Power, WorkerJobs.Water, WorkerJobs.Mine, WorkerJobs.Lab, WorkerJobs.Cool };

        private void Start()
        {
            // EventManager มาก่อนเสมอ ([DefaultExecutionOrder(-100)]) — แต่กันเคส scene แปลก
            // (memory: auto-spawn + OnEnable แตะ EventManager unguarded เคย crash ใน build)
            if (EventManager.Instance == null) return;

            if (WorkerManager.Instance == null)
            {
                var go = new GameObject("WorkerManager (Sprint1 playtest)");
                go.AddComponent<WorkerManager>();
                Debug.Log("[Sprint1TestPanel] spawned WorkerManager — v6.3 worker/hope systems active");
            }

            // Sprint 2: spawn ResearchLab too so the research queue is drivable from this panel
            if (ResearchLab.Instance == null)
            {
                var go = new GameObject("ResearchLab (Sprint2 playtest)");
                go.AddComponent<ResearchLab>();
                Debug.Log("[Sprint1TestPanel] spawned ResearchLab — v6.3 research queue active (starts ruined)");
            }

            // Sprint 4: spawn CardManager so crisis cards are drivable from this panel
            if (CardManager.Instance == null)
            {
                var go = new GameObject("CardManager (Sprint4 playtest)");
                go.AddComponent<CardManager>();
                Debug.Log("[Sprint1TestPanel] spawned CardManager — v6.3 crisis cards active");
            }

            // Sprint 5: spawn ZoneB / RadSuit / DataRecovery / Bark managers
            if (ZoneBController.Instance == null) new GameObject("ZoneBController (Sprint5)").AddComponent<ZoneBController>();
            if (RadSuitManager.Instance == null) new GameObject("RadSuitManager (Sprint5)").AddComponent<RadSuitManager>();
            if (DataRecovery.Instance == null) new GameObject("DataRecovery (Sprint5)").AddComponent<DataRecovery>();
            if (BarkManager.Instance == null) new GameObject("BarkManager (Sprint5)").AddComponent<BarkManager>();

            // Sprint 6: reactor / storm / sensor / ending / inner voice
            if (ReactorController.Instance == null) new GameObject("ReactorController (Sprint6)").AddComponent<ReactorController>();
            if (StormSystem.Instance == null) new GameObject("StormSystem (Sprint6)").AddComponent<StormSystem>();
            if (SensorArray.Instance == null) new GameObject("SensorArray (Sprint6)").AddComponent<SensorArray>();
            if (EndingSystem.Instance == null) new GameObject("EndingSystem (Sprint6)").AddComponent<EndingSystem>();
            if (InnerVoiceDirector.Instance == null) new GameObject("InnerVoiceDirector (Sprint6)").AddComponent<InnerVoiceDirector>();

            // WorkerManager-driven world avatars + health emoji badges (one sprite per real worker)
            if (FindFirstObjectByType<WorkerAvatarSpawner>() == null)
            {
                var go = new GameObject("WorkerAvatarSpawner (playtest)");
                go.AddComponent<WorkerAvatarSpawner>();
                Debug.Log("[Sprint1TestPanel] spawned WorkerAvatarSpawner — health emoji badges over workers");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _open = !_open;
                if (_open) _placed = false;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!_open)
            {
                GUI.Label(new Rect(14, 34, 500, 30), $"[{toggleKey}] เปิดแผงทดสอบ Sprint 1 (Worker/Hope)", _hint);
                return;
            }

            // ขนาดเริ่มต้นครั้งแรกเท่านั้น — เต็มจอ (เว้นขอบ FullScreenMargin ให้จับมุม ◢ ย่อได้)
            // หลังจากนั้นผู้ใช้ลากปรับเองได้ (มุมจับ ◢ ขวาล่าง)
            if (!_placed)
            {
                _window = new Rect(FullScreenMargin, FullScreenMargin,
                                   Screen.width - FullScreenMargin * 2f,
                                   Screen.height - FullScreenMargin * 2f);
                _placed = true;
            }

            // กันหน้าต่างหลุดจอ/เล็กเกินอ่าน (เช่นย่อหน้าต่าง Game view)
            _window.width = Mathf.Clamp(_window.width, MinWidth, Screen.width);
            _window.height = Mathf.Clamp(_window.height, MinHeight, Screen.height);
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - 60f));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - 40f));

            // Bump the shared IMGUI skin's default control fonts so every plain Label/Button in the
            // window gets the larger size, then restore so nothing else in-game is affected.
            var skin = GUI.skin;
            int pLabel = skin.label.fontSize, pBtn = skin.button.fontSize,
                pWin = skin.window.fontSize, pField = skin.textField.fontSize, pBox = skin.box.fontSize;
            skin.label.fontSize = FontSize;
            skin.button.fontSize = FontSize;
            skin.textField.fontSize = FontSize;
            skin.box.fontSize = FontSize;
            skin.window.fontSize = HeaderSize;

            _window = GUILayout.Window(918274, _window, DrawWindow, "⚙  Sprint 1 — Worker · Shift · HopeLedger · Inventory");

            skin.label.fontSize = pLabel; skin.button.fontSize = pBtn;
            skin.textField.fontSize = pField; skin.box.fontSize = pBox; skin.window.fontSize = pWin;
        }

        private void DrawWindow(int id)
        {
            var wm = WorkerManager.Instance;
            if (wm == null)
            {
                GUILayout.Label("ไม่มี WorkerManager ในซีน (EventManager ยังไม่พร้อม?)");
                GUI.DragWindow(new Rect(0, 0, 10000, 22));
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStatus(wm);
            DrawJobs(wm);
            DrawStress(wm);
            DrawResearch(wm);
            DrawMastery(wm);
            DrawCards(wm);
            DrawZoneB(wm);
            DrawReactor(wm);
            DrawHope(wm);
            DrawInventory(wm);

            GUILayout.Space(6);
            if (GUILayout.Button($"ปิดแผง ({toggleKey})")) _open = false;

            GUILayout.EndScrollView();
            HandleResizeGrip();
            GUI.DragWindow(new Rect(0, 0, 10000, 22)); // ลากย้ายได้เฉพาะแถบหัว — ไม่ชนกับมุมจับ
        }

        /// <summary>มุมจับ ◢ ขวาล่าง — ลากเพื่อปรับขนาดหน้าต่างเอง (พิกัดใน DrawWindow เป็น local)</summary>
        private void HandleResizeGrip()
        {
            var grip = new Rect(_window.width - GripSize, _window.height - GripSize, GripSize, GripSize);
            GUI.Label(grip, "◢");

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (grip.Contains(e.mousePosition)) { _resizing = true; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (_resizing)
                    {
                        _window.width = Mathf.Clamp(_window.width + e.delta.x, MinWidth, Screen.width);
                        _window.height = Mathf.Clamp(_window.height + e.delta.y, MinHeight, Screen.height);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    _resizing = false;
                    break;
            }
        }

        // ── สถานะรวม ─────────────────────────────────────────────
        private void DrawStatus(WorkerManager wm)
        {
            GUILayout.Label("สถานะ Worker (v6.3)", _hdr);
            var gm = GameManager.Instance;
            if (gm != null)
                GUILayout.Label($"วัน {gm.CurrentDay}  ·  เฟสวัน {gm.CurrentDayPhase}");

            GUILayout.Label(
                $"คน {wm.AliveCount}  ·  Healthy {wm.CountByStatus(WorkerStatus.Healthy)}  ·  Tired {wm.CountByStatus(WorkerStatus.Tired)}" +
                $"  ·  Exhausted {wm.ExhaustedCount}  ·  Hungry {wm.HungryCount}  ·  Sick {wm.SickCount}  ·  Dying {wm.DyingCount}");

            // รายคน (ย่อ): job / fatigue / hunger / rad
            foreach (var w in wm.Workers)
            {
                if (!w.alive) continue;
                string flags = (w.resting ? "" : "") + (w.strikeDaysLeft > 0 ? $" ✊{w.strikeDaysLeft}" : "");
                GUILayout.Label(
                    $"  {w.displayName,-8} {w.job,-7} F{w.fatigue,3:0} H{w.hunger,3:0} R{w.radiation,3:0}  {w.status}{flags}",
                    w.status == WorkerStatus.Healthy ? GUI.skin.label : _loss);
            }
        }

        // ── จัดคนลงงาน ────────────────────────────────────────────
        private void DrawJobs(WorkerManager wm)
        {
            GUILayout.Label("จัดคนลงงาน (Planning)", _hdr);

            foreach (var job in AssignableJobs)
            {
                GUILayout.BeginHorizontal();
                int count = wm.GetWorkers(job).Count;
                GUILayout.Label($"{job,-8} {count} คน  (Σeff {wm.SumEfficiency(job):0.00})", GUILayout.Width(260));
                if (GUILayout.Button("+", GUILayout.Width(30))) MoveOne(wm, WorkerJobs.Idle, job);
                if (GUILayout.Button("−", GUILayout.Width(30))) MoveOne(wm, job, WorkerJobs.Idle);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"idle {wm.GetWorkers(WorkerJobs.Idle).Count} คน");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("เร่งเตา 2/2/2/4/4")) Preset(wm, 2, 2, 2, 4, 4, 0);
            if (GUILayout.Button("สร้างเมือง 3/2/2/3/3")) Preset(wm, 3, 2, 2, 3, 3, 0);
            if (GUILayout.Button("สมดุล 3/2/2/4/3")) Preset(wm, 3, 2, 2, 4, 3, 0);
            if (GUILayout.Button("พลาด (ฟาร์ม 1)")) Preset(wm, 1, 2, 2, 6, 0, 3);
            GUILayout.EndHorizontal();
        }

        private static void MoveOne(WorkerManager wm, string from, string to)
        {
            var list = wm.GetWorkers(from);
            if (list.Count > 0) wm.AssignJob(list[0], to);
        }

        private static void Preset(WorkerManager wm, int farm, int power, int water, int mine, int lab, int cool)
        {
            foreach (var w in wm.Workers)
                if (w.alive && w.strikeDaysLeft <= 0) wm.AssignJob(w, WorkerJobs.Idle);
            wm.AssignJobCounts(new Dictionary<string, int>
            {
                { WorkerJobs.Farm, farm }, { WorkerJobs.Power, power }, { WorkerJobs.Water, water },
                { WorkerJobs.Mine, mine }, { WorkerJobs.Lab, lab }, { WorkerJobs.Cool, cool },
            });
        }

        // ── เครื่องมือกดดันคน (ดู threshold/status เดินจริง) ─────────
        private void DrawStress(WorkerManager wm)
        {
            GUILayout.Label("กดดันคน (ทดสอบ status/threshold)", _hdr);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("อาหาร = 0 (อดทั้งเมือง)"))
                EventManager.Instance.RaiseResourceDelta(ResourceType.Food, -99999f);
            if (GUILayout.Button("ความล้า +30 ทุกคน"))
                foreach (var w in wm.Workers) { if (w.alive) w.fatigue = Mathf.Min(100f, w.fatigue + 30f); }
            if (GUILayout.Button("รังสี +30 คนเหมือง"))
                foreach (var w in wm.GetWorkers(WorkerJobs.Mine)) w.radiation = Mathf.Min(100f, w.radiation + 30f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ข้ามวัน (End Day)")) GameManager.Instance?.RequestEndDay();
            if (GUILayout.Button("ข้าม 5 วัน"))
                for (int i = 0; i < 5; i++) GameManager.Instance?.RequestEndDay();
            GUILayout.EndHorizontal();
        }

        // ── Research (Sprint 2 · §19) ────────────────────────────
        private void DrawResearch(WorkerManager wm)
        {
            var lab = ResearchLab.Instance;
            var db = KnowledgeDB.Instance;
            if (lab == null) return;

            GUILayout.Label("Research (§19)", _hdr);
            GUILayout.Label("  " + ResearchQueuePanel.LabStatusLine(lab));
            GUILayout.Label("  " + ResearchQueuePanel.ActiveJobLine(lab));

            // ซ่อมซาก
            if (lab.IsRuined)
            {
                GUILayout.BeginHorizontal();
                if (!lab.RepairPaid && GUILayout.Button("จ่ายเหล็ก 80 เริ่มซ่อม")) lab.StartRepair();
                if (GUILayout.Button("จัด lab 2 คน (ซ่อม)")) MoveToLab(wm, 2);
                if (GUILayout.Button("เดินวันซ่อม")) lab.TickDay();
                GUILayout.EndHorizontal();
            }

            // เบาะแส (soft trigger) — บังคับปลดเพื่อทดสอบ
            GUILayout.Label("  เบาะแส (leads):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ประเมิน soft trigger"))
                lab.Triggers.Evaluate(SoftTriggerWatcher.Snapshot(), db);
            if (GUILayout.Button("บังคับปลด water_analysis")) db.UnlockLead("water_analysis");
            if (GUILayout.Button("ปลดทุก lead")) foreach (var k in KnowledgeDB.LeadMap.Keys) db.UnlockLead(k);
            GUILayout.EndHorizontal();

            // note ที่วิจัยได้
            foreach (var note in db.AllNotes)
            {
                if (db.HasNote(note.noteId)) { GUILayout.Label($"  ✅ {note.title}", _gain); continue; }
                if (!db.IsResearchable(note)) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label("  " + ResearchQueuePanel.NoteOfferLine(note), GUILayout.Width(430));
                if (GUILayout.Button("เริ่มวิจัย", GUILayout.Width(90)))
                    lab.TryStartResearch(note.noteId);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("เดินวันวิจัย (TickDay)")) lab.TickDay();
        }

        private static void MoveToLab(WorkerManager wm, int count)
        {
            int moved = 0;
            foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
            {
                if (moved >= count) break;
                if (wm.AssignJob(w, WorkerJobs.Lab)) moved++;
            }
        }

        // ── Mastery + Codex quiz (Sprint 3 · §21) ────────────────
        private void DrawMastery(WorkerManager wm)
        {
            var cq = CodexQuizManager.Instance;
            var mr = MasteryRegistry.Instance;

            GUILayout.Label("Mastery + Codex (§21)", _hdr);
            GUILayout.Label($"  Codex {cq.UnlockedCodexCount}/{cq.TotalCodex}  ·  Mastery {mr.EarnedCount}  ·  " +
                            (cq.HasNewQuiz ? "มีควิซใหม่ให้ตอบ" : "— ยังไม่มีควิซใหม่"));

            // ★ answer-vs-no-answer proof — these numbers flip only when a quiz is mastered
            GUILayout.Label($"  ★ ZoneB tritium {mr.ZoneBTritiumPerDay():0.0}/วัน  ·  radiation ×{mr.RadiationMult():0.00}  ·  " +
                            $"MedBay heal {mr.MedBayHeal():0}  ·  fuelEff +{mr.FuelEfficiencyBonus():0.00}", _gain);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ใช้ความรู้ครบ (ปลด requiresApplied)")) ApplyAllKnowledge(cq);
            if (GUILayout.Button("รีเซ็ต Mastery (เทส)"))
            {
                MetaProgress.ClearForTest();        // in-memory only — PlayerPrefs kept
                MasteryRegistry.ResetForTest();
                CodexQuizManager.ResetForTest();
            }
            GUILayout.EndHorizontal();

            if (cq.TotalCodex == 0)
            {
                GUILayout.Label("  (ยังไม่มี asset — รันเมนู NuclearReMind ▸ Setup Quiz + Codex)", _loss);
                return;
            }

            foreach (var v in cq.GetAll())
            {
                if (v.quiz == null) continue;
                GUILayout.BeginHorizontal();
                string tag = v.state == QuizState.Earned ? "✅" : v.state == QuizState.Answerable ? "○" : "[ล็อก]";
                var style = v.state == QuizState.Earned ? _gain : v.state == QuizState.Locked ? _loss : GUI.skin.label;
                GUILayout.Label($"  {tag} {v.quiz.topicTitle}", style, GUILayout.Width(300));
                if (v.state == QuizState.Answerable)
                {
                    if (GUILayout.Button("ตอบถูก", GUILayout.Width(78)))
                        cq.Submit(v.quiz.quizId, v.quiz.correctIndex);
                    if (GUILayout.Button("ตอบผิด", GUILayout.Width(78)))
                        cq.Submit(v.quiz.quizId, (v.quiz.correctIndex + 1) % Mathf.Max(1, v.quiz.options.Length));
                }
                GUILayout.EndHorizontal();
            }
        }

        // Satisfy every requiresApplied gate at once (bug #3: tritiumEverProduced is the LATCH)
        private static void ApplyAllKnowledge(CodexQuizManager cq)
        {
            cq.SetApplied(new MasteryAppliedState
            {
                extractorRanDays = 1, coilTypesInstalled = 2, medBayHealedCount = 1,
                riskZoneEntered = true, mutationLabRan = true, co60Ran = true, tritiumFed = true,
                zoneBProducedDays = 2, tritiumEverProduced = true, coreProgress = 100f, reachedEnding = true,
            });
        }

        // ── Crisis Cards (Sprint 4 · §25) ────────────────────────
        private string _lastAfter = "";
        private void DrawCards(WorkerManager wm)
        {
            var cm = CardManager.Instance;
            if (cm == null) return;
            var db = KnowledgeDB.Instance;

            GUILayout.Label("Crisis Cards (§25)", _hdr);

            if (cm.HasPending)
            {
                var card = cm.Pending;
                GUILayout.Label(CrisisCardPanel.Header(card), _loss);
                string dlg = CrisisCardPanel.Dialogue(card);
                if (!string.IsNullOrEmpty(dlg)) GUILayout.Label("  " + dlg.Replace("\n", "\n  "));

                for (int i = 0; card.options != null && i < card.options.Length; i++)
                {
                    var opt = card.options[i];
                    bool locked = CrisisCardPanel.IsRowLocked(opt, db);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(CrisisCardPanel.OptionRow(i, opt, db), locked ? _loss : GUI.skin.label);
                    GUI.enabled = !locked;   // ★ locked option is VISIBLE but not choosable (rule #6)
                    if (GUILayout.Button("เลือก", GUILayout.Width(90)))
                        _lastAfter = cm.ResolveOption(i).afterText;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                return;
            }

            if (!string.IsNullOrEmpty(_lastAfter))
                GUILayout.Label("บทปิด: " + _lastAfter, _gain);

            GUILayout.Label("  จำลองการ์ด (ForcePresent — ข้าม trigger/cooldown):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("เตาร้อน")) cm.ForcePresent(CardIds.Heat);
            if (GUILayout.Button("คนป่วย")) cm.ForcePresent(CardIds.Sick);
            if (GUILayout.Button("เสบียงเน่า")) cm.ForcePresent(CardIds.Spoil);
            if (GUILayout.Button("คนหิว")) cm.ForcePresent(CardIds.Hunger);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("หมดแรง")) cm.ForcePresent(CardIds.Overwork);
            if (GUILayout.Button("Zone B")) cm.ForcePresent(CardIds.ZoneB);
            if (GUILayout.Button("Triage")) cm.ForcePresent(CardIds.Triage);
            if (GUILayout.Button("Decree")) cm.ForcePresent(CardIds.Decree);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("ประเมินจากสถานะจริง (EvaluateDay)"))
            {
                int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 2;
                cm.EvaluateDay(day, CardWorldState.Snapshot());
            }
        }

        // ── ZoneB · RadSuit · DataRecovery · Bark (Sprint 5) ─────
        private string _lastBark = "";
        private void DrawZoneB(WorkerManager wm)
        {
            var zb = ZoneBController.Instance;
            var suits = RadSuitManager.Instance;
            var dr = DataRecovery.Instance;
            var db = KnowledgeDB.Instance;
            if (zb == null) return;

            GUILayout.Label("Zone B · Suits · Records · Bark (§22/§24)", _hdr);

            int zStaff = wm.GetWorkers(WorkerJobs.ZoneB).Count;
            GUILayout.Label($"  Zone B: {(zb.IsOpen ? "เปิด" : "ปิด")}  ·  คน {zStaff}  ·  ชุด {(suits != null ? suits.SuitsMade : 0)}  ·  " +
                            $"tritium {zb.TritiumStock:0.0} (ผลิตล่าสุด {zb.LastProduced:0.0}/วัน · {zb.ProducedDays} วัน)",
                            zStaff < 2 && zb.IsOpen ? _loss : GUI.skin.label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(zb.IsOpen ? "ปิด Zone B" : "เปิด Zone B")) { if (zb.IsOpen) zb.Close(); else zb.Open(); }
            if (GUILayout.Button("จัดคนเข้า Zone B (2)")) MoveTo(wm, WorkerJobs.ZoneB, 2);
            if (GUILayout.Button("คราฟต์ชุด (labMat 30)")) { if (suits != null && !suits.CraftSuit()) _lastBark = "คราฟต์ไม่ได้ (ต้องวิจัย nuclear_medicine / labMat ไม่พอ / ครบ 5)"; }
            GUILayout.EndHorizontal();
            GUILayout.Label("  ★ ไม่มีชุด: rad +15/วัน → พ้น 32 ใน ~2 วัน → ดึงออก → tritium หยุด = Death Spiral");

            // Data Recovery
            if (dr != null)
            {
                GUILayout.Label($"  Records: {dr.RecordsRecovered}/{dr.TotalRecords}  ·  progress {dr.Progress:0}/100");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("เดินวัน DataRecovery")) dr.TickDay();
                if (GUILayout.Button("เดินวัน ZoneB")) zb.TickDay();
                GUILayout.EndHorizontal();
                foreach (var r in dr.Recovered)
                    GUILayout.Label("  " + RecordsPanel.ArchiveRow(r) + (string.IsNullOrEmpty(r.unlocksLead) ? "" : $"  → lead {r.unlocksLead} {(db.HasLead(r.unlocksLead) ? "✓" : "")}"), _gain);
            }

            // Bark
            var bm = BarkManager.Instance;
            if (bm != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("ประเมิน Bark วันนี้"))
                {
                    int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 2;
                    var fired = bm.EvaluateDay(day, BarkWorldState.Snapshot());
                    _lastBark = fired.Count == 0 ? "(เงียบ — ไม่มีเงื่อนไขเข้า)"
                        : string.Join("  ·  ", fired.Select(b => $"{b.speaker}: {b.text}"));
                }
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(_lastBark)) GUILayout.Label("" + _lastBark);
            }
        }

        private static void MoveTo(WorkerManager wm, string job, int count)
        {
            int moved = 0;
            foreach (var w in wm.GetWorkers(WorkerJobs.Idle))
            {
                if (moved >= count) break;
                if (wm.AssignJob(w, job)) moved++;
            }
        }

        // ── Reactor · Storm · Sensor · Ending (Sprint 6) ─────────
        private void DrawReactor(WorkerManager wm)
        {
            var r = ReactorController.Instance;
            var storm = StormSystem.Instance;
            if (r == null) return;

            GUILayout.Label("Reactor · Storm · Ending (§26/§23)", _hdr);
            int phase = PhaseManager.CurrentPhase;
            GUILayout.Label($"  CORE {r.Core:0.0}/100  ·  HEAT {r.Heat:0.0}  ·  {(r.IsBoosting ? "BOOST" : "idle")}  ·  " +
                            $"Phase {phase} ({PhaseManager.PhaseName(phase)})  ·  cool {r.LastCooling:0.0} gain {r.LastGain:0.00}",
                            r.Heat >= 90f ? _loss : GUI.skin.label);
            GUILayout.Label($"  fuel {r.Fuel:0} · coils T{r.ToroidalLv}/P{r.PoloidalLv} · tritium {(ZoneBController.Instance != null ? ZoneBController.Instance.TritiumStock : r.Tritium):0.0}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(r.IsBoosting ? "→ Idle" : "→ BOOST")) r.SetBoosting(!r.IsBoosting);
            if (GUILayout.Button("ติด Toroidal")) { if (!r.InstallToroidal()) _lastBark = "ต้องวิจัย confinement ก่อน / ครบแล้ว"; }
            if (GUILayout.Button("ติด Poloidal")) { if (!r.InstallPoloidal()) _lastBark = "ต้องวิจัย confinement ก่อน / ครบแล้ว"; }
            if (GUILayout.Button($"SCRAM {(r.CanScram ? "" : "(ล็อก)")}")) r.Scram();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("เดินวัน Reactor"))
            {
                float water = ResourceManager.Instance != null ? ResourceManager.Instance.Current.water : 90f;
                int cool = wm.GetWorkers(WorkerJobs.Cool).Count;
                bool st = storm != null && storm.IsStormActive;
                bool sn = SensorArray.Instance != null && SensorArray.Instance.IsActive;
                r.DailyTick(water, cool, st, sn);
            }
            if (storm != null && GUILayout.Button("เดินวัน Storm"))
                storm.TickDay(r.Core, r.IsBoosting, ZoneBController.Instance != null && ZoneBController.Instance.IsOpen);
            GUILayout.EndHorizontal();

            if (storm != null)
            {
                int eta = SensorArray.PredictedDaysUntilStorm(storm);
                bool sensor = SensorArray.Instance != null && SensorArray.Instance.IsActive;
                GUILayout.Label($"  Storm pressure {storm.Pressure:0}/100  ·  {(storm.IsStormActive ? "★ พายุมาแล้ว!" : sensor ? $"Sensor: อีก ~{eta} วัน" : "ไม่มี Sensor (มืด)")}",
                                storm.IsStormActive ? _loss : GUI.skin.label);
            }

            var end = EndingSystem.Instance;
            if (end != null && end.Ended)
                GUILayout.Label($"จบเกม: {end.Result}", _gain);
        }

        // ── Hope ledger + breakdown ──────────────────────────────
        private void DrawHope(WorkerManager wm)
        {
            var ledger = wm.Hope;
            GUILayout.Label("HopeLedger (§18)", _hdr);

            float d = ledger.LastDelta;
            GUILayout.Label($"HOPE {ledger.Current:0}  {(d > 0.01f ? "▲" : d < -0.01f ? "▼" : "—")} {d:+0.#;−0.#;0}" +
                            $"  ·  แนวโน้ม {HopeBreakdownPanel.BuildTrend(ledger.History)}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Report −10 (การ์ดจำลอง)"))
                ledger.Report("card.debug", "เหตุการณ์ทดสอบ", -10f, HopeCategory.Card);
            if (GUILayout.Button("Report +6 (วิจัยจำลอง)"))
                ledger.Report("research.complete", "วิจัยสำเร็จ (ทดสอบ)", 6f, HopeCategory.Research);
            if (GUILayout.Button("สรุปวันทันที (Commit)"))
                wm.CommitDay();
            GUILayout.EndHorizontal();

            GUILayout.Label("ค้างวันนี้ (ยังไม่ apply):");
            foreach (var e in ledger.GetTodayBreakdown())
                GUILayout.Label($"  {(e.value > 0 ? "✓" : "✕")} {e.text}  {e.value:+0.#;−0.#}", e.value > 0 ? _gain : _loss);

            GUILayout.Label("สรุปวันล่าสุด:");
            foreach (var e in ledger.GetCommittedBreakdown())
                GUILayout.Label($"  {(e.value > 0 ? "✓" : "✕")} {e.text}  {e.value:+0.#;−0.#}", e.value > 0 ? _gain : _loss);
        }

        // ── Inventory view (delta/วัน) ────────────────────────────
        private void DrawInventory(WorkerManager wm)
        {
            GUILayout.Label("Inventory — delta/วัน (view layer)", _hdr);

            if (InventoryManager.Instance != null)
            {
                foreach (var s in InventoryManager.Instance.GetSlots(InventoryTab.All))
                    GUILayout.Label("  " + InventoryPanel.FormatSlot(s), s.deltaPerDay >= 0f ? _gain : _loss);
                return;
            }

            // ไม่มี InventoryManager ในซีน → คำนวณจากสูตรเดียวกันตรง ๆ (InventoryDeltaMath)
            var rm = ResourceManager.Instance;
            var cfg = GameConfigSO.Instance;
            if (rm == null) { GUILayout.Label("  (ไม่มี ResourceManager)"); return; }
            var c = rm.Current;
            int pop = wm.AliveCount;

            Row("⚡ Power", c.energy, InventoryDeltaMath.PowerPerDay(cfg, wm.SumEfficiency(WorkerJobs.Power)));
            Row("Water", c.water, InventoryDeltaMath.WaterPerDay(cfg, wm.SumEfficiency(WorkerJobs.Water), pop));
            Row("Food", c.food, InventoryDeltaMath.FoodPerDay(cfg, wm.SumEfficiency(WorkerJobs.Farm), pop));
            Row("⛏ Iron", c.iron, InventoryDeltaMath.IronPerDay(cfg, wm.SumEfficiency(WorkerJobs.Mine)));
            Row("labMat", c.labMat, cfg.labMatPerDay);
        }

        private void Row(string label, float count, float delta) =>
            GUILayout.Label($"  {label}  {count:0}  {delta:+0.#;−0.#}/วัน", delta >= 0f ? _gain : _loss);

        private void EnsureStyles()
        {
            if (_hdr != null) return;
            _hdr = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = HeaderSize };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = FontSize };
            _gain = new GUIStyle(GUI.skin.label) { fontSize = FontSize, normal = { textColor = new Color(0.5f, 0.9f, 0.5f) } };
            _loss = new GUIStyle(GUI.skin.label) { fontSize = FontSize, normal = { textColor = new Color(1f, 0.55f, 0.5f) } };
        }
    }
}
#endif
