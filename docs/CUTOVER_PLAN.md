# CUTOVER_PLAN.md — legacy → v6.3

> เป้าหมาย: ทำให้ **เกมจริง (scene + build)** วิ่งบนระบบ v6.3 ทั้ง 6 sprint แทน legacy
> โดยไม่พังกลางทาง — ทำทีละ slice, ผ่าน compile + test ก่อนไป slice ถัดไป
> เขียนจากการวัด dependency จริง 2026-07-16 (blast radius ด้านล่างเป็นของจริง ไม่ใช่ประมาณ)

---

## 0. หลักการ (อ่านก่อนเริ่มทุก slice)

1. **Rewire ก่อน Delete** — ห้ามลบ manager เก่าก่อน reroute reference ครบ (ลบก่อน = คอมไพล์พังเป็นร้อยจุด)
2. **ทีละ vertical slice** — 1 ระบบ/รอบ, จบด้วย compile ผ่าน + เทสเขียว + เล่นทดสอบได้
3. **Scene เป็นเจ้าของ v6.3 managers** — เป้าหมายคือย้าย `AddComponent` ออกจาก `Sprint1TestPanel` ไปวางเป็น GameObject จริงใน`Gamescene.unity` (แก้ปัญหา "release build ไม่มี v6.3" + "Lab ไม่พัง")
4. **Adapter/Shim = อาวุธลดความเสี่ยง** (ดู §2) — ให้เกมวิ่ง v6.3 ได้เร็วโดยยังไม่ต้องรื้อ UI ทั้งหมด
5. **กติกา CLAUDE.md ยังบังคับ** — ไม่ hardcode เลข, mutation ผ่าน EventManager, ไม่มี `if(day==X)` ยกเว้น 30

---

## 1. Blast radius (โค้ด+เทส+ซีน — ไม่รวม docs)

| legacy manager | v6.3 แทน | อ้างจาก | ระบบที่พันด้วย (ไม่มี v6.3 แทนตรงๆ) |
|---|---|---|---|
| ResearchManager | ResearchLab | **13 ไฟล์ / 37** | LabPanelUI, CoreTowerPanelUI, Save |
| Dilemma/Decree/Crisis/CrisisEffect | CardManager | **23 ไฟล์ / 52** | Story, Save, RadiationManager, UI |
| CodexManager | CodexQuizManager | **33 ไฟล์ / 71** | CodexUIController, RecordsPanel, Save, Story |
| QuizManager | CodexQuizManager | **35 ไฟล์ / 78** | QuizPopup UI, Story/DilemmaData, HUD |
| PopulationManager | WorkerManager | **48 ไฟล์ / 93** | ResourceManager, WorkerAssignment, Hospital UI |
| CoreTowerManager | ReactorController | **47 ไฟล์ / 152** | HUD (CoreTowerUI/Panel), Story, CrisisEffect, Save, **30 เทส** |

**3 ระบบไม่มี v6.3 มารับ = งานหลักของ cutover:**
- **HUD/UI** — v6.3 มี panel จริงแค่ Research/Hope/Inventory · Reactor/Cards/Codex/Records ยังเป็น F9 / static formatter
- **Story/Narrative** (StoryDirector, DilemmaManager, CrisisSchedule, Beat_*.asset) — อ่าน `CoreTowerManager.Q` ตรงๆ
- **Save/Scene** — SaveData serialize state เก่า, Gamescene ผูก component + serialized ref

---

## 2. Adapter/Shim strategy (แนะนำ — ลด blast radius ก่อนลุย)

แทนที่จะ "ลบ CoreTowerManager แล้วแก้ UI 47 ไฟล์พร้อมกัน" → ทำ manager เก่าให้เป็น **facade บางๆ ที่ delegate ไป v6.3**:

```
// ตัวอย่างแนวคิด — CoreTowerManager กลายเป็น shim
public float Q => ReactorController.Instance != null
    ? ReactorController.Instance.Core / GameConfigSO.Instance.coreWin * 100f
    : _legacyQ;   // fallback ระหว่าง migrate
```

ผลลัพธ์:
- เกม + UI + Story เดิม **ยังคอมไพล์และวิ่งได้** แต่ตัวเลขมาจาก v6.3 แล้ว
- ย้าย logic ไป v6.3 ได้ทันทีโดยไม่ต้องแตะ UI 47 ไฟล์ในรอบเดียว
- ค่อยๆ ลบ shim ทีละตัวเมื่อ migrate UI ของระบบนั้นเสร็จ

**ลำดับที่แนะนำ:** shim ให้ scene วิ่ง v6.3 logic ก่อน (value เร็ว) → แล้วค่อยไล่ลบ shim ทีละ slice

---

## 3. Decisions — ✅ เคาะแล้ว 2026-07-16 (อ้าง GDD.md จริง)

