# NUCLEAR Re:Mind

เกม Survival City-Builder สอนฟิสิกส์นิวเคลียร์/ฟิวชัน · Unity 6 / C# · NSC #28
ผู้เล่นบริหารเมือง Veltara 30 วัน ดัน CORE TOWER ให้ถึง 100% ก่อนพายุรังสี

---

## 🔴 กติกาที่ห้ามละเมิด

1. **ห้าม `if (day == X)`** — ยกเว้น `day == 30` (เส้นตาย)
   ทุก trigger ต้องผูก **state** (core%, heat, hunger, rad) ไม่ใช่ปฏิทิน
2. **ตัวเลขทั้งหมดอ่านจาก `docs/CONFIG.md`** — ห้าม hardcode
   ทำเป็น `GameConfigSO` ตัวเดียว
3. **ห้ามแตะ `boost_core_gain = 3.0`** — เปราะมาก (2.3 = แพ้ 100%)
   ค่าที่มี 🔒 ใน CONFIG.md = ห้ามแก้โดยไม่รัน `nrm_sim.py`
4. **ห้ามระบบบอกคำตอบก่อนวิจัย** — NPC ไม่รู้ ระบบไม่รู้
5. **ไม่มีทางเลือกฟรี** — ทุกทางเลือกมีราคา
6. **★ ตัวเลือกที่ดีในการ์ดต้องล็อก 🔒 จนกว่าจะวิจัย และต้องแสดงให้เห็น ห้ามซ่อน**
7. **Building output ต้องคูณ `Σ GetEfficiency(workers)`** ไม่ใช่นับหัว
   ไม่ทำ = ระบบ Worker ทั้งหมดไม่มีผล
8. **Hope ห้ามเขียนตรง** — ทุกระบบส่ง `HopeEntry` เข้า `HopeLedger`
9. **Inventory ห้ามเก็บ state ซ้ำ** — เป็น view layer อ่านจาก `ResourceManager`

---

## หลักการเดียวที่ต้องจำ

```
ความรู้ไม่ใช่รางวัล — มันคือกลไกที่ทำให้ชนะ

Zone B ผลิต 3.0/วัน · Boost กิน 9.0/วัน → ขาด 6/วัน → CORE ค้าง 80 → แพ้
ตอบควิซ q_tritium_breeding ถูก → 8.0/วัน → ชนะ

ผล sim: ไม่ตอบควิซ = แพ้ 100% ทุก playstyle
```

---

## เอกสาร

| ไฟล์ | เนื้อหา |
|---|---|
| `PROMPTS.md` | **คำสั่ง Claude Code 6 Sprint — ก๊อปวางได้เลย** |
| `QA_CHECKLIST.md` | **ตรวจเกมหลัง Sprint 1–6 · 120 ข้อ** |
| `docs/GDD.md` | ระบบทั้งหมด §1–§33 — **อ่านก่อนเริ่มทุก Sprint** |
| `docs/CONFIG.md` | 🔴 ค่าเกมทั้งหมด — แก้ที่นี่ที่เดียว |
| `docs/NOTES.md` | Research Notes 8 ใบ (ResearchNoteSO assets) + **บทตอนวิจัยเสร็จ 8 บรรทัด** |
| `docs/QUIZZES.md` | Quiz 11 ข้อ (QuizQuestionSO assets) |
| `docs/CARDS.md` | Crisis Cards 8 ใบ (CrisisCardSO assets) + **บทตัวละครชนกัน 4 การ์ด** |
| `docs/BARKS.md` | Bark 62 บรรทัด (BarkSO assets) |
| `docs/STORY.md` | Records / Intro / Endings |
| `docs/CODEX.md` | Codex 11 entry (CodexEntrySO assets) |
| `docs/INVENTORY.md` | ระบบคลัง 6 แท็บ + คราฟต์ Rad Suit |

---

## Sprint Plan

| Sprint | งาน | Test ที่ต้องผ่าน |
|---|---|---|
| 1 | Worker + Shift + HopeLedger + UI + **Inventory panel** | รัน D1-30 → Hope ไม่ 100 ตลอด · เห็นคนหิว/ป่วย · เห็น delta/วัน |
| 2 | ResearchQueue + SoftTrigger + gating | ต้องติดขัดถ้าไม่วิจัย |
| 3 | MasteryRegistry + Codex quiz + Codex panel | ตอบ vs ไม่ตอบ → ต่างกันชัด · ปลด entry x/11 |
| 4 | CrisisCards 8 ใบ + ตัวเลือกล็อก | เห็น 🔒 บนตัวเลือกที่ยังไม่วิจัย |
| 5 | Bark + DataRecovery + ZoneB | Death Spiral เกิดจริง |
| 6 | Storm + Sensor + Endings | รัน sim เทียบ |

