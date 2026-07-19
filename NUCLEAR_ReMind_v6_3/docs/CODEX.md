# CODEX — คลังความรู้

**สำหรับ:** `CodexEntrySO` assets + Codex panel · **Sprint 3**
**หลักการ:** ระบบระดับเมตา · ไม่ผูกกับอาคารใดๆ · คงข้ามรอบเล่น

> **แปลงจาก:** Codex Spec v4.0 → กติกา v5.2
> ตัด Day-based unlock · ตัด Knowledge · ตัด Zone A · ตัด VESTA

---

## 1. Codex คืออะไร

คลังความรู้นิวเคลียร์ที่รวบรวมทุกหัวข้อที่ผู้เล่นปลดล็อกระหว่างเล่น
**ตอบควิซถูก 1 ข้อ → ได้ 1 entry** เก็บเข้าคลัง เปิดอ่านทบทวนได้ตลอดเวลา

### จุดสำคัญ

```
Codex = ข้อมูลถาวร ข้ามรอบเล่น ไม่รีเซ็ตเหมือนทรัพยากรอื่น
ผู้เล่นแพ้แล้วเริ่มใหม่ → ของในคลังยังอยู่ครบ
เก็บใน MetaProgress.UnlockedCodex (GDD §16)
```

### ความรู้แยก 2 ชั้น (v5.2)

| ชั้น | ที่อยู่ | บทบาท |
|---|---|---|
| **ให้ความรู้** | `ResearchNoteSO.knowledgeBody` (`NOTES.md`) | มา**ก่อน** — ผู้เล่นวิจัยได้ Note → อ่านความรู้ |
| **ทบทวน** | ควิซใน Codex (`QUIZZES.md`) | มา**หลัง** — ยืนยันว่าเข้าใจ → ปลด Codex entry |

> ⚠ **ต่างจาก v4.0:** เดิมใช้ `InfoCardSO` ให้ความรู้ — v5.2 ตัดทิ้ง ย้ายเข้า `ResearchNoteSO.knowledgeBody` (GDD §0.1)

---

## 2. เข้าถึงจากไหน

เปิดจาก **ปุ่ม Codex บน HUD หลัก** — ไม่ใช่ในห้องวิจัย
เข้าได้ตลอดเวลา **แม้ห้องวิจัยยังเป็นซากหรือถูกทำลาย** เพราะเป็นระบบเมตาถาวร

### ⚠ Codex ≠ Records (คนละแผง)

| | Codex | Records |
|---|---|---|
| **เก็บอะไร** | ความรู้นิวเคลียร์ 11 entry | บันทึก Dr. Elara Vane 4 ใบ |
| **ปลดยังไง** | ตอบควิซถูก | `DataRecovery.progress >= 100` |
| **ถาวรข้ามรอบ** | ✅ ใช่ | ❌ ไม่ (รีเซ็ตทุกรอบ) |
| **ไฟล์** | `CODEX.md` (นี่) | `STORY.md` |

> v8 บอกว่าจะทำ Records เป็นแท็บใน Codex panel เดิม **หรือ**แยกแผงก็ได้
> **แนะนำแยก** — เพราะ Codex ถาวร Records ไม่ถาวร ปนกันแล้วผู้เล่นสับสน

---

## 3. Entry ทั้งหมด 11 อัน

> ★ **map 1:1 กับ `QUIZZES.md`** — ทุก entry ปลดจากควิซที่ตรงกัน

