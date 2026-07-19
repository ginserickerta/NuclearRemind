# ภาค 2 — Research Notes

**สำหรับ:** `ResearchNoteSO` assets · Sprint 2
**สถานะ:** ตัวเลขตรง GDD v5.1 · เนื้อความรู้อ้างอิง Info Card v8 (ใช้ต่อได้เกือบหมด)
**ไม่ผูกวัน** — เขียนได้เลย ไม่ต้องรอ sim

---

### กฎการเขียน knowledgeBody

1. **3-5 บรรทัด** อ่านจบใน 15 วินาที
2. **ไม่ต้องบอกว่า "จำไว้นะ เดี๋ยวมีควิซ"** — ควิซอยู่ Codex คนละที่
3. **บรรทัดสุดท้าย = ผลในเกม** (→ ปลดอะไร)
4. **ห้ามอุปมา ห้ามคำคม** — นี่คือรายงานวิจัย ไม่ใช่บทละคร
5. **ศัพท์อังกฤษวงเล็บไว้** ให้ผู้เล่นเอาไปค้นต่อได้
6. **บทตอนวิจัยเสร็จ = 1 บรรทัด** — NPC ตอบสนอง ไม่ทวนสิ่งที่ knowledgeBody เพิ่งบอก

---

## 1. `deuterium` — เชื้อเพลิงที่ซ่อนอยู่ในน้ำ

```yaml
noteId:           deuterium
title:            เชื้อเพลิงที่ซ่อนอยู่ในน้ำ
category:         เชื้อเพลิง
researcherSlots:  2
daysRequired:     2
costPower:        80
costIron:         0
costLabMat:       0
requiredLead:     water_analysis
prerequisites:    []
unlocksBuildings: [DeuteriumExtractor]
unlocksCommands:  []
quizzes:          [q_deuterium]
linkedRecord:     record_02
```

**knowledgeBody:**
```
น้ำทั่วไปมีไอโซโทปของไฮโดรเจนชนิดหนึ่งชื่อ ดิวเทอเรียม (²H) ปนอยู่แล้ว
ในน้ำทุกหยด มีดิวเทอเรียมประมาณ 1 ใน 6,400 อะตอมของไฮโดรเจน

เราไม่ต้องผลิตมันขึ้นใหม่ แค่ แยกมันออกจากน้ำ ก็ได้เชื้อเพลิงป้อนเตาฟิวชันได้เลย

→ ปลดล็อก: Deuterium Extractor (ต้องวางติดโรงน้ำ)
```

**Lead hint (แสดงในเมนูวิจัยก่อนกดเริ่ม):**
```
"คลังน้ำของเมืองอาจมีมากกว่าที่เราคิด"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
KOVA: "อยู่ในน้ำที่เราดื่มทุกวันเนี่ยนะ เอาเครื่องมาต่อโรงน้ำเลย"
```

---

## 2. `confinement` — ขังพลาสมาด้วยสนามแม่เหล็ก

```yaml
noteId:           confinement
title:            ขังพลาสมาด้วยสนามแม่เหล็ก
category:         เตาปฏิกรณ์
researcherSlots:  3
daysRequired:     2
costPower:        40
costIron:         60
costLabMat:       0
requiredLead:     magnetic_theory
prerequisites:    []
unlocksBuildings: [ToroidalCoil, PoloidalCoil]
unlocksCommands:  [install_toroidal, install_poloidal]
quizzes:          [q_plasma, q_magnetic_pair]
linkedRecord:     record_03
```

**knowledgeBody:**
```
ในเตาฟิวชัน เชื้อเพลิงร้อนจนกลายเป็น พลาสมา หลายล้านองศา
ร้อนเกินกว่าผนังวัสดุใดจะทนได้ เราจึงใช้ สนามแม่เหล็ก ขังมันไว้กลางเตา ไม่ให้แตะผนัง

ถ้าสนามอ่อนลง พลาสมาจะหลุดชนผนัง ถ่ายเทความร้อนเข้าตัวอาคาร แล้วเตาจะหลอมละลาย

ขดลวด 2 ชนิดทำงานคู่กัน:
• Toroidal Coils — ขดลวดวงรอบ บีบพลาสมาให้วิ่งเป็นวง → ยกเพดานกำลังหล่อเย็น
• Poloidal Coils — ขดลวดรัดแนวตั้ง กันพลาสมาชนผนัง → ลด HEAT ต่อวัน

→ ปลดล็อก: Toroidal Coil · Poloidal Coil (ที่หอควบคุมเตา)
```