| # | คำถาม | ผล | หลักฐาน | → ลบ |
|---|---|---|---|---|
| D1 | PowerGrid/Conduit วางท่อ? | **✂️ ตัด** | ไม่มี conduit/grid ใน GDD · Power = resource, `power += powerWorkers×45` (§260), enum ResourceType (§451) | `PowerGridManager` |
| D2 | construction queue? | **✂️ ตัด** | job list ไม่มี "construction" (§206/§603) · วางอาคารทันที + Placement Pause §15 | `ConstructionController` |
| D3 | ore-node เดินขุด? | **✂️ ตัด** | "❌ Zone A ตัดทิ้ง" (§290) · เหมือง = `iron += mineWorkers×14` abstract | `OreDepositManager` |
| D4 | Zone B strip+fence? | **✂️ ตัด strip เก่า** | v6.3 Zone B = อาคารปลด Phase 4 (§278/§304) ไม่ใช่แถบตายตัว · logic จาก ZoneBController | strip visual เก่า |
| D5 | Story เก่า? | **✂️ ตัด** | user ทำ story ใหม่แล้ว · ใส่หลัง cutover | `StoryDirector`/`DilemmaManager`/beats |

> ทั้ง 5 = ตัดตาม v6.3 · Reactor slice ยังใช้ **shim** `Q`→`Core` ชั่วคราวระหว่างย้าย HUD (ดู §2)
> ★ Story: **ตัดของเก่า** (ไม่ rebind content) — เนื้อเรื่องใหม่ที่ user ทำจะใส่หลัง cutover เสร็จ

---

## 4. แผนต่อ slice (เรียง blast radius น้อย → มาก)

### Slice 1 — Research ⭐ นำร่อง (13 ไฟล์)
**ทำไมก่อน:** เล็กสุด + v6.3 มี `ResearchQueuePanel`/`NoteCardPopup` อยู่แล้ว → พิสูจน์ pattern เต็ม (scene-own + UI rebind + save + ลบ + ลบเทส) ได้เร็ว

- **Scene:** วาง `ResearchLab` เป็น GameObject จริง (เลิกพึ่ง F9 spawn)
- **แก้:**
  - `UI/LabPanelUI.cs` — `ResearchManager.Instance`/`ProjectSeeds…` → `ResearchLab` (queue + repair state)
  - `UI/CoreTowerPanelUI.cs` — ref ResearchManager ออก
  - `InventoryManager.cs` — จุดที่อ่าน ResearchManager
  - `SaveManager/SaveData` — serialize repair/queue ของ ResearchLab (มี default, เซฟเก่า deserialize ได้)
  - `Sprint1TestPanel` — เอา spawn ResearchLab ออก (ย้ายไป scene แล้ว)
- **ลบ:** `Managers/ResearchManager.cs`
- **ลบ/เขียนใหม่เทส:** `ResearchManagerTests`(9) → แทนด้วย `ResearchSystemTests` เดิมที่มีอยู่
- **Acceptance:** เริ่มเกม → คลิก Lab ในโลก → **Lab พังจริง** ต้องจ่ายเหล็กซ่อม (นี่คืออาการที่ผู้ใช้เจอ) · วิจัยเดินได้ · เซฟ/โหลดผ่าน

### Slice 2 — Quiz + Codex (35+33 ไฟล์ ร่วม)
- **Scene:** CodexQuizManager เป็น plain singleton (ไม่ต้อง GameObject) — แค่รับประกันว่ามีเมนู setup asset แล้ว
- **แก้:**
  - `UI/QuizPopupController.cs` + `UI/QuizExplanationPopupController.cs` → `CodexQuizManager.Submit` (ตอบผิดไม่มีโทษ, explanation เสมอ)
  - `UI/CodexUIController.cs` → `CodexQuizManager` (นับ x/11, locked ไม่ซ่อน)
  - `UI/UIManagerHUD.cs` — red dot ใช้ `HasNewQuiz`
  - Story/DilemmaData ที่อ้าง QuizManager — rebind หรือ shim
  - `SaveManager` — MasteryBank/UnlockedCodex (MetaProgress persist อยู่แล้ว)
- **ลบ:** `Managers/QuizManager.cs` · `Managers/CodexManager.cs` · `Data/CodexEntry.cs`(legacy)
- **ลบเทส:** `QuizManagerTests` · `CodexLabUnlockTests` · ส่วน quiz ใน `IntegrationFlowTests`
- **Acceptance:** ควิซอยู่ใน Codex เปิดเอง ไม่บังคับ · ตอบถูก→Mastery ถาวร · x/11 นับถูก

