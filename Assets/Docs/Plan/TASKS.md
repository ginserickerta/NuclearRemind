# TASKS.md — Nuclear Re:Mind · Sprint Plan
> Deadline: **9 ก.ค. 2569** (build พร้อมส่ง) | อัปเดต: 2026-06-25
> ใช้ไฟล์นี้เปิด Claude Code session ทุกวัน — tick ✅ เมื่อจบ

---

## HOW TO USE
เปิด Claude Code แล้ว paste:
> "อ่าน CLAUDE.md และ TASKS.md แล้วทำ task [X] ให้เสร็จ"

---

## BLOCK A — gameplay loop สมบูรณ์ (ต้องทำก่อน)
*ถ้า A ไม่เสร็จ เกมยังเล่นไม่ได้จริง*

- [x] **A1** · `ResourceManager` — เพิ่ม ongoing consumption ต่อ tick ✅ (27 มิ.ย.)
  - อาคารแต่ละ L กิน Energy/Water ตามตาราง §2.4 ใน CLAUDE.md
  - ถ้าคลัง < 0 → อาคารหยุด (skip production วัน/tick นั้น)
  - ไฟล์: `Managers/ResourceManager.cs` + เพิ่ม `energyConsumption`/`waterConsumption` ใน `BuildingData`
  - ✅ ค่า §2.4 L1 เข้า assets แล้วผ่าน `Editor/BuildingBalanceSetup.cs`
    (PowerPlant +60⚡/1คน/2💧 · WaterPlant +50💧/1คน/10⚡ · Farm +40🌿/1คน/8💧+5⚡)
  - ⚠ ยังค้าง: Mine (ไม่มี asset + ResourceData ไม่มี `material`), L2–L5 (ไม่มีระบบ level)
  - test: `ResourceManagerTests` 4 ตัว ✅

- [x] **A2** · `ResourceManager` — worker-per-building ✅ (27 มิ.ย.)
  - เปลี่ยนโมเดล: workers = **reserve pool** (จองตาม workerRequired) ไม่ใช่ consumed
  - `workerScale = min(1, workers / totalNeeded)` → ผลผลิตทุกอาคารปรับตามสัดส่วน
  - แก้สอดคล้อง 4 ไฟล์: เอา deduction ออกจาก placement, เอา worker refund ออกจาก
    `ConstructionController` (cancel) + `DemolitionController` (ทุบ)
  - test: `ResourceManagerTests` 5 ตัว + แก้ assertion ใน `IntegrationFlowTests` ✅
  - 🔸 ตัดสินใจ: understaffed กิน consumption เต็มแต่ผลิตน้อย (inefficiency penalty)

- [x] **A3** · `UIManagerHUD` — Day counter + Deadline countdown ✅ (27 มิ.ย.)
  - `GameManager` เป็นเจ้าของ day cycle (§2.3): Day 1 ไม่จับเวลา, Day 2–30 นับถอยหลัง 90s
  - event ใหม่ `OnDayStarted(day, timed)` / `OnDayEnded(day)` ใน `EventManager`
  - HUD แสดง "DAY X / 30" + countdown "m:ss" (อ่าน `DayTimeRemaining` read-only ต่อเฟรม)
  - `HUDCanvasSetup` สร้าง DayPanel + ผูก `dayText`/`timerText` อัตโนมัติ
  - `OnDayEnded` เป็น hook ไว้ให้ EndOfDay resolve / crisis (Block C) มาเกาะ
  - test: `GameManagerTests` 5 ตัว ✅
  - ⚠ ต้องรันเมนู `NuclearReMind/Setup HUD Canvas` ใหม่ใน Unity (สร้าง DayPanel)

