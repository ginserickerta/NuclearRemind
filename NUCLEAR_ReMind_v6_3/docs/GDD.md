# NUCLEAR Re:Mind — GDD (ฉบับรวม)

**Engine:** Unity 6 / C# · **Length:** 30 in-game days · **Genre:** Edu-Survival City-Builder
**Project:** NSC #28

| | |
|---|---|
| **สถานะ** | ระบบ + ตัวเลข ครบ · พร้อมลงมือทำเกม |
| **ทดสอบ** | 3,000 รอบ (4 playstyle × 3 ระดับความรู้ × 250 seeds) + stability 2,400 รอบ |
| **ผล** | 0 invariant violations · drift ≤ 5.5% |
| **ที่มา** | GDD v4.1 (§1–§16) + GDD v5.2 (§17–§33) + Script v8 |
| **อ้างอิง sim** | `nrm_sim.py` |

---

## 🔴 กติกาที่ห้ามละเมิด (อ่านก่อนเขียนโค้ดบรรทัดแรก)

1. **ห้าม `if (day == X)`** — ยกเว้น `day == 30` (เส้นตาย = กติกา ไม่ใช่ trigger)
2. **ห้ามระบบบอกคำตอบก่อนวิจัย** — Kova ไม่รู้ ระบบไม่รู้
3. **ความรู้ต้องผลิต** — เวลา + คน + ทรัพยากร
4. **ควิซ = ยืนยันความเข้าใจ** — ไม่บังคับ อยู่ Codex หลังใช้จริง
5. **ไม่มีทางเลือกฟรี** — ทุกทางมีราคา
6. **★ ตัวเลือกที่ดีในการ์ดต้องล็อกจนกว่าจะวิจัย**
7. **ตัวเลขทั้งหมดอ่านจาก `CONFIG.md`** — ห้าม hardcode ในโค้ด
8. **ห้ามแตะ `boost_core_gain`** — เปราะมาก (2.6 ยังชนะ · 2.3 = แพ้ 100%)

---

## หลักการเดียวที่ต้องจำ

```
ความรู้ไม่ใช่รางวัล — มันคือกลไกที่ทำให้ชนะ

Zone B ผลิต 3.0/วัน · Boost กิน 9.0/วัน → ขาด 6/วัน → CORE ค้าง 80 → แพ้
ตอบควิซ Tritium Breeding ถูก → 8.0/วัน → ชนะ

ผล sim: ไม่ตอบควิซ = แพ้ 100% ทุก playstyle
```

---

## สารบัญ

| ส่วน | เนื้อหา | ไฟล์ | Sprint |
|---|---|---|---|
| **ภาค 0** | พื้นฐาน: Overview · Core Loop · เวลา · ทรัพยากร · ประชากร · อาคาร · **Phase** · **SCRAM** · ไอเทม · Placement · Architecture | `GDD.md` (นี่) | — |
| **ภาค 1** | ระบบ: §17 Worker → §33 | `GDD.md` (นี่) | 1–3 |
| **ค่าเกม** | ตัวเลขทั้งหมดที่เดียว | `CONFIG.md` | ทุก Sprint |
| **ภาค 2** | Research Notes 8 ใบ | `NOTES.md` | 2 |
| **ภาค 3** | Quiz 11 ข้อ | `QUIZZES.md` | 3 |
| **ภาค 4** | Crisis Cards 8 ใบ | `CARDS.md` | 4 |
| **ภาค 5** | Bark 62 บรรทัด | `BARKS.md` | 4 |
| **ภาค 6** | Records / Intro / Endings | `STORY.md` | 5 |
| **Codex** | คลังความรู้ 11 entry | `CODEX.md` | 3 |
| **คลัง** | Inventory 6 แท็บ + คราฟต์ | `INVENTORY.md` | 1 / 4 |

---

# ภาค 0 — พื้นฐานเกม (จาก v4.1 · ปรับให้ตรง v5.2)

> **ที่มา:** §1–§16 ของ GDD v4.1 — เก็บเฉพาะส่วนที่ v5.2 ไม่ได้ทับ
> **ค่าตัวเลขทั้งหมดในไฟล์นี้ยึด v5.2 เป็นหลัก** ถ้าขัดกับ v4.1 → v5.2 ชนะเสมอ

---

## §1 ภาพรวมเกม (Overview)

NUCLEAR Re:Mind = เกมสร้างเมืองแนวเอาตัวรอด (survival city-builder) มุมมอง isometric
สอนฟิสิกส์นิวเคลียร์/ฟิวชันผ่านการเล่นจริง

ผู้เล่นรับบท **Dr. Auren Vasek** — วิศวกรคนสุดท้ายที่รอดภารกิจ
ต้องบริหารเมือง Veltara ใต้พายุรังสี พร้อมดัน CORE TOWER ให้ถึง **CORE 100%** (= Q ≥ 1.0)

### 4 เสาหลักการออกแบบ

| เสา | ความหมาย |
|---|---|
| **Learn by Playing** | ความรู้นิวเคลียร์ฝังในวิกฤตจริง — ผู้เล่นเข้าใจเพราะต้องใช้ ไม่ใช่ท่องจำ |
| **Grounded Physics** | ทุกวิกฤตอิงหลักจริง: โทคาแมก, ALARA, เวชศาสตร์นิวเคลียร์, การฉายรังสีอาหาร, D-T Fusion |
| **Tense but Thoughtful** | นาฬิกาเดินตอนผลิต แต่ตอนวางแผน/ตอบควิซ/วางอาคาร = เวลาหยุด |
| **Meaningful Choices** | ทุกทางเลือกแลกด้วยทรัพยากร/คน/เวลา — **ไม่มีตัวเลือกฟรี** |

### เงื่อนไขชนะ/แพ้ (v5.2 — ห้ามใช้ Day 30 เป็นตัวชนะ)

| ผลลัพธ์ | เงื่อนไข |
|---|---|
| ☀ **True Ending** | `core >= 100` — **เช็คทุกวัน จบทันที ห้ามรอ D30** |
| ◐ **Normal Ending** | `day == 30 && core >= 50 && core < 100` |
| ✕ **Game Over** | `hope <= 0` · หรือ `heat >= 100` (Meltdown) · หรือ `day == 30 && core < 50` |

> ⚠ `day == 30` คือ **กติกา** ไม่ใช่ trigger — เป็นข้อยกเว้นเดียวของกฎ "ห้าม if(day==X)"

---

## §2 Core Loop (ฉบับเต็ม)

### ลูปความรู้ (v5.2 — ตัวหลัก)

```
เจอปัญหา → การ์ดเด้ง (ตัวเลือกดีล็อกอยู่ 🔒)
  → Lead ปลด → ดึงคนไปวิจัย ★ เมืองแย่ลงชั่วคราว
  → ได้ Note → สร้าง/ติดตั้ง
  → การ์ดเด้งอีกครั้ง → ★ คราวนี้ตัวเลือกดีปลดแล้ว
  → ควิซใน Codex → Mastery ถาวร
```

### ลูปวัน (โครงเวลา)

```
[1] เริ่มวัน → รับสรุปสถานะ (คลัง, Hope, CORE%, HEAT, ประชากร)
[2] Planning Phase 30s → จัดคนงาน, สั่งวิจัย, วางอาคาร, เลือกโหมดเตา
[3] Live Phase 60s → เวลาเดิน, เตาเดินตามโหมดที่ล็อกไว้, การ์ด/bark เด้งได้
[4] End of Day → คำนวณผลผลิต batch → หักบริโภค → tick เตา
                → เช็คการ์ด → เช็ค Ending → ขึ้นวันใหม่
```

**1 วัน = 90 วินาที** (Planning 30s + Live 60s)
ผลิตคำนวณแบบ **"จบวันทีเดียว"** ไม่สะสมต่อเนื่องระหว่างวัน

---

## §3 ระบบเวลา (Time System)

| เฟส | เวลา | เกิดอะไร |
|---|---|---|
| **Planning** | 30s | จัดคน, อัปเกรด, วางอาคาร, เลือกโหมดเตา (ล็อกทั้งวัน) |
| **Live** | 60s | เวลาเดิน, เตาเดิน, การ์ดเด้ง |
| **End of Day** | ทันที | batch calc → เช็ค win/lose → วันใหม่ |

### กฎเวลาสำคัญ

นาฬิกาเดินเฉพาะตอนไม่มี pause reason ใด ๆ
Pause reason ซ้อนกันได้: กำลังวางอาคาร / popup การ์ด / popup Note / popup Record / เปิดเมนูหลัก

