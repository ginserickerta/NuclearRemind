using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แหล่งแร่เหล็กบนแมพ (V4 §5 — โซน A ปลอดภัย · โซน B เสี่ยงรังสี/โควตาสูง)
    /// แมพ 43×43 แบ่งเป็น 2 โซนแบบกรอบ: โซน A = สี่เหลี่ยมกลาง (29×29, เมือง+CORE TOWER อยู่ตรงกลาง)
    /// · โซน B = กรอบรอบนอกหนา zoneBorderThickness ช่อง (HIGH RADIATION AREA ล้อมรอบทั้ง 4 ด้าน)
    /// scatter สุ่มตำแหน่งในพื้นที่โซนของตัวเอง ผ่านท่อเดียวกับ PrePlacedBuilding (โหนดอยู่ใน BuildingRegistry)
    ///
    /// งานขุดมีเวลา (ไม่ผลิตต่อเนื่องแบบอาคาร): แต่ละโหนดถือแร่ทั้งก้อน (payload สุ่มตอนโผล่)
    ///   • ใส่คนงาน → สะสม work = Σ(คน × dt) ต่อวินาที · ครบ baseMineSeconds → ได้แร่เต็มก้อน โหนดหาย
    ///   • เวลาจริง = baseMineSeconds ÷ จำนวนคน (คนแปรผกผันกับเวลา) · เริ่มอัตโนมัติเมื่อมีคน
    ///   • ขุดอีกต้องรอวันใหม่ (OnDayStarted) — ถอนโหนดเก่าทั้งหมด แล้วสุ่มตำแหน่งใหม่ทั้งชุด
    ///
    /// โซน B (oreExposurePerWorkerDay > 0): รับรังสี + สุ่มป่วย "ตอนขุดเสร็จ" (ตามคนที่จบงาน)
    ///   • รังสีสะสมเมือง +ค่า×คนงาน (OnRadiationExposureDelta) → ดันวิกฤต crisis_radiation_disease (≥60)
    ///   • สุ่มป่วยรายคน (OnPopulationSickInjected) — medic รักษาได้รายวัน
    /// Q5 (ALARA) เด้งครั้งแรกที่จ่ายคนเข้าโซน B (GDD §12 — สอนก่อนเสี่ยงจริง)
    /// </summary>
    // 60: Start หลัง PrePlacedBuilding(0) จอง CORE TOWER/อนุสรณ์แล้ว ·
    // OnSaveLoaded วิ่งหลัง GridManager/BuildingRegistry(0)/WAM(50) restore เสร็จ (ลำดับ subscribe = execution order)
    [DefaultExecutionOrder(60)]
    public class OreDepositManager : MonoBehaviour
    {
        public static OreDepositManager Instance { get; private set; }

        [Header("Node assets (ผูกโดย Setup Ore Deposits)")]
        public BuildingData zoneANode;
        public BuildingData zoneBNode;

        [Header("Zone layout — Zone A สี่เหลี่ยมกลางแมพ · Zone B กรอบรังสีสูงรอบนอก")]
        [Tooltip("ความหนากรอบ Zone B (ช่อง) รอบทั้ง 4 ด้าน — Zone A = ส่วนกลางที่เหลือ (กริด 43×43 · border 7 → Zone A 29×29)")]
        public int zoneBorderThickness = 7;

        [Header("Scatter — สุ่มตำแหน่งในพื้นที่โซน (จูนได้)")]
        public int zoneACount = 5;
        public int zoneBCount = 3;
        public int minSpacing = 3;          // ระยะห่างขั้นต่ำระหว่างโหนดในชุดเดียวกัน
        public int maxAttemptsPerNode = 200;

        [Header("Mining Job — งานขุดมีเวลา (คนงานแปรผกผันกับเวลา)")]
        [Tooltip("เวลาฐาน (วินาที) ต่อการขุดจนหมดโหนดด้วยคนงาน 1 คน — เวลาจริง = base ÷ จำนวนคน")]
        public float baseMineSeconds = 60f;
        [Tooltip("ความเร็วเดินคนงาน (world units/วินาที) — ใช้คำนวณเวลาเดินไปถึงแหล่งแร่ก่อนเริ่มขุด (ให้ตรงกับ WorkerView.speed)")]
        public float workerSpeed = 1.0f;

        // สถานะงานขุดต่อโหนด — payload = แร่ทั้งก้อน (สุ่มตอนโหนดโผล่) · _work = worker-seconds สะสม
        // ขุดครบ (_work ≥ baseMineSeconds) → ได้แร่เต็มก้อน แล้วโหนดหายไป (ขุดใหม่ต้องรอวันใหม่ scatter ใหม่)
        private readonly Dictionary<Vector2Int, float> _ironPayload = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _tritiumPayload = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _work = new Dictionary<Vector2Int, float>();
        // เวลาเดิน: _walkNeed = วินาทีที่คนต้องเดินไปถึงโหนด (ตามระยะจากจุดพัก) · _walkTime = เดินสะสม (รีเซ็ตเมื่อไม่มีคน)
        // งานขุดจะเริ่มสะสมก็ต่อเมื่อ _walkTime ≥ _walkNeed (คนเดินถึงแร่แล้ว)
        private readonly Dictionary<Vector2Int, float> _walkNeed = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _walkTime = new Dictionary<Vector2Int, float>();
        private System.Random _rng = new System.Random();

        // Q5 เด้งครั้งเดียว — หน่วง 1 เฟรม (_q5Pending) เพื่อให้ HandleSaveLoaded ยกเลิกได้
        // (ตอนโหลดเซฟ WAM restore จะยิง OnWorkerAssignmentChanged ก่อน handler โหลดของเราทำงาน)
        private bool _q5Fired;
        private bool _q5Pending;

        /// <summary>แร่เหล็กที่เหลือในโหนดนี้ (0 ถ้าไม่ใช่โหนด) — UI อ่าน read-only</summary>
        public float GetIronPayload(Vector2Int cell) => _ironPayload.TryGetValue(cell, out float q) ? q : 0f;

        /// <summary>ทริเทียมที่เหลือในโหนดนี้ (0 = โซน A) — UI อ่าน read-only</summary>
        public float GetTritiumPayload(Vector2Int cell) => _tritiumPayload.TryGetValue(cell, out float q) ? q : 0f;

        /// <summary>ความคืบหน้าการขุด 0..1 (worker-seconds ÷ baseMineSeconds)</summary>
        public float GetMineProgress01(Vector2Int cell)
            => baseMineSeconds > 0f && _work.TryGetValue(cell, out float w) ? Mathf.Clamp01(w / baseMineSeconds) : 0f;

        /// <summary>เวลาที่เหลือ (วินาที) ถ้าใช้คน workers คน — เวลา = งานที่เหลือ ÷ คน</summary>
        public float GetMineSecondsRemaining(Vector2Int cell, int workers)
        {
            float w = _work.TryGetValue(cell, out float v) ? v : 0f;
            float left = Mathf.Max(0f, baseMineSeconds - w);
            return workers > 0 ? left / workers : left;
        }

        /// <summary>คนงานยังเดินไปไม่ถึงแหล่งแร่ (ยังไม่เริ่มขุด) — UI อ่าน read-only</summary>
        public bool IsWalking(Vector2Int cell)
            => (_walkTime.TryGetValue(cell, out float t) ? t : 0f)
             < (_walkNeed.TryGetValue(cell, out float n) ? n : 0f);

        /// <summary>เวลาที่เหลือก่อนคนเดินถึงแหล่งแร่ (วินาที)</summary>
        public float GetWalkRemaining(Vector2Int cell)
        {
            float need = _walkNeed.TryGetValue(cell, out float n) ? n : 0f;
            float walked = _walkTime.TryGetValue(cell, out float t) ? t : 0f;
            return Mathf.Max(0f, need - walked);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventManager.Instance.OnDayStarted += HandleDayStarted;
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleWorkerAssignmentChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
            EventManager.Instance.OnWorkerAssignmentChanged -= HandleWorkerAssignmentChanged;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
        }

        private void Start()
        {
            ScatterIfNeeded();
        }

        private void Update()
        {
            // Q5 หน่วงมาจากเฟรมก่อน (ดูคอมเมนต์ที่ field) — โหลดเซฟจะยกเลิก pending ไปแล้ว
            if (_q5Pending)
            {
                _q5Pending = false;
                ShowAlaraQuiz();
            }

            TickMining();
        }

        // ─────────────────────────────────────────
        //  งานขุด: คนงานสะสม work ต่อวินาที · ครบ baseMineSeconds → ได้แร่ + โหนดหาย
        // ─────────────────────────────────────────

        private void TickMining()
        {
            // หยุดตามนาฬิกาเกม (โหมดวาง/ทุบ/พอส) — สอดคล้องกับ ResourceManager
            if (TimeManager.Instance != null && !TimeManager.Instance.IsRunning) return;
            AdvanceMining(Time.deltaTime);
        }

        /// <summary>เดินงานขุดไป dt วินาที — แยกจาก Time.deltaTime ให้เทสต์ควบคุมเวลาได้</summary>
        public void AdvanceMining(float dt)
        {
            if (baseMineSeconds <= 0f || dt <= 0f) return;

            var registry = BuildingRegistry.Instance;
            var assign = WorkerAssignmentManager.Instance;
            if (registry == null || assign == null) return;

            List<(Vector2Int pos, BuildingData data, int workers)> completed = null;

            foreach (var kvp in registry.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null || !data.isOreNode) continue;

                int workers = assign.GetAssigned(kvp.Key);
                if (workers <= 0) { _walkTime[kvp.Key] = 0f; continue; } // ไม่มีคน → รีเซ็ตการเดิน · งานไม่เดิน

                // รอคนงานเดินไปถึงแหล่งแร่ก่อน แล้วค่อยเริ่มสะสมงานขุด (เดินแบบ real-time ไม่ขึ้นกับจำนวนคน)
                float need = _walkNeed.TryGetValue(kvp.Key, out float wn) ? wn : 0f;
                float walked = _walkTime.TryGetValue(kvp.Key, out float wt) ? wt : 0f;
                if (walked < need)
                {
                    _walkTime[kvp.Key] = walked + dt;
                    continue; // ยังเดินอยู่ — ยังไม่ขุด
                }

                float done = (_work.TryGetValue(kvp.Key, out float w) ? w : 0f) + workers * dt;
                _work[kvp.Key] = done;

                if (done >= baseMineSeconds)
                    (completed ??= new List<(Vector2Int, BuildingData, int)>()).Add((kvp.Key, data, workers));
            }

            // ลบโหนดหลังวนจบ (RaiseBuildingRemoved แก้ dict registry — ห้ามลบระหว่าง foreach)
            if (completed != null)
                foreach (var c in completed)
                    CompleteMining(c.pos, c.data, c.workers);
        }

        private void CompleteMining(Vector2Int pos, BuildingData data, int workers)
        {
            float iron = GetIronPayload(pos);
            float trit = GetTritiumPayload(pos);

            if (iron > 0f) EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, iron);
            if (trit > 0f) EventManager.Instance.RaiseResourceDelta(ResourceType.Tritium, trit);

            // โซน B: รับรังสี + สุ่มป่วยตอนขุดเสร็จ (แทนโมเดลรายวันเดิม) — ตามคนที่ขุดจบงาน
            if (data.oreExposurePerWorkerDay > 0f)
            {
                float exposure = workers * data.oreExposurePerWorkerDay;
                if (exposure > 0f) EventManager.Instance.RaiseRadiationExposureDelta(exposure);

                int sick = OreMath.SickCount(workers, data.oreSickChancePerWorkerDay, _rng);
                if (sick > 0)
                {
                    EventManager.Instance.RaisePopulationSickInjected(sick);
                    EventManager.Instance.RaiseNotice($"☢ คนงานเหมืองโซน B ล้มป่วยจากรังสี {sick} คน — แพทย์จะรักษาให้รายวัน");
                }
            }

            string got = trit > 0f
                ? $"+{Mathf.RoundToInt(iron)} เหล็ก +{Mathf.RoundToInt(trit)} ทริเทียม"
                : $"+{Mathf.RoundToInt(iron)} เหล็ก";
            EventManager.Instance.RaiseNotice($"⛏ ขุดแร่เสร็จ {got} — แหล่งแร่หมดแล้ว (รอวันใหม่)");

            // เคลียร์สถานะ + ลบโหนด (cascade: grid ปลดล็อก cell · visual ลบสไปรต์ · registry ถอน · WAM คืนคน)
            _ironPayload.Remove(pos);
            _tritiumPayload.Remove(pos);
            _work.Remove(pos);
            _walkNeed.Remove(pos);
            _walkTime.Remove(pos);
            EventManager.Instance.RaiseBuildingRemoved(pos);
        }

        // ─────────────────────────────────────────
        //  Scatter (ล้อ PrePlacedBuilding.Start ทีละโหนด)
        // ─────────────────────────────────────────

        /// <summary>
        /// วางโหนดถ้ายังไม่มีในเกม — เริ่มเกมใหม่/โหลดเซฟเก่าที่ไม่มีโหนด
        /// (เซฟใหม่มีโหนดใน placedBuildings อยู่แล้ว → restore ผ่าน registry ไม่ scatter ซ้ำ)
        /// </summary>
        private void ScatterIfNeeded()
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null) return;

            foreach (var kvp in registry.PlacedBuildings)
                if (kvp.Value != null && kvp.Value.isOreNode)
                    return; // มีโหนดอยู่แล้ว

            ScatterFresh();
        }

        /// <summary>สุ่มวางโหนดแร่ชุดใหม่ (ตำแหน่งสุ่มใหม่) — ผู้เรียกต้องเคลียร์โหนดเก่าก่อนถ้าไม่อยากได้ซ้ำ</summary>
        private void ScatterFresh()
        {
            var grid = GridManager.Instance;
            var registry = BuildingRegistry.Instance;
            if (grid == null || registry == null || EventManager.Instance == null) return;
            if (zoneANode == null || zoneBNode == null) return;

            Func<Vector2Int, bool> isFree = pos =>
            {
                var c = grid.GetCell(pos.x, pos.y);
                return c != null && !c.isOccupied;
            };

            // โมเดล col-split: Zone A = col < cols-border (ฝั่ง SW) · Zone B = col ≥ cols-border (แถบ NE)
            int cols = grid.columns, rows = grid.rows;
            int border = Mathf.Clamp(zoneBorderThickness, 0, Mathf.Min(cols, rows) / 2 - 1);

            // ห้ามแร่เกิดชิดแนวรั้ว (col = fenceCol) — ต้องห่าง ≥ 1 tile ทั้งสองฝั่ง
            // → Zone A ≤ fenceCol-2 · Zone B ≥ fenceCol+1 (เว้น col {fenceCol-1, fenceCol})
            int fenceCol = cols - border;
            Func<Vector2Int, bool> awayFromFence = pos => pos.x <= fenceCol - 2 || pos.x >= fenceCol + 1;

            // Zone A = ฝั่ง SW (กันขอบแมพด้วย border) แต่ห่างรั้ว ≥ 1 tile
            Func<Vector2Int, bool> isFreeZoneA = pos => isFree(pos) && awayFromFence(pos);
            foreach (var pos in OreMath.PickPositionsInRect(zoneACount,
                         border, cols - 1 - border, border, rows - 1 - border,
                         minSpacing, isFreeZoneA, _rng, maxAttemptsPerNode))
                PlaceNode(pos, zoneANode);

            // Zone B = แถบ NE (col ≥ fenceCol) แต่ห่างรั้ว ≥ 1 tile
            Func<Vector2Int, bool> isFreeZoneB = pos =>
                isFree(pos) && !IsoGroundPainter.IsZoneA(pos.x, pos.y, cols, rows, border) && awayFromFence(pos);
            foreach (var pos in OreMath.PickPositionsInRect(zoneBCount, 0, cols - 1, 0, rows - 1,
                         minSpacing, isFreeZoneB, _rng, maxAttemptsPerNode))
                PlaceNode(pos, zoneBNode);
        }

        /// <summary>ถอนโหนดแร่ทั้งหมดออกจากแมพ (ปลดล็อก cell + คืนคน) — ใช้ก่อน scatter รอบใหม่</summary>
        private void RemoveAllNodes()
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null) return;

            var cells = new List<Vector2Int>();
            foreach (var kvp in registry.PlacedBuildings)
                if (kvp.Value != null && kvp.Value.isOreNode) cells.Add(kvp.Key);

            foreach (var pos in cells)
            {
                _ironPayload.Remove(pos);
                _tritiumPayload.Remove(pos);
                _work.Remove(pos);
                _walkNeed.Remove(pos);
                _walkTime.Remove(pos);
                EventManager.Instance.RaiseBuildingRemoved(pos); // cascade ถอน registry/visual/cell/WAM
            }
        }

        // จอง cell แล้วส่งเข้า pipeline ปกติ (registry/visual/จ่ายคน) + ข้ามคิวก่อสร้าง — เหมือน PrePlacedBuilding
        private void PlaceNode(Vector2Int pos, BuildingData data)
        {
            var cell = GridManager.Instance.GetCell(pos.x, pos.y);
            if (cell == null || cell.isOccupied) return;

            cell.isOccupied = true;
            cell.buildingType = data.buildingType;

            EventManager.Instance.RaiseBuildingPlaced(cell, data); // cost 0 → ResourceManager ไม่หักอะไร
            EventManager.Instance.RaiseConstructionCompleteRequested(pos);
        }

        // ─────────────────────────────────────────
        //  วันใหม่: ถอนโหนดเก่าทั้งหมด แล้วสุ่มตำแหน่งใหม่ (สเปก: ขุดเสร็จแร่หาย — ขุดอีกต้องรอวันใหม่)
        // ─────────────────────────────────────────

        private void HandleDayStarted(int day, bool timed) => RescatterAll();

        /// <summary>ถอนโหนดเดิมทั้งหมด + สุ่มโหนดใหม่ทั้งชุด (ตำแหน่งใหม่) — public ให้เทสต์เรียกตรงได้</summary>
        public void RescatterAll()
        {
            RemoveAllNodes();
            ScatterFresh();
        }

        /// <summary>สุ่มแร่ทั้งก้อนของโหนดเดียว + รีเซ็ตความคืบหน้า/เวลาเดิน — เหล็กเสมอ · Tritium เฉพาะโซน B</summary>
        private void RollNode(Vector2Int pos, BuildingData data)
        {
            _ironPayload[pos] = OreMath.RollQuota(data.oreQuotaMin, data.oreQuotaMax, _rng);
            _tritiumPayload[pos] = OreMath.RollQuota(data.oreTritiumMin, data.oreTritiumMax, _rng);
            _work[pos] = 0f;
            _walkNeed[pos] = ComputeWalkSeconds(pos);
            _walkTime[pos] = 0f;
        }

        // เวลาเดินไปถึงโหนด = ระยะจากจุดพัก idle (กลางกริด ~CORE TOWER) ถึงโหนด ÷ ความเร็วเดิน
        // grid ยังไม่พร้อม/ความเร็ว ≤ 0 → 0 (เริ่มขุดทันที) เพื่อไม่ให้ค้างในเทสต์/ระหว่าง init
        private float ComputeWalkSeconds(Vector2Int pos)
        {
            var grid = GridManager.Instance;
            if (grid == null || workerSpeed <= 0f) return 0f;
            Vector3 from = grid.IsoToWorldF((grid.columns - 1) * 0.5f, (grid.rows - 1) * 0.5f);
            Vector3 to = grid.IsoToWorld(pos.x, pos.y);
            return Vector3.Distance(from, to) / workerSpeed;
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (data == null || !data.isOreNode) return;
            RollNode(new Vector2Int(cell.col, cell.row), data);
        }

        private void HandleBuildingRemoved(Vector2Int pos)
        {
            _ironPayload.Remove(pos);
            _tritiumPayload.Remove(pos);
            _work.Remove(pos);
            _walkNeed.Remove(pos);
            _walkTime.Remove(pos);
        }

        // ─────────────────────────────────────────
        //  Q5 (ALARA) — ครั้งแรกที่ส่งคนเข้าโซน B
        // ─────────────────────────────────────────

        private void HandleWorkerAssignmentChanged(Vector2Int cell, int count)
        {
            if (_q5Fired || count <= 0) return;

            var registry = BuildingRegistry.Instance;
            if (registry == null ||
                !registry.PlacedBuildings.TryGetValue(cell, out var data) ||
                data == null || !data.isOreNode || data.oreExposurePerWorkerDay <= 0f)
                return;

            _q5Fired = true;
            _q5Pending = true; // เด้งจริงเฟรมถัดไปใน Update (โหลดเซฟยกเลิกได้)
        }

        private void ShowAlaraQuiz()
        {
            if (QuizManager.Instance == null || QuizManager.Instance.AlreadyAnswered("Q5")) return;

            // primer 1 บรรทัดแทน info card (คำถาม Q5 อ้าง "ที่เพิ่งอ่าน")
            EventManager.Instance.RaiseNotice("VESTA: หลัก ALARA — รับรังสีให้น้อยที่สุดเท่าที่ทำได้ (เวลา·ระยะห่าง·กำบัง)");
            QuizManager.Instance.TriggerByIds("Q5"); // precedent: DilemmaManager/DecreeManager เรียกตรง
        }

        // ─────────────────────────────────────────
        //  Save/Load — ไม่มี field ใหม่ใน SaveData
        //  (ตำแหน่ง+ชนิดโหนด round-trip ผ่าน placedBuildings/buildingTypes ของเดิม)
        // ─────────────────────────────────────────

        private void HandleSaveLoaded(SaveData save)
        {
            _ironPayload.Clear();
            _tritiumPayload.Clear();
            _work.Clear();
            _walkNeed.Clear();
            _walkTime.Clear();
            ScatterIfNeeded();  // เซฟเก่าไม่มีโหนด → scatter ใหม่ (grid/registry restore แล้ว — เราวิ่งท้ายสุด)

            // โหนดที่ restore จากเซฟไม่มี payload/ความคืบหน้า (ไม่ได้เซฟ) → สุ่มแร่ใหม่ + รีเซ็ตความคืบหน้าทุกโหนด
            var reg0 = BuildingRegistry.Instance;
            if (reg0 != null)
                foreach (var kvp in reg0.PlacedBuildings)
                    if (kvp.Value != null && kvp.Value.isOreNode && !_ironPayload.ContainsKey(kvp.Key))
                        RollNode(kvp.Key, kvp.Value);

            // re-latch Q5: เซฟที่มีคนประจำโซน B อยู่แล้ว = เคยผ่านจังหวะสอนไปแล้ว → ไม่เด้งซ้ำ
            _q5Pending = false; // ยกเลิก pending ที่เกิดจาก event ระหว่าง WAM restore
            _q5Fired = false;
            var registry = BuildingRegistry.Instance;
            var assign = WorkerAssignmentManager.Instance;
            if (registry == null || assign == null) return;

            foreach (var kvp in assign.Assignments)
            {
                if (kvp.Value <= 0) continue;
                if (registry.PlacedBuildings.TryGetValue(kvp.Key, out var data) &&
                    data != null && data.isOreNode && data.oreExposurePerWorkerDay > 0f)
                {
                    _q5Fired = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// สูตร pure ของแหล่งแร่ — EditMode ตรวจได้ · ต่างจาก CrisisEffectMath (no-RNG โดยตั้งใจ):
    /// งานนี้ "สุ่ม" คือสเปก จึงรับ System.Random จาก caller (เกมสุ่มจริง / เทสต์ seed ได้)
    /// </summary>
    public static class OreMath
    {
        /// <summary>โควตา/วัน ∈ [min..max] ปัดจำนวนเต็ม (UI อ่านง่าย) · max ≤ min → คืน min (≥0)</summary>
        public static float RollQuota(float min, float max, System.Random rng)
        {
            min = Mathf.Max(0f, min);
            if (max <= min) return Mathf.Round(min);
            return Mathf.Round(min + (float)rng.NextDouble() * (max - min));
        }

        /// <summary>จำนวนคนป่วย = Bernoulli(chance) ต่อคน · chance ≤ 0 → 0 · ≥ 1 → ป่วยทุกคน</summary>
        public static int SickCount(int workers, float chance, System.Random rng)
        {
            if (workers <= 0 || chance <= 0f) return 0;
            if (chance >= 1f) return workers;

            int sick = 0;
            for (int i = 0; i < workers; i++)
                if (rng.NextDouble() < chance) sick++;
            return sick;
        }

        /// <summary>
        /// เลือกตำแหน่ง count จุดในกรอบสี่เหลี่ยม [xMin..xMax]×[yMin..yMax] (พื้นที่โซน A/B)
        /// ห่างกันเอง ≥ spacing (Chebyshev) · isFree(cell) ต้องจริง · rejection sampling (maxAttempts/จุด)
        /// พื้นที่ไม่พอ → คืนน้อยกว่า count (ไม่ค้าง) · pure — เทสต์ยัด isFree ปลอมได้
        /// </summary>
        public static List<Vector2Int> PickPositionsInRect(int count, int xMin, int xMax, int yMin, int yMax,
            int spacing, Func<Vector2Int, bool> isFree, System.Random rng, int maxAttempts = 200)
        {
            var picked = new List<Vector2Int>();
            if (xMax < xMin || yMax < yMin) return picked;

            for (int n = 0; n < count; n++)
            {
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var pos = new Vector2Int(rng.Next(xMin, xMax + 1), rng.Next(yMin, yMax + 1));
                    if (!isFree(pos)) continue;

                    bool tooClose = false;
                    foreach (var p in picked)
                    {
                        if (Mathf.Max(Mathf.Abs(p.x - pos.x), Mathf.Abs(p.y - pos.y)) < spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    picked.Add(pos);
                    break;
                }
            }

            return picked;
        }
    }
}