- [x] **A4** · `UIManagerHUD` — Speed controls ✅ (27 มิ.ย.)
  - ปุ่ม II / 1× / 2× → raise `OnSpeedChangeRequested` → `GameManager.SetSpeed`
  - `GameManager.GameSpeed` + `SetState` ใช้ timeScale ตามความเร็ว (pause คงความเร็วเดิม)
  - `OnSpeedChanged` → HUD highlight ปุ่มที่เลือก ; countdown A3 เร็ว/ช้าตาม timeScale เอง
  - `HUDCanvasSetup` สร้าง SpeedPanel + ผูกปุ่มอัตโนมัติ
  - test: `GameManagerTests` +5 A4 ✅
  - ⚠ รันเมนู `Setup HUD Canvas` ใหม่ใน Unity (สร้าง SpeedPanel)

- [x] **A5** · Alert/Notification popup ✅ (27 มิ.ย.)
  - `AlertController.cs` ฟัง `OnResourceCritical` / `OnResourceDepleted` / `OnDilemmaTriggered`
  - corner popup มุมล่างขวา ซ้อนขึ้น auto-dismiss 5s (real-time, ไม่ขึ้นกับ pause/ความเร็ว)
  - debounce ด้วย active-key: alert ชนิดเดียวกันไม่เด้งซ้ำระหว่างยังค้าง (กันสแปม tick ทุก 5 วิ)
  - `HUDCanvasSetup` สร้าง AlertContainer + AlertController + wire อัตโนมัติ
  - test: `AlertControllerTests` 6 ตัว ✅

---

## BLOCK B — CORE TOWER v2.1 (GDD เปลี่ยนมาก)
*CoreTowerManager.cs ยังเป็น S0–S4 เก่า ต้อง refactor เป็น 3 เฟส*

- [x] **B1** · Refactor `CoreTowerManager` → 3 เฟส + CORE% ✅ (27 มิ.ย.)
  - TowerData ใหม่: corePercent / coreHeat / currentPhase(0–3) / overclockMode / heatCap / isUnlocked
  - ก้าวหน้า **ต่อวัน** (OnDayEnded), ปลดล็อก Day 11 → 30%, win ที่ 100%, meltdown coreHeat≥heatCap
  - สูตร §3.4 + overclock 4 โหมด (Phase 1 ล็อก, Phase 2–3 เปิด) + RedZone heatCap-- + storm Phase 3
  - แก้ EventManager (+overclock events), UIManagerHUD (แถบ→CORE%/HEAT), tests (rewrite tower + CoreTowerManagerTests 8 ตัว)
  - PowerGridManager ไม่แตะ (คงชื่อ field currentPhase)
  - ⚠ proxy: fuel=Energy (ยังไม่มี Deuterium/Tritium ใน ResourceData), cooling ละ engineers/towerLevel/decrees (=0)
  - 🔸 B4 (Overdrive TODOs เดิม cs:25–26) **ถูกแทนที่** ด้วยโมเดลใหม่ (fuel consumption + heatCap degradation) — ปิดไปแล้วโดยปริยาย

- [x] **B2** · Overclock mode UI (4 โหมด) ✅ (27 มิ.ย.)
  - `CoreTowerUI.cs` ใหม่: CORE% bar + HEAT bar (เปลี่ยนสีเตือน) + ปุ่ม Idle/Normal/Boost/Overdrive + status
  - ส่งคำสั่งผ่าน event `OnOverclockModeRequested`, highlight โหมดจาก `OnOverclockModeChanged`
  - Phase 1 ปุ่มโหมด disable (ล็อก), Phase 2–3 เปิด
  - `HUDCanvasSetup` สร้าง CoreTowerPanel + wire อัตโนมัติ

- [x] **B3** · SCRAM button ✅ (27 มิ.ย.)
  - `CoreTowerManager.Scram()`: HEAT≥90 → HEAT −40, CORE% −10, เสีย Water 50, cooldown 2 เทิร์น
  - cooldown ลดทุกเทิร์น, ปุ่ม disable เมื่อ HEAT<90 หรือ cooldown ยังไม่หมด
  - event `OnScramRequested` (UI→mgr) + `scramCooldown` ใน TowerData
  - test: +4 SCRAM tests ✅