---

## Code style

- **Comment เป็นภาษาอังกฤษ** ทั้งหมด
- ให้โค้ดเต็มไฟล์ copy-paste ได้ทันที ไม่ใช่ snippet
- ตั้งชื่อตรงกับ `docs/GDD.md` §31 (Worker.cs, HopeLedger.cs, ...)
- ScriptableObject ทุกตัวตรงกับ spec ใน GDD §16

---

## Structure

```
Assets/Scripts/
  Workers/    Worker.cs · WorkerManager.cs · ShiftSystem.cs
  Hope/       HopeLedger.cs · HopeEntry.cs · HopeThresholdWatcher.cs
              UI/HopeBreakdownPanel.cs          ← บังคับมี
  Research/   ResearchNoteSO.cs · ResearchLeadSO.cs · ResearchLab.cs
              SoftTriggerWatcher.cs · KnowledgeDB.cs
              UI/ResearchQueuePanel.cs · UI/NoteCardPopup.cs
  Mastery/    MasteryBonusSO.cs · MasteryRegistry.cs   ← singleton
  Cards/      CrisisCardSO.cs · CardOption.cs · CardManager.cs
              UI/CrisisCardPanel.cs             ← แสดง 🔒 บนตัวเลือกที่ล็อก
  Barks/      BarkSO.cs · BarkManager.cs
  Storm/      StormSystem.cs · SensorArray.cs
  Records/    RecordCardSO.cs · DataRecovery.cs · UI/RecordsPanel.cs
  ZoneB/      ZoneBController.cs · RadSuitManager.cs
  Codex/      CodexEntrySO.cs · CodexManager.cs · UI/CodexPanel.cs
  Inventory/  ItemSO.cs · InventoryManager.cs · UI/InventoryPanel.cs
  Core/       GameManager.cs · TimeManager.cs · PhaseManager.cs
              ReactorController.cs · ResourceManager.cs
              BuildingManager.cs · BuildPlacementManager.cs
              MetaProgress.cs · GameConfigSO.cs
```

**`MasteryRegistry` ต้อง query ได้จาก:**
`ReactorController` · `WorkerManager` · `MedBay` · `Farm` · `FoodStorage` · `ZoneB`

---

## บั๊กที่ sim เจอแล้ว (ห้ามทำซ้ำ — ดู GDD §30)

| # | บั๊ก | ผลถ้าไม่แก้ |
|---|---|---|
| 1 | `cooling = 15 + water/10` ไม่มีเพดาน | HEAT = 0 ตลอดเกม เตาไร้ความหมาย |
| 2 | หักค่าวิจัยทุกวัน | Tritium ดูดเหล็กจนบล็อกตัวเอง วิจัยได้ใบเดียว |
| 3 | ควิซเช็ค `tritium > 0` | tritium เผาเป็น 0 ทุกเทิร์น → Mastery ปลดได้ 1/40 |
| 7 | Research deadlock | นักวิจัยถูกดึงออก → progress ค้าง 1.4 ตลอดกาล |
| 8 | 14 คน ทำ 18 ตำแหน่งไม่ได้ | mine=0 → เหล็กค้าง → แพ้ 100% |
| 9 | ดึงชาวนาไปวิจัย | อาหารหมด D22 → Hope 0 |
| 14 | `storm_heat = 12` | cooling กลบสนิท → พายุไร้ผลสิ้นเชิง |
| **15** | **CORE 100 แล้วเกมไม่จบ** | รันต่อถึง D30 → Hope ไหลจนแพ้ ทั้งที่ชนะแล้ว |
| 17 | power ติดลบได้ | −395 → สูตรเพี้ยนทั้งระบบ |
| 18 | water ติดลบได้ | −10 → cooling คำนวณผิด |

---

## เมื่อไม่แน่ใจ

- **ค่าตัวเลข** → `docs/CONFIG.md`
- **ระบบทำงานยังไง** → `docs/GDD.md`
- **ข้อความในเกม** → `docs/NOTES.md` / `QUIZZES.md` / `CARDS.md` / `BARKS.md` / `STORY.md`
- **Codex / คลัง** → `docs/CODEX.md` / `docs/INVENTORY.md`
- **ยังไม่มีในเอกสาร** → ถามก่อน อย่าเดา