| # | `entryId` | หัวข้อ (ไทย / EN) | หมวด | ปลดจาก `quizId` |
|---|---|---|---|---|
| 1 | `codex_deuterium` | ดิวเทอเรียม · Deuterium | Reactor | `q_deuterium` |
| 2 | `codex_plasma_confinement` | การกักพลาสมา · Plasma Confinement | Reactor | `q_plasma` |
| 3 | `codex_magnetic_confinement` | สนามแม่เหล็กคู่ · Magnetic Confinement | Reactor | `q_magnetic_pair` |
| 4 | `codex_nuclear_medicine` | เวชศาสตร์นิวเคลียร์ · Nuclear Medicine | Medical | `q_nuclear_medicine` |
| 5 | `codex_alara` | หลัก ALARA · ALARA | Ethics | `q_alara` |
| 6 | `codex_mutation_breeding` | ปรับปรุงพันธุ์ด้วยรังสี · Mutation Breeding | Agriculture | `q_mutation` |
| 7 | `codex_food_irradiation` | ฉายรังสีถนอมอาหาร · Food Irradiation | Agriculture | `q_food_irradiation` |
| 8 | `codex_dt_fusion_fuel` | เชื้อเพลิงคู่ D–T · D–T Fusion Fuel | Reactor | `q_dt_fuel` |
| 9 | `codex_tritium_breeding` | ★ การเพาะทริเทียม · Tritium Breeding | Reactor | `q_tritium_breeding` |
| 10 | `codex_nuclear_fusion` | ฟิวชันคืออะไร · Nuclear Fusion | Reactor | `q_fusion` |
| 11 | `codex_clean_energy` | ทำไมฟิวชันสะอาด · Clean Energy | Reactor | `q_clean_energy` |

### ⚠ สิ่งที่แก้จาก Codex Spec v4.0

| v4.0 | v5.2 | เหตุผล |
|---|---|---|
| ปลดตาม "Day 11 / ~Day 17 / ~Day 24" | ❌ **ตัด Day ทิ้งทั้งหมด** | กฎข้อ 1: ห้าม `if (day == X)` |
| "แก้วิกฤต 1/2/3 เสร็จ" | ❌ **เปลี่ยนเป็น `requiresApplied`** | วิกฤต 3 ใบเก่าถูกลบ → การ์ด 8 ใบ |
| "ส่งคนเข้า **Zone A**" | ❌ **→ Zone B / Mine** | Zone A ตัดทิ้ง (GDD §6) |
| **+2 Knowledge** ครั้งแรก | ❌ **ตัด** | Knowledge ตัดทิ้ง → Mastery แทน |
| ควิซ #10 = ALARA ซ้ำ (ไม่ปลด entry ใหม่) | ✅ **`q_alara` แยกเป็น Q5** + `q_clean_energy` เป็น Q11 | v5.2 มี 11 ควิซ = 11 entry พอดี |
| "ผู้บรรยาย: VESTA" | ❌ **ตัด** | v8 ตัด VESTA ออกแล้ว |
| หน้าจอโชว์ "5 / 9" | ❌ **→ x / 11** | เพิ่ม entry Tritium 2 อัน |

---

## 4. CodexEntrySO — ฟิลด์

```csharp
[CreateAssetMenu(menuName = "NRM/Codex Entry")]
public class CodexEntrySO : ScriptableObject {
    public string entryId;          // เช่น codex_nuclear_fusion
    public string titleTh;          // ชื่อหัวข้อไทย
    public string titleEn;          // ชื่อหัวข้ออังกฤษ
    public QuizCategory category;   // Reactor / Medical / Agriculture / Ethics
    [TextArea(4, 10)]
    public string bodyText;         // เนื้อความรู้ (= explanation ของควิซที่ปลดมัน)
    public string iconName;         // ไอคอนหน้า entry
    public string unlockedFromQuiz; // ★ quizId ที่ปลด — ไม่ใช่ "Day X"

    // ❌ ไม่มี isUnlocked ใน SO — เป็น state
    //    อ่านจาก MetaProgress.UnlockedCodex.Contains(entryId)
    // ❌ ไม่มี speaker — v8 ตัด VESTA แล้ว
}
```

> ★ **`bodyText` = `explanation` ของควิซที่ปลดมัน** — ไม่ต้องเขียนใหม่
> ก๊อปจาก `QUIZZES.md` ได้ตรงๆ (ดูข้อ 7)

---

## 5. Flow ปลดล็อก

```csharp
// CodexManager.cs
void OnQuizAnswered(QuizQuestionSO quiz, bool correct) {
    if (!correct) return;                    // ตอบผิด → ไม่ปลด แต่ลองใหม่วันถัดไปได้

    var entry = codexDB.FindByQuiz(quiz.quizId);
    if (entry == null) return;
    if (MetaProgress.UnlockedCodex.Contains(entry.entryId)) return;  // ปลดแล้ว

    MetaProgress.UnlockedCodex.Add(entry.entryId);
    MetaProgress.Save();                     // ★ ถาวร ข้ามรอบเล่น
    ui.ShowCodexUnlockedToast(entry.titleTh);
}
```

