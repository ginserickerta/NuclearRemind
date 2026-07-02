# Unity Setup + Playtest Checklist — NUCLEAR Re:Mind (เฟส 8)

> โค้ดทุกระบบ (เฟส 0–7) implement ครบแล้วแบบ headless · เอกสารนี้ = ขั้นตอนทำให้เล่นได้จริงใน Unity + จูน
> อ้างอิงแผน: `Final Plan/DEV_PLAN_V4.md` · spec: `Final Plan/FINAL NUCLEAR ReMind V4.md`

---

## 1 · Compile ก่อน (สำคัญสุด)
เปิด Unity → รอ recompile → **ต้องไม่มี compile error** (โค้ดแก้แบบ headless ไม่มี compiler ยืนยัน — ตรวจด้วย grep ครบทุก symbol แล้ว แต่ยืนยันจริงที่นี่)
ถ้ามี error ให้ส่ง error มา แก้ทีละจุด

## 2 · รัน Setup ตามลำดับ (เมนู `NuclearReMind/…`)
ลำดับสำคัญ — manager/HUD ต้องมาก่อน content ที่ wire เข้า manager:

| # | เมนู | สร้าง/ทำอะไร |
|---|------|-------------|
| 1 | **Setup HUD Canvas** | HUD ครบ + สร้าง GameObject: **TimeManager · QuizManager · QuizPopupController** + ปุ่ม (mode/SCRAM/Coils/train/decree/restart) + Knowledge/Hope bar |
| 2 | **Setup Tooltip and Dilemma UI** | DilemmaPopupController (ปุ่ม A/B/C) |
| 3 | **Apply 2.4 Building Balance (L1)** | ค่าผลิต/เดินระบบ L1 |
| 4 | **Setup Phase 3 Population** | ตั้งธง Laboratory → ปลดฝึก Engineer/Medic |
| 5 | **Setup Phase 6 Buildings** | Water→Deuterium, Lab→Tritium (ผลิตที่ L3) |
| 6 | **Setup Codex System** | 25 codex (5 core + 5 fusion + 15 สาขา) + CodexManager |
| 7 | **Setup Crisis Dilemmas** | 3 crisis A/B/C + DilemmaManager |
| 8 | **Setup Quiz System** | 10 quiz + wire `allQuizzes` + `linkedQuizIds` (ต้องรัน**หลัง** Crisis + HUD) |
| 9 | **Setup Decrees** | 2 decree + DecreeManager |
| 10 | **Setup Day 11 Systems / Power Grid / Demolition** | ระบบเดิม (ถ้ายังไม่ได้ตั้ง) |
| 11 | **Setup Grid 43x43** | กริด 43×43 (§18) |
| 12 | **Fill Grids / Apply Kanit Font** | tile art placeholder + ฟอนต์ |
| 13 | **Ctrl+S** | Save Scene |

## 3 · ล้าง orphan GameObject (เฟส 0–5 ถอด field)
Setup HUD Canvas สร้างของใหม่ให้ แต่ถ้ามี GameObject/ref เก่าค้างในซีน ให้ลบมือ:
- `DespairBar` · `RiotWarning` · `StrikeWarning` · `WorkersBar` · `RadiationProtectionBar` (ถอดเฟส 0)
- ตรวจ `UIManagerHUD` component ใน Inspector: ไม่มี field ที่ขึ้น "Missing"

## 4 · รัน EditMode Tests (Test Runner → EditMode → Run All)
ควรเขียวทั้งหมด — ชุดที่เพิ่ม/แก้:
`TimeManagerTests · GameManagerTests · ResourceManagerTests · PopulationManagerTests · CoreTowerManagerTests · QuizManagerTests · DilemmaManagerTests · BuildingRegistryTests · MetaProgressTests · DecreeManagerTests · IntegrationFlowTests · AlertControllerTests · GridManagerTests`

## 5 · Playtest Day 1 → 30 (2–3 รอบ) — เช็กแต่ละระบบ
- [ ] **Day 1 tutorial** — popup โผล่, กดปิด → เข้า Day 2
- [ ] **Planning 30s / Live 60s** — dayText แสดง "วางแผน"/"เดินเครื่อง" · เปลี่ยนโหมดเตาได้เฉพาะ Planning
- [ ] **วางอาคาร** → นาฬิกาหยุด (ghost ยังเลื่อน) → วาง/ยกเลิก → นาฬิกาเดินต่อ (§15)
- [ ] **อัปอาคาร (กด U)** → ผลิต ×3 (L2) / ×7.5 (L3) · Water L3 → Deuterium
- [ ] **ฝึก Engineer/Medic** (มี Lab) → ได้คลาสวันถัดไป · growth +1/วัน (Hope≥50)
- [ ] **CORE TOWER Day 11** → เฟส 1→2→3 · Boost/Overdrive · SCRAM · Coils (+Toroidal/+Poloidal)
- [ ] **วิกฤต** เด้ง (heat>70/day18/food<120) → เลือก A/B/C → นาฬิกาหยุด → เด้งควิซ 2 ข้อ
- [ ] **ควิซ** ตอบบังคับ → Knowledge +8/+3 → ปลด Codex → tier บน HUD
- [ ] **วิกฤต 2·B** → เตา Idle 1 วัน (CORE% ไม่ขึ้นวันนั้น)
- [ ] **Decree** (Day 25–30) → +cooling, Hope ดิ่ง, เด้ง Q10
- [ ] **ฉากจบ**: CORE 100% → True · Day 30 Q 0.5–0.99 → Normal · Q<0.5 → แพ้ · Hope 0 → แพ้ · HEAT 100 → Meltdown
- [ ] **Restart** → รีเซ็ตทุกอย่าง **ยกเว้น Knowledge/Codex** (เล่นซ้ำแล้ว Knowledge เริ่มสูงขึ้น)

## 6 · จูนสมดุล (§18) — ปรับใน Inspector ของ manager (ไม่ต้องแตะโค้ด)
- **ResourceManager:** maxEnergy/Water/Iron, LevelProductionMultiplier (โค้ด), `consumeFoodPerPerson`/`consumeWaterPerPerson` (2/คน/วัน — 5b)
- **CoreTowerManager:** baseCoreGain, FuelNeed, stormHeat 12, cooling costs, coil costs
- **PopulationManager:** hopeLoss/Recovery, growthHopeThreshold, train cost
- **GameManager:** dayLength 90 / liveSeconds 60
- โฟกัส (V4 §7): ความตึง Energy Phase 4 · จำนวนวัน Boost ที่จำเป็น · ระยะกันชน Phase 3

## 7 · ค้าง (ทำต่อได้)
- ~~**5b · batch production + บริโภค Food/Water 2/คน/วัน**~~ ✅ **เสร็จ 2 ก.ค.** — ผลิต batch ตอนจบวัน (`OnDayProduction` ยิงก่อน `OnDayEnded`) + บริโภค 2/คน/วัน · tick 5 วิ เหลือแค่เดินงานก่อสร้าง
- **Zone B / Hospital building** — ตอนนี้ Tritium มาจาก Lab L3, Medic ฝึกที่ Lab (สะพานชั่วคราว)
- **GameConfigSO** — รวมค่าจูนที่เดียว (ออปชัน)
- **อาร์ตจริง + เสียง + กล้องจัดเฟรม 43×43**

---
*เฟส 8 · 2026-07-02 — โค้ดครบทุกระบบ (รวม 5b) เหลืองาน Unity (setup/playtest/จูน/อาร์ต)*
