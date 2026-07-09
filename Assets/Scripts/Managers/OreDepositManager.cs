using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แหล่งแร่เหล็กบนแมพ (V4 §5 — โซน A ปลอดภัย · โซน B เสี่ยงรังสี/โควตาสูง)
    /// แมพ 43×28 แบ่งเป็น 2 โซนตามคอลัมน์: โซน A = คอลัมน์ 0..28 (29×28, เมือง+CORE TOWER อยู่ฝั่งนี้)
    /// · โซน B = คอลัมน์ 29..42 (14×28, HIGH RADIATION AREA — คั่นด้วยแนว GATE ที่คอลัมน์ 29)
    /// scatter ครั้งเดียวตอนเริ่มเกม (สุ่มตำแหน่งในพื้นที่โซนของตัวเอง) ผ่านท่อเดียวกับ PrePlacedBuilding
    /// → ระบบจ่ายคน/คนเดินไปขุด/ผลิตเรียลไทม์/เซฟ ใช้ของเดิมทั้งหมด (โหนดอยู่ใน BuildingRegistry)
    ///
    /// โควตาขุด/วัน: สุ่มใหม่ทุกเช้า (OnDayStarted) คงที่ตลอดวัน → reconciler ของ ResourceManager ตรงเป๊ะ
    /// ResourceManager อ่าน GetDailyQuota() แบบ read-only query (รูปแบบเดียวกับ GetAssigned)
    ///
    /// โซน B (oreExposurePerWorkerDay > 0) ทุกจบวัน (OnDayProduction — ยิงก่อน OnDayEnded เสมอ
    /// → StoryDirector เห็น exposure วันเดียวกัน):
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

        [Header("Zone layout — แบ่งกริดตามคอลัมน์ (A = ฝั่งปลอดภัย · B = ฝั่งรังสีสูงหลังแนว GATE)")]
        public int zoneAColumns = 29;       // โซน A = คอลัมน์ 0..zoneAColumns-1 · โซน B = ที่เหลือ (29..42 บนกริด 43×28)

        [Header("Scatter — สุ่มตำแหน่งในพื้นที่โซน (จูนได้)")]
        public int zoneACount = 5;
        public int zoneBCount = 3;
        public int minSpacing = 3;          // ระยะห่างขั้นต่ำระหว่างโหนดในชุดเดียวกัน
        public int maxAttemptsPerNode = 200;

        // โควตาขุดวันนี้ต่อโหนด (สุ่มใหม่ทุกเช้า/ตอนวาง/หลังโหลดเซฟ) — เหล็ก + Tritium (โซน B)
        private readonly Dictionary<Vector2Int, float> _quota = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _tritiumQuota = new Dictionary<Vector2Int, float>();
        private System.Random _rng = new System.Random();

        // Q5 เด้งครั้งเดียว — หน่วง 1 เฟรม (_q5Pending) เพื่อให้ HandleSaveLoaded ยกเลิกได้
        // (ตอนโหลดเซฟ WAM restore จะยิง OnWorkerAssignmentChanged ก่อน handler โหลดของเราทำงาน)
        private bool _q5Fired;
        private bool _q5Pending;

        /// <summary>โควตาขุดเหล็กวันนี้ของโหนดที่ cell นี้ (0 ถ้าไม่ใช่โหนด) — ResourceManager/UI อ่าน read-only</summary>
        public float GetDailyQuota(Vector2Int cell) => _quota.TryGetValue(cell, out float q) ? q : 0f;

        /// <summary>โควตา Tritium วันนี้ (0 = โหนดปลอด Tritium เช่นโซน A) — ResourceManager/UI อ่าน read-only</summary>
        public float GetDailyTritiumQuota(Vector2Int cell) => _tritiumQuota.TryGetValue(cell, out float q) ? q : 0f;

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
            EventManager.Instance.OnDayProduction += HandleDayProduction;
            EventManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            EventManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
            EventManager.Instance.OnWorkerAssignmentChanged += HandleWorkerAssignmentChanged;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnDayStarted -= HandleDayStarted;
            EventManager.Instance.OnDayProduction -= HandleDayProduction;
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
            var grid = GridManager.Instance;
            var registry = BuildingRegistry.Instance;
            if (grid == null || registry == null || EventManager.Instance == null) return;
            if (zoneANode == null || zoneBNode == null) return;

            foreach (var kvp in registry.PlacedBuildings)
                if (kvp.Value != null && kvp.Value.isOreNode)
                    return; // มีโหนดอยู่แล้ว

            Func<Vector2Int, bool> isFree = pos =>
            {
                var c = grid.GetCell(pos.x, pos.y);
                return c != null && !c.isOccupied;
            };

            // แบ่งโซนตามคอลัมน์: A = 0..split-1 (ฝั่งเมือง) · B = split..columns-1 (ฝั่งรังสีสูง)
            int split = Mathf.Clamp(zoneAColumns, 1, grid.columns - 1);

            foreach (var pos in OreMath.PickPositionsInRect(zoneACount, 0, split - 1, 0, grid.rows - 1,
                         minSpacing, isFree, _rng, maxAttemptsPerNode))
                PlaceNode(pos, zoneANode);

            foreach (var pos in OreMath.PickPositionsInRect(zoneBCount, split, grid.columns - 1, 0, grid.rows - 1,
                         minSpacing, isFree, _rng, maxAttemptsPerNode))
                PlaceNode(pos, zoneBNode);
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
        //  โควตารายวัน
        // ─────────────────────────────────────────

        private void HandleDayStarted(int day, bool timed) => RollAllQuotas();

        /// <summary>สุ่มโควตาวันนี้ใหม่ทุกโหนด — public ให้เทสต์เรียกตรงได้ (ล้อ ApplyDailyProduction)</summary>
        public void RollAllQuotas()
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null) return;

            foreach (var kvp in registry.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null || !data.isOreNode) continue;
                RollNode(kvp.Key, data);
            }
        }

        // สุ่มโควตาวันนี้ของโหนดเดียว — เหล็กเสมอ · Tritium เฉพาะโหนดที่ตั้งช่วงไว้ (โซน B)
        private void RollNode(Vector2Int pos, BuildingData data)
        {
            _quota[pos] = OreMath.RollQuota(data.oreQuotaMin, data.oreQuotaMax, _rng);
            _tritiumQuota[pos] = OreMath.RollQuota(data.oreTritiumMin, data.oreTritiumMax, _rng);
        }

        private void HandleBuildingPlaced(Cell cell, BuildingData data)
        {
            if (data == null || !data.isOreNode) return;
            RollNode(new Vector2Int(cell.col, cell.row), data);
        }

        private void HandleBuildingRemoved(Vector2Int pos) // กันเหนียว — โหนดทุบไม่ได้อยู่แล้ว
        {
            _quota.Remove(pos);
            _tritiumQuota.Remove(pos);
        }

        // ─────────────────────────────────────────
        //  โซน B: รังสีสะสม + สุ่มป่วย (จบวัน)
        // ─────────────────────────────────────────

        private void HandleDayProduction(int day)
        {
            var registry = BuildingRegistry.Instance;
            var assign = WorkerAssignmentManager.Instance;
            if (registry == null || assign == null) return;

            float exposure = 0f;
            int sick = 0;

            foreach (var kvp in registry.PlacedBuildings)
            {
                var data = kvp.Value;
                if (data == null || !data.isOreNode || data.oreExposurePerWorkerDay <= 0f) continue;

                int workers = assign.GetAssigned(kvp.Key);
                if (workers <= 0) continue;

                exposure += workers * data.oreExposurePerWorkerDay;
                sick += OreMath.SickCount(workers, data.oreSickChancePerWorkerDay, _rng);
            }

            if (exposure > 0f)
                EventManager.Instance.RaiseRadiationExposureDelta(exposure);

            if (sick > 0)
            {
                EventManager.Instance.RaisePopulationSickInjected(sick);
                EventManager.Instance.RaiseNotice($"☢ คนงานเหมืองโซน B ล้มป่วยจากรังสี {sick} คน — แพทย์จะรักษาให้รายวัน");
            }
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
            _quota.Clear();
            _tritiumQuota.Clear();
            ScatterIfNeeded();  // เซฟเก่าไม่มีโหนด → scatter ใหม่ (grid/registry restore แล้ว — เราวิ่งท้ายสุด)
            RollAllQuotas();    // โควตาสุ่มใหม่หลังโหลดเสมอ (สเปก: สุ่มรายวัน)

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