```
ตอบควิซ q_fusion ถูก → แสดง explanation
  → ปลด codex_nuclear_fusion
  → เก็บเข้า MetaProgress.UnlockedCodex → Save()
  → ผู้เล่นเปิด Codex panel มาอ่านได้
```

> ❌ **ไม่มี "+2 Knowledge ครั้งแรก"** แล้ว — Knowledge ถูกตัดทิ้ง (GDD §0.2)
> รางวัลของการตอบควิซคือ **Mastery Bonus** (ดู `QUIZZES.md`) ไม่ใช่แต้ม

---

## 6. หน้าจอ Codex

### โครง 3 ส่วน

```
┌────────────────────────────────────────────────┐
│  คลังความรู้ (Codex)          ปลดแล้ว 7 / 11   │  ← ส่วนหัว
│  บันทึกถาวร — ข้ามรอบเล่น                       │
├────────────────────────────────────────────────┤
│ [ทั้งหมด] [เตา/ฟิวชัน] [แพทย์] [เกษตร] [จริยธรรม]│  ← แถบกรอง
├──────────────────┬─────────────────────────────┤
│ ● ดิวเทอเรียม     │  ดิวเทอเรียม                 │
│ ● การกักพลาสมา    │  [เตา/ฟิวชัน] ปลดจาก Q1      │
│ ● สนามแม่เหล็กคู่  │                             │
│ 🔒 ? ? ?          │  ดิวเทอเรียม (²H) เป็น...    │
│ ● เวชศาสตร์นิวฯ   │                             │
│ 🔒 ? ? ?          │                             │
└──────────────────┴─────────────────────────────┘
     ลิสต์ซ้าย            รายละเอียดขวา
```

### แถบกรอง 5 ปุ่ม

| หมวด (tab) | จำนวน | entry |
|---|---|---|
| **เตา/ฟิวชัน** (Reactor) | **7** | Deuterium, Plasma Confinement, Magnetic Confinement, D–T Fusion Fuel, Tritium Breeding, Nuclear Fusion, Clean Energy |
| **แพทย์/รังสี** (Medical) | **1** | Nuclear Medicine |
| **เกษตร** (Agriculture) | **2** | Mutation Breeding, Food Irradiation |
| **จริยธรรม** (Ethics) | **1** | ALARA |

> **การนับบนหัวจอ:** `"ปลดล็อกแล้ว x / 11"` — นับจาก entry ที่ปลดทั้งหมด
> **ไม่ใช่**นับต่อหมวด (ตอนกรองหมวด ตัวเลขหัวจอยังเป็น x/11 เท่าเดิม)

### สีตามหมวด (ตรงกับ GDD §17-UI)

| หมวด | สี |
|---|---|
| Reactor | น้ำเงิน |
| Medical | แดง |
| Agriculture | เขียว |
| Ethics | เทา |

### Entry ที่ยังไม่ปลด

- โชว์เป็น **`? ? ?`** + ไอคอนแม่กุญแจ 🔒
- **ห้ามซ่อน** — ให้ผู้เล่นเห็นว่ายังมีให้เก็บอีกกี่อัน = แรงจูงใจเล่นซ้ำ
  (หลักการเดียวกับตัวเลือกล็อกในการ์ด — `CARDS.md`)
- กดแล้วแผงขวาโชว์: **"ยังไม่ปลดล็อก — ปลดได้จากควิซ [ชื่อ]"** = hint

### ★ จุดแดงเมื่อมีควิซใหม่

```
[Codex ●]  ← จุดแดงบนปุ่ม HUD เมื่อมีควิซที่ requiresApplied ผ่านแล้วแต่ยังไม่ตอบ
```
(ตรงกับ UI Spec ใน `QUIZZES.md`)

---

## 6.1 ไอคอนแนะนำ