```csharp
TimeManager.IsRunning == (activePauseReasons.Count == 0)
```

> ⚠ **ห้ามตั้ง `Time.timeScale = 0`** — หยุดเฉพาะ "นาฬิกาวัน" ผ่าน flag `IsRunning`
> เพื่อให้ ghost preview ลื่นและ UI ตอบสนอง

### จังหวะ 30 วัน

- **Day 1** — Tutorial Day: ไม่มีการ์ด สอน 3 อย่าง
  1. คุมคนงาน (จัดคนลงงาน)
  2. เดินโรงงานพื้นฐาน 3 โรง (ไฟ/น้ำ/อาหาร)
  3. ★ **ระบบพัก** — "คนเหนื่อยเกิน 70 จะพักเอง — เผื่อคนสำรองไว้" (§17.5)
- **Day 2–30** — วนลูปเต็ม การ์ดเด้งตาม state
- **จบ 1 รอบเกม ≈ 30–45 นาที**

---

## §4 ทรัพยากร (Resources)

### ค่าเริ่มเกม — ★ ยึด §27 v5.2

| ทรัพยากร | ค่าเริ่ม | เพดานคลัง |
|---|---|---|
| **Power** (พลังงาน) | 0 | 400 (floor 0) |
| **Water** (น้ำ) | 90 | 200 (floor 0) |
| **Food** (อาหาร) | 40 | — |
| **Iron** (แร่เหล็ก) | 200 | — |
| **labMat** (วัสดุแล็บ) | 100 | — |
| **Deuterium** (fuel) | 0 | — |
| **Tritium** | 0 | — |
| **Hope** | 70 | 100 (min 0 = แพ้) |
| **CORE%** | 30 | 100 (= ชนะ) |
| **HEAT** | 20 | 100 (= Meltdown) |

> ⚠ **ต่างจาก v4.1 ทุกค่า** — v4.1 ใช้ 160/150/120/100 + Hope 100 ทั้งหมดถูกทับแล้ว
> Power/Water มี **floor 0** เพราะบั๊ก #17/#18 (ติดลบ → สูตรเพี้ยนทั้งระบบ)

### บทบาททรัพยากร

| ทรัพยากร | บทบาท | บริโภค/วัน |
|---|---|---|
| **Power** | "เงิน" ของเกม + ค่าเดินระบบอาคาร · ใช้วิจัย/เปิดโซน/แก้วิกฤต | ตาม draw (§27) |
| **Water** | คนดื่ม · **หล่อเย็นเตา** · สกัด Deuterium | pop × 1.0 |
| **Food** | คนกิน · ขาด = hunger +30/วัน → Hope ดิ่ง | 1/คน/วัน (§27) |
| **Iron** | สร้าง/อัปเกรดอาคาร · คราฟต์ | ตามต้องการ |
| **labMat** | วิจัย · **คราฟต์ Rad Suit (30/ชุด)** | +5/วัน |

> ★ **น้ำใช้คลังเดียวกัน** — คนดื่ม + หล่อเย็นเตา ไม่มีถังแยก
> ยิ่ง Boost ยิ่งแย่งน้ำดื่มของประชากร → ต้องบาลานซ์โรงน้ำ

### เชื้อเพลิงฟิวชัน 2 ชนิด

| เชื้อเพลิง | ระดับ | แหล่งผลิต | ใช้เมื่อ |
|---|---|---|---|
| **Deuterium (²H)** | พื้นฐาน | Deuterium Extractor (ปลดจาก note `deuterium`) | ดัน CORE 30→80% |
| **Tritium (³H)** | ขั้นสูง | **Zone B** (ปลดจาก note `tritium`) | ★ **บังคับ** ตั้งแต่ CORE ≥ 80% |

> ★ **Method B Gate:** `core >= 80 && tritium < 5` → `gain = 0`
> ไม่มี Tritium = CORE ค้าง 80 ตลอดกาล = **แพ้** (ดู §26)

---

## §5 ประชากร (Population)

### ★ ไม่มีระบบคลาสแล้ว (ต่างจาก v4.1 สิ้นเชิง)

v4.1 มี Worker / Engineer / Medic + ระบบฝึกคลาส — **v5.2 ตัดทิ้งทั้งหมด**
แทนด้วย **per-worker state + job string** (ดู §17):

```csharp
public string job; // farm/power/water/mine/lab/cool/zoneb/extract/idle
```

คนทุกคนเหมือนกัน ต่างกันแค่ "ตอนนี้ทำงานอะไร" — ย้ายงานได้ทันที ไม่ต้องฝึก

| v4.1 (ตาย) | v5.2 (ใช้) |
|---|---|
| ฝึก Engineer (Food 30 + E 50, 1 วัน) | ย้าย job → `"lab"` ฟรี |
| ฝึก Medic (Food 40 + E 60) | ย้าย job → Med Bay รักษาเอง |
| Engineer Floor ≥ 1 | **floor=2** ที่ farm/water (§19 re-staff) |

> **ทำไมตัด:** ระบบฝึกคลาสทำให้ตอบสนองวิกฤตช้า 1 วันเสมอ
> v5.2 ต้องการให้ผู้เล่น "ดึงคนไปวิจัย → เมืองแย่ลงทันที" = ราคาที่จ่ายต้องรู้สึกได้เดี๋ยวนั้น

### ★ Shelter (เพดานประชากร) — L1–L3

| ระดับ | เพดาน | ต้นทุนสร้าง/อัป | ปลดล็อก |
|---|---|---|---|
| **L1** | 14 (เริ่มมี) | — | Phase 1 |
| **L2** | 20 | Iron 60 + Power 100 | Phase 2 |
| **L3** | 28 | Iron 120 + Power 250 | Phase 3 |

### กลไกเติมประชากร

```csharp
// §27 v5.2
if (food > pop * 5 && pop < shelterCap)
    if (Random() < 0.25f) { pop += 1; food -= 20; }
```

- ต้องมี **อาหารเหลือ** (food > pop×5) และ **ยังไม่เต็มเพดาน**
- โอกาส 25%/วัน · +1 คน · หัก food 20
- **ไม่ผูก Hope** (v4.1 ผูก Hope ≥ 50 — ตัดออกเพราะ Hope เริ่ม 70 ต่ำกว่าเดิม)

> ⚠ **ผลกระทบ sim:** ค่า sim ทั้งหมดรันที่ pop 14 คงที่
> เปิดระบบเติมประชากร = ค่าอาจ drift → ต้องรัน sim ใหม่ยืนยัน (ดู §33)

---

## §6 อาคารทั้งหมด (Buildings)

> **นิยาม:** ผลิต/วัน = output เมื่อคนงานครบ · คน = แรงงานประจำ
> ต้นทุนสร้าง/อัป = จ่ายครั้งเดียว · ค่าเดินระบบ = หักทุกวัน (draw)

### อาคารผลิต (Production)

| อาคาร | L1 | L2 | L3 | คน | ต้นทุนอัป | ปลดล็อก L2/L3 |
|---|---|---|---|---|---|---|
| **Power Plant** | +45/คน | ×1.5 | ×2.2 | 1–3 | Iron 40 / Iron 80 + P 150 | Phase 2 / **Phase 4** |
| **Water Plant** | +30/คน | ×1.5 | ×2.2 | 1–3 | Iron 40 / Iron 80 + P 150 | Phase 2 / Phase 3 |
| **Farm** | +9/คน | ×1.5 | ×2.2 | 1–3 | Iron 40 / Iron 80 + P 150 | Phase 2 / Phase 3 |
| **Mine** | +14/คน | ×1.5 | ×2.2 | 1–3 | Iron 40 / Iron 80 + P 150 | Phase 2 / **Phase 4** |

> ★ **ผลิตคำนวณต่อคน ไม่ใช่ต่ออาคาร** — ตรงกับ §27 v5.2
> `food += farmWorkers × 9` · `water += waterWorkers × 30` · `iron += mineWorkers × 14` · `power += powerWorkers × 45`
>
> ⚠ **ต้องคูณ `Σ GetEfficiency(workers)` เสมอ** ไม่ใช่นับหัว (§17 CRITICAL)

### อาคารปลดจากงานวิจัย (ดู §19 · ตารางเต็มใน `NOTES.md`)

