# Prompts — 3 ระบบใหม่ (Inventory · Research Lab · Building Status)

สร้างจาก GDD `FINAL NUCLEAR ReMind V4` (§4 ทรัพยากร · §6 อาคาร · §13 ไอเทม & คราฟต์) + สถาปัตยกรรมจริงของโปรเจกต์
วิธีใช้: แปะ **"บริบทร่วม"** ด้านล่างนำหน้า แล้วตามด้วย prompt ของระบบที่จะทำ (ทีละระบบ)

---

## บริบทร่วม (แปะหัวทุก prompt)

```
คุณกำลังพัฒนา "NUCLEAR Re:Mind" เกม isometric edu-survival city-builder ใน Unity 6 (6000.3.6f1), C#.
ยึด GDD ไฟล์ "Assets/Docs/Final Plan/FINAL NUCLEAR ReMind V4.md" เป็นสเปกหลัก (ตัวเลขทั้งหมดมาจากที่นี่)

กฎสถาปัตยกรรมที่ห้ามผิด (มีใน CLAUDE.md):
1. โค้ดเกมทั้งหมดอยู่ assembly เดียว NuclearReMind.asmdef · namespace NuclearReMind ทุกคลาส
2. Manager = MonoBehaviour singleton (public static Instance, ตั้งใน Awake, Destroy ตัวซ้ำ)
3. ★ ระบบห้ามเรียก method ของ manager อื่นข้ามกันตรง ๆ — สื่อสารผ่าน EventManager.Instance เท่านั้น
   (subscribe += ใน OnEnable, -= ใน OnDisable, กัน OnDisable ด้วย `if (EventManager.Instance == null) return;`)
   ★ ยกเว้น: read-only query (อ่านค่า .Current / GetX()) ข้าม manager ได้ ห้ามเฉพาะ method ที่เปลี่ยนสถานะ
4. ค่าเกม (cost/production/เวลา) ต้องอยู่ใน ScriptableObject เท่านั้น ห้าม hardcode ในโค้ด manager
5. แยก "ตรรกะล้วน" (คำนวณ, ไม่มี Unity scene) เป็น static class ต่างหากให้ EditMode test ตรวจได้
   (ดูแพทเทิร์นที่มีอยู่: OreMath, WorkerSeparation, IsoGroundPainter, StatCondition, CrisisSchedule)
6. SaveData & struct ลูก: เพิ่ม field ใหม่ต้องมี default เสมอ (เซฟเก่าต้อง deserialize ได้)
7. ห้าม GameObject.Find()/FindObjectOfType() ใน Update() — cache ใน Awake

โฟลเดอร์: Scripts/Managers · Scripts/Systems · Scripts/Data · Scripts/UI · Scripts/Narrative
          Tests/EditMode (NUnit) · Editor (setup scripts) · ScriptableObjects/…

แพทเทิร์น Editor setup script (สำคัญ — โปรเจกต์นี้ใช้ทุกฟีเจอร์):
- แต่ละฟีเจอร์มี [MenuItem("NuclearReMind/…")] ที่ idempotent (รันซ้ำได้ผลเท่าเดิม, find-or-create, save)
- สร้าง/wire อาคาร-UI-asset ให้อัตโนมัติ แล้วลงทะเบียนใน Assets/Editor/RunAllSetups.cs (MenuOrder)

การทดสอบ/คอมไพล์ (ต้องปิด Unity Editor ก่อน batch):
- คอมไพล์:  Unity.exe -batchmode -quit -projectPath <proj> -logFile compile.log  → grep "error CS"
- EditMode: Unity.exe -batchmode -runTests -testPlatform EditMode -testResults results.xml
เขียน EditMode test คู่กับตรรกะล้วนทุกครั้ง

สิ่งที่ "มีอยู่แล้ว" (ต่อยอด ห้ามสร้างซ้ำ):
- ResourceManager        — คลัง 6 ชนิด (Energy/Water/Food/Iron/Deuterium/Tritium) + Hope + Knowledge + เพดาน
  ใช้จ่าย/รับผ่าน event: EventManager.RaiseResourceDelta(ResourceType, amount) · query: ResourceManager.Instance.Current
- BuildingData (SO)      — ค่าอาคารทุกหลัง (cost/production/workerRequired/size/buildingType/requiredClass ฯลฯ)
- BuildingRegistry       — ทะเบียนอาคารที่วางแล้ว: PlacedBuildings[cell] → BuildingData · GetLevel(cell)
- ConstructionController  — คืบหน้าการสร้างต่ออาคารทีละ tick (progress ตามจำนวนคนงานประจำ) — ★ ใช้เป็นต้นแบบระบบ "งานที่กินเวลา"
- WorkerAssignmentManager — คนงานประจำอาคาร: GetAssigned(cell), EffectiveCap(cell,data), OnConstructionComplete
- BuildingUpgradeUI      — แผง hover เหนืออาคาร (ชื่อ/ระดับ/ผลิต/คนงาน/ปุ่มอัป) — จุดต่อยอด UI สถานะ
- QuizManager/CodexManager/Knowledge — ระบบ "การเรียนรู้" (ควิซ/สารานุกรม) — แยกจาก "research project" ของโรงวิจัย
- EventManager          — event bus กลาง (ดู Managers/EventManager.cs สำหรับรายการ event + เมธอด Raise*)
- SaveManager/SaveData  — เซฟ/โหลด (มี placedBuildings, workerAssignmentCells, construction ฯลฯ)

ยังคอมไพล์/รันจริงในเครื่องนี้ไม่ได้ (Unity Editor เปิดค้าง) — ส่งงานเป็นโค้ด + test + editor setup พร้อมรัน
```