| entry | `iconName` (สไตล์ Tabler) |
|---|---|
| ดิวเทอเรียม | `droplet` |
| การกักพลาสมา | `flame` |
| สนามแม่เหล็กคู่ | `magnet` |
| เชื้อเพลิงคู่ D–T | `atom-2` |
| การเพาะทริเทียม | `atom` |
| เวชศาสตร์นิวเคลียร์ | `stethoscope` |
| หลัก ALARA | `shield` |
| ปรับปรุงพันธุ์ด้วยรังสี | `seeding` |
| ฉายรังสีถนอมอาหาร | `meat` |
| ฟิวชันคืออะไร | `atom-2` |
| ทำไมฟิวชันสะอาด | `leaf` |

---

## 7. เนื้อหาครบทุก entry (ก๊อปใส่ `bodyText`)

> ★ **= `explanation` ของควิซที่ปลดมัน** — ตรงกับ `QUIZZES.md` 100%

### 1 · `codex_deuterium` — ดิวเทอเรียม [Reactor]

```
ปลดจาก: q_deuterium

ดิวเทอเรียม (²H) เป็นไอโซโทปของไฮโดรเจนที่ปนอยู่ในน้ำทั่วไปอยู่แล้ว
เราจึงแยกออกมาใช้เป็นเชื้อเพลิงฟิวชันได้เลย โดยไม่ต้องผลิตขึ้นใหม่
```

### 2 · `codex_plasma_confinement` — การกักพลาสมา [Reactor]

```
ปลดจาก: q_plasma

ในโทคาแมกพลาสมาร้อนหลายล้านองศาถูกกักด้วยสนามแม่เหล็ก
ถ้าสนามไม่นิ่ง พลาสมาหลุดไปชนผนัง จะถ่ายเทความร้อนเข้าตัวอาคารจนหลอมละลาย
```

### 3 · `codex_magnetic_confinement` — สนามแม่เหล็กคู่ [Reactor]

```
ปลดจาก: q_magnetic_pair

สนามแม่เหล็กคู่ทำงานร่วมกัน — Toroidal บีบพลาสมาให้เป็นวง
ส่วน Poloidal กันไม่ให้พลาสมาชนผนัง
เมื่อเสริมให้แข็งแรงจะกักพลาสมาไว้กลางเตาและรีดความร้อนที่รั่วออก
```

### 4 · `codex_nuclear_medicine` — เวชศาสตร์นิวเคลียร์ [Medical]

```
ปลดจาก: q_nuclear_medicine

เวชศาสตร์นิวเคลียร์ทำงาน 2 ขั้น —
(1) วินิจฉัย: PET/SPECT ฉีดสารเภสัชรังสีถ่ายภาพหาตำแหน่งเซลล์ผิดปกติ
(2) รักษา: ยาเฉพาะจุด (targeted therapy) ส่งรังสีไปทำลายเฉพาะเป้า กระทบเนื้อดีน้อย
```

### 5 · `codex_alara` — หลัก ALARA [Ethics]

```
ปลดจาก: q_alara

ALARA (As Low As Reasonably Achievable) คือ "ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้"
และต้องปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ
```

### 6 · `codex_mutation_breeding` — ปรับปรุงพันธุ์ด้วยรังสี [Agriculture]

```
ปลดจาก: q_mutation

การฉายรังสีกระตุ้นให้เกิดการกลายพันธุ์
นักวิจัยคัดเลือกเฉพาะสายพันธุ์ที่ทนทานและให้ผลผลิตสูงไว้ใช้ถาวร
เช่น ข้าว กข 6 ของไทย — เป็นการแก้ปัญหาที่ต้นเหตุ
```

### 7 · `codex_food_irradiation` — ฉายรังสีถนอมอาหาร [Agriculture]

```
ปลดจาก: q_food_irradiation

รังสีแกมมาทะลุผ่านอาหารและฆ่าจุลินทรีย์/เชื้อรา ทำให้เก็บได้นานขึ้น
โดยอาหารไม่กลายเป็นสารกัมมันตรังสี (อาหารฉายรังสี ≠ อาหารมีรังสี)
```

### 8 · `codex_dt_fusion_fuel` — เชื้อเพลิงคู่ D–T [Reactor]