| อาคาร | ต้นทุน | ปลดจาก | ผล |
|---|---|---|---|
| **Deuterium Extractor** | Iron 110 | `deuterium` | fuel +6/วัน · water −10/วัน · draw +15 |
| **Toroidal Coil** | Iron 50 (L2/3: 90) | `confinement` | cooling +9/Lv (max 3) |
| **Poloidal Coil** | Iron 50 (L2: 90) | `confinement` | poloidalDamp +6/Lv |
| **Med Bay** | Iron 90 | `nuclear_medicine` | รักษา 4 คน · rad −25 (−35 ถ้ามี Mastery) · draw +30 |
| **Rad Suit** (คราฟต์) | labMat 30/ชุด | `nuclear_medicine` | rad ×0.4 · เป้า 5 ชุด |
| **Co-60 Chamber** | Power 50 | `irradiation` | spoil ×0.3 · draw +20 |
| **Mutation Lab** | — | `irradiation` | ผลผลิตฟาร์ม +15% (ต้องตอบควิซ) |
| **Granary** ★ | Iron 50 | `food_logistics` | spoil ×0.6 · การ์ด #4 ปลด C |
| **Barracks** ★ | Iron 60 | `shift_management` | rest −45/วัน · การ์ด #5 ปลด A |
| **Sensor Array** | — | `storm_detection` | เห็นเกจพายุ + คาดการณ์วัน · heat_room +4 |
| **Zone B** | Iron 150 + P 200 | `tritium` | ★ Tritium 3.0 (8.0 ถ้าตอบควิซ) · draw +25 · rad 15/วัน |

### อาคารพิเศษ

| อาคาร | รายละเอียด |
|---|---|
| **Research Lab** | ★ **เริ่มเกมเป็นซาก** (`isRuined = true`) — ซ่อม: Iron 80 · 2 วัน · 2 คน · researcherSlots 3 (Lv2→5, Lv3→8) |
| **Memorial** | ★ **คลิกได้ตั้งแต่ D1** · เกมไม่ชี้ไม่ไกด์ · Hope +2 ครั้งแรก (ดู `STORY.md`) |
| **CORE TOWER** | เตาปฏิกรณ์ — เริ่มเกม CORE 30% แล้ว ไม่ต้องสร้าง |

> ❌ **Agri Dome ตัดทิ้ง** (v4.1 §6) — แทนด้วย Granary + Co-60 + Mutation Lab
> ❌ **Hospital ตัดทิ้ง** — แทนด้วย Med Bay
> ❌ **Zone A ตัดทิ้ง** — rad_mine 4.0 คุมความเสี่ยงเหมืองแทน

---

## §7 Phase System — ★ ผูก `core%` ไม่ผูก `day`

> ⚠ **v4.1 ผูก Phase กับ Day (1-5/6-11/12-20/21-30) — ผิดกฎ v5.2 ข้อ 1**
> เปลี่ยนเป็นผูก `core%` ทั้งหมด → **เล่นเก่ง = ไปถึง Phase 4 เร็ว = ได้รางวัลจริง**

| Phase | เงื่อนไข | ชื่อ | ปลดล็อก |
|---|---|---|---|
| **1** | `core < 40` | เอาตัวรอด | โรงไฟ/น้ำ/ฟาร์ม/เหมือง L1 · Shelter L2 |
| **2** | `core >= 40` | ฟื้นฟู | ซ่อมห้องวิจัย · อาคารผลิต L2 · Shelter L3 |
| **3** | `core >= 60` | นิวเคลียร์ | Med Bay · Co-60 · Water/Farm L3 |
| **4** | `core >= 80` | จุดติดเตา | ★ Zone B · Power L3 · Mine L2 |

```csharp
// PhaseManager.cs
public int CurrentPhase =>
    core >= 80 ? 4 :
    core >= 60 ? 3 :
    core >= 40 ? 2 : 1;
```

**Phase ทำหน้าที่ 2 อย่างเท่านั้น:**
1. **UI ป้ายบอกทาง** — ผู้เล่นรู้ว่าตอนนี้อยู่ตรงไหน
2. **Building unlock gate** — กันไม่ให้สร้าง Zone B ตั้งแต่ D1

> ❌ **ห้ามใช้ Phase เป็น trigger ของการ์ด/วิกฤต/ควิซ** — พวกนั้นผูก state ล้วน (ดู §19, §25)

---

## §8 SCRAM (เบรกฉุกเฉิน) — ★ เก็บจาก v4.1

**SCRAM** = ปุ่มดับเตาฉุกเฉิน (ของจริงมีในโรงไฟฟ้านิวเคลียร์ทุกแห่ง — หย่อนแท่งควบคุมดับปฏิกิริยาทันที)

```csharp
// ReactorController.cs
public bool CanScram => heat >= 90 && scramCooldown <= 0;

void Scram() {
    heat  = Max(0, heat - 40);
    core  = Max(0, core - 10);
    water = Max(0, water - 30);
    scramCooldown = 3;              // วัน
    isBoosting = false;             // ★ บังคับออกจาก Boost
    hopeLedger.Report("reactor.scram", "ดับเตาฉุกเฉิน", -3, HopeCategory.Reactor);
}
```

| คีย์ | ค่า |
|---|---|
| `scram_heat_threshold` | 90 |
| `scram_heat_reduce` | −40 |
| `scram_core_penalty` | −10 |
| `scram_water_cost` | 30 |
| `scram_cooldown_days` | 3 |
| `scram_hope_penalty` | −3 |

**เหตุผลที่เก็บ:**
- ผล sim: MELTDOWN แค่ 0–2.8% → บอทแทบไม่ใช้ → **ไม่กระทบสมดุลที่ยืนยันไว้**
- แต่ §33 บอกว่า "ต้องทดสอบกับคนจริง" — คนจริง Boost มั่วกว่าบอทมาก
- SCRAM = ตัวกันคนเล่นรอบแรกแพ้แบบไม่ทันตั้งตัว
- ★ **เป็นความรู้นิวเคลียร์จริง** — ขายกรรมการ NSC ได้

**UI:** ปุ่มสีแดงข้าง HUD เตา · เทาเมื่อ `heat < 90` · แสดง cooldown ถ้าเพิ่งใช้

> **Kova bark ที่เกี่ยวข้อง:** K06 (`heat > 90`) "เก้าสิบแล้ว ถึงร้อยเมื่อไหร่คือจบ ลดโหมดเดี๋ยวนี้"
> ตรงกับจังหวะที่ SCRAM ใช้ได้พอดี

---

## §13 ไอเทม & คราฟต์ (Items)

> **ที่รอดจาก v4.1** — ครึ่งนึงตายไปกับวิกฤตที่ถูกลบ

| ไอเทม | ประเภท | ผลิตจาก | ใช้ทำอะไร |
|---|---|---|---|
| **Deuterium** | เชื้อเพลิง | Extractor (fuel 6/วัน) | ดัน CORE 30→80% |
| **Tritium** | เชื้อเพลิง | ★ Zone B (3.0 / **8.0** ถ้าตอบควิซ) | ★ **บังคับ** ตั้งแต่ CORE 80% |
| **Rad Suit** | อุปกรณ์ | labMat 30/ชุด (ปลดจาก `nuclear_medicine`) | rad ×0.4 · Zone B อยู่ได้ 3.5→8 วัน |
| **เมล็ดฉายรังสี** | เกษตร | Mutation Lab (ปลดจาก `irradiation`) | ผลผลิต +15% (ต้องตอบ `q_mutation`) |
| **Co-60 Chamber** | ถนอมอาหาร | Power 50 (ปลดจาก `irradiation`) | spoil ×0.3 · การ์ด #3 ปลด B |
| **★ โล่พลาสมา** | ชิ้นชนะ | CORE 100% | กางรับพายุรังสี = ชนะเกม |

### ❌ ที่ตัดทิ้ง (ผูกกับวิกฤตที่ถูกลบ)

| v4.1 | เหตุผล |
|---|---|
| **Rad-Gear** | เปลี่ยนชื่อ → Rad Suit (labMat 30) |
| **PET/SPECT** | ผูกวิกฤต 2·A ที่ถูกลบ — เนื้อความรู้ย้ายเข้า `q_nuclear_medicine` |
| **ไอโซโทปการแพทย์** | ผูกวิกฤต 2·B ที่ถูกลบ — เนื้อความรู้ย้ายเข้า note `nuclear_medicine` |

> ★ **หมายเหตุฟิสิกส์ (เก็บไว้ขายกรรมการ):** ฟิวชัน D-T ปล่อยนิวตรอน — "ฟลักซ์นิวตรอน"
> จากเตาใช้ผลิตไอโซโทปการแพทย์จริง (Mo-99/Tc-99m, Lu-177, I-131)
> **และ** ใช้ยิงลิเทียมผลิต Tritium (breeding blanket) — กลไกเดียวกัน คนละปลายทาง