---

## Prompt 1 — ระบบ Inventory (ไอเทมคราฟต์ · GDD §13)

```
สร้าง "ระบบ Inventory" — คลังไอเทมที่คราฟต์/ผลิต แยกจากคลังทรัพยากรบัลก์ (ResourceManager)

สเปกจาก GDD §13 (ไอเทม & คราฟต์) — ไอเทมคือ "ของที่คราฟต์เพื่อดัน Q หรือพลิกวิกฤต ผลิตที่ห้องวิจัย/โรงพยาบาล":
| ไอเทม | ประเภท | ผลิตจาก | ใช้ทำอะไร |
| Rad-Gear | อุปกรณ์ | Iron + วิจัย | กันคนป่วย/ตายเมื่อเข้าพื้นที่รังสี |
| เครื่องสแกน PET/SPECT | การแพทย์ | โรงพยาบาล + พลังงาน | วินิจฉัย (วิกฤต 2·A) |
| ไอโซโทปการแพทย์ | การแพทย์ | วัสดุแล็บ + เตา Idle 1 วัน | รักษาหายสนิท (วิกฤต 2·B) |
| เมล็ดพันธุ์ฉายรังสี | เกษตร | ห้องวิจัย + Iron 250 | เพิ่มผลผลิตอาหารยาว (วิกฤต 3·A) |
| เครื่องฉายโคบอลต์-60 | ถนอมอาหาร | โรงงาน + Energy 300 | อาหารเน่า → 0% (วิกฤต 3·B) |
| น้ำหล่อเย็นฉุกเฉิน | ฉุกเฉิน | น้ำสะอาด | ลด HEAT เตาเร่งด่วน (วิกฤต 1·C) |
(Deuterium/Tritium ยังเป็น "ทรัพยากร" ใน ResourceManager เหมือนเดิม ไม่ย้ายมา Inventory)

สร้าง:
1. Data/ItemSO.cs — ScriptableObject ต่อไอเทม 1 ชนิด:
   id, displayName, description, Sprite icon, ItemCategory (Equipment/Medical/Agri/Emergency/Fuel),
   ResourceCost craftCost (ใช้ struct/แนว ResourceCost ของโปรเจกต์: ResourceType+amount หลายตัว),
   BuildingType craftedAt (ห้องวิจัย/รพ./โรงงาน), int craftTicks (เวลาผลิต · 0 = ทันที),
   bool requiresResearch (ต้องปลดวิจัยก่อนถึงคราฟต์ได้ — เชื่อมกับ ResearchManager ระบบที่ 2),
   int maxStack (เพดานถือครอง · 0 = ไม่จำกัด)
2. Data/InventoryData.cs — [Serializable] struct/คลาสเก็บ (itemId → count) สำหรับ save (ทุก field มี default)
3. Managers/InventoryManager.cs — singleton:
   - เก็บ Dictionary<string,int> counts (key = ItemSO.id)
   - public query: GetCount(id), CanAfford/HasItem — read-only
   - รับคำสั่งผ่าน event: OnCraftItemRequested(itemId) / OnUseItemRequested(itemId)
   - คราฟต์: ตรวจ craftCost ผ่าน ResourceManager.Current → ถ้าพอ Raise ResourceDelta ลบต้นทุน → เพิ่มไอเทม
     (ถ้า craftTicks>0 ให้เข้า "คิวคราฟต์" แบบเดียวกับ ConstructionController: คืบต่อ tick แล้วค่อยได้ของ)
   - ใช้ไอเทม: ลด count แล้ว Raise event ผลของไอเทม (เช่น OnEmergencyCoolantUsed → CoreTowerManager ลด HEAT)
   - เพิ่ม event ใหม่ใน EventManager: OnItemCrafted(id), OnItemUsed(id), OnInventoryChanged, OnCraftItemRequested, OnUseItemRequested
   - subscribe OnSaveLoaded → คืนค่า · export ให้ SaveManager
4. Systems/InventoryMath.cs (หรือ CraftQueue) — ตรรกะล้วน pure ให้เทสต์: ตรวจ affordability, add/remove + clamp maxStack, คืบหน้าคิวคราฟต์
5. SaveData: เพิ่ม field inventory (มี default = ว่าง) + wire ใน SaveManager
6. UI: แผง Inventory (เปิด/ปิดด้วยปุ่ม/hotkey) โชว์ไอเทม+จำนวน+ปุ่มคราฟต์/ใช้ · ปุ่ม disabled ตอนทรัพยากรไม่พอ (แนว BuildingUpgradeUI)
7. Editor/InventorySetup.cs — [MenuItem] idempotent: สร้าง ItemSO ทั้ง 8 ชนิดตามตาราง §13 (ค่าใน asset) + สร้าง/wire แผง UI + ลงทะเบียนใน RunAllSetups
8. Tests/EditMode/InventoryMathTests.cs — คราฟต์ได้เมื่อพอจ่าย/ไม่ได้เมื่อไม่พอ · maxStack clamp · คิวคราฟต์คืบตาม tick · deterministic

จุดตัดสินใจ (เลือกให้ผมด้วย หรือถามก่อนถ้าไม่ชัด):
- ไอเทมมีเพดานถือครองไหม (maxStack) หรือถือได้ไม่จำกัด?
- คราฟต์ใช้เวลา (ticks) หรือได้ทันที? GDD บอกบางอย่างใช้ "เตา Idle 1 วัน" → ต้องผูกเงื่อนไขพิเศษ
- ต้องมีอาคารที่ถูกต้อง (craftedAt) วางอยู่ + คนงานประจำครบ ถึงคราฟต์ได้ ใช่ไหม?

เกณฑ์ยอมรับ: คราฟต์ Rad-Gear จ่าย Iron ถูกต้อง, ทรัพยากรไม่พอคราฟต์ไม่ได้, ไอเทมเซฟ/โหลดคงอยู่,
ใช้ "น้ำหล่อเย็นฉุกเฉิน" แล้ว HEAT เตาลดจริง (ผ่าน event ไม่เรียก CoreTowerManager ตรง ๆ), test เขียว
```

