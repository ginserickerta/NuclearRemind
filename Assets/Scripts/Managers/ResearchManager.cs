using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// โครงการวิจัยของห้องวิจัย (ResearchLab_Spec §3) — 3 โครงการเท่านั้น ไม่ใช่ tech tree:
    ///   1. seeds      — เมล็ดพันธุ์ฉายรังสี: Iron 250 + มีวิศวกร ≥3 คน → ผลผลิตฟาร์ม +100% ถาวร
    ///                   (ผูกวิกฤต 3 ทางเลือก A — ผ่าน CrisisEffectManager.FoodYieldMultiplier ตัวเดียวกัน)
    ///   2. isotope    — ยาไอโซโทปการแพทย์: Iron 200 + ฟลักซ์นิวตรอนจากเตา (ตั้งเตาโหมด Idle 1 วัน)
    ///                   → รักษาคนป่วยสูงสุด 15 คน (ผูกวิกฤต 2 ทางเลือก B)
    ///   3. core_tower — ปลดล็อก CORE TOWER: Iron 200 + Energy 200 — เงื่อนไข "วิจัยก่อนเดินเตา"
    ///                   (GDD §6 แถว CORE TOWER: "Iron 200 + Energy 200 + วิจัย" · เตาพร้อมใช้เฟส 3)
    ///
    /// ทุกโครงการต้องมีห้องวิจัยที่มีวิศวกรประจำครบ (workerRequired) จึงกด "วิจัย" ได้
    /// สถานะลง SaveData (research*) — SaveManager เขียน / ที่นี่ restore ใน HandleSaveLoaded
    /// cross-manager: สั่งงานผ่าน EventManager เท่านั้น · อ่านสถานะ manager อื่นแบบ read-only query
    /// </summary>
    public class ResearchManager : MonoBehaviour
    {
        public static ResearchManager Instance { get; private set; }

        public const string ProjectSeeds = "seeds";
        public const string ProjectIsotope = "isotope";
        public const string ProjectCoreTower = "core_tower";

        [Header("1) เมล็ดพันธุ์ฉายรังสี (สเปก: Iron 250 + วิศวกร 3 คนคุมแล็บ · ผลผลิต +100% ถาวร)")]
        public int seedsIronCost = 250;
        public int seedsEngineerCrew = 3;      // ต้องมีวิศวกรในเมือง ≥ เท่านี้ (ตีความ "3 คนคุมแล็บ")
        public float seedsFoodYieldBonus = 1f; // +100% → CrisisEffectManager.FoodYieldMultiplier += ค่านี้
        public int seedsUnlockPhase = 2;

        [Header("2) ยาไอโซโทปการแพทย์ (สเปก: วัสดุแล็บ 200 + เตา Idle 1 วัน · รักษา ≤15 คน)")]
        public int isotopeIronCost = 200;      // "วัสดุแล็บ 200" — ใช้ Iron แทน (ไม่มีทรัพยากรวัสดุแล็บแยก)
        public int isotopeCureCount = 15;

        [Header("3) ปลดล็อก CORE TOWER (สเปก: Iron 200 + Energy 200 · พร้อมใช้เฟส 3)")]
        public int coreIronCost = 200;
        public int coreEnergyCost = 200;
        public int coreUnlockPhase = 2; // วิจัยได้ตั้งแต่เฟส 2 → เตาปลดจริงเมื่อถึง Day 11 (เฟส 3) ตาม CoreTowerManager

        // สถานะ (ลงเซฟผ่าน SaveData.research*)
        public bool SeedsDone { get; private set; }
        public bool IsotopeDone { get; private set; }
        public bool IsotopePending { get; private set; } // จ่ายแล้ว รอวันที่เตาโหมด Idle
        public bool CoreUnlockDone { get; private set; }

        // สร้างตัวเองอัตโนมัติหลังโหลดซีน (precedent: CoreTowerPanelUI.AutoSpawn) — ไม่ต้อง wire ซีน
        // ★ AfterSceneLoad ยิงครั้งเดียวที่ซีนแรกของแอป (build = MainMenu) — ตัวที่ spawn ในเมนูถูกทำลาย
        //   ตอน LoadScene(Gamescene) → ต้อง spawn ซ้ำทุก sceneLoaded (ดูคำอธิบายเต็มที่ CoreTowerPanelUI)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnHook()
        {
            AutoSpawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => AutoSpawn();

        // ★ WebGL build: webGLExceptionSupport ตั้งจับเฉพาะ throw ตรง ๆ — exception จาก runtime เอง (เช่น
        //   NullReferenceException) จะทำให้เงียบสนิท ไม่มี error ขึ้น console (ดูรายละเอียดที่ CoreTowerPanelUI)
        private static void AutoSpawn()
        {
            try { AutoSpawnUnsafe(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResearchManager] AutoSpawn ล้มเหลว — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void AutoSpawnUnsafe()
        {
            if (FindFirstObjectByType<ResearchManager>() != null) return;
            new GameObject("ResearchManager (auto)").AddComponent<ResearchManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // เชื่อม hook วิจัยของระบบไอเทม (§13): ไอเทม requiresResearch คราฟต์ได้เมื่อวิจัยที่เกี่ยวเสร็จ
            // เมล็ดพันธุ์/ยาไอโซโทป ผูกโครงการตัวเอง · ไอเทมอื่น (เช่น rad_gear — สเปก §6: ไม่นับเป็น
            // โครงการที่ 4) = ไม่บล็อก คงพฤติกรรมเดิม
            InventoryManager.ResearchUnlockedQuery = itemId => itemId switch
            {
                "irradiated_seeds" => SeedsDone,
                "medical_isotope" => IsotopeDone,
                _ => true,
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) InventoryManager.ResearchUnlockedQuery = null; // คืน stub (คราฟต์ได้หมด)
        }

        private bool _subscribed;

        // auto-spawn AfterSceneLoad → OnEnable อาจรันตอน EventManager.Instance ยัง null (build) → guard กัน NullRef ที่ทำให้ AutoSpawn ล้ม
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe(); // retry หลัง Awake ทุกตัว (EventManager พร้อมแน่)

        private void TrySubscribe()
        {
            if (_subscribed || EventManager.Instance == null) return;
            EventManager.Instance.OnResearchRequested += HandleResearchRequested;
            EventManager.Instance.OnDayEnded += HandleDayEnded;
            EventManager.Instance.OnSaveLoaded += HandleSaveLoaded;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed || EventManager.Instance == null) { _subscribed = false; return; }
            EventManager.Instance.OnResearchRequested -= HandleResearchRequested;
            EventManager.Instance.OnDayEnded -= HandleDayEnded;
            EventManager.Instance.OnSaveLoaded -= HandleSaveLoaded;
            _subscribed = false;
        }

        public bool IsDone(string projectId) => projectId switch
        {
            ProjectSeeds => SeedsDone,
            ProjectIsotope => IsotopeDone,
            ProjectCoreTower => CoreUnlockDone,
            _ => false,
        };

        /// <summary>
        /// เช็คว่าโครงการกดวิจัยได้ไหม + เหตุผลที่ล็อก (ให้ปุ่มใน LabPanelUI จางลงพร้อมคำอธิบาย)
        /// </summary>
        public bool CanResearch(string projectId, out string reason)
        {
            if (IsDone(projectId)) { reason = "วิจัยสำเร็จแล้ว"; return false; }

            // ห้องวิจัยต้องมีวิศวกรประจำครบจึงเดินงานวิจัย (ทุกโครงการ)
            if (!LabStaffed(out string labReason)) { reason = labReason; return false; }

            int phase = GameManager.Instance != null ? GameManager.Instance.CurrentPhase : 1;
            var res = ResourceManager.Instance != null ? ResourceManager.Instance.Current : default;

            switch (projectId)
            {
                case ProjectSeeds:
                    if (phase < seedsUnlockPhase) { reason = $"ปลดล็อกในเฟส {seedsUnlockPhase}"; return false; }
                    int engineers = PopulationManager.Instance != null ? PopulationManager.Instance.Current.engineers : 0;
                    if (engineers < seedsEngineerCrew) { reason = $"ต้องมีวิศวกร {seedsEngineerCrew} คน (มี {engineers})"; return false; }
                    if (res.iron < seedsIronCost) { reason = $"เหล็กไม่พอ (ต้องใช้ ⛏{seedsIronCost})"; return false; }
                    break;

                case ProjectIsotope:
                    if (IsotopePending) { reason = "รอฟลักซ์นิวตรอน — ตั้งเตาโหมด Idle ให้ครบ 1 วัน"; return false; }
                    var ct = CoreTowerManager.Instance;
                    if (ct == null || !ct.Current.isUnlocked) { reason = "ต้องรอเตา CORE TOWER เดินเครื่องก่อน (แหล่งนิวตรอน)"; return false; }
                    if (res.iron < isotopeIronCost) { reason = $"เหล็กไม่พอ (ต้องใช้ ⛏{isotopeIronCost})"; return false; }
                    break;

                case ProjectCoreTower:
                    if (phase < coreUnlockPhase) { reason = $"ปลดล็อกในเฟส {coreUnlockPhase}"; return false; }
                    if (res.iron < coreIronCost) { reason = $"เหล็กไม่พอ (ต้องใช้ ⛏{coreIronCost})"; return false; }
                    if (res.energy < coreEnergyCost) { reason = $"พลังงานไม่พอ (ต้องใช้ ⚡{coreEnergyCost})"; return false; }
                    break;

                default:
                    reason = $"ไม่รู้จักโครงการ '{projectId}'";
                    return false;
            }

            reason = "";
            return true;
        }

        // ห้องวิจัยมีอยู่ + วิศวกรประจำครบ workerRequired — read-only query registry/WAM
        private bool LabStaffed(out string reason)
        {
            var registry = BuildingRegistry.Instance;
            if (registry != null)
                foreach (var kvp in registry.PlacedBuildings)
                    if (kvp.Value != null && kvp.Value.buildingType == BuildingType.Laboratory)
                    {
                        int need = registry.WorkersRequired(kvp.Key);
                        if (need <= 0) need = kvp.Value.workerRequired;
                        int assigned = WorkerAssignmentManager.Instance != null
                            ? WorkerAssignmentManager.Instance.GetAssigned(kvp.Key) : 0;
                        if (assigned >= need) { reason = ""; return true; }
                        reason = $"ต้องมีวิศวกรประจำห้องวิจัยครบ {need} คน (มี {assigned})";
                        return false;
                    }

            reason = "ไม่พบห้องวิจัยในเมือง";
            return false;
        }

        private void HandleResearchRequested(string projectId)
        {
            if (!CanResearch(projectId, out string reason))
            {
                EventManager.Instance.RaiseNotice($"วิจัยไม่ได้ — {reason}");
                return;
            }

            switch (projectId)
            {
                case ProjectSeeds:
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -seedsIronCost);
                    SeedsDone = true;
                    EventManager.Instance.RaiseResearchCompleted(ProjectSeeds); // CrisisEffectManager → FoodYield ×2
                    EventManager.Instance.RaiseNotice("🌾 วิจัยเมล็ดพันธุ์ฉายรังสีสำเร็จ — ผลผลิตฟาร์ม +100% ถาวร");
                    break;

                case ProjectIsotope:
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -isotopeIronCost);
                    IsotopePending = true; // เสร็จเมื่อจบวันที่เตาโหมด Idle (HandleDayEnded)
                    EventManager.Instance.RaiseNotice("☢ เริ่มสังเคราะห์ยาไอโซโทป — ตั้งเตาโหมด Idle ให้ครบ 1 วันเพื่อรับฟลักซ์นิวตรอน");
                    break;

                case ProjectCoreTower:
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Iron, -coreIronCost);
                    EventManager.Instance.RaiseResourceDelta(ResourceType.Energy, -coreEnergyCost);
                    CoreUnlockDone = true;
                    EventManager.Instance.RaiseResearchCompleted(ProjectCoreTower); // CoreTowerManager → ปลดเตา (เมื่อถึงวัน)
                    EventManager.Instance.RaiseNotice("★ วิจัยปลดล็อก CORE TOWER สำเร็จ — เตาพร้อมเดินเครื่องเมื่อถึงเฟส 3");
                    break;
            }
        }

        // ยาไอโซโทปเสร็จเมื่อจบวันที่เตาอยู่โหมด Idle (ฟลักซ์นิวตรอนว่างจากการดัน CORE%)
        private void HandleDayEnded(int day)
        {
            if (!IsotopePending) return;
            var ct = CoreTowerManager.Instance;
            if (ct == null || !ct.Current.isUnlocked) return;
            if (ct.Current.overclockMode != CoreTowerManager.ModeIdle) return;

            IsotopePending = false;
            IsotopeDone = true;
            EventManager.Instance.RaisePopulationSickCured(isotopeCureCount);
            EventManager.Instance.RaiseResearchCompleted(ProjectIsotope);
            EventManager.Instance.RaiseNotice($"💊 ยาไอโซโทปการแพทย์พร้อมใช้ — รักษาคนป่วยสูงสุด {isotopeCureCount} คน");
        }

        private void HandleSaveLoaded(SaveData save)
        {
            SeedsDone = save.researchSeedsDone;
            IsotopeDone = save.researchIsotopeDone;
            CoreUnlockDone = save.researchCoreUnlockDone;
            IsotopePending = save.researchIsotopePending;
        }
    }
}