**Lead hint:**
```
"ความร้อนขึ้นเร็วเกินที่หล่อเย็นจะรับไหว"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
KOVA: "คอยล์สองตัว ตัวหนึ่งกันชนผนัง อีกตัวรีดความร้อน ติดตัวเดียวไม่พอนะ"
```

---

## 3. `nuclear_medicine` — รังสีกับร่างกายคน

```yaml
noteId:           nuclear_medicine
title:            รังสีกับร่างกายคน
category:         การแพทย์
researcherSlots:  2
daysRequired:     3
costPower:        0
costIron:         0
costLabMat:       60
requiredLead:     radiation_biology
prerequisites:    []
unlocksBuildings: [MedBay, RadSuit]
unlocksCommands:  [cmd_scan, craft_radsuit]
quizzes:          [q_nuclear_medicine, q_alara]
linkedRecord:     null
```

**knowledgeBody:**
```
รังสีปริมาณสูงทำลายเซลล์ในร่างกาย ทำให้เนื้อเยื่อโตผิดปกติได้

เวชศาสตร์นิวเคลียร์ (Nuclear Medicine) ทำงาน 2 ขั้น:
• วินิจฉัย — ฉีดสารเภสัชรังสีเข้าไป "ถ่ายภาพ" หาตำแหน่งก่อน (PET / SPECT)
• รักษา — ส่งรังสีไป "ทำลาย" เฉพาะจุด กระทบเนื้อดีน้อย

หลัก ALARA (As Low As Reasonably Achievable) — ให้คนรับรังสีน้อยที่สุดเท่าที่ทำได้
และปกป้องกลุ่มที่ไวต่อรังสีเป็นพิเศษ (ผู้ป่วย/เด็ก) ก่อนเสมอ

→ ปลดล็อก: Med Bay · ชุดกันรังสี (Rad Suit)
```

**Lead hint:**
```
"คนงานล้มโดยไม่มีบาดแผล ไม่มีไข้"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
MIRA: "ที่พวกเขาล้ม เพราะรังสี... ฉันวินิจฉัยผิดมาทั้งอาทิตย์"
```

---

## 4. `irradiation` — รังสีช่วยเรื่องอาหารได้ 2 ทาง

```yaml
noteId:           irradiation
title:            รังสีช่วยเรื่องอาหารได้ 2 ทาง
category:         เกษตร
researcherSlots:  3
daysRequired:     2
costPower:        120
costIron:         0
costLabMat:       0
requiredLead:     food_preservation
prerequisites:    []
unlocksBuildings: [Co60Chamber, MutationLab]
unlocksCommands:  []
quizzes:          [q_food_irradiation, q_mutation]
linkedRecord:     null
```

**knowledgeBody:**
```
ทางที่ 1 — ถนอมอาหาร: ฉายรังสีแกมมา (เช่น จากโคบอลต์-60) ทะลุผ่านอาหาร
ฆ่าจุลินทรีย์และเชื้อรา อาหารเก็บได้นานขึ้น
สำคัญ: อาหารฉายรังสี ≠ อาหารมีรังสี — รังสีทะลุผ่านไป ไม่ตกค้างในอาหาร

ทางที่ 2 — ปรับปรุงพันธุ์: ฉายรังสีใส่เมล็ด กระตุ้นให้เกิดการกลายพันธุ์
นักวิจัยคัดเฉพาะสายพันธุ์ที่ทนทาน/ผลผลิตสูงไว้ใช้ถาวร
(ข้าว กข6 ของไทย มาจากวิธีนี้)

→ ปลดล็อก: Co-60 Chamber · Mutation Lab
```

**Lead hint:**
```
"ข้าวเน่าเร็วกว่าปกติสามเท่า"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
DORN: "คุณจะเอารังสีมายิงใส่ข้าวที่คนต้องกิน แล้วบอกว่ามันปลอดภัยเนี่ยนะ"
```

