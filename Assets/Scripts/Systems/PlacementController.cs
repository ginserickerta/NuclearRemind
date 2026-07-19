using UnityEngine;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// จัดการการวางอาคารบน grid: แสดง ghost preview สีเขียว/แดง,
    /// ยืนยันการวางด้วยคลิกซ้าย, ยกเลิกด้วยคลิกขวา
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        [Header("Ghost Preview")]
        public SpriteRenderer ghostRenderer;

        [Header("Ghost Colors")]
        public Color validColor = new Color(0f, 1f, 0f, 0.5f);
        public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("Testing - Building Hotbar (กด 1-9 เพื่อเลือกอาคาร)")]
        public BuildingData[] buildingHotbar;

        [Header("Zone B (เขตรังสีสูง — ห้ามวางอาคาร)")]
        [Tooltip("คอลัมน์แรกของ Zone B — วางอาคารในคอลัมน์ ≥ ค่านี้ไม่ได้ · Awake จะ sync จาก ZoneBarrierRenderer.barrierColumn ให้ตรงกับรั้วที่เห็นอัตโนมัติ")]
        [SerializeField] private int zoneBBarrierColumn = 36;
        [Tooltip("ปิด = อนุญาตวางใน Zone B ได้ (เผื่อดีไซน์เปลี่ยน)")]
        [SerializeField] private bool blockZoneBPlacement = true;

        private static readonly KeyCode[] HotbarKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8,
            KeyCode.Alpha9
        };

        public static PlacementController Instance { get; private set; }

        private BuildingData selectedBuilding;
        private Vector2Int currentCell;
        private bool isPlacing;
        private bool _dragMode;   // true = เข้าโหมดวางจากการลากช่อง hotbar (drop=วาง แทนคลิกซ้าย)

        /// <summary>กำลังอยู่โหมดวางอาคาร (read-only — PauseMenuController ใช้เช็คก่อนเปิดเมนูจาก ESC)</summary>
        public bool IsPlacing => isPlacing;

        /// <summary>เฟรมล่าสุดที่ ESC ถูกใช้ยกเลิกการวาง — กัน PauseMenu เปิดซ้อนในเฟรมเดียวกัน
        /// (ลำดับ Update ระหว่างสองสคริปต์ไม่การันตี จึงต้องเช็คทั้ง IsPlacing และเฟรมนี้)</summary>
        public int LastEscCancelFrame { get; private set; } = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // sync ขอบ Zone B ให้ตรงกับรั้วที่ผู้เล่นเห็น (ถ้ามี ZoneBarrierRenderer ในซีน) — Awake เท่านั้น (ไม่ใช่ Update)
            var barrier = FindFirstObjectByType<ZoneBarrierRenderer>();
            if (barrier != null) zoneBBarrierColumn = barrier.barrierColumn;

            if (ghostRenderer != null)
                ghostRenderer.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventManager.Instance.OnBuildingSelectRequested += BeginPlacement;
            EventManager.Instance.OnBuildingDragStarted     += BeginDragPlacement;
            EventManager.Instance.OnBuildingDragDropped     += EndDragPlacement;
            EventManager.Instance.OnDemolishModeToggled     += HandleDemolishModeToggled;
        }

        private void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnBuildingSelectRequested -= BeginPlacement;
            EventManager.Instance.OnBuildingDragStarted     -= BeginDragPlacement;
            EventManager.Instance.OnBuildingDragDropped     -= EndDragPlacement;
            EventManager.Instance.OnDemolishModeToggled     -= HandleDemolishModeToggled;
        }

        private void HandleDemolishModeToggled(bool active)
        {
            if (active && isPlacing)
                CancelPlacement();
        }

        private void Update()
        {
            HandleHotbarInput();

            // อัประดับอาคารใต้เคอร์เซอร์ (กด U — V4 §7) เมื่อไม่ได้อยู่โหมดวาง
            if (!isPlacing && Input.GetKeyDown(KeyCode.U) && InputManager.Instance != null)
                EventManager.Instance.RaiseUpgradeBuildingRequested(InputManager.Instance.GetMouseGridPosition());

            if (!isPlacing || selectedBuilding == null)
                return;

            UpdateGhost();

            // ยกเลิกด้วยคลิกขวา/Esc ได้ทั้งสองโหมด
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) // §15: ยกเลิก = คลิกขวา หรือ Esc
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    LastEscCancelFrame = Time.frameCount;
                CancelPlacement();
                return;
            }

            // โหมด drag: วาง/ยกเลิกเมื่อปล่อยเมาส์ (ปกติ HotbarSlotDrag.OnEndDrag เรียก EndDragPlacement
            // อยู่แล้ว — อันนี้เป็น backstop เผื่อลำดับ update ต่างกัน · EndDragPlacement เป็น idempotent)
            if (_dragMode)
            {
                if (Input.GetMouseButtonUp(0))
                    EndDragPlacement();
            }
            // โหมดคลิก: คลิกซ้ายเพื่อยืนยันวาง
            else if (Input.GetMouseButtonDown(0))
                ConfirmPlace();
        }

        /// <summary>
        /// เช็คปุ่มเลข 1-9 เพื่อเลือกอาคารจาก buildingHotbar และเริ่มวาง
        /// </summary>
        private void HandleHotbarInput()
        {
            for (int i = 0; i < HotbarKeys.Length && i < buildingHotbar.Length; i++)
            {
                if (Input.GetKeyDown(HotbarKeys[i]) && buildingHotbar[i] != null)
                {
                    BeginPlacement(buildingHotbar[i]);
                    break;
                }
            }
        }

        /// <summary>
        /// เริ่มโหมดวางอาคารด้วยข้อมูลอาคารที่เลือก
        /// ทรัพยากรไม่พอ / CORE TOWER มีอยู่แล้ว → ไม่เข้าโหมดวาง (ปุ่ม HUD หรี่อยู่แล้ว แต่ hotkey ยังกดได้)
        /// </summary>
        public void BeginPlacement(BuildingData buildingData) => StartPlacement(buildingData, false);

        /// <summary>เริ่มวางแบบ drag (ลากจากช่อง hotbar) — วาง/ยกเลิกเมื่อปล่อยเมาส์ ไม่ต้องคลิกซ้ำ</summary>
        public void BeginDragPlacement(BuildingData buildingData) => StartPlacement(buildingData, true);

        private void StartPlacement(BuildingData buildingData, bool dragMode)
        {
            if (buildingData == null) return;

            // ล็อกเฟส (GDD §6/§7): ยังไม่ถึงเฟสปลดล็อก → ไม่เข้าโหมดวาง (กันทั้ง hotkey 1-9, ลากวาง และคลิกปุ่ม)
            // ★ v6.3: เฟสอ่านจาก PhaseManager (core%) ไม่ใช่ GameManager.CurrentPhase (ผูกวัน = ผิดกฎข้อ 1)
            //   ต้องตรงกับ BuildingSelectionUI ที่ซ่อนช่อง ไม่งั้นซ่อนปุ่มแล้วยังลากวางทะลุได้
            if (buildingData.unlockPhase > PhaseManager.CurrentPhase)
            {
                EventManager.Instance.RaiseNotice($"{buildingData.buildingName} ปลดล็อกในเฟส {buildingData.unlockPhase} " +
                                                  $"(ตอนนี้เฟส {PhaseManager.CurrentPhase} · CORE ต้องถึง " +
                                                  $"{PhaseCoreRequirement(buildingData.unlockPhase)}%)");
                return;
            }

            if (!CanAfford(buildingData))
            {
                Debug.Log($"[PlacementController] ทรัพยากรไม่พอสร้าง {buildingData.buildingName} " +
                          $"(ต้องใช้ ⚡{buildingData.energyCost} ⛏{buildingData.ironCost})");
                return;
            }

            if (IsUniqueAlreadyPlaced(buildingData))
            {
                Debug.Log($"[PlacementController] {buildingData.buildingName} สร้างได้แค่หลังเดียว — มีอยู่ในเมืองแล้ว");
                return;
            }

            selectedBuilding = buildingData;
            isPlacing = true;
            _dragMode = dragMode;

            // §15: เข้าโหมดวาง → หยุดนาฬิกาวัน (ghost ยังเลื่อนได้ เพราะไม่แตะ timeScale)
            TimeManager.Instance?.Pause(PauseReason.Placement);

            // ออกจาก demolish mode เมื่อเริ่มวางอาคาร
            if (DemolitionController.Instance != null && DemolitionController.Instance.IsDemolishing)
                EventManager.Instance.RaiseDemolishModeToggled(false);

            if (ghostRenderer != null)
            {
                ghostRenderer.sprite = buildingData.sprite;
                ghostRenderer.gameObject.SetActive(true);
            }

            EventManager.Instance.RaiseBuildingSelected(buildingData);
        }

        /// <summary>
        /// อัปเดตตำแหน่งและสีของ ghost ตามตำแหน่งเมาส์บน grid
        /// </summary>
        private void UpdateGhost()
        {
            currentCell = InputManager.Instance.GetMouseGridPosition();

            if (ghostRenderer == null)
                return;

            // anchor ghost ที่กึ่งกลาง footprint + spriteOffset เหมือน BuildingVisualSpawner → preview ตรงกับตอนวางจริง
            var size = selectedBuilding != null ? selectedBuilding.size : Vector2Int.one;
            var offset = selectedBuilding != null ? (Vector3)selectedBuilding.spriteOffset : Vector3.zero;
            ghostRenderer.transform.position = GridManager.Instance.FootprintCenterWorld(currentCell, size) + offset;
            float visScale = selectedBuilding != null && selectedBuilding.spriteScale > 0f ? selectedBuilding.spriteScale : 1f;
            ghostRenderer.transform.localScale = new Vector3(visScale, visScale, 1f);
            ghostRenderer.color = IsPlacementValid(currentCell) ? validColor : invalidColor;
        }

        /// <summary>
        /// ตรวจสอบว่าวางอาคาร (ตาม footprint ของ selectedBuilding) ที่ตำแหน่งนี้ได้หรือไม่
        /// รวมเงื่อนไขทรัพยากรพอจ่าย + อาคาร unique (ghost แดงและ ConfirmPlace ใช้ร่วมกัน)
        /// </summary>
        private bool IsPlacementValid(Vector2Int origin)
        {
            if (!CanAfford(selectedBuilding) || IsUniqueAlreadyPlaced(selectedBuilding))
                return false;
            if (!IsResearchUnlocked(selectedBuilding)) // v6.3 §19: ต้องวิจัย note ก่อน (บั๊ก #11 gate)
                return false;

            for (int dx = 0; dx < selectedBuilding.size.x; dx++)
            {
                for (int dy = 0; dy < selectedBuilding.size.y; dy++)
                {
                    Cell cell = GridManager.Instance.GetCell(origin.x + dx, origin.y + dy);
                    if (cell == null || cell.isOccupied)
                        return false;
                    if (IsInZoneB(origin.x + dx)) // เขตรังสีสูง — ห้ามวาง
                        return false;
                }
            }

            return true;
        }

        /// <summary>คอลัมน์นี้อยู่ใน Zone B (เขตรังสีสูง ฝั่ง NE ของรั้ว) ไหม — วางอาคารไม่ได้</summary>
        private bool IsInZoneB(int col) => blockZoneBPlacement && col >= zoneBBarrierColumn;

        /// <summary>footprint ที่ origin แตะ Zone B ไหม (ใช้แจ้งเหตุผลตอนวางไม่ได้)</summary>
        private bool FootprintTouchesZoneB(Vector2Int origin)
        {
            if (!blockZoneBPlacement || selectedBuilding == null) return false;
            for (int dx = 0; dx < selectedBuilding.size.x; dx++)
                if (IsInZoneB(origin.x + dx)) return true;
            return false;
        }

        /// <summary>
        /// อาคารที่ต้องวิจัยก่อน (v6.3 §19) — ยังไม่วิจัย note = วางไม่ได้ (ghost แดง)
        /// "" = อาคารพื้นฐาน ไม่ผูกวิจัย · ไม่มี KnowledgeDB (test/scene เก่า) → ไม่บล็อก
        /// </summary>
        /// <summary>CORE% ที่ต้องถึงเพื่อเข้าเฟสนั้น (GDD §7) — ใช้บอกผู้เล่นว่าต้องดันเตาถึงเท่าไหร่</summary>
        private static int PhaseCoreRequirement(int phase) =>
            phase >= 4 ? 80 : phase == 3 ? 60 : phase == 2 ? 40 : 0;

        private static bool IsResearchUnlocked(BuildingData data)
        {
            // single gate — same method the Sprint 2 acceptance test asserts on (KnowledgeDB.IsBuildingUnlocked)
            return KnowledgeDB.Instance.IsBuildingUnlocked(data);
        }

        /// <summary>คลังปัจจุบันพอจ่ายค่าสร้างไหม (ตัวเลขเดียวกับที่ ResourceManager หักตอนวาง)</summary>
        private static bool CanAfford(BuildingData data)
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return true;
            return rm.Current.energy >= data.energyCost && rm.Current.iron >= data.ironCost;
        }

        /// <summary>
        /// อาคาร "หลังเดียว" สร้างซ้ำไม่ได้ — เช็กจาก registry ว่ามีอยู่แล้วหรือยัง
        /// CORE TOWER (V4 §8) + โรงวิจัย (GDD rework: มากับแมพ ไม่อยู่ใน hotbar — เช็คนี้กันหลุดทางอื่น)
        /// </summary>
        private static bool IsUniqueAlreadyPlaced(BuildingData data)
        {
            if (data.buildingType != BuildingType.CoreTower &&
                data.buildingType != BuildingType.Laboratory) return false;

            var registry = BuildingRegistry.Instance;
            if (registry == null) return false;

            foreach (var kvp in registry.PlacedBuildings)
                if (kvp.Value != null && kvp.Value.buildingType == data.buildingType)
                    return true;

            return false;
        }

        /// <summary>
        /// ยืนยันการวางอาคารที่ตำแหน่งปัจจุบัน ถ้าตำแหน่งใช้ได้
        /// </summary>
        public void ConfirmPlace()
        {
            if (!isPlacing || selectedBuilding == null)
                return;

            if (!IsPlacementValid(currentCell))
            {
                if (FootprintTouchesZoneB(currentCell))
                    EventManager.Instance.RaiseNotice("วางอาคารในเขตรังสีสูง (Zone B) ไม่ได้");
                else if (!IsResearchUnlocked(selectedBuilding))
                {
                    var note = KnowledgeDB.Instance.GetNote(selectedBuilding.requiredNoteId);
                    EventManager.Instance.RaiseNotice($"ต้องวิจัย \"{(note != null ? note.title : selectedBuilding.requiredNoteId)}\" ก่อนถึงจะสร้างได้");
                }
                Debug.Log("[PlacementController] ตำแหน่งนี้วางอาคารไม่ได้");
                return;
            }

            OccupyFootprint(currentCell);

            Debug.Log($"[PlacementController] วาง {selectedBuilding.buildingName} ที่ ({currentCell.x}, {currentCell.y})");
            EventManager.Instance.RaiseBuildingPlaced(GridManager.Instance.GetCell(currentCell.x, currentCell.y), selectedBuilding);

            EndPlacement();
        }

        /// <summary>
        /// ทำเครื่องหมาย cell ทั้งหมดใน footprint ของอาคารว่าถูกครอบครองแล้ว
        /// </summary>
        private void OccupyFootprint(Vector2Int origin)
        {
            for (int dx = 0; dx < selectedBuilding.size.x; dx++)
            {
                for (int dy = 0; dy < selectedBuilding.size.y; dy++)
                {
                    Cell cell = GridManager.Instance.GetCell(origin.x + dx, origin.y + dy);
                    cell.isOccupied = true;
                    cell.buildingType = selectedBuilding.buildingType;
                }
            }
        }

        /// <summary>
        /// จบการลาก (ปล่อยเมาส์) — วางถ้าตำแหน่งใช้ได้และไม่ได้ปล่อยบน UI, ไม่งั้นยกเลิก
        /// </summary>
        public void EndDragPlacement()
        {
            if (!isPlacing || !_dragMode) return;

            // ปล่อยเมาส์เหนือ UI (เช่น ลากกลับมาปล่อยบน hotbar) = ยกเลิก
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (!overUI && IsPlacementValid(currentCell))
                ConfirmPlace();
            else
                CancelPlacement();
        }

        /// <summary>
        /// ยกเลิกการวางอาคาร
        /// </summary>
        public void CancelPlacement()
        {
            Debug.Log("[PlacementController] ยกเลิกการวางอาคาร");
            EventManager.Instance.RaisePlacementCancelled();
            EndPlacement();
        }

        /// <summary>
        /// ออกจากโหมดวางอาคารและซ่อน ghost
        /// </summary>
        private void EndPlacement()
        {
            isPlacing = false;
            _dragMode = false;
            selectedBuilding = null;

            if (ghostRenderer != null)
                ghostRenderer.gameObject.SetActive(false);

            // §15: ออกจากโหมดวาง (วาง/ยกเลิก) → นาฬิกาวันเดินต่อ
            TimeManager.Instance?.Resume(PauseReason.Placement);

            EventManager.Instance.RaiseBuildingSelected(null);
        }
    }
}
