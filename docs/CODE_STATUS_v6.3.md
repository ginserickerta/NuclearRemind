# CODE_STATUS_v6.3 — แผนที่โค้ดทั้งโปรเจกต์เทียบ GDD v6.3

> จัดทำ 2026-07-16 · อ้างอิง `docs/GDD.md` §1–§33 + `CLAUDE.md` (root) + สแกนโค้ดจริงทั้ง 127 ไฟล์ runtime / 80 editor / 49 tests
> ใช้ประกอบ proposal ส่วน "โค้ดที่ใช้ในเกม" — บอกว่าไฟล์ไหน **คงที่ / เสร็จแล้ว / ต้องแก้ / ถูกแทน / ต้องสร้าง**

---

## 0) ภาพรวมตัวเลข

| สถานะ | ความหมาย | จำนวนโดยประมาณ |
|---|---|---|
| ✅ **คงที่** | ใช้ได้ตาม v6.3 โดยไม่ต้องแตะ (engine/รากฐาน/visual) | ~45 ไฟล์ |
| 🆕 **v6.3 เสร็จแล้ว** | ระบบใหม่ตามสเปกที่ implement แล้ว (Sprint 1–2) | ~20 ไฟล์ |
| 🔧 **ต้องแก้** | ไฟล์อยู่ต่อ แต่เนื้อในต้องปรับตามสเปก | ~25 ไฟล์ |
| 🔻 **ถูกแทน/รอปลด** | ระบบเก่าที่ GDD §16 สั่งแทนที่ — ลบเมื่อตัวแทนพร้อม | ~35 ไฟล์ |
| ➕ **ต้องสร้างใหม่** | ตามโครง §31 ที่ยังไม่มีในโปรเจกต์ | ~25 ไฟล์ |

สถานะ Sprint (ตาม CLAUDE.md): **Sprint 1 ✅ · Sprint 2 ✅ · Sprint 3–6 ⬜ ยังไม่เริ่ม**

---

## 1) ✅ คงที่ — ใช้ต่อได้เลย (ใส่ใน proposal ว่าเป็น "โครงสร้างพื้นฐานที่เสถียร")

### แกนกริด/กล้อง/อินพุต
| ไฟล์ | เหตุผล |
|---|---|
| `Systems/GridManager.cs` | คณิต IsoToWorld/WorldToIso **ห้ามแก้** (กติกา v6.3) · grid 43×43 เคาะแล้ว — เหลือแค่ยืนยันค่า columns/rows ในซีน + อัป `GridManagerTests` |
| `Systems/CameraController.cs` | กล้อง city-builder สมบูรณ์ ไม่ขึ้นกับ GDD |
| `Systems/InputManager.cs` · `CursorManager.cs` | เมาส์→กริด, เคอร์เซอร์ตามบริบท |