---

## 5. ★ `tritium` — เชื้อเพลิงระยะสอง (ประตูชนะ)

```yaml
noteId:           tritium
title:            เชื้อเพลิงระยะสอง — ทริเทียม
category:         เชื้อเพลิง
researcherSlots:  4
daysRequired:     2
costPower:        140
costIron:         60
costLabMat:       0
requiredLead:     lithium_breeding
prerequisites:    [deuterium]
unlocksBuildings: [ZoneB]
unlocksCommands:  [open_zone_b]
quizzes:          [q_dt_fuel, q_tritium_breeding]
linkedRecord:     record_04
```

**knowledgeBody:**
```
เตาฟิวชันจุดติดง่ายที่สุดด้วยเชื้อเพลิงคู่ ดิวเทอเรียม–ทริเทียม (D–T)
เพราะคู่นี้หลอมรวมกันได้ที่อุณหภูมิต่ำกว่าคู่อื่น

ดิวเทอเรียมล้วนดันเตาได้ถึงระดับหนึ่ง แต่ปฏิกิริยาไม่แรงพอจะดันต่อถึงจุดติด
ตั้งแต่ CORE 80% ขึ้นไป เตา ต้องการทริเทียม (³H) มาป้อนคู่กัน

ทริเทียมแทบไม่มีในธรรมชาติ แต่ผลิตเองได้:
นำ นิวตรอน ที่เตาปล่อยออกมา ยิงใส่ ลิเทียม (breeding blanket)
ลิเทียมแตกตัวคายทริเทียมออกมา — เตาจึงเลี้ยงเชื้อเพลิงของตัวเองได้
เรียกว่า tritium breeding

→ ปลดล็อก: Zone B (โรงเพาะทริเทียม)
⚠ Zone B มีรังสีสูงมาก — จัดคนให้น้อยที่สุด และต้องมีชุดกันรังสี
```

**Lead hint:**
```
"CORE ใกล้ตัน เชื้อเพลิงที่มีดันได้แค่นี้"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
KOVA: "เตาเลี้ยงเชื้อเพลิงตัวเองได้... แต่ต้องมีคนเข้าไปในนั้น"
```

> **★ หมายเหตุสำคัญสำหรับโปรแกรมเมอร์:**
> Note ใบนี้คือประตูชนะ · ควิซ `q_tritium_breeding` ให้ Zone B **3.0 → 8.0/วัน**
> Boost กิน 9.0/วัน → **ไม่ตอบควิซ = ขาด 6/วัน = แพ้ 100%**

---

## 6. `storm_detection` — อ่านสัญญาณพายุรังสี

```yaml
noteId:           storm_detection
title:            อ่านสัญญาณพายุรังสี
category:         ระบบ
researcherSlots:  2
daysRequired:     2
costPower:        60
costIron:         0
costLabMat:       30
requiredLead:     storm_detection    # มาจาก Record #3 เท่านั้น
prerequisites:    []
unlocksBuildings: [SensorArray]
unlocksCommands:  []
quizzes:          []
linkedRecord:     record_final
```

**knowledgeBody:**
```
รังสีจากการระเบิดของ CORE TOWER ไม่ได้หายไปทันที
มันเคลื่อนเป็นคลื่นความดันในชั้นบรรยากาศ แล้ววนกลับมาเป็นระลอก

เครื่องวัดที่เพี้ยนบ่อยผิดปกติ คือสัญญาณแรก
ถ้าวัดความดันคลื่นได้ต่อเนื่อง เราจะรู้ล่วงหน้าว่ามันจะมาถึงเมื่อไหร่

→ ปลดล็อก: Sensor Array (เห็นเกจ Storm Pressure + คาดการณ์วันที่มาถึง)
```

**Lead hint:**
```
"เครื่องวัดเพี้ยนอีกแล้ว ครั้งที่สามวันนี้"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
KOVA: "เครื่องวัดไม่ได้เสีย มันพยายามบอกอะไรเราอยู่"
```