---

## §15 หยุดเวลาตอนวางอาคาร (Placement Pause)

เมื่อผู้เล่นกดเลือกอาคารจากเมนูสร้าง **เวลาในเกมต้องหยุด** ให้คิด/วิเคราะห์ว่าจะวางตรงไหน
จนกว่าจะวางเสร็จ (หรือยกเลิก) เวลาถึงเดินต่อ

### พฤติกรรมที่ต้องได้

- กดอาคารใน Build Menu → เข้า Placement Mode → นาฬิกาหยุด, การ์ดไม่เด้ง
- แสดง **ghost preview** เกาะเคอร์เซอร์ + ต้นทุน + สถานะวางได้/ไม่ได้ (เขียว/แดง)
- ยืนยันวาง → หักต้นทุน, เวลาเดินต่อ
- ยกเลิก (คลิกขวา/Esc/X) → ไม่หักต้นทุน, เวลาเดินต่อ
- วางได้ทีละ 1 อาคาร · **popup การ์ด/Note/Record ก็หยุดเวลาด้วย**

### เงื่อนไขวางได้ (ghost เขียว)

ครบทุกข้อ: ช่องว่างไม่ทับอาคารอื่น · ทรัพยากรพอ · **Phase ปลดล็อกแล้ว (§7)** · อยู่ในพื้นที่อนุญาต

> ⚠ **implement:** ใช้ pause-reason stack (§3) ห้ามตั้ง `Time.timeScale = 0` ตรง ๆ

---

## §16 สถาปัตยกรรม Unity (Architecture)

> **ปรับจาก v4.1** — ลบ manager ที่ตายไปกับระบบเก่า

### Manager / Service

| คลาส | หน้าที่ | สถานะ |
|---|---|---|
| `GameManager` | สเตตแมชชีน, นับวัน, win/lose, สาเหตุแพ้ + จอสรุป | คง |
| `TimeManager` | นาฬิกา 90s (Planning 30s + Live 60s), pause-reason stack | คง |
| `ResourceManager` | คลังทรัพยากร + ผลิต/บริโภครายวัน | คง (ตัด Hope/Knowledge ออก) |
| `WorkerManager` | ★ **แทน PopulationManager** — per-worker state (§17) | **ใหม่** |
| `ShiftSystem` | ★ ระบบพัก rest 70/return 25 (§17.5) | **ใหม่** |
| `HopeLedger` | ★ **แทน Hope field ลอย** — ทุกระบบส่ง HopeEntry (§18) | **ใหม่** |
| `BuildingManager` | ทะเบียนอาคารบนกริด 43×43 + ผลผลิต batch จบวัน | คง |
| `BuildPlacementManager` | ระบบ ghost/วาง/หยุดเวลา (§15) | คง |
| `ReactorController` | CORE%, HEAT, mode (Idle/Boost), เชื้อเพลิง, **SCRAM** (§8, §26) | คง — สูตรใหม่ |
| `ResearchLab` | ★ **แทน CrisisManager trigger** — production building (§19) | **ใหม่** |
| `SoftTriggerWatcher` | ★ ปลด Lead ผูก `core%` ไม่ผูก day (§19) | **ใหม่** |
| `MasteryRegistry` | ★ **แทน Knowledge** — singleton, query จาก 6 ระบบ (§21) | **ใหม่** |
| `CardManager` | ★ **แทน CrisisManager** — 8 ใบ + ตัวเลือกล็อก (§25) | **ใหม่** |
| `BarkManager` | ★ ระบบบทพูดข้างจอ (`BARKS.md`) | **ใหม่** |
| `StormSystem` / `SensorArray` | ระบบพายุ + เกจ (§23) | **ใหม่** |
| `DataRecovery` | ★ ระบบกู้ Record (§24) | **ใหม่** |
| `ZoneBController` / `RadSuitManager` | Zone B + ชุดกันรังสี (§22) | **ใหม่** |
| `PhaseManager` | ★ Phase ผูก `core%` (§7) | คง — logic ใหม่ |
| `MetaProgress` | ★ **Mastery + Codex** ข้ามรอบ (ไม่ใช่ Knowledge แล้ว) | คง — เนื้อใหม่ |
| `UIManager` / `AudioManager` | HUD, popup, Defeat Summary · เสียง | คง |

### ❌ Manager ที่ตัดทิ้ง

| v4.1 | เหตุผล |
|---|---|
| `PopulationManager` | → `WorkerManager` (per-worker state) |
| `QuizManager` | → ควิซไม่บังคับแล้ว อยู่ใน Codex (§21) |
| `DecreeManager` | → Decree = การ์ด #8 ใน `CardManager` |
| `CrisisManager` | → `CardManager` |

### ScriptableObject หลัก

```csharp
enum ResourceType { Power, Water, Food, Iron, LabMat, Deuterium, Tritium }

// ★ แทน InfoCardSO
ResearchNoteSO {
    string noteId, title, category;
    [TextArea] string knowledgeBody;
    int researcherSlots, daysRequired;
    int costPower, costIron, costLabMat;
    string requiredLead;
    string[] prerequisiteNotes;
    BuildingSO[] unlocksBuildings;
    string[] unlocksCommands;
    QuizQuestionSO[] quizzes;
    RecordCardSO linkedRecord;
}

QuizQuestionSO {
    string quizId, linkedNoteId;
    bool requiresApplied;           // ★ ต้องใช้จริงก่อน
    string question; string[] options; int correctIndex;
    string explanation; MasteryBonusSO bonus;
}

CrisisCardSO {
    string cardId, trigger;
    int cooldownDays;
    string unlockedBy;              // ★ noteId ที่ปลดตัวเลือกดี
    CardOption[] options;           // แต่ละอันมี isLocked
}

BuildingSO { id, category, workersRequired, buildCost,
             upkeepPerDay, productionPerDay, unlockPhase, levels[] }

RecordCardSO { recordId, bodyText, unlocksLead }
BarkSO       { barkId, speaker, conditions[], text, priority, cooldown, onceOnly }
MasteryBonusSO { bonusId, targetSystem, value }
```

### Persistence

```
MetaProgress.MasteryBank + UnlockedCodex
  → เก็บนอกเซฟปกติ (PlayerPrefs / ไฟล์ meta)
  → restart รีเซ็ตทุกอย่างยกเว้น 2 ค่านี้
  → ★ ผู้เล่นที่แพ้แล้วเริ่มใหม่ ยังเก่งขึ้นจริง
```

---

## §17-UI · HUD Spec

**HUD แสดงตลอด:**
- แถบทรัพยากร 6 ชนิด (Power/Water/Food/Iron/labMat + fuel)
- **Hope + ปุ่มดู Breakdown** (§18 — บังคับมี)
- CORE% + HEAT + Storm Pressure (ถ้ามี Sensor Array)
- วัน + Phase + ตัวนับเวลา (90s)
- ปุ่มโหมดเตา (Idle / Boost) + **ปุ่ม SCRAM** (§8)
- Build Menu · ปุ่ม Codex · ปุ่ม Records · ปุ่ม "เริ่มวัน"

**สีตามหมวดควิซ (QuizCategory):**

| หมวด | สี | ใช้กับ |
|---|---|---|
| **Reactor** | น้ำเงิน | q_deuterium, q_plasma, q_magnetic_pair, q_dt_fuel, q_tritium_breeding, q_fusion, q_clean_energy |
| **Agriculture** | เขียว | q_mutation, q_food_irradiation |
| **Medical** | แดง | q_nuclear_medicine |
| **Ethics** | เทา | q_alara |

### หลัก UX สำคัญ

- ★ **ควิซไม่บังคับ** — อยู่ใน Codex ผู้เล่นเปิดเอง (ต่างจาก v4.1 สิ้นเชิง)
- ★ **ตัวเลือกล็อกต้องแสดงให้เห็น ห้ามซ่อน** (§25) — ผู้เล่นต้องรู้ว่ามีทางที่ดีกว่า
- ตอบเสร็จแสดง explanation เสมอ ไม่ว่าถูกหรือผิด
- ไม่มีตัวจับเวลากดดันในควิซ/วางอาคาร


---

# ภาค 1 — ระบบ (System Spec)

> **ต่อจากภาค 0** — ส่วนนี้ทับ v4.1 ทั้งหมด

**สถานะ:** ตัวเลขทุกตัวผ่าน balance sim · **0 invariant violations** · drift ≤ 5.5%
**ทดสอบ:** 3,000 รอบ (4 playstyle × 3 ระดับความรู้ × 250 seeds) + stability 2,400 รอบ
**อ้างอิง:** GDD v4.1 · Script v8 · `nrm_sim.py`
**เปลี่ยนจาก v5.0:** ระบบการ์ดกลับมา 8 ใบ · เพิ่ม S7/S8 · แก้บั๊ก #11-18