---

## Prompt 2 — ระบบโรงวิจัย (Research Lab · GDD §6/§13)

```
สร้าง "ระบบโรงวิจัย (Research Lab)" — สถานีวิจัยที่ปลดล็อกเทค/สูตรคราฟต์ + งานวิจัยที่กินเวลา

สเปกจาก GDD §6: ห้องวิจัย (Research Lab) = "ปลดล็อกเทค · ฝึก Engineer · สกัด Deuterium · วิจัยยา/เมล็ดพันธุ์"
  คน 2 (engineer) · สร้าง Iron 80 + Energy 120 · ค่าเดินระบบ Energy 20/วัน · ปลดล็อก Phase 2
สเปก §13: ไอโซโทปการแพทย์/เมล็ดพันธุ์ฉายรังสี/Rad-Gear ต้อง "วิจัย" ก่อนถึงผลิตได้ที่ห้องวิจัย

★ แยกให้ชัด: "research project" ของระบบนี้ = งานวิจัยปลดล็อกสูตร/โบนัส (คนละอันกับ Knowledge/Quiz/Codex ที่เป็นระบบเรียนรู้)
★ อาคาร Laboratory มี asset อยู่แล้ว (Assets/ScriptableObjects/Buildings/Laboratory.asset) — ต่อยอด ไม่สร้างอาคารใหม่
★ การฝึก Engineer มีอยู่แล้ว (BuildingData.unlocksEngineerTraining + PopulationManager) — ระบบนี้เพิ่มเฉพาะ "งานวิจัย"

สร้าง:
1. Data/ResearchProjectSO.cs — ScriptableObject ต่อโครงการวิจัย:
   id, displayName, description, ResourceCost cost, int researchTicks (เวลาวิจัย),
   string[] prerequisiteIds (ต้องเสร็จก่อน), ResearchReward reward
   โดย reward ปลดล็อกได้ 1 ใน: ItemSO recipe (เชื่อมระบบ Inventory), building upgrade/bonus, tech flag
2. Managers/ResearchManager.cs — singleton:
   - เก็บ HashSet<string> completedProjects + โครงการที่กำลังวิจัย (คิวต่อห้องวิจัย)
   - รับ OnStartResearchRequested(projectId, labCell) ผ่าน event → ตรวจ prereq + cost (ResourceManager.Current) → หักต้นทุน (RaiseResourceDelta) → เข้าคิว
   - คืบหน้าต่อ tick แบบ ConstructionController: ความเร็ว = จำนวน Engineer ประจำห้องวิจัยนั้น (WorkerAssignmentManager.GetAssigned) · 0 คน = ไม่คืบ
   - เสร็จ → completedProjects.Add(id) → Raise OnResearchCompleted(id) (ปลดสูตร Inventory / โบนัส)
   - public query: IsCompleted(id), IsUnlocked(itemOrTechId) — read-only ให้ InventoryManager/UI เช็ก
   - เพิ่ม event: OnStartResearchRequested, OnResearchProgressChanged(id,progress), OnResearchCompleted(id)
   - subscribe OnSaveLoaded → คืน completed + คิว
3. Systems/ResearchQueue.cs — ตรรกะล้วน pure: ตรวจ prereq ครบ, คืบหน้าตามจำนวน engineer, ลำดับความสำคัญ, gate "ต้องมีห้องวิจัยที่คนครบ"
4. SaveData: field completedResearchIds + activeResearch (default ว่าง)
5. UI: แผงโรงวิจัย (เปิดเมื่อคลิกอาคาร Laboratory) — ลิสต์โครงการ (ล็อก/พร้อม/กำลังวิจัย%/เสร็จ) + ปุ่มเริ่มวิจัย + แถบคืบหน้า
6. Editor/ResearchSetup.cs — [MenuItem] idempotent: สร้าง ResearchProjectSO ตามไอเทม §13 (เช่น "วิจัย Rad-Gear", "วิจัยไอโซโทปการแพทย์", "วิจัยเมล็ดพันธุ์ฉายรังสี") + wire แผง UI เข้าอาคาร Laboratory + RunAllSetups
7. Tests/EditMode/ResearchQueueTests.cs — prereq gate, ไม่มี engineer ไม่คืบ, เสร็จแล้วปลดสูตร, เซฟ/โหลด completed, deterministic

จุดตัดสินใจ:
- วิจัยได้ทีละโครงการต่อห้องวิจัย หรือขนานหลายโครงการ? (แนะนำ: ทีละโครงการต่อห้อง แบบคิว ConstructionController)
- ความเร็ววิจัย = จำนวน engineer ประจำ (แบบ ConstructionController) หรือคงที่ต่อวัน?
- research project ผูกกับ "ปลดสูตรคราฟต์ใน Inventory" เป็นหลักใช่ไหม (แนะนำ: ใช่ — ทำระบบ 1 ก่อนแล้วเชื่อม)

เกณฑ์ยอมรับ: เริ่มวิจัยหักทรัพยากรถูก, ไม่มี engineer ประจำห้องวิจัยแล้วไม่คืบ, เสร็จแล้ว InventoryManager คราฟต์ไอเทมนั้นได้,
prereq บังคับลำดับ, เซฟ/โหลดคงสถานะ, test เขียว
```