> **★ นี่คือรางวัลของคนอ่าน lore** — Lead มาจาก Record #3 เท่านั้น
> ผู้เล่นที่เผื่อนักวิจัยว่าง → Data Recovery เร็ว → ได้ Sensor
> ผล sim: เล่นดีมาก sensor **100%** ↔ สมดุล **14%**

---

## 7. ★ `food_logistics` — คลังเสบียงกับการจัดคน

```yaml
noteId:           food_logistics
title:            คลังเสบียงกับการจัดคน
category:         บริหาร
researcherSlots:  2
daysRequired:     2
costPower:        0
costIron:         40
costLabMat:       0
requiredLead:     food_logistics
prerequisites:    []
unlocksBuildings: [Granary]
unlocksCommands:  []
quizzes:          []              # ★ ไม่มีควิซ — เป็นความรู้เชิงบริหาร
linkedRecord:     null
```

**knowledgeBody:**
```
คนหนึ่งคนกินอาหาร 1 หน่วยต่อวัน ไม่มีข้อยกเว้น
คนที่ไม่ได้กิน 2 วันติด จะทำงานได้แค่ 60% — และขวัญเมืองจะเริ่มตก

ปัญหาไม่ใช่ว่าเราผลิตอาหารไม่พอ
ปัญหาคือเราดึงคนออกจากฟาร์มไปทำอย่างอื่น แล้วลืมว่ายุ้งไม่เติมตัวเอง

คลังเสบียงที่ปิดสนิทและมีระบบหมุนเวียน ช่วยยืดเวลาให้เราแก้ตัวได้

→ ปลดล็อก: Granary (อัตราเน่า ×0.6 · เปิดคลังสำรองได้ตอนวิกฤต)
```

**Lead hint:**
```
"ยุ้งเหลือไม่ถึงสามวัน"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
DORN: "ผมพูดมาสามอาทิตย์แล้วว่ายุ้งมันไม่เติมตัวเอง"
```

---

## 8. ★ `shift_management` — กะและการพัก

```yaml
noteId:           shift_management
title:            กะและการพัก
category:         บริหาร
researcherSlots:  2
daysRequired:     2
costPower:        0
costIron:         50
costLabMat:       0
requiredLead:     shift_management
prerequisites:    []
unlocksBuildings: [Barracks]
unlocksCommands:  []
quizzes:          []              # ★ ไม่มีควิซ
linkedRecord:     null
```

**knowledgeBody:**
```
คนทำงานต่อเนื่องโดยไม่พัก ประสิทธิภาพจะตกลงเรื่อยๆ
พอความล้าเกินระดับหนึ่ง เขาจะหยุดเอง ไม่ว่าเราจะสั่งอะไร

การบังคับให้ทำงานต่อไม่ได้ทำให้ได้งานเพิ่ม — มันแค่ทำให้คนบาดเจ็บ

ที่พักที่มีเตียงจริงและมืดสนิท ทำให้คนฟื้นตัวเร็วขึ้นเกือบเท่าตัว
คนที่พักครบ กลับมาทำงานได้เต็มร้อย

→ ปลดล็อก: Barracks (ฟื้นความล้า −45/วัน แทน −30)
```

**Lead hint:**
```
"คนของนายยืนหลับคาเครื่องแล้ว"
```

**บทตอนวิจัยเสร็จ (เด้งพร้อม knowledgeBody):**
```
MIRA: "ไม่ต้องวิจัยก็รู้ว่าคนต้องนอน แต่เอาเถอะ ตอนนี้มีเตียงแล้ว"
```

---

## ตารางสรุปสำหรับโปรแกรมเมอร์

| noteId | slots×days | power | iron | labMat | ควิซ | ปลด |
|---|---|---|---|---|---|---|
| `deuterium` | 2×2 | 80 | 0 | 0 | 1 | Extractor |
| `confinement` | 3×2 | 40 | 60 | 0 | 2 | Toroidal + Poloidal |
| `nuclear_medicine` | 2×3 | 0 | 0 | 60 | 2 | Med Bay + Rad Suit |
| `irradiation` | 3×2 | 120 | 0 | 0 | 2 | Co-60 + Mutation Lab |
| **`tritium`** | **4×2** | **140** | **60** | 0 | **2** | **Zone B ★** |
| `storm_detection` | 2×2 | 60 | 0 | 30 | 0 | Sensor Array |
| `food_logistics` | 2×2 | 0 | 40 | 0 | **0** | Granary |
| `shift_management` | 2×2 | 0 | 50 | 0 | **0** | Barracks |