### Slice 3 — Workers / Population (48 ไฟล์) — ต้องเคาะ D1/D2/D3
- **Scene:** วาง `WorkerManager` + `ShiftSystem` เป็น GameObject จริง
- **แก้:** `ResourceManager` (per-person consumption), `WorkerAssignmentManager`, `Hospital`/UI, `BuildingSelectionUI`, `WorkerVisualSpawner`
- **ตัดตาม decision:** PowerGridManager(D1), ConstructionController(D2), OreDepositManager(D3)
- **ลบ:** `Managers/PopulationManager.cs` (+ manager ที่ตัดตาม D1-D3)
- **ลบเทส:** `PopulationManagerTests`(22), `ShelterCapTests`, `WorkerAssignmentTests`(เก่า), `OreDepositTests`, `ConstructionWorkerGateTests` — แทนด้วย `WorkerSystemTests`/`HopeSystemTests` เดิม
- **Acceptance:** รัน D1-30 → เห็นคนหิว/ป่วย/รังสีรายคน · output = Σ efficiency

### Slice 4 — Cards vs Dilemma/Decree/Crisis (23 ไฟล์) — ต้องเคาะ D5
- **Scene:** `CardManager` เป็น GameObject จริง
- **แก้:** `UI/DilemmaPopupController` → `UI/CrisisCardPanel` (จริง ไม่ใช่ formatter) · RadiationManager hooks · Story (rebind/migrate ตาม D5)
- **ลบ:** `DilemmaManager` · `DecreeManager` · `CrisisEffectManager` · `Data/DilemmaData` · `Data/CrisisChoiceEffects`
- **ลบเทส:** `DilemmaManagerTests`, `DecreeManagerTests`, `CrisisEffectManagerTests`, `CrisisEffectIntegrationTests`, `CrisisScheduleTests`
- **Acceptance:** การ์ดผูก state จริง · ตัวเลือกล็อก 🔒 โชว์ (กติกา #6)

### Slice 5 — Reactor 🔴 สุดท้าย/หนักสุด (47 ไฟล์, 30 เทส) — พึ่ง D5
- **Scene:** `ReactorController` + `StormSystem` + `SensorArray` + `EndingSystem` เป็น GameObject จริง
- **กลยุทธ์:** ใช้ **shim** (§2) — CoreTowerManager เหลือ facade delegate `.Q`→`.Core` → HUD/Story/CrisisEffect เดิมยังวิ่ง แล้วค่อยลบ shim เมื่อ migrate `CoreTowerUI`/`CoreTowerPanelUI` เป็น HEAT/CORE UI ใหม่
- **แก้:** `CoreTowerUI`(10) · `CoreTowerPanelUI`(17) · `UIManagerHUD` · `GameManager` (endings→`EndingSystem` ตัด logic D30 แบบ Q — บั๊ก #15) · `SaveData`
- **ลบ (หลังลบ shim):** `Managers/CoreTowerManager.cs`
- **ลบเทส:** `CoreTowerManagerTests`(30), ส่วน core ใน `GameManagerTests`/`IntegrationFlowTests`
- **Acceptance:** เตาโชว์ CORE/HEAT จริง · Method B gate ทำงาน · พายุมีผล · ชนะ core100 จบทันที · **รัน sim เทียบ: ไม่ตอบควิซ=แพ้ / ตอบ=ชนะ**

---

## 5. หลังครบ 5 slice (cleanup)
- ลบ `Sprint1TestPanel` spawn block ทั้งหมด (เหลือแค่ debug view ถ้าอยากเก็บ)
- ลบ Editor setup เก่า: `QuizSetup` `CodexSetup` `CrisisSetup` `DecreeSetup`
- ลบ Docs banner "superseded" ที่ไม่ใช้
- อัปเดต `CODE_STATUS_v6.3.md` → cutover done
- **Build จริง (ไม่ใช่ dev)** → ยืนยัน v6.3 วิ่งครบ (เพราะย้ายเข้า scene แล้ว ไม่ใช่ `#if DEVELOPMENT_BUILD`)

---

## 6. Risk register
| เสี่ยง | กัน |
|---|---|
| ลบ manager แล้ว serialized ref ในซีนขาด (missing script) | shim ก่อน · แก้ scene ก่อนลบ .cs |
| เซฟเก่าโหลดไม่ได้ | field ใหม่มี default เสมอ (กติกา SaveData) |
| day-end ทำงานซ้ำ (legacy + v6.3 subscribe OnDayEnded พร้อมกัน) | 1 slice = ปิด handler เก่าทันทีที่เปิด v6.3 |
| Story พังตอนแตะ Reactor | D5 = rebind ด้วย shim ไม่ใช่ลบ |
| เทสแดงยกแผง | ลบ/เขียนเทสใน slice เดียวกับที่ลบ manager |

---

## 7. สถานะ (อัปเดตเมื่อทำ)
- [ ] เคาะ D1–D5
- [ ] Slice 1 Research
- [ ] Slice 2 Quiz+Codex
- [ ] Slice 3 Workers
- [ ] Slice 4 Cards
- [ ] Slice 5 Reactor
- [ ] Cleanup + build จริง