### เรนเดอร์/visual (ไม่ขึ้นกับกติกาเกม)
`BuildingDepthSort` · `BuildingClickTarget` · `BuildingVisualSpawner` · `BuildingUpgradeEffect` · `SpriteFrameAnimator` · `DecorPiece` · `DecorSpawner` · `IsoGroundPainter`* · `BaseColliderTuner`
*(\*IsoGroundPainter: สี Zone B เป็นแค่ visual — ดูข้อตัดสินใจ #4)*

### คนงานฝั่ง "ภาพ" (คณิต pure ผ่านเทสต์แล้ว)
`WorkerPathing` · `WorkerSeparation` · `WorkerView` — คงที่ · `WorkerVisualSpawner` ดูตาราง 🔧 (ต้อง rewire แหล่งข้อมูล)

### UI infrastructure (ไม่ผูกระบบเกม)
`GameUIStack` · `UIClickPop` · `UIGlowPulse` · `UIPopIn` · `DayTransitionUI` · `TooltipController` · `AlertController` · `Palette` · `HotkeyHelpController` · `HudMenuButtons` · `HotbarSlotDrag`

### เมนู/แผงที่สเปก v6.3 ยังใช้แนวเดิม
`MainMenuController` · `MainMenuIntroAnimator` · `MenuVideoBackground` · `PauseMenuController` · `DialogueUIController` (ใช้กับบทชนกันใน Cards + NPC reaction ของ Notes) · `MemorialPanelController` (§27 memorial_clickable — เปลี่ยนแค่ให้รายงาน Hope ผ่าน HopeLedger) · `RecordCardUI` / `RecordsPanelController` / `RecordNotificationHUD` (Records อยู่ต่อใน §24 — เปลี่ยนแค่ "ต้นทาง" เป็น DataRecovery)

### Manager กลางที่ตรงสเปกอยู่แล้ว
| ไฟล์ | เหตุผล |
|---|---|
| `Managers/TimeManager.cs` | pause-reason stack = ตรง §3/§15 เป๊ะ (ห้าม timeScale=0) |
| `Managers/EventManager.cs` | โครง Observer อยู่ต่อ — แค่เพิ่ม/ปลด event ตามระบบ (นับเป็นคงที่เชิงสถาปัตยกรรม) |
| `Managers/SaveManager.cs` | กลไก JSON save อยู่ต่อ (schema ใน SaveData ต้องแก้ — ดู 🔧) |
| `Managers/MetaProgress.cs` | 🆕 ทำแล้ว — MasteryBank + UnlockedCodex ข้ามรอบเล่น (PlayerPrefs) ตรง §16 |
| `Managers/BuildingRegistry.cs` | ทะเบียนอาคาร+เลเวลใช้ต่อ (ตารางอาคารใน BuildingData เปลี่ยน — ดู 🔧) |

### เครื่องมือ dev
`DebugCheatPanel` · `EventTestSubscriber` · `Sprint1TestPanel` · `StoryTestPanel`(จะตายพร้อมระบบเก่า) · Editor bakers: `GameBuilder` · `HotbarPrefabBaker` · `CoreTowerPanelBaker` · `BuildingPanelBaker` · `PlayModeSaver` · `RunAllSetups` · เครื่องมือ art/grid ทั้งชุด (`GridSizeSetup`, `TileColorPainter`, `IsoTilesetSetup`, `LightingSetup`, ฯลฯ)

---

## 2) 🆕 v6.3 ที่ implement เสร็จแล้ว (Sprint 1–2) — อ้างใน proposal ได้ว่า "ทำแล้ว"

| โฟลเดอร์/ไฟล์ | ระบบ (GDD §) |
|---|---|
| `Core/GameConfigSO.cs` | ค่าทั้งเกมจาก CONFIG.md จุดเดียว (กติกา 2) — มี section WORKER/HOPE/RADIATION/ECONOMY + ค่า 🔒 |
| `Workers/Worker.cs` · `WorkerManager.cs` · `ShiftSystem.cs` | per-worker state ไม่มีคลาส (§17) + Shift auto-rest 70/25 + lab-deadlock guard 92 (§17.5) |
| `Hope/HopeLedger.cs` · `HopeEntry.cs` · `HopeThresholdWatcher.cs` · `Hope/UI/HopeBreakdownPanel.cs` | Hope เขียนผ่าน Ledger ที่เดียว (§18, กติกา 8) + threshold hysteresis + แผง breakdown บังคับมี |
| `Research/ResearchNoteSO.cs` · `ResearchLeadSO.cs` · `ResearchLab.cs` · `SoftTriggerWatcher.cs` · `KnowledgeDB.cs` + `UI/ResearchQueuePanel.cs` · `UI/NoteCardPopup.cs` | ResearchQueue เต็มระบบ (§19): แล็บเริ่มพัง (ซ่อม Iron 80/2วัน/2คน), จ่ายครั้งเดียว, re-staff มี floor, soft trigger ผูก core% |
| `Inventory/InventorySlotView.cs` · `Inventory/UI/InventoryPanel.cs` | แผงคลัง 6 แท็บ + delta/วัน + Tritium ติดลบกะพริบแดง (INVENTORY.md) |
| `Managers/MetaProgress.cs` | meta-persistence (§16/§21) |
| Editor: `ResearchNotesSetup.cs` (8 Notes จาก NOTES.md) · `GridSizeSetup.cs` (43×43) | เครื่องมือ deploy เนื้อหา v6.3 |
| Tests: `WorkerSystemTests` · `HopeSystemTests` · `ResearchSystemTests` · `MetaProgressTests` · `Sprint1SimulationTests` · `Sprint2RegressionTests` · `TimeManagerTests` | เทสต์รับ Sprint 1–2 |

> ⚠ ระบบใหม่พวกนี้ "อยู่คู่" ระบบเก่า (มี guard, เปิดผ่าน Sprint1TestPanel) — **ยังไม่ cutover เข้าเกมจริง**

---

## 3) 🔧 ต้องแก้ — ไฟล์อยู่ต่อแต่เนื้อในเปลี่ยน

| ไฟล์ | ต้องแก้อะไร | อ้าง |
|---|---|---|
| `Managers/ResourceManager.cs` | รื้อชุดทรัพยากรเป็น 7 ตัว (**เพิ่ม LabMat**, **ตัด Knowledge/Hope ออก**) · floor 0 ทั้ง Power/Water (บั๊ก #17/#18) · blackout เป็น **flag** ก่อน clamp · สระน้ำรวม (ดื่ม+หล่อเย็น+สกัด) · สูตร draw/spoil จาก GameConfigSO · ผลิตแบบ batch ปลายวัน × Σ GetEfficiency (กติกา 7) | §4 §27 |
| `Managers/GameManager.cs` | เช็คชนะ `core≥100` **ทันทีทุกวัน** (บั๊ก #15) · ตัด trigger ตามวันทั้งหมด (เหลือ day==30) · จอจบ 3 แบบ + สรุปสถิติรอบ (STORY.md) · ผูก MetaProgress ตอน restart | §1 §3 |
| `Data/ResourceData.cs` | enum → `{Power, Water, Food, Iron, LabMat, Deuterium, Tritium}` (append-only เดิมเป็น Energy/…/Knowledge) | §16 |
| `Data/BuildingData.cs` | ตัด `requiredClass` (ไม่มีคลาสแล้ว) · ตารางอาคารใหม่ทั้งชุด (Granary/Barracks/Med Bay/Sensor Array/Zone B/Co-60/Mutation Lab; ตัด Agri Dome/Hospital) · output ต่อคน×eff ไม่ใช่ต่อหลัง | §6 §27 |
| `Data/SaveData.cs` · `InventoryData.cs` | schema ใหม่: Worker list (สถานะต่อคน), research queue, storm pressure, card cooldowns — ทุก field ต้องมี default | §16 |
| `Data/GameEndType.cs` | เพิ่ม/แยกเหตุแพ้ตาม STORY.md (Hope0/Meltdown/Timeout) | STORY |
| `Data/RecordCardSO.cs` | คงไว้ — ปรับ field ให้ตรง §24 (4 ใบของ Elara, ปลดด้วย DataRecovery) | §24 |
| `Data/InfoCardSO.cs` | **ห้ามลบ** — เนื้อหาย้ายเข้า `ResearchNoteSO.knowledgeBody` แล้ว rename/mark obsolete ตามกติกา | CLAUDE |
| `Data/ItemSO.cs` | ลดเหลือ 8 item ตาม INVENTORY.md · craft แบบ B (labMat 30, เช็ค `MasteryRegistry.HasNote`) | §13 |
| `Managers/CodexManager.cs` + `UI/CodexUIController.cs` | รื้อเป็น Codex 11 entry 1:1 ควิซ · unlock state อยู่ใน MetaProgress ไม่ใช่ SO · x/11 + จุดแดง + ควิซตอบใน Codex (แทน QuizManager) | §21 CODEX |
| `UI/UIManagerHUD.cs` | HUD ใหม่ตาม §17-UI: แถบ 6 ทรัพยากร (มี LabMat) · Hope + ปุ่ม Breakdown · Storm gauge (โผล่เมื่อมี Sensor) · ตัดหลอด Knowledge | §17-UI |
| `UI/BuildingSelectionUI.cs` + `Systems/PlacementController.cs` | รายชื่ออาคารใหม่ · gate ปลดล็อกด้วย **PhaseManager (core%)** แทน GamePhase(วัน) · placement pause ผ่าน reason stack (มีแล้ว) | §7 §15 |
| `UI/BuildingUpgradeUI.cs` | ตาราง L1/L2/L3 ใหม่ (×1.5/×2.2, ปลดตามเฟส core%) · แถวคนงานอ่านจาก WorkerManager (job) แทนคลาส | §6 §27 |
| `Systems/WorkerVisualSpawner.cs` | เปลี่ยนแหล่งข้อมูลจาก PopulationData(คลาส) → `WorkerManager` (รายคน + job) — โครง diff-reconcile เดิมเก็บไว้ | §17 |
| `UI/TutorialManager.cs` | Day-1 tutorial ใหม่: สอนกฎพัก (§17.5) + 3 โรงผลิต + จัดคน | §2 |
| `UI/InventoryPanelController.cs` | ยุบรวม/ชี้ไปใช้ `Inventory/UI/InventoryPanel` (v6.3) — เหลือ UI คลังชุดเดียว | INVENTORY |
| `Utils/StatCondition.cs` | คงเฉพาะถ้าเอาไปใช้ parse trigger ของ Cards — ไม่งั้นปลดพร้อม Dilemma | §25 |
| `Managers/EventManager.cs` | เพิ่ม event ชุดใหม่ (card/bark/storm/scram/mastery/phase) + ทยอยลบ event ของระบบที่ปลด | §16 |
| Tests: `GridManagerTests` | อัปเป็น 43×43 (ค่า columns/rows ในโค้ดยัง default 20×12 — ซีนเป็นตัวจริง) | CLAUDE |

---

## 4) 🔻 ถูกแทน / รอปลด — ลบเมื่อตัวแทนพร้อม (ห้ามลบก่อน cutover)

| ระบบเก่า (ไฟล์) | ถูกแทนด้วย | ปลดตอน |
|---|---|---|
| `PopulationManager` (453) · `WorkerAssignmentManager` (263) · `Data/PopulationData` · `Data/WorkerClass` · Editor `Phase3PopulationSetup` · `CharacterSpriteSetup`(ส่วนคลาส) | `Workers/` (✅มีแล้ว) — per-worker + job string | **Cutover Sprint 1** (ตัวแทนเสร็จแล้ว — เหลือสลับ) |
| `QuizManager` (276) · `QuizPopupController` · `QuizExplanationPopupController` · Editor `QuizSetup`/`QuizExplanationSetup` · Tests `QuizManagerTests` | Codex quiz + `Mastery/` (Sprint 3) — ควิซสมัครใจใน Codex, โบนัสถาวร | Sprint 3 |
| `DilemmaManager` · `DecreeManager` · `CrisisEffectManager` · `Narrative/CrisisSchedule`(ผูกวัน=ผิดกติกา 1) · `Data/DilemmaData` · `CrisisChoiceEffects` · `DecreeSO` · `DilemmaPopupController` · Editor `CrisisSetup`/`DecreeSetup`/`CrisisEffectSetup` · Tests ชุด Dilemma/Decree/CrisisEffect/CrisisSchedule | `Cards/` (Sprint 4): CrisisCardSO 8 ใบ + CardManager + CrisisCardPanel (ตัวเลือกล็อก 🔒 ต้องมองเห็น) — Decree = การ์ด #8 | Sprint 4 |
| `Narrative/StoryDirector` (584) · `Data/StoryBeatSO` · `StoryEnums` · Editor `StorySetup`(738)/`StoryUISetup`(634) · Tests ชุด Story | แตกเป็น: `Records/DataRecovery` (Sprint 5) + NoteCardPopup(✅) + Intro/Endings ใน GameManager + `Barks/` — เหลือแค่ Records 4 ใบ + intro 3 การ์ด + 3 ฉากจบ | Sprint 5–6 |
| `CoreTowerManager` (649) · `UI/CoreTowerUI` (เก่า) · `Data/TowerData` · Tests `CoreTowerManagerTests` | `Core/ReactorController` (สูตร §26: cooling มีเพดาน, Method B gate, boost 3.0, ชนะทันที) — SCRAM ยกมาทั้งชุด · `CoreTowerPanelUI` (skin) เก็บไว้ rewire | Sprint 6 |
| `RadiationManager` · Editor `RadiationSetup` · Tests `RadiationManagerTests` | รังสีเป็น **ต่อคน** ใน WorkerManager tick (rad_mine/core/zoneb + suit ×0.4) — มิเตอร์เมืองถูกตัด | Cutover Sprint 1 |
| `OreDepositManager` (556) · `Data/OreDeposit*` · Editor `OreDepositSetup` · Tests `OreDepositTests` | v6.3 ไม่มีโหนดแร่/เดินขุด — เหล็กมาจากอาคาร Mine (`mineWorkers×14`) · Tritium มาจากอาคาร Zone B (`ZoneB/ZoneBController`) | Sprint 5 (ดูข้อตัดสินใจ #3) |
| `Utils/GamePhase.cs` · Tests `GamePhaseTests` | `Core/PhaseManager` — เฟสผูก core% 40/60/80 (day-based ผิดกติกา 1) | Sprint 2–3 (เล็ก ทำก่อนได้) |
| `Managers/ResearchManager.cs` (เก่า 3 โปรเจกต์) · `UI/LabPanelUI` (451) · Editor `LabUISetup` · Tests `ResearchManagerTests` | `Research/` (✅มีแล้ว) + ResearchQueuePanel(✅) | **Cutover Sprint 2** |
| `PowerGridManager` · `PowerGridVisual` · PowerConduit ใน BuildingData · Editor `PowerGridSetup` | v6.3 ไฟเป็น pool + สูตร draw — ไม่มีระบบสายไฟเชิงพื้นที่ | ดูข้อตัดสินใจ #1 |
| `UI/InventoryGridUI` (635 เก่า) · `Managers/InventoryManager`(เก่า ถ้าซ้ำกับตัว Inventory/) · Editor `InventorySetup`(ชุดไอเทมเก่า) | `Inventory/UI/InventoryPanel` (✅) + ItemSO ชุดใหม่ 8 ตัว | Cutover Sprint 1 |
| Editor `HospitalSetup` · Tests `HospitalTests` | Med Bay (อาคารใหม่ heal 4, rad −25/−35) | Sprint 3–4 |
| `Systems/ConstructionController` · `UI/BuildingQueueUI` · Tests `ConstructionWorkerGateTests` | §15 วางแล้วจ่ายเลย ไม่มีคิวก่อสร้างในสเปก | ดูข้อตัดสินใจ #2 |

---

## 5) ➕ ต้องสร้างใหม่ (ตามโครง §31 — ยังไม่มีในโปรเจกต์เลย ยืนยันด้วยการสแกน)

| โฟลเดอร์ | ไฟล์ | Sprint |
|---|---|---|
| `Mastery/` | `MasteryBonusSO.cs` · `MasteryRegistry.cs` (singleton, 6 ระบบ query ตรงได้: Reactor/Worker/MedBay/Farm/FoodStorage/ZoneB) | 3 |
| `Codex/` | `CodexEntrySO.cs` (11 ใบ 1:1 ควิซ) · ปรับ `CodexManager` · `UI/CodexPanel.cs` | 3 |
| `Cards/` | `CrisisCardSO.cs` · `CardOption.cs` (มี isLocked) · `CardManager.cs` · `UI/CrisisCardPanel.cs` (โชว์ 🔒 + "ต้องวิจัย X ก่อน") | 4 |
| `Barks/` | `BarkSO.cs` · `BarkManager.cs` (62 บรรทัด, max 2/วัน, priority/cooldown/onceOnly) | 5 |
| `Records/` | `DataRecovery.cs` (rate 14 + idle×10, target 100 → ปลด Record ถัดไป) | 5 |
| `ZoneB/` | `ZoneBController.cs` (tritium 3.0→8.0, หมุนคนออกที่ rad>32) · `RadSuitManager.cs` (stock 5, ×0.4) | 5 |
| `Storm/` | `StormSystem.cs` (pressure สะสม, storm_heat 40) · `SensorArray.cs` (เผย gauge + heat_room +4) | 6 |
| `Core/` | `PhaseManager.cs` (core% 40/60/80) · `ReactorController.cs` (§26 + SCRAM §8) · `BuildingManager.cs`/`BuildPlacementManager.cs` (ถ้าจะ rename ตาม §31 — หรือคง BuildingRegistry/PlacementController เดิมแล้ว map ชื่อใน proposal) | 3–6 |
| Editor ใหม่ | `CardsSetup` (8 ใบจาก CARDS.md) · `BarksSetup` (62 จาก BARKS.md) · `CodexSetup v6.3` (11 จาก CODEX.md) · `QuizzesSetup v6.3` (11 จาก QUIZZES.md) · `StormSetup` ฯลฯ | ตาม sprint |
| Tests ใหม่ | Mastery/Cards(ตัวเลือกล็อก)/Barks(cap 2)/Storm/DataRecovery/ZoneB/Reactor(สูตร §26 + บั๊ก 18 ข้อเป็น regression) | ตาม sprint |

---

## 6) ⚠ จุดที่ต้องตัดสินใจ (สเปกไม่ครอบ/ขัดของเดิม — ควรระบุใน proposal ว่าเลือกทางไหน)

1. **Power Grid + Conduit** — v6.3 ไม่มีระบบสายไฟเชิงพื้นที่ (ไฟเป็น pool) → ตัดทิ้ง หรือคงเป็น visual/flavor? (ตัด = ลบ 3 ไฟล์ + อาคาร Conduit)
2. **คิวก่อสร้าง (ConstructionController)** — สเปก §15 วางแล้วเสร็จทันที → ตัดคิวก่อสร้าง+คนเดินไปสร้าง หรือคงไว้เป็น house-rule? (คงไว้ = ต้องระวังไม่ชน re-staff ของ Research §19)
3. **โหนดแร่เดินขุด (OreDeposit)** — สเปกให้เหล็กมาจากอาคาร Mine → ตัดระบบโหนด+เดินขุด หรือคงเป็น visual ของ Mine?
4. **แถบ Zone B บนแมพ + รั้ว (ZoneBarrierRenderer/IsoGroundPainter)** — v6.3 Zone B เป็น "อาคาร" ไม่ใช่โซนแมพ → คงรั้ว/พื้นสีน้ำตาลเป็นฉากประกอบ (แนะนำ: คงไว้ เพราะ visual ล้วน ไม่ชนกติกา) หรือรื้อ
5. **จังหวะ cutover** — ระบบเก่า-ใหม่อยู่คู่กันตอนนี้ เกมจริงยังรันระบบเก่า: ต้องกำหนดวันสลับ (แนะนำ: cutover Workers/Research ก่อน Sprint 3 เพื่อไม่ต้อง maintain คู่)

---

## 7) สรุปสำหรับเขียน proposal (ย่อหน้าพร้อมใช้)

> โครงสร้างโค้ดแบ่ง 3 ชั้น: **(1) ชั้นรากฐานที่คงที่แล้ว** — isometric grid 43×43 (คณิตผ่าน unit test), กล้อง/อินพุต, ระบบเรนเดอร์ depth-sort, TimeManager แบบ pause-reason stack, EventManager (Observer pattern) และ UI infrastructure — รวม ~45 ไฟล์; **(2) ชั้นระบบเกมตาม GDD v6.3 ที่พัฒนาเสร็จ** — GameConfigSO (ค่าเกมทั้งหมดจุดเดียว), ระบบคนงานรายบุคคล + กะพัก (Worker/ShiftSystem), Hope Ledger + แผง breakdown, Research Queue (แล็บเริ่มพัง/soft trigger ผูกสถานะ), แผงคลัง 6 แท็บ และ MetaProgress ข้ามรอบเล่น — พร้อมชุดเทสต์อัตโนมัติ 49 ไฟล์ (EditMode + headless 30-day simulation); **(3) ชั้นที่อยู่ระหว่างพัฒนา (Sprint 3–6)** — Mastery/Codex quiz, Crisis Cards 8 ใบ (ตัวเลือกล็อกจนกว่าจะวิจัย), Bark system, Storm + Sensor Array, Zone B + Rad Suit, Data Recovery และ ReactorController สูตรใหม่ ซึ่งทั้งหมดขับด้วยค่าจาก CONFIG.md ที่ผ่านการ validate ด้วย simulation 3,000 รอบ

---

*เอกสารนี้สร้างจากการสแกนโค้ดจริง + GDD v6.3 — อัปเดตเมื่อ cutover/สร้างไฟล์ใหม่แล้วให้แก้สถานะในตารางตาม*