---

# 0. Diff — สิ่งที่ต้องแก้ในเกมเก่า

## 0.1 ลบทิ้ง

| ของเดิม | เหตุผล |
|---|---|
| `InfoCardSO` (ผูกกับ CrisisSO) | เนื้อความรู้ย้ายเข้า `ResearchNoteSO.knowledgeBody` |
| `AudioLogSO` / VoiceLog | ค้างจาก v3 ไม่ได้ใช้ |
| Info Card "หอเตากลางเมือง" (Day 6) | ระบบห้ามบอกก่อนผู้เล่นวิจัย |
| Info Card "เชื้อเพลิงที่ซ่อนในน้ำ" (Day 11) | ย้ายเข้า Note `deuterium` |
| ควิซที่เด้งทันทีหลังการ์ด | ย้ายเข้า Codex |
| `if (day == X)` ทุกจุด | ยกเว้น Day 30 = เส้นตาย |

## 0.2 แก้

| ระบบเดิม | แก้เป็น |
|---|---|
| `PopulationManager` (ตัวเลขรวม) | per-worker state (§17) |
| `Hope` (field ลอย) | Hope Ledger (§18) |
| `ResearchLab` (trigger) | production building + เริ่มเป็นซาก (§19) |
| `CrisisSO` 3 ใบ | **8 ใบ + ตัวเลือกล็อก** (§25) |
| `QuizQuestionSO` | + `linkedNoteId`, `requiresApplied`, `bonus` |
| สูตร cooling | ★ `water/10` ไม่มีเพดาน = บั๊ก |

## 0.3 เพิ่มใหม่

§17 Worker · §17.5 Shift · §18 Hope Ledger · §19 Research Queue · §20 Bark
§21 Mastery · §22 Zone B & Suits · §23 Storm · §24 Data Recovery · §25 Crisis Cards

---

# 1. หลักการ (ห้ามละเมิด)

1. **ห้าม `if (day == X)`** — ยกเว้น Day 30 (กติกา ไม่ใช่ trigger)
2. **ห้ามระบบบอกคำตอบก่อนวิจัย** — Kova ไม่รู้ ระบบไม่รู้
3. **ความรู้ต้องผลิต** — เวลา + คน + ทรัพยากร
4. **ควิซ = ยืนยันความเข้าใจ** — ไม่บังคับ อยู่ Codex หลังใช้จริง
5. **ไม่มีทางเลือกฟรี**
6. **★ ตัวเลือกที่ดีในการ์ดต้องล็อกจนกว่าจะวิจัย**

---

# 2. Core Loop

```
เจอปัญหา → การ์ดเด้ง (ตัวเลือกดีล็อกอยู่ 🔒)
  → Lead ปลด → ดึงคนไปวิจัย ★ เมืองแย่ลงชั่วคราว
  → ได้ Note → สร้าง/ติดตั้ง
  → การ์ดเด้งอีกครั้ง → ★ คราวนี้ตัวเลือกดีปลดแล้ว
  → ควิซใน Codex → Mastery ถาวร
```

---

---

# §17 Worker State

```csharp
public enum WorkerStatus { Healthy, Tired, Exhausted, Hungry, Sick, Dying, Dead }

public class Worker {
    public int id; public string displayName;
    public string job;          // farm/power/water/mine/lab/cool/zoneb/extract/idle
    public float fatigue, hunger, radiation;   // 0-100
    public WorkerStatus status;
    public bool alive = true, hasRadSuit, resting;
    public string lastJob;
}
```

## Daily Tick (ห้ามสลับ)
```
1. ApplyFatigue  2. ApplyHunger  3. ApplyRadiation
4. MedBayHeal    5. RecalcStatus 6. ProcessDeaths
7. ReportToHopeLedger
```

## สูตร
```csharp
// Fatigue
float g = isWorking ? 12.0f : (barracks ? -45.0f : -30.0f);
if (boosting && job == "cool") g += 6f;
fatigue = Clamp(fatigue + g, 0, 100);

// Hunger -- คนหิวสุดได้กินก่อน (ห้ามสุ่ม)
int need = aliveWorkers.Count;
int avail = Min((int)foodStock, need);
foodStock -= avail;
var q = aliveWorkers.OrderByDescending(w => w.hunger);
int fed = 0;
foreach (var w in q) {
    if (fed < avail) { w.hunger = 0; fed++; }
    else             { w.hunger = Min(w.hunger + 30f, 100f); }   // ★ 30 ไม่ใช่ 20
}

// Radiation
float r = zone switch {
    Zone.Mine => 4.0f, Zone.B => 15.0f, Zone.Core => 6.0f, _ => 0f };
if (heat > 85)   r += 5.0f;
if (stormActive) r += 3.0f;
if (w.hasRadSuit) r *= 0.4f;
if (Mastery("nuclear_medicine")) r *= 0.8f;
w.radiation = Clamp(w.radiation + r, 0, 100);   // ไม่ลดเอง — Med Bay เท่านั้น
```

## Status Table
| Status | เงื่อนไข | Eff | Hope/วัน |
|---|---|---|---|
| Healthy | — | 100% | 0 |
| Tired | fatigue > 60 | 70% | 0 |
| Exhausted | fatigue > 85 | **0%** | −1 |
| **Hungry** | **hunger > 55** | 60% | −2 |
| Sick | radiation > 50 | **0%** | −3 |
| Dying | radiation > 80 | **0%** | −5 |
| Dead | — | — | −8 ครั้งเดียว |

```csharp
float GetEfficiency(Worker w) {
    if (w.fatigue > 85 || w.radiation > 50) return 0f;
    float e = 1f;
    if (w.fatigue > 60) e *= 0.7f;
    if (w.hunger  > 55) e *= 0.6f;
    return e;
}
```

> **⚠ CRITICAL:** Building output ต้องคูณ `Σ GetEfficiency(workers)` **ไม่ใช่นับหัว**
> ไม่ทำ = ระบบ Worker ทั้งหมดไม่มีผล

**Death:** `radiation > 80` → 20%/วัน

---

# §17.5 Shift System

```csharp
const float REST_THRESHOLD = 70f, REST_RETURN = 25f;

void TickShifts() {
    bool labBusy = (researchJob != null);
    foreach (var w in aliveWorkers) {
        if (labBusy && w.job == "lab" && w.fatigue < 92) continue;   // ★ กัน deadlock
        if (!w.resting && w.fatigue >= 70f) {
            w.lastJob = w.job; w.job = "idle"; w.resting = true;
        } else if (w.resting && w.fatigue <= 25f) {
            w.resting = false;
            w.job = (w.lastJob != "idle") ? w.lastJob : "idle";
            w.lastJob = "idle";
        }
    }
}
```
**Tutorial D1 ต้องสอน:** *"คนเหนื่อยเกิน 70 จะพักเอง — เผื่อคนสำรองไว้"*

---

---

# §18 Hope Ledger

**Hope ห้ามเขียนตรง — ทุกระบบส่ง `HopeEntry` เข้า Ledger**

```csharp
public class HopeLedger {
    public float current = 70f;      // ★ เริ่ม 70
    public void Report(string key, string text, float v, HopeCategory c);
    public void OnDayEnd();          // sum → apply → clamp(0,100) → events → clear
    public List<HopeEntry> GetTodayBreakdown();
}
```

## Source Table
| sourceKey | เงื่อนไข | ค่า |
|---|---|---|
| `worker.exhausted` | /คน/วัน | −1 |
| `worker.hungry` | /คน/วัน | −2 |
| `worker.sick` | /คน/วัน | −3 |
| `worker.dying` | /คน/วัน | −5 |
| `worker.death` | ครั้งเดียว | −8 |
| `food.surplus` | food > pop×3 | +3 |
| `food.empty` | food == 0 | −6 |
| `water.shortage` | water < pop | −4 |
| `power.blackout` | ★ ใช้ flag ไม่ใช่ power<0 | −5 |
| `heat.critical` | HEAT > 90 | −3 |
| `storm.active` | พายุ | −1 |
| **`core.progress`** | **/CORE% ที่ขึ้น** | **+1.6** |
| `core.stalled` | ไม่ขึ้น 3 วันติด | −4 |
| `research.complete` | /ใบ | +6 |
| `memorial.visited` | ครั้งแรก | +2 |
| `alara.compliant` | Zone B ชุดครบ | +2 |
| `card.*` | ดู §25 | ตามการ์ด |