```
ปลดจาก: q_dt_fuel

เชื้อเพลิงที่หลอมรวมได้ง่ายที่สุดคือคู่ ดิวเทอเรียม–ทริเทียม (D–T)
เพราะจุดติดที่อุณหภูมิต่ำกว่าคู่อื่น
ดิวเทอเรียมจากน้ำพาเตาขึ้นมาได้ระดับหนึ่ง
แต่การจะดันถึงจุดติดเต็มรอบต้องมีทริเทียมป้อนคู่
```

### 9 · ★ `codex_tritium_breeding` — การเพาะทริเทียม [Reactor]

```
ปลดจาก: q_tritium_breeding

ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตได้ด้วยการนำนิวตรอนที่เกิดจากปฏิกิริยาฟิวชัน
ไปยิงใส่ลิเทียม (breeding blanket) ลิเทียมจะแตกตัวให้ทริเทียม
เตาฟิวชันจึงสามารถผลิตเชื้อเพลิงส่วนหนึ่งของตัวเองได้
เรียกว่า tritium breeding
```

> ★★★ **entry ที่สำคัญที่สุดในเกม** — ควิซนี้คือประตูชนะ
> ไม่ตอบ → Zone B ผลิต 3.0/วัน → ขาด 6/วัน → CORE ค้าง 80 → **แพ้ 100%**

### 10 · `codex_nuclear_fusion` — ฟิวชันคืออะไร [Reactor]

```
ปลดจาก: q_fusion

ฟิวชันคือการหลอมรวมนิวเคลียสเบา (เช่น ไฮโดรเจน) ให้กลายเป็นธาตุที่หนักกว่า
แล้วปลดปล่อยพลังงานมหาศาล
"ตรงข้าม" กับฟิชชันที่เป็นการแตกตัวของนิวเคลียสหนัก
```

### 11 · `codex_clean_energy` — ทำไมฟิวชันสะอาด [Reactor]

```
ปลดจาก: q_clean_energy

ฟิวชันสะอาดกว่าเพราะเชื้อเพลิงหาได้จากน้ำ ไม่ปล่อย CO₂
ถ้าเสียสมดุลเตาจะดับเอง (ไม่ระเบิด)
และไม่มีกากรังสีอายุยืนแบบฟิชชัน
— แต่ "สะอาดกว่า" ไม่ได้แปลว่า "ไม่มีรังสีเลย" เพราะเชื้อเพลิง D-T ยังปล่อยนิวตรอน
```

---

## 8. งานที่ต้องทำจริง (ประเมิน)

`MetaProgress.UnlockedCodex` เขียนไว้แล้วใน GDD §16 — งานที่เหลือ:

- [ ] สร้าง `CodexEntrySO` 11 ตัว + กรอกข้อมูลจากข้อ 7
- [ ] เพิ่ม field `codexId` ใน `QuizQuestionSO` (หรือ lookup จาก `unlockedFromQuiz`)
- [ ] ต่อ `OnQuizAnswered` → ปลด entry → `Save()`
- [ ] ทำ Codex panel 1 จอ (เปิดจากปุ่ม HUD)
- [ ] จุดแดงบนปุ่ม HUD เมื่อมีควิซใหม่พร้อมตอบ

**ประมาณ 1 วันโปรแกรมเมอร์** — logic เก็บถาวรมีแล้ว เหลือแค่ UI + กรอกข้อมูล

---

## 9. สรุปสิ่งที่ Codex **ไม่ทำ** (กันสับสน)

- ❌ **ไม่ผูกกับห้องวิจัย** — เป็นคนละระบบ เข้าจากปุ่ม HUD
- ❌ **ไม่เก็บบันทึก Elara** — นั่นคือแผง Records (`STORY.md`)
- ❌ **ไม่รีเซ็ตตอน restart** — คงอยู่ถาวรพร้อม Mastery
- ❌ **ไม่ให้ Knowledge** — Knowledge ถูกตัดทิ้งแล้ว รางวัลคือ Mastery Bonus
- ❌ **ไม่บังคับเปิด** — ผู้เล่นไม่เปิดเลยก็เล่นจบได้ (แต่ไม่ตอบควิซ = แพ้ 100%)