---

## Prompt 3 — ระบบ Status ของแต่ละ Building (GDD §6)

```
สร้าง "ระบบ Status อาคาร" — สถานะการทำงานเรียลไทม์ต่ออาคาร + ตัวบ่งชี้บนแมพ

ที่มา: GDD §6 บอก "ถ้าคลังไม่พอจ่าย consumption → อาคารหยุดผลิตวันนั้น (idle)" และอาคารมีหลายภาวะ
ผู้เล่นต้องเห็นได้ทันทีว่าอาคารไหน "ทำงาน / ขาดคน / ขาดทรัพยากร / กำลังสร้าง / ควรอัป"

สถานะที่ต้องมี (enum BuildingStatus):
- UnderConstruction  — กำลังสร้าง (ConstructionController.IsUnderConstruction == true)
- NoWorker           — ยังไม่มีคนงานประจำ (GetAssigned < workerRequired ที่จำเป็น)
- Starved            — คนครบแต่คลังไม่พอจ่ายค่าเดินระบบ/วัน → หยุดผลิต (§6)
- Operational        — เดินเครื่องปกติ ผลิตได้
- Idle               — อาคารที่ผู้ใช้สั่งหยุด / เตา Idle (ถ้ามี)
(ปรับ/เพิ่มได้ตามจริง เช่น Researching/Crafting ถ้าเป็นห้องวิจัย/สถานีคราฟต์)

สร้าง:
1. Systems/BuildingStatusEvaluator.cs — ★ ตรรกะล้วน pure (หัวใจของงานนี้ ให้เทสต์ครอบ):
   static BuildingStatus Evaluate(bool underConstruction, int assigned, int workerRequired,
                                  bool canAffordUpkeep, bool manuallyIdle)
   ลำดับความสำคัญ: UnderConstruction > NoWorker > Starved > Idle > Operational (กำหนดให้ชัดและเทสต์)
2. Managers/BuildingStatusManager.cs — singleton (หรือ System):
   - คำนวณสถานะต่ออาคารจากข้อมูลที่มี (read-only query ข้าม manager: ConstructionController.IsUnderConstruction,
     WorkerAssignmentManager.GetAssigned, ResourceManager.Current เทียบ upkeep จาก BuildingData)
   - อัปเดตเมื่อ event ที่เกี่ยวข้องยิง (OnBuildingPlaced/Removed, OnWorkerAssignmentChanged, OnConstructionComplete,
     OnResourceChanged, OnDayEnded) — ห้าม recompute ใน Update ทุกเฟรม
   - Raise OnBuildingStatusChanged(cell, status) เมื่อสถานะเปลี่ยน
   - public query: GetStatus(cell)
3. Systems/BuildingStatusView.cs (+ spawner) — ไอคอน/สีบ่งชี้เหนืออาคาร (แนวเดียวกับ BuildingVisualSpawner):
   ⛏ กำลังสร้าง · 👷 ขาดคน (เหลือง) · ⚠ ขาดทรัพยากร (แดง) · ✓/ไม่มีไอคอน = ปกติ
   sprite gen ในโค้ด/atlas · sortingLayer "Buildings" · sortingOrder เหนือตัวอาคาร
4. ต่อยอด BuildingUpgradeUI: โชว์ข้อความสถานะในแผง hover (เช่น "⚠ ขาดพลังงาน — หยุดผลิต")
5. Editor/BuildingStatusSetup.cs — [MenuItem] idempotent: สร้าง/wire StatusManager + View spawner ในซีน + RunAllSetups
6. Tests/EditMode/BuildingStatusEvaluatorTests.cs — ครบทุกสถานะ + ลำดับความสำคัญ
   (เช่น กำลังสร้างต้องชนะทุกอย่าง, คนครบแต่จ่าย upkeep ไม่ไหว = Starved, ปกติ = Operational)

หมายเหตุ integration:
- "ขาดคน" ใช้เกณฑ์เดียวกับ WorkerAssignmentManager (บางอาคาร workerRequired=0 เช่น Habitat = ไม่ถือว่าขาดคนตอนเดินเครื่อง)
- "ขาดทรัพยากร" = คลังจ่าย energyConsumption/waterConsumption ต่อวันของ BuildingData ไม่ไหว
- ระวัง event loop: อัปเดตสถานะเมื่อ resource เปลี่ยน อย่าให้ยิง event ที่ทำ resource เปลี่ยนซ้ำ

จุดตัดสินใจ:
- แสดงเป็นไอคอนลอยเหนืออาคารเสมอ, เฉพาะตอน hover, หรือเฉพาะสถานะที่ต้อง action (ขาดคน/ขาดของ)?
  (แนะนำ: โชว์ไอคอนถาวรเฉพาะสถานะที่ต้อง action + รายละเอียดเต็มในแผง hover)
- Starved เช็กแบบ "จ่าย upkeep วันนี้ไม่ไหว" ตอนจบวัน หรือประเมินล่วงหน้าจากคลังปัจจุบัน?

เกณฑ์ยอมรับ: วางอาคารใหม่ = UnderConstruction, สร้างเสร็จไม่มีคน = NoWorker, จัดคนครบ+ทรัพยากรพอ = Operational,
ทำพลังงานติดลบ/ไม่พอ upkeep = Starved มีไอคอนแดง, test ครอบทุกสถานะเขียว
```

---

## ลำดับที่แนะนำให้ทำ

1. **Inventory** ก่อน (เป็นฐาน — Research ปลดสูตรให้มันคราฟต์)
2. **Research Lab** (ผูกกับ Inventory: วิจัยเสร็จ → ปลดสูตรคราฟต์)
3. **Building Status** (อิสระ ทำเมื่อไรก็ได้ — พึ่งระบบที่มีอยู่แล้วเป็นหลัก)

ทั้ง 3 ระบบเชื่อม ConstructionController เป็นต้นแบบ "งานที่กินเวลาต่ออาคารตามจำนวนคนงาน" — โครงเดียวกัน ลดโค้ดซ้ำได้
```