## Threshold Events
```csharp
if (current < 60) BarkPool("hope_low");
if (current < 40) StrikeEvent(0.10f);    // 10% หยุดงาน 2 วัน
if (current < 25) ExodusEvent(0.15f);    // ประชากร −15% ถาวร
if (current <= 0) GameOver(HopeZero);
// hysteresis: reset flag เมื่อ current > threshold + 8
```

## UI (บังคับ)
```
HOPE 62 ▼ −6
✕ คนหิว 4 คน   −8    ✓ CORE% +5   +8
✕ คนป่วย 2 คน  −6    ✓ อาหารเต็ม  +3
✕ พายุ         −1    ✓ วิจัยสำเร็จ +6
รวม −6 · แนวโน้ม 7 วัน [กราฟ]
```

---

---

# §19 Research Queue

## Note Table (8 ใบ)
| noteId | slots×days | power | iron | labMat | ปลด |
|---|---|---|---|---|---|
| `deuterium` | 2×2 | 80 | 0 | 0 | 🏗 Extractor |
| `confinement` | 3×2 | 40 | 60 | 0 | ⚙ Toroidal+Poloidal |
| `nuclear_medicine` | 2×3 | 0 | 0 | 60 | 🏗 Med Bay + 🥼 Suit |
| `irradiation` | 3×2 | 120 | 0 | 0 | 🏗 Co-60 / Mutation Lab |
| **`tritium`** | **4×2** | **140** | **60** | 0 | **⚙ Zone B (ประตูชนะ)** |
| `storm_detection` | 2×2 | 60 | 0 | 30 | 🏗 Sensor Array |
| **`food_logistics`** ★ | 2×2 | 0 | 40 | 0 | 🏗 Granary |
| **`shift_management`** ★ | 2×2 | 0 | 50 | 0 | 🏗 Barracks |