- [x] **B4** · ปิด Overdrive TODOs ✅ (รวมใน B1)
  - โมเดล S0–S4 + durability เดิมถูกแทนด้วย CORE%/HEAT/heatCap — TODO cs:25–26 หายไปแล้ว
  - energy consumption = fuel proxy ต่อเทิร์น ; durability decay = heatCap-- ใน RedZone (15–30%)

---

## BLOCK C — Crisis Events (วิกฤต 3 ตัว)
*DilemmaManager มีแค่ 1 asset, ไม่มี trigger จริง*

- [x] **C1** · Crisis trigger system ✅ (27 มิ.ย.)
  - เพิ่มใน `Narrative/DilemmaManager.cs` (แทน CrisisManager แยก) — ประเมินตอน `OnDayEnded`
  - condition: heat_above_ / food_below_ / food_above_ / energy_below_ / water_below_ / day_reached_
  - track resource/tower state ผ่าน OnResourceChanged / OnTowerProgressChanged (ไม่ direct ref)
  - test: `DilemmaManagerTests` 6 ตัว ✅
  - 🔸 zoneA_workers ยังไม่มี (ไม่มีระบบ zone worker)

- [~] **C2** · Crisis 1 · Plasma Instability ✅ asset (27 มิ.ย.) — trigger `heat_above_70`
  - A: −300 Energy −60 Water +5 Trust (ระบายความร้อน) | B: −12 Trust (เดินเครื่องต่อ เสี่ยง)
  - 🔸 ปรับเป็น 2 choice (DilemmaData รองรับ A/B) — Material/worker-lock/CORE% ยังไม่มีระบบรองรับ

- [~] **C3** · Crisis 2 · Malignant Outbreak ✅ asset (27 มิ.ย.) — trigger `day_reached_18`
  - A: −200 Energy −100 Food +8 Trust (รักษาด้วยเภสัชรังสี) | B: −12 Trust (กักตัว)

- [~] **C4** · Crisis 3 · Food Shortage ✅ asset (27 มิ.ย.) — trigger `food_below_120`
  - A: −10 Trust (ปันส่วน) | B: −150 Energy +250 Food +3 Trust (ฉายรังสีถนอมอาหาร)

---

## BLOCK D — NSC Content (หัวใจคะแนน)
*ทำขนานกับ Block B–C ได้ เป็นงาน data ไม่ใช่ code*

- [~] **D1** · Codex Agriculture (5 entries) — เนื้อหา ✅ (27 มิ.ย.) ใน `Editor/CodexBranchContent.cs`
  - ⚠ ปลดล็อกด้วย RP ชั่วคราว (cost 40–80) แทน event-trigger — ขาด building + dilemma
  - โคบอลต์-60 ถนอมอาหาร
  - ฉายรังสีแกมมาทำลายจุลินทรีย์
  - ปรับปรุงพันธุ์พืช (ข้าว กข6/กข10/กข15)
  - การกลายพันธุ์เชิงบวกจากรังสี
  - Agri Dome กับการเกษตรควบคุม
  - unlock trigger: สร้าง Research Lab / Agri Dome / วิกฤต Food

- [~] **D2** · Codex Medical (5 entries) — เนื้อหา ✅ (27 มิ.ย.) ใน `Editor/CodexBranchContent.cs`
  - PET Scan — สารเภสัชรังสีตรวจเนื้อร้าย
  - SPECT — สแกนโมเลกุล
  - Targeted Radionuclide Therapy
  - ปริมาณรังสีที่ปลอดภัย (Sv/Gy)
  - เซลล์กลายพันธุ์จากรังสีไอออไนซ์
  - unlock trigger: สร้าง Med Center / วิกฤต Outbreak

- [~] **D3** · Codex Environment (5 entries) — เนื้อหา ✅ (27 มิ.ย.) ใน `Editor/CodexBranchContent.cs`
  - ALARA principle
  - รังสีพื้นหลัง (Background radiation)
  - เขตรังสี Zone A/B
  - ผลกระทบรังสีระยะยาว
  - การฟื้นฟูพื้นที่ปนเปื้อน
  - unlock trigger: เปิด Zone A / เข้า Phase 3