**รวมควิซ 9 ข้อจาก Note + 2 ข้อจาก milestone (`q_fusion`, `q_clean_energy`) = 11 ข้อ**

---

## ตารางบทตอนวิจัยเสร็จ (สรุป)

| noteId | ผู้พูด | บท |
|---|---|---|
| `deuterium` | Kova | "อยู่ในน้ำที่เราดื่มทุกวันเนี่ยนะ เอาเครื่องมาต่อโรงน้ำเลย" |
| `confinement` | Kova | "คอยล์สองตัว ตัวหนึ่งกันชนผนัง อีกตัวรีดความร้อน ติดตัวเดียวไม่พอนะ" |
| `nuclear_medicine` | Mira | "ที่พวกเขาล้ม เพราะรังสี... ฉันวินิจฉัยผิดมาทั้งอาทิตย์" |
| `irradiation` | Dorn | "คุณจะเอารังสีมายิงใส่ข้าวที่คนต้องกิน แล้วบอกว่ามันปลอดภัยเนี่ยนะ" |
| **`tritium`** | Kova | "เตาเลี้ยงเชื้อเพลิงตัวเองได้... แต่ต้องมีคนเข้าไปในนั้น" |
| `storm_detection` | Kova | "เครื่องวัดไม่ได้เสีย มันพยายามบอกอะไรเราอยู่" |
| `food_logistics` | Dorn | "ผมพูดมาสามอาทิตย์แล้วว่ายุ้งมันไม่เติมตัวเอง" |
| `shift_management` | Mira | "ไม่ต้องวิจัยก็รู้ว่าคนต้องนอน แต่เอาเถอะ ตอนนี้มีเตียงแล้ว" |

**หมายเหตุการเขียน:**
- `nuclear_medicine` — Mira ยอมรับว่าวินิจฉัยผิด → เธอมีจุดอ่อน ไม่ใช่แค่ปากอธิบายรังสี
- `irradiation` — Dorn ไม่ขอบคุณข้อมูล เขาต่อต้าน → ตรงกับ D03 ใน BARKS.md (ต้นทางของสาย D03→D04→D08)
- `food_logistics` / `shift_management` — ความรู้เชิงบริหาร NPC **ประชด** ว่ามันควรรู้อยู่แล้ว
- `tritium` — Kova พูดถึง**คน**เป็นครั้งแรก → จุดเริ่มที่เขาจะไปชนกับ Mira ที่การ์ด 6

**implement:** ผูกกับ event `OnResearchComplete(noteId)` → เด้ง popup knowledgeBody แล้วต่อด้วยบรรทัดนี้
ห้ามใช้ BarkManager (bark มีเพดาน 2/วัน — บทนี้ต้องขึ้นเสมอ)

---

## ⚠ หมายเหตุการ implement

**อย่าลบ `InfoCardSO` ทิ้ง — เปลี่ยนชื่อ + เพิ่ม field แทน**

```csharp
// เดิม
InfoCardSO { title, category, body, buttonText }

// ใหม่ — โครงเดิม + field เพิ่ม
ResearchNoteSO {
    string noteId, title, category;
    [TextArea] string knowledgeBody;     // ← body เดิม
    int researcherSlots, daysRequired;
    int costPower, costIron, costLabMat;
    string requiredLead;
    string[] prerequisiteNotes;
    BuildingSO[] unlocksBuildings;
    string[] unlocksCommands;
    QuizQuestionSO[] quizzes;
    RecordCardSO linkedRecord;
}
```
**→ asset ที่เขียนเนื้อไปแล้วใน v8 ใช้ต่อได้ทันที**

**★ `lead_map` ต้องมีครบทั้ง 8 ใบ** (บั๊ก #11 — ถ้าขาด note นั้นจะถูกข้ามตลอด ไม่มีวันวิจัยได้)

---