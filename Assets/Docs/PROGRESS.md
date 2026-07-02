# Progress — Nuclear Re:Mind
> อัปเดตล่าสุด: 2026-07-01 | แผนหลักปัจจุบัน: `Final Plan/DEV_PLAN_V4.md` (ยึด GDD V4) · แผนเดิม (v2.x): `Plan/TASKS.md`

---

## 🚀 อัปเดต 30 มิ.ย.–1 ก.ค. 2026 — ย้ายเข้า GDD V4 (เฟส 0–1 เสร็จ)

Spec หลักเปลี่ยนจาก GDD v2.1/v2.2 → **V4 (Developer Handoff Edition)**. โค้ดเดิมทำตาม v2.1/v2.2 จึงมีทั้ง refactor ย้อน + ระบบใหม่. แผนเต็ม + task ทุกเฟส: `Final Plan/DEV_PLAN_V4.md`

**เฟส 0 (30 มิ.ย.) — Refactor ฐานข้อมูล ✅ (25 ไฟล์)**
- ResourceData → 6 ทรัพยากร (Energy/Water/Food/**Iron**/**Deuterium**/**Tritium**) + **Knowledge**
- Morale: Hope/Despair (2 ค่า) → **Hope เดี่ยว** (เริ่ม 100) · ถอด Despair/riot/strike
- GameEndType → {Win, HopeZero, Meltdown, TimeoutLowQ} · material→Iron (`FormerlySerializedAs`)
- Codex ปลดด้วย event (ไม่จ่าย RP) · Knowledge สะสมอย่างเดียว · แก้ tests 5 ไฟล์

**เฟส 1 (1 ก.ค.) — Quiz + Knowledge ✅ (7 ใหม่ + 7 แก้)**
- ควิซ **10 ข้อ** (เนื้อหา §12 verbatim · Q4 ฉบับ 2-ขั้นล่าสุด) · `QuizManager` (queue/scoring +8/+3) · `QuizPopupController` (ตอบบังคับ+สีหมวด) · `QuizSetup` editor
- Knowledge threshold + `KnowBonus` (Expert ≥80 → +0.10 เข้าสูตรเตา) · HUD Knowledge bar
- ผูกควิซท้ายวิกฤต (Plasma→Q2,Q3 · Outbreak→Q4,Q5 · Food→Q6,Q7)
- ทำผ่าน multi-agent workflow + verify เอง (แก้ 2 blocker: static `GetById`, ลืมสร้าง QuizManager GameObject)

> ⚠️ เฟส 0-1 **รอ compile + tests ใน Unity** (แก้แบบ headless ไม่มี compiler) + รัน Setup 3 เมนูตามลำดับ: `Setup Crisis Dilemmas` → `Setup HUD Canvas` → `Setup Quiz System`

### 📊 ไทม์ไลน์ความคืบหน้าทั้งหมด (ตั้งแต่เริ่มโปรเจกต์)

| ช่วง | งานหลัก | สถานะ |
|------|---------|-------|
| ต้น มิ.ย. (Day 1–7) | รากฐาน: Grid isometric, Camera, Input, Placement, EventManager, BuildingData + tests | ✅ commit |
| กลาง มิ.ย. (Day 5–10) | Core managers: Resource, Population, CoreTower, Save + Codex 5 core entries | ✅ |
| 16–17 มิ.ย. | Demolition, Power Grid, Construction queue, gap analysis (vs Frostpunk) | ✅ |
| 25 มิ.ย. | เขียน GDD v2.1 (`CLAUDE_3`) + CODEX_CONTENT + TASKS (sprint plan) | ✅ |
| 26–27 มิ.ย. | **Block A** (gameplay loop) · **Block B** (CORE TOWER v2.1) · **Block C** (crisis trigger, 3 dilemma) · **Block D** (Codex 15 branch) | ✅ |
| 28 มิ.ย. | Hope/Despair (v2.2 §10) + code-review fixes + audit โค้ด↔GDD v2.2 | ✅ |
| **30 มิ.ย.** | เทียบ v2.2↔V4 · สร้าง `DEV_PLAN_V4` · **เฟส 0** (refactor ฐานข้อมูล) | ✅ implement |
| **1 ก.ค.** | GDD ฉบับแก้ (Crisis 2·B/Q4) + จัดไฟล์ archive · **เฟส 1** (Quiz/Knowledge) · **เฟส 2** (Reactor: fuel Deuterium/Tritium จริง, cooling เต็มสูตร, พายุ+12, micro-damage, ForceIdle) · **ปิด Gap G1** (สร้าง 5 codex ฟิวชัน + wire ควิซ) | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 3** (ประชากร 3 คลาส Worker/Engineer/Medic + ฝึก 1 วัน + Shelter cap + growth +1/วัน + cooling engineers เข้าสูตรเตา) | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 4** (Crisis A/B/C 3 ทาง + retarget เป็น Hope/Iron + เนื้อหา §10 3 วิกฤต + ForceIdle วิกฤต 2·B เตาผลิตไอโซโทป) | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 5** (TimeManager pause-reason stack + Planning 30s/Live 60s + mode-lock + Placement Pause §15 + quiz/crisis หยุดนาฬิกาวัน) · batch production เลื่อน 5b | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 6** (Building levels L1–L3 ผลิต ×1/×3/×7.5 + upgrade กด U + Water L3→Deuterium + Toroidal/Poloidal Coils ปิด stub เฟส 2) | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 7** (Endings 3-tier Q-based: True/Normal/GameOver + Defeat Summary + Restart + MetaProgress คลังความรู้ถาวร PlayerPrefs + Decrees 2 ข้อ→cooling/Q10) | ✅ implement |
| **1 ก.ค. (ต่อ)** | **เฟส 8** (HUD แสดงเฟส Planning/Live + GridSizeSetup 43×43 + checklist `UNITY_SETUP_AND_PLAYTEST.md`) — code polish · playtest/จูน/อาร์ต=Unity | ✅ code |
| **2 ก.ค.** | **5b** (batch production ตอนจบวัน — event ใหม่ `OnDayProduction` ยิงก่อน `OnDayEnded` การันตีลำดับ ผลิต→บริโภค→เตา/วิกฤต + บริโภค Food/Water 2/คน/วัน + tick 5 วิ เหลือแค่งานก่อสร้าง) · +3 tests | ✅ **122/122 tests ผ่าน** (batchmode) |
| **🎉 สรุป** | **9/9 เฟส code-complete (0-8) + 5b** — ครบทุกระบบ V4 · เหลืองาน Unity (รัน tests/playtest/จูน/อาร์ต) | ⏳ |

> หมายเหตุ: ส่วนด้านล่างนี้เป็นบันทึกช่วง v2.x (ก่อนย้ายเข้า V4) — เก็บไว้เป็นประวัติ

---

## ภาพรวม

**ตำแหน่งปัจจุบัน: Block A + Block B เสร็จครบ** — gameplay loop เล่นได้จริง + CORE TOWER v2.1 (3 เฟส)
ครบแล้ว · **49/49 EditMode tests ผ่าน, compile สะอาด** · งานถัดไป = Block C (Crisis) + Block D (NSC content)

| หมวด | % สมบูรณ์ |
|---|---|
| สถาปัตยกรรม / Core systems | 95% |
| Gameplay loop (วาง → resource → tower) | 90% |
| UI / HUD | 75% |
| เนื้อหาวิทยาศาสตร์นิวเคลียร์ | 55% |
| Narrative / Dilemma | 40% |
| Art (sprite จริง) | 5% |
| Audio | 0% |
| **รวมทั้งโปรเจกต์** | **~50%** |

---

## 🆕 อัปเดต 26–27 มิ.ย.

### เมื่อวาน (26 มิ.ย.) — Design / Planning
- เขียน GDD v2.1: `Plan/CLAUDE_3.md` (CORE TOWER เปลี่ยนจาก S0–S4 เป็น **3 เฟส + CORE%/HEAT**)
- เขียน `Plan/CODEX_CONTENT.md` (เนื้อหา Codex 3 สาขา: เกษตร/การแพทย์/สิ่งแวดล้อม) + `Plan/TASKS.md` (sprint plan)

### วันนี้ (27 มิ.ย.) — Implementation (Block A + B เสร็จครบ)

**Block A — gameplay loop สมบูรณ์**
- **A1** `ResourceManager` ongoing consumption ต่อ tick + ค่า §2.4 เข้า assets ผ่าน `Editor/BuildingBalanceSetup.cs`
- **A2** worker-per-building → เปลี่ยนเป็น **reserve pool** + `workerScale` ปรับผลผลิตตามสัดส่วน (แก้สอดคล้อง 4 ไฟล์)
- **A3** Day counter + Deadline countdown — `GameManager` เป็นเจ้าของ day cycle (Day 1 ไม่จับเวลา, Day 2–30 = 90s) + event `OnDayStarted/OnDayEnded`
- **A4** Speed controls II / 1× / 2× ผ่าน `OnSpeedChangeRequested` → `GameManager.SetSpeed` (timeScale)
- **A5** `AlertController.cs` ใหม่ — corner popup auto-dismiss 5s + debounce กันสแปม

**Block B — CORE TOWER v2.1**
- **B1** refactor `CoreTowerManager` → 3 เฟส + CORE%/HEAT + overclock 4 โหมด + meltdown (TowerData ใหม่)
- **B2** `CoreTowerUI.cs` ใหม่ — CORE%/HEAT bar + ปุ่ม Idle/Normal/Boost/Overdrive
- **B3** SCRAM button (HEAT≥90 → ลดความร้อนฉุกเฉิน + cooldown 2 เทิร์น)
- **B4** ปิด Overdrive TODO เดิม (`CoreTowerManager.cs:25-26`) — ถูกแทนด้วยโมเดล CORE%/HEAT แล้ว

> เพิ่ม events ใน `EventManager`, แก้ `UIManagerHUD` (แถบ→CORE%/HEAT), `HUDCanvasSetup` (DayPanel/SpeedPanel/AlertContainer/CoreTowerPanel auto-wire), dark theme + grid 20×12 visual
> เทสต์ใหม่: `GameManagerTests` (10), `AlertControllerTests` (6), `CoreTowerManagerTests` (12), `ResourceManagerTests` (9)

> ⚠️ ต้องรันเมนู `NuclearReMind → Setup HUD Canvas` ใหม่ใน Unity เพื่อสร้าง DayPanel/SpeedPanel/AlertContainer/CoreTowerPanel

**Block D (เริ่ม) — NSC Codex content**
- **D1–D3** เขียนเนื้อหา Codex 3 สาขาครบ **15 entries** (Agriculture/Medical/Environment) ใน
  `Assets/Editor/CodexBranchContent.cs` — เนื้อหาวิทยาศาสตร์จาก `Plan/CODEX_CONTENT.md`
- รวม `nuclearKnowledge` เข้าท้าย `content` (schema จริงมีแค่ field `content`) + จัดรูปแบบ emoji-section ตาม entry เดิม
- ปลดล็อกด้วย **Research Point** (cost 40–90, tiered) — ใช้ระบบ RP + CodexUI เดิมได้ทันที
  (CodexManager auto-unlock รองรับแค่ `phase_{n}_complete` → event-gated ค่อยต่อเมื่อ Block C/อาคารสาขาพร้อม)
- `CodexSetup.BuildEntryDefs()` รวม 5 Core + 15 สาขา = **20 entries** → เมนู `Setup Codex System` สร้าง asset + wire ให้อัตโนมัติ

> ⚠️ ต้องรันเมนู `NuclearReMind → Setup Codex System` ใหม่ใน Unity เพื่อ generate asset 15 ตัว + re-wire CodexManager (20 entries)
> ⚠️ ยังขาด: อาคารสาขา (Agri Dome / Med Center) + content QA กับอาจารย์ที่ปรึกษา

**Block C — Crisis system (dilemma มี trigger จริง)**
- **C1** `DilemmaManager` ประเมินเงื่อนไข **day-end** ตอน `OnDayEnded` (track resource/tower state ผ่าน event)
  รองรับ condition ใหม่: `heat_above_` / `food_below_` / `food_above_` / `energy_below_` / `water_below_` / `day_reached_`
- **C2–C4** 3 crisis assets (`Editor/CrisisSetup.cs`): Plasma Instability (heat>70), Malignant Outbreak (day≥18),
  Food Shortage (food<120) — ผูกวิทยาศาสตร์ (cooling/meltdown, เวชศาสตร์นิวเคลียร์, food irradiation)
- ขยาย `DilemmaData` consequence: เพิ่ม Energy/Water (เดิมมีแค่ Food/Trust/relationship)
- test: `DilemmaManagerTests` 6 ตัว ✅

> ⚠️ ต้องรันเมนู `NuclearReMind → Setup Crisis Dilemmas` ใหม่ใน Unity เพื่อ generate 3 assets + wire dilemmaPool
> 🔸 crisis ยังไม่ผูกกับ Codex unlock (ต้องต่อ event เมื่อ CodexManager รองรับ event เพิ่ม) + consequence ยังไม่มี Material/CORE% (ResourceData ไม่มี material)

**Phase 1b (28 มิ.ย.) — Hope/Despair (GDD v2.2 §10)** — เทียบ audit เจอว่า trust เดิม ≠ GDD
- แทน `PopulationData.trust` ด้วย `hope`(เริ่ม 50) + `despair`(เริ่ม 20) เป็น 2 ค่าอิสระ
- recalc รายวัน (`OnDayEnded`): ขาดทรัพยากร → Hope−5/Despair+5 ต่ออย่าง, ครบ → Hope+3/Despair−2
- **Hope=0 → Game Over** (`MoraleCollapsed`, GameManager หยุดเกม) · **Despair>80 → จลาจล/สไตรค์**
- event `OnTrustChanged/Delta` → `OnMoraleChanged/Delta(hope,despair)` · dilemma/crisis consequence → Hope/Despair
- HUD: trust bar → Hope bar + Despair bar · `GameEndType.TrustCollapsed` → `MoraleCollapsed`
- tests: `PopulationManagerTests` 6 ตัว + แก้ IntegrationFlow/Dilemma/Crisis

> ⚠️ ต้องรันเมนู `Setup HUD Canvas` (สร้าง Hope/Despair bar) + `Setup Crisis Dilemmas` (regenerate consequence) ใหม่
> 🔸 save เก่า (ก่อนมี hope/despair) จะโหลดได้ hope=0 → game over ทันที (dev only, ไม่มี save จริง)
> ⚠️ งาน 17–27 มิ.ย. (Block A+B) commit แล้ว — งาน Codex (Block D) นี้ **ยังไม่ได้ commit**

**Phase 1b — แก้ตาม code-review (#1–#3)** — lifecycle bug รอบ game-over
- **#1** `GameManager.SetState`: GameOver/Victory → `timeScale=0` (เดิม simulation/นับวันยังเดินต่อหลังเกมจบ)
- **#2** `PopulationManager._gameOverRaised` latch กัน `OnGameOver` ยิงซ้ำทุกวันเมื่อ Hope ค้างที่ 0 (reset ตอนโหลดเซฟ)
- **#3** `HandleDayEnded` ข้าม `day<=1` — Day 1 tutorial ไม่คิดขวัญกำลังใจ (GDD §05/§10)
- tests ล็อกทั้ง 3: `GameManagerTests` +3 (freeze/หยุดวัน/Win ไม่เข้า GameOver), `PopulationManagerTests` +2 (game-over ครั้งเดียว/Day1 ไม่คิด morale)
- 🔸 #4 (orphan TrustBar/TrustText ใน scene) = งานมือใน Unity

---

## ✅ เสร็จแล้ว

### Phase 1 — Foundation (Day 1–7)

| ระบบ | ไฟล์ | % | หมายเหตุ |
|---|---|---|---|
| GameManager | `Managers/GameManager.cs` | 100% | State + pause |
| EventManager | `Managers/EventManager.cs` | 100% | MonoBehaviour Singleton, `[DefaultExecutionOrder(-100)]` |
| GridManager | `Systems/GridManager.cs` | 100% | 20×12, IsoToWorld/WorldToIso ล็อค, 8/8 tests ✅ |
| CameraController | `Systems/CameraController.cs` | 100% | WASD + zoom |
| InputManager | `Systems/InputManager.cs` | 100% | click → grid cell |
| PlacementController | `Systems/PlacementController.cs` | 100% | ghost, footprint, ValidateCell |
| BuildingData SO | `Data/BuildingData.cs` | 100% | schema ครบ incl. powerRange/isPowerRelay |
| BuildingVisualSpawner | `Systems/BuildingVisualSpawner.cs` | 100% | spawn/destroy sprite, power dimming |
| Assembly definitions | `NuclearReMind.asmdef` | 100% | |
| 7 BuildingData assets | `ScriptableObjects/Buildings/` | 100% | placeholder sprites (รอ art จริง) |

### Phase 2 — Core Code (Day 5–9)

| ระบบ | ไฟล์ | % | หมายเหตุ |
|---|---|---|---|
| ResourceManager | `Managers/ResourceManager.cs` | 90% | tick ทุก 5 วิ, production จาก BuildingData, skip unpowered — ขาด: ongoing consumption, worker-per-building |
| PopulationManager | `Managers/PopulationManager.cs` | 85% | trust decay, worker strike — ขาด: population growth |
| CoreTowerManager | `Managers/CoreTowerManager.cs` | 70% | 3 phases + win condition ✅ — **Overdrive stub** (2 TODO ค้าง) |
| SaveManager | `Managers/SaveManager.cs` | 90% | JSON round trip, Integration test ผ่าน |
| BuildingRegistry | `Managers/BuildingRegistry.cs` | 100% | lookup by name, re-occupy cells หลัง load |
| UIManagerHUD | `UI/UIManagerHUD.cs` | 60% | resource bars bind real-time — ขาด: Day counter, Speed controls, Alert |
| TooltipController | `UI/TooltipController.cs` | 80% | 3 ชั้น (name/description/nuclearKnowledge) |
| DilemmaManager | `Narrative/DilemmaManager.cs` | 75% | popup UI ✅ + **crisis trigger จริง (Block C)** ✅ — 3 crisis assets, day-end conditions, 6 tests |
| Integration tests | `Tests/EditMode/` | 100% | 12/12 pass (GridManager 8 + Flow 4) |

### Day 10 — Codex System

| ระบบ | ไฟล์ | % | หมายเหตุ |
|---|---|---|---|
| CodexManager | `Managers/CodexManager.cs` | 100% | TryUnlock, AutoUnlockByEvent |
| CodexUIController | `UI/CodexUIController.cs` | 90% | list, detail view, RP counter |
| Core entries | `ScriptableObjects/CodexEntries/` | 100% | 5/5 (ฟิชชัน, ลูกโซ่, ครึ่งชีวิต, สารหล่อเย็น, รังสี) |
| Agriculture branch | `Editor/CodexBranchContent.cs` | 70% | 5/5 entries (เนื้อหา) ✅ — ขาด 0/3 buildings, dilemma |
| Medical branch | `Editor/CodexBranchContent.cs` | 70% | 5/5 entries (เนื้อหา) ✅ — ขาด 0/3 buildings, dilemma |
| Environment branch | `Editor/CodexBranchContent.cs` | 70% | 5/5 entries (เนื้อหา) ✅ — ขาด radiation zones, dilemma |

### Day 11+ — ระบบเพิ่มเติม (นอกแผน CSV เดิม)

| ระบบ | ไฟล์ | % | หมายเหตุ |
|---|---|---|---|
| ConstructionController | `Systems/ConstructionController.cs` | 95% | build queue, cancel, refund |
| DemolitionController | `Systems/DemolitionController.cs` | 90% | ทุบ + คืน workers |
| PowerGridManager | `Systems/PowerGridManager.cs` | 85% | BFS coverage, primary sources + relay |
| PowerGridVisual | `UI/PowerGridVisual.cs` | 85% | overlay กด P toggle — PowerConduit ไม่มี sprite |
| BuildingSelectionUI | `UI/BuildingSelectionUI.cs` | 90% | hotbar 1–8 + ปุ่มทุบ |
| BuildingQueueUI | `UI/BuildingQueueUI.cs` | 80% | แสดง queue, cancel — prefab ยังไม่ polish |
| TutorialManager | `UI/TutorialManager.cs` | 30% | skeleton เท่านั้น |
| Kanit Font | `Resources/Fonts/` | 100% | Regular/Bold/SemiBold + fallback |

---

## ❌ ยังไม่ได้ทำ / ขาดอยู่

### Critical — ขาดแล้วเกมเล่นไม่สมบูรณ์

| สิ่งที่ขาด | แผน | Priority |
|---|---|---|
| Day counter "วันที่ X" + Deadline countdown บน HUD | ไม่มี | 🔴 สูง |
| Speed controls ⏸ / 1× / 2× | ไม่มี | 🔴 สูง |
| Alert / Notification popup | ไม่มี | 🔴 สูง |
| Overdrive: energy consumption + durability decay | 30% stub | 🟡 กลาง |
| Worker assignment ต่ออาคาร (ตอนนี้เป็น global pool) | ไม่มี | 🟡 กลาง |
| อาคารกิน resource ongoing (มีแค่ production ฝั่งเดียว) | ไม่มี | 🟡 กลาง |
| Population growth | ไม่มี | 🟡 กลาง |

### Content — หัวใจของ NSC (เกณฑ์คะแนนหลัก)

| สิ่งที่ขาด | แผน CSV | Priority |
|---|---|---|
| Agriculture: 5 entries + 3 buildings + dilemma | Day 11 | 🔴 สูง |
| Medical: 5 entries + 3 buildings + dilemma | Day 12 | 🔴 สูง |
| Environment: 5 entries + radiation zones + dilemma | Day 13 | 🔴 สูง |
| Content QA กับอาจารย์ที่ปรึกษา | Day 14 | 🔴 สูง |
| Dilemma เนื้อหาจริง (มีแค่ 1 asset ไม่มี trigger) | Day 8 ค้าง | 🔴 สูง |

### ระบบที่ยังไม่เริ่ม

| ระบบ | แผน CSV | Priority |
|---|---|---|
| Exploration System (6 zones + risk calc) | Day 15 | 🟡 กลาง |
| Story arc Dr. Auren Vasek (4 scenarios) | Day 16 | 🟡 กลาง |
| NarrativeManager + Aethon/Keran relationship | Day 17 | 🟡 กลาง |
| End Game Summary + Knowledge Quiz | Day 18 | 🔴 สูง |
| AudioManager + BGM/SFX | Day 21 | 🟢 ต่ำ |
| Main Menu scene | Phase 3 | 🟡 กลาง |
| Loading / Credits screen | Day 29 | 🟢 ต่ำ |
| Resource flow breakdown tooltip | ค้าง | 🟢 ต่ำ |

### Art — 0% ทั้งหมด

- Building sprites จริง (pixel art): ไม่มีเลย ยัง placeholder
- CORE TOWER 3-stage visual: ไม่มี
- ตัวละคร (Auren Vasek, Aethon, Keran): ไม่มี
- Tilemap tiles จริง: ไม่มี (ยังเป็น generated placeholder)

---

## ลำดับงานถัดไป (เรียง impact)

> ✅ Block A (gameplay loop) + Block B (CORE TOWER v2.1) เสร็จแล้ว — เหลือ Crisis + Content + Polish

1. **Block C — Crisis trigger system (C1–C4)** — `CrisisManager` เช็กทุก EndOfDay → ยิง DilemmaManager (Plasma / Outbreak / Food)
2. **Block D — NSC Codex content (D1–D3)** — Agriculture/Medical/Environment 5 entries/สาขา = **คะแนน NSC หลัก**
3. **Block D4 — Knowledge Quiz** (End Game) — `QuizManager` + `EndGameSummaryUI`
4. **Block E — Polish & Ship** — Tutorial Day 1, End Game Summary, Emergency Decrees, build + playtest Day 1→30
5. **commit งาน 17–27 มิ.ย. ที่ค้างใน working tree** (commit ล่าสุดยัง 16 มิ.ย.)

---

## TODO ที่ค้างในโค้ด

```
(เคลียร์แล้ว) Overdrive TODOs เดิม cs:25-26 ถูกแทนด้วยโมเดล CORE%/HEAT ใน B1/B4
```
ค้างเชิงดีไซน์: fuel ใช้ Energy เป็น proxy (ยังไม่มี Deuterium/Tritium ใน `ResourceData`),
cooling ละ engineers/towerLevel/decrees (=0)

อัปเดต 2 ก.ค.: Mine มี asset แล้ว (ผลิต Iron 30/90/225 ตามระดับ — hotbar ช่อง 8) ·
Habitat = ตึก Shelter ขยายเพดานประชากร 20/40/80 (V4 §5) · ค่าอัประดับคิด Iron+Energy ตาม V4 §6
→ รัน `NuclearReMind → Apply Building Balance (V4 §6)` (หรือ Run All Setups) เพื่อ apply

---

## Assets สำหรับ Setup ก่อนเล่น (ต้องรันใน Editor ครั้งแรก)

1. `NuclearReMind → Setup Codex System`
2. `NuclearReMind → Setup Day 11 Systems` (Construction, BuildingQueue, Tutorial, BuildingSelection)
3. `NuclearReMind → Setup Demolition System`
4. `NuclearReMind → Setup Power Grid System`
5. `NuclearReMind → Apply Kanit Font`
6. Save Scene (Ctrl+S)