- [ ] **D4** · Knowledge Quiz (End Game)
  - 5–10 คำถามจาก Codex entries ที่ผู้เล่นปลดล็อก
  - แสดงในหน้า End Game Summary
  - บันทึก score ลงใน ending stats
  - ไฟล์: `UI/EndGameSummaryUI.cs`, `Managers/QuizManager.cs`

---

## BLOCK E — Polish & Ship

- [ ] **E1** · Tutorial Day 1 (3 steps: ซ่อม Shelter → สร้าง 3 โรง → จัดคน)
  - ไฟล์: `UI/TutorialManager.cs` (ต่อจาก 30% skeleton)

- [ ] **E2** · End Game Summary screen
  - แสดง Q / CORE% / ผู้รอด / Hope / วันที่ชนะ
  - map → ฉากจบ 4 แบบ + epilogue flags

- [ ] **E3** · Emergency Decrees UI (Phase 3)
  - ป้ายเล็กมุมจอ กดเพื่อดูกฎ
  - confirm → ตรากระแทก "ปึง" + ค้างใน HUD
  - set flag usedSickLabor / usedChildLabor

- [ ] **E4** · Hope/Despair ให้ independent (ถ้า code ยังเป็น inverse)
  - ตรวจ `PopulationManager.cs` — hope ≠ 100 − despair

- [ ] **E5** · Build + Test (WebGL หรือ Windows)
  - เทสต์ end-to-end Day 1 → Day 30
  - ตรวจ 4 ending paths

---

## DAILY SESSION LOG
| วัน | วันที่ | Tasks | ผล |
|---|---|---|---|
| D1 | 25 มิ.ย. | A1, A3 | |
| D2 | 26 มิ.ย. | A2, A4, A5 | |
| — | 27 มิ.ย. | A1 ✅, A2 ✅ + fix SaveLoad test (CodexManager) | 21/21 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | §2.4 balance → assets (Editor script), A3 ✅ | 26/26 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | A4 ✅ (speed controls) | 31/31 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | A5 ✅ (alert popup) → **Block A เสร็จครบ** | 37/37 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | dark theme + grid 20×12 (logic/visual/fog) | scene apply + verified |
| — | 27 มิ.ย. | **B1 ✅ + B4 ✅** (CORE TOWER v2.1 refactor) | 45/45 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | **B2 ✅ + B3 ✅** (overclock UI + SCRAM) → **Block B เสร็จครบ** | 49/49 tests ผ่าน, compile สะอาด |
| — | 27 มิ.ย. | **D1–D3 เนื้อหา ✅** Codex 15 entries (เกษตร/แพทย์/สิ่งแวดล้อม) ปลดล็อกด้วย RP | รอ generate asset + content QA |
| — | 27 มิ.ย. | **C1 ✅ + C2–C4 ✅** Crisis trigger (day-end) + 3 crisis assets | +6 tests, รอรันเมนู Setup Crisis Dilemmas |
| D3 | 27 มิ.ย. | B1 | |
| D4 | 28 มิ.ย. | B2, B3, B4 | |
| D5 | 29 มิ.ย. | C1, C2 | |
| D6 | 30 มิ.ย. | C3, C4 | |
| D7 | 1 ก.ค. | D1, D2 | |
| D8 | 2 ก.ค. | D3, D4 | |
| D9 | 3 ก.ค. | E1, E2 | |
| D10 | 4 ก.ค. | E3, E4 | |
| D11 | 5 ก.ค. | balance + bug fix | |
| D12 | 6 ก.ค. | E5 + full playtest | |
| D13 | 7 ก.ค. | bug fix + polish | |
| D14 | 8 ก.ค. | art integration (Naraphat) | |
| D15 | 9 ก.ค. | final build + pack | |

---

*อัปเดต log ทุกคืน เพื่อให้ session ถัดไปรู้ว่าหยุดตรงไหน*