> **จ่ายค่าวิจัยครั้งเดียวตอนเริ่ม ไม่ใช่ทุกวัน** (บั๊ก #2)

## Soft Trigger — ผูกกับ `core` ไม่ใช่ `day`
```csharp
float p = core / 100f;
if (core < 100 && fuel <= 0)            UnlockLead("water_analysis");
if (heat > 35f - p*10f)                 UnlockLead("magnetic_theory");     // 35→25
if (AnyWorker(w => w.radiation > 25f - p*6f))  UnlockLead("radiation_biology"); // 25→19
if (food > 45f - p*8f && avgRad > 8f - p*4f)   UnlockLead("food_preservation");
if (core >= 45)                         UnlockLead("lithium_breeding");
if (hungryCount >= 3)                   UnlockLead("food_logistics");      // ★ S7
if (exhaustedCount >= 3)                UnlockLead("shift_management");    // ★ S8
```

## ResearchLab
```csharp
isRuined = true;         // ★ เริ่มเกมเป็นซาก
repairIron = 80; repairDays = 2; repairWorkers = 2;
researcherSlots = 3;     // Lv2→5, Lv3→8
queueCapacity = 1;       // Lv2→2, Lv3→3
```

```csharp
void OnDayEnd() {
    if (activeJob == null) return;
    var labW = GetWorkers("lab");
    if (labW.Count == 0) return;
    float staffRatio = Min(1f, (float)labW.Count / spec.researcherSlots);
    if (staffRatio < 0.5f) return;
    activeJob.progress += 1f * labW.Average(GetEfficiency) * staffRatio;
    if (activeJob.progress >= spec.daysRequired) CompleteResearch();
}
```

> **⚠ CRITICAL — Deadlock:** งานวิจัยต้อง **re-staff ตัวเองทุกเทิร์น**
> ```csharp
> if (activeJob != null && !lab.isRuined) {
>     int need = activeJob.note.researcherSlots - GetWorkers("lab").Count;
>     if (need > 0) {
>         need -= AssignFromIdle(need, "lab");
>         foreach (var donor in new[]{"mine","water","farm"}) {
>             int floor = (donor=="farm"||donor=="water") ? 2 : 1;   // ★ ห้ามต่ำกว่านี้
>             while (need > 0 && GetWorkers(donor).Count > floor) { Move(donor,"lab"); need--; }
>         }
>     }
> }
> ```
> **floor=2** — ไม่งั้นดึงชาวนาไปวิจัยจนอดตาย

## Emergency Preempt (S7/S8)
```csharp
// ทิ้งงานวิจัยปัจจุบันเมื่อวิกฤตคนรุนแรง — progress หาย = ราคาที่จ่าย
if (hungryCount >= 3 && !HasNote("food_logistics") && current != "food_logistics")
    { activeJob = null; }
else if (exhaustedCount >= 3 && !HasNote("shift_management") && current != "shift_management")
    { activeJob = null; }
```

---

---

# §21 Mastery (ควิซ)

```csharp
public class QuizQuestionSO {
    public string quizId, linkedNoteId;
    public bool requiresApplied;      // ★ ต้องใช้จริงก่อน
    public string question; public string[] options; public int correctIndex;
    public string explanation; public MasteryBonusSO bonus;
}
```

| quizId | ต้องทำก่อน | Bonus |
|---|---|---|
| `q_deuterium` | Extractor เดิน 1 วัน | `fuelEfficiency +0.08` |
| `q_plasma` | ติดคอยล์ ≥1 | `cooling +5` |
| `q_magnetic_pair` | ติดครบ 2 | `poloidalDamp ×1.15` |
| `q_nuclear_medicine` | รักษา ≥1 | heal 25→35 · `radiation ×0.8` |
| `q_mutation` | Mutation Lab | ผลผลิต +15% |
| `q_food_irradiation` | Co-60 | เน่า −50% |
| `q_dt_fuel` | ป้อน Tritium | `fuelEfficiency +0.08` |
| **`q_tritium_breeding`** | **Zone B ≥2 วัน** | **★ Zone B: 3.0 → 8.0/วัน** |
| `q_fusion` | CORE ≥95 | Boost heat −10% |

**ตอบผิด:** ไม่มีโทษ · ลองใหม่วันถัดไป · แสดง explanation เสมอ
**ไม่ตอบ:** แพ้ 100% (ยืนยันด้วย sim)

## ★ หัวใจของเกม
```
Zone B ผลิต 3.0/วัน · Boost กิน 9.0/วัน → ขาด 6/วัน → CORE ค้าง 80 → แพ้
ตอบควิซถูก → 8.0/วัน → ดันถึง 100 → ชนะ
```
> **ประโยคสำหรับ NSC:** *"ผู้เล่นที่ไม่ตอบควิซ Tritium Breeding แพ้ 100% ทุก playstyle"*

---

---

# §22 Zone B & Rad Suits

```csharp
rad_zoneb       = 15.0f;   // ป่วยใน ~3.5 วัน · มีชุด ~8 วัน · +Mastery ~10 วัน
zoneb_min_staff = 2;
tritium_base    = 3.0f;    // ★ ไม่ตอบควิซ
tritium_mastery = 8.0f;    // ★ ตอบควิซ
suit_cost       = 30;      // labMat/ชุด · เป้า 5 ชุด · ปลดจาก nuclear_medicine
```

```csharp
void TickZoneBRotation() {
    foreach (var w in GetWorkers("zoneb"))
        if (w.radiation > 32) { w.job = "idle"; w.lastJob = "idle"; }   // ดึงออกก่อนป่วย
    // ส่งเฉพาะ radiation < 30 · เรียงจากน้อยไปมาก
}
```

---

---

# §23 Storm

```csharp
void OnDayEnd() {
    if (isBoosting) boostDaysTotal++;
    float rise = 0.9f
               + boostDaysTotal * 0.30f        // ★ ความก้าวร้าวสะสม
               + (core / 100f) * 1.1f;
    if (core >= 80)    rise += 2.0f;
    if (zoneB.isOpen)  rise += 0.8f;
    pressure = Min(pressure + rise, 100f);
    if (pressure >= 100) TriggerStorm();
}
// ระหว่างพายุ
storm_heat_per_day = 40.0f;   // ★ เดิม 12 = cooling กลบสนิท พายุไร้ผล
storm_rad_per_day  = 3.0f;
```

**Sensor Array:** เห็นเกจ + คาดการณ์วัน · `heat_room += 4`

---

# §24 Data Recovery

```csharp
void OnDayEnd() {
    if (lab.isRuined) return;
    int busy = (activeJob != null) ? Min(labW.Count, activeJob.note.researcherSlots) : 0;
    int idleRes = Max(0, labW.Count - busy);
    float rate = 14.0f + idleRes * 10.0f;      // ★ passive + idle bonus
    if (lab.level >= 2) rate *= 1.3f;
    progress += rate;
    if (progress >= 100) { UnlockNextRecord(); progress = 0; }
}
```

| Record # | ปลด Lead ล่วงหน้า |
|---|---|
| #1 | `water_analysis` |
| #2 | `magnetic_theory` |
| #3 | **`storm_detection`** → Sensor Array |
| #4 | `lithium_breeding` |

> **ผู้เล่นที่เผื่อนักวิจัยว่าง = Record เร็ว = ได้ Lead ล่วงหน้า**
> ผล sim: เล่นดีมาก records 7.1 · sensor 100% ↔ สมดุล records 5.0 · sensor 14%

---

---

# §25 ★ Crisis Cards (8 ใบ)

## หลักการ
```
ยังไม่มีความรู้ → การ์ดโผล่ · ตัวเลือกดีล็อก 🔒 · เหลือแต่ทางแย่
มีความรู้แล้ว  → การ์ดโผล่ · ★ ตัวเลือกดีปลด → ทางเลือกจริง
```
**ผู้เล่นเจอการ์ด 5.8-11.8 ใบ/รอบ · ~38-66% เป็นตอนยังไม่มีความรู้**

## ตารางการ์ด

| # | การ์ด | Trigger | cooldown | ปลดจาก |
|---|---|---|---|---|
| **1** | **เตาร้อน** | HEAT > 62 | 3 | `confinement` |
| **2** | **คนป่วย** | sick ≥ 3 | 5 | `nuclear_medicine` + Med Bay |
| **3** | **เสบียงเน่า** | food>30 && avgRad>6 | 6 | `irradiation` |
| **4** | **★ คนงานหิว** | hungry ≥ 3 | 4 | `food_logistics` → Granary |
| **5** | **★ คนงานหมดแรง** (A/B) | exhausted ≥ 3 | 4 | `shift_management` → Barracks |
| **6** | **Zone B หมดคน** (A/B) | staff < 2 | 4 | — |
| **7** | **Triage** | sick > เตียง && ≥5 | 6 | — |
| **8** | **Decree** | พายุ && cool < 3 | ครั้งเดียว | — |

## ผลของแต่ละตัวเลือก

**#1 เตาร้อน**
```
🔒 ไม่มี confinement → A เร่งไฟ: power −250 · คน 3 คน rad +12 · HEAT −8 · Hope −4
✓  มี confinement   → C ฉีดหล่อเย็น: HEAT −22 · water −40
```
**#2 คนป่วย**
```
🔒 → C กักตัว: fatigue +15 ทุกคนป่วย · Hope −6
✓  → B บำบัด: Med Bay รักษา (heal 25 หรือ 35 ถ้ามี Mastery)
```
**#3 เสบียงเน่า**
```
🔒 → C ลดปันส่วน: food ×0.7
✓  → B ฉาย Co-60: เน่า ×0.3
```
**#4 คนงานหิว**
```
🔒 → B ปันส่วนครึ่ง: food +20 · hunger −20 ทุกคน · Hope −6
✓  → C เปิดคลัง Granary: food +80 · ไม่เสียคน
```
**#5 คนงานหมดแรง (A/B เท่านั้น)**
```
🔒 → B อัดงานต่อ: 30%/คน บาดเจ็บ (rad +20) · Hope −8
✓  → A บังคับพัก: fatigue −60 ทุกคน · Hope +3
```
**#6 Zone B หมดคน (A/B เท่านั้น)**
```
✓ มีคน rad<30 ≥2 → A หมุนคนใหม่เข้า
🔒 ไม่มีคน       → B หยุด Zone B 2 วัน · Hope −4 · ★ Tritium หยุด
```
**#7 Triage** — Hope −10 (ยิ่งไม่มี Med Bay ยิ่งหนัก)
**#8 Decree** — Hope −8

## Trigger ซ้ำได้ไหม
| | |
|---|---|
| **Research Lead** | **ครั้งเดียวตลอดเกม** |
| **การ์ด** | **เกิดซ้ำได้** ตาม cooldown |

---

---

# §26 Reactor Formula

```csharp
// ★ BUG FIX: v4.1 ใช้ water/10 ไม่มีเพดาน → น้ำ 480 → cooling 63 → HEAT=0 ตลอดเกม
float cooling = 6f
              + Min(water / 12f, 7f)              // ★ เพดาน 7
              + coolWorkers * 3f
              + Min(toroidalLv, 3) * 9f;
if (Mastery("confinement")) cooling += 5f;

float poloidalDamp = poloidalLv * 6f;
if (Mastery("confinement")) poloidalDamp *= 1.15f;

float modeHeat = 9f + (boosting ? 14f : 0f);
float stormHeat = stormActive ? 40f : 0f;         // ★ เดิม 12 = ไร้ผล
heat = Max(0, heat + modeHeat + stormHeat - cooling - poloidalDamp);

// ---- CORE ----
if (fuel <= 0) gain = 0;
else if (core >= 80 && tritium < 5) gain = 0;     // ★ Method B gate
else {
    float baseGain = boosting ? 3.0f : 1.05f;
    float fe = Min(1f, fuel / 6f);
    if (Mastery("deuterium")) fe += 0.08f;
    if (Mastery("tritium"))   fe += 0.08f;
    gain = baseGain * fe;
    if (core >= 80) {
        tritium -= boosting ? 9f : 6f;
        tritium = Max(0, tritium);
        if (tritium < 6) gain *= Min(1f, tritium / 6f);
    }
}
core = Min(100, core + gain);

// ★ WIN CONDITION — เช็คทุกวัน ห้ามรอ Day 30
if (core >= 100) { GameWin(); return; }
```

> **⚠ `boost_core_gain = 3.0` เปราะมาก** — 2.6 ยังชนะ · **2.3 = แพ้ทุก playstyle 100%**

---

---

# §27 ค่าเริ่มเกม

```csharp
population = 14;
iron = 200; power = 0; food = 40; water = 90; labMat = 100;
core = 30f; heat = 20f; fuel = 0f; tritium = 0f; hope = 70f;
researchLab.isRuined = true;
memorial.clickable = true;      // ★ เปิดได้ตั้งแต่ D1
```

## ตำแหน่งงาน (pop 14)
| สาย | farm | power | water | mine | lab |
|---|---|---|---|---|---|
| เร่งเตา | 2 | 2 | 2 | 4 | 4 |
| สร้างเมือง | 3 | 2 | 2 | 3 | 3 |
| สมดุล | 3 | 2 | 2 | 4 | 3 |

## Economy/วัน
```
food  += farmWorkers × 9        (−1/คน/วัน)   ★ เดิม 11
water += waterWorkers × 30 − pop × 1.0        (cap 200, floor 0)
iron  += mineWorkers × 14
raw    = power + powerWorkers × 45 − draw
blackout = (raw < 0);  power = Clamp(raw, 0, 400)    // ★ floor 0
labMat += 5
draw = 20 + (extractor?15:0) + (medbay?30:0) + (zoneB?25:0) + (co60?20:0)
spoil = 0.05 + (avgRad/100)×0.15
if (co60) spoil ×= 0.3;  if (granary) spoil ×= 0.6
```
**Population growth:** `food > pop×5 && pop < 20` → 25%/วัน +1 คน (−food 20)

## Buildings
| อาคาร | cost | ปลดจาก | ผล |
|---|---|---|---|
| Deuterium Extractor | เหล็ก 110 | `deuterium` | fuel 6/วัน · water −10/วัน |
| Toroidal Coil | เหล็ก 50 (Lv2/3: 90) | `confinement` | cooling +9/Lv (max 3) |
| Poloidal Coil | เหล็ก 50 (Lv2: 90) | `confinement` | poloidalDamp +6/Lv |
| Med Bay | เหล็ก 90 | `nuclear_medicine` | รักษา 4 คน · rad −25 (−35 ถ้า Mastery) |
| Co-60 Chamber | power 50 | `irradiation` | spoil ×0.3 |
| **Granary** ★ | เหล็ก 50 | `food_logistics` | spoil ×0.6 · การ์ด#4 ปลด C |
| **Barracks** ★ | เหล็ก 60 | `shift_management` | rest −45/วัน · การ์ด#5 ปลด A |
| Sensor Array | — | `storm_detection` | เห็นเกจพายุ · heat_room +4 |

---

---

# §28 ผลทดสอบ (3,000 รอบ)

## Win Rate
| playstyle | ตอบครบ | ตอบครึ่ง | **ไม่ตอบ** | Δ |
|---|---|---|---|---|
| เร่งเตา | 75.6% | 23.2% | **0.0%** | +75.6 ✓ |
| สร้างเมือง | 76.8% | 39.6% | **0.0%** | +76.8 ✓ |
| สมดุล | 54.0% | 24.0% | **0.0%** | +54.0 ✓ |
| เล่นดีมาก | 60.0% | 16.4% | **0.0%** | +60.0 ✓ |

## แพ้คนละแบบ (เกมมีบุคลิก)
| playstyle | MELTDOWN | HOPE=0 | TIMEOUT |
|---|---|---|---|
| เร่งเตา | 0.0% | 0.8% | **23.6%** |
| สร้างเมือง | 0.0% | **23.2%** | 0.0% |
| สมดุล | 0.0% | **46.0%** | 0.0% |
| เล่นดีมาก | **2.8%** | 0.0% | **37.2%** |

## การ์ด 8 ใบ (% ของรอบ)
| การ์ด | เร่งเตา | สร้างเมือง | สมดุล | เล่นดีมาก |
|---|---|---|---|---|
| 1 เตาร้อน | 100% | 100% | 100% | 98% |
| 2 คนป่วย | 84% | 96% | 83% | **25%** |
| 3 เสบียงเน่า | 100% | 100% | 100% | 74% |
| **4 คนหิว** | 28% | 34% | 28% | **0%** |
| 5 หมดแรง | 8% | 100% | 60% | 46% |
| 6 ZoneB หมดคน | 100% | 100% | 100% | 61% |
| 7 Triage | 16% | 65% | 58% | **7%** |
| 8 Decree | 100% | 100% | 100% | 100% |

## ตัวเลือกล็อก
| playstyle | การ์ด/รอบ | ล็อก | ปลดแล้ว | %ล็อก |
|---|---|---|---|---|
| เร่งเตา | 9.4 | 5.1 | 4.3 | 54% |
| สร้างเมือง | 11.8 | 7.8 | 4.0 | 66% |
| สมดุล | 10.1 | 6.6 | 3.6 | 65% |
| **เล่นดีมาก** | **5.8** | **2.2** | 3.6 | **38%** |

## ความรู้
| playstyle | notes | mastery | records | sensor | ตาย |
|---|---|---|---|---|---|
| เร่งเตา | 4.4/8 | 3.9 | 4.8 | 70% | 0.21 |
| สร้างเมือง | 5.3/8 | 4.3 | 5.4 | 60% | 0.07 |
| สมดุล | 4.7/8 | 3.7 | 5.0 | 14% | 0.04 |
| **เล่นดีมาก** | 4.9/8 | 3.9 | **7.1** | **100%** | 0.06 |

## คุณภาพ
```
Invariant violations : 0 / 600 runs
Stability drift      : ≤ 5.5% (3 seed blocks อิสระ)
```

---

---

# §30 บั๊กที่ sim เจอ (18 ตัว)

| # | บั๊ก | ผลถ้าไม่แก้ |
|---|---|---|
| 1 | `cooling = 15 + water/10` ไม่มีเพดาน | HEAT = 0 ตลอดเกม เตาไร้ความหมาย |
| 2 | หักค่าวิจัยทุกวัน | Tritium ดูดเหล็กจนบล็อกตัวเอง วิจัยได้ใบเดียว |
| 3 | ควิซเช็ค `tritium > 0` | tritium เผาเป็น 0 ทุกเทิร์น → Mastery ปลดได้ 1/40 |
| 4 | Zone B ไม่มีคน | เปิดแล้ว staff=0 ตลอด 14 วัน |
| 5 | ไม่มีระบบกะ | ทั้งเมือง Exhausted ถาวร D8 |
| 6 | Lead threshold ผิด | HEAT>65 ไม่เคยเกิด · rad>45 ต้องใช้ 45 วัน |
| 7 | **Research deadlock** | นักวิจัยถูกดึงออก → job ค้าง progress 1.4 ตลอดกาล |
| 8 | 14 คน ทำ 18 ตำแหน่งไม่ได้ | mine=0 → เหล็กค้าง → แพ้ 100% |
| 9 | ดึงชาวนาไปวิจัย | อาหารหมด D22 → Hope 0 |
| 10 | Record #5 มา D30 | Sensor Array ใช้ไม่ได้ |
| 11 | **`lead_map` ไม่มี note ใหม่** | โน้ตใหม่ถูกข้ามตลอด ไม่มีวันวิจัยได้ |
| 12 | hunger 20/วัน ช้าเกิน | ต้องอด 4 วันติด → S7 ไม่เคยเกิด |
| 13 | preempt ตั้ง ≥5 | ระบบกะจำกัด exhausted ที่ 4 → ไม่มีวันถึง |
| 14 | **`storm_heat = 12`** | cooling กลบสนิท → HEAT ตอนพายุ = 1.0 |
| 15 | **★ CORE 100 แล้วเกมไม่จบ** | รันต่อถึง D30 → Hope ไหลจนแพ้ **ทั้งที่ชนะแล้ว** |
| 16 | AI ไม่อัปคอยล์ | toroidal ค้าง Lv1 → cooling นิ่ง |
| 17 | **power ติดลบได้** | −395 → สูตรเพี้ยนทั้งระบบ |
| 18 | **water ติดลบได้** | −10 → cooling คำนวณผิด |

---

---

# §31 ไฟล์ที่ต้องสร้าง

```
Scripts/
  Workers/   Worker.cs · WorkerManager.cs · ShiftSystem.cs
  Hope/      HopeLedger.cs · HopeEntry.cs · HopeThresholdWatcher.cs
             UI/HopeBreakdownPanel.cs        ← บังคับ
  Research/  ResearchNoteSO.cs · ResearchLeadSO.cs · ResearchLab.cs
             SoftTriggerWatcher.cs · KnowledgeDB.cs
             UI/ResearchQueuePanel.cs · UI/NoteCardPopup.cs
  Mastery/   MasteryBonusSO.cs · MasteryRegistry.cs      ← singleton
  Cards/     CrisisCardSO.cs · CardOption.cs · CardManager.cs
             UI/CrisisCardPanel.cs           ← แสดง 🔒 บนตัวเลือกที่ล็อก
  Barks/     BarkSO.cs · BarkManager.cs
  Storm/     StormSystem.cs · SensorArray.cs
  Records/   RecordCardSO.cs · DataRecovery.cs · UI/RecordsPanel.cs
  ZoneB/     ZoneBController.cs · RadSuitManager.cs
```

**MasteryRegistry ต้อง query จาก:** ReactorController · WorkerManager · MedBay · Farm · FoodStorage · ZoneB

---

# §32 Sprint Plan

| Sprint | งาน | Test ที่ต้องผ่าน |
|---|---|---|
| **1** | Worker + Shift + HopeLedger + UI | รัน D1-30 → Hope ไม่ 100 ตลอด · เห็นคนหิว/ป่วย |
| **2** | ResearchQueue + SoftTrigger + gating | ต้อง**ติดขัด**ถ้าไม่วิจัย |
| **3** | MasteryRegistry + Codex quiz | ตอบ vs ไม่ตอบ → ต่างกันชัด |
| **4** | **CrisisCards 8 ใบ + ตัวเลือกล็อก** | เห็น 🔒 บนตัวเลือกที่ยังไม่วิจัย |
| **5** | Bark + DataRecovery + ZoneB | Death Spiral เกิดจริง |
| **6** | Storm + Sensor + Endings | รัน sim เทียบ |

---

# §33 ที่ยังต้องแก้ (ยอมรับตรงๆ)

1. **พายุ D22-23 ทุก playstyle** — บอทเล่นเหมือนกัน (boost 19-20 วัน) → **ต้องทดสอบกับคนจริง**
2. **D3/D5 เหมือนกันหมด** — เหล็กเริ่ม 200 เท่ากัน → แนะนำสุ่ม 160-240
3. **การ์ด #8 Decree = 100% ทุกสาย** — เกิดแน่นอน ไม่ใช่ "วิกฤต"
4. **`boost_core_gain` เปราะ** — ห้ามแตะโดยไม่รัน sim

---

*GDD v5.1 · 3,000 runs · 0 invariant violations · drift ≤5.5% · อ้างอิง nrm_sim.py*

---

---

*GDD รวม · v4.1 (§1–§16) + v5.2 (§17–§33) · 3,000 runs · 0 invariant violations · drift ≤5.5% · อ้างอิง `nrm_sim.py`*
