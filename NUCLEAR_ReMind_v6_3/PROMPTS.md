# คำสั่ง Claude Code — 6 Sprint

**วิธีใช้:** ก๊อปทีละบล็อก วางใน Claude Code ที่เปิดในโฟลเดอร์โปรเจกต์ (ที่มี `CLAUDE.md`)
**ห้ามข้าม Sprint** — แต่ละอันต้องผ่าน test ก่อนไปอันถัดไป

---

## ⚠ กติกาที่ใช้ทุก Sprint (ไม่ต้องพิมพ์ซ้ำ ถ้ามี CLAUDE.md)

```
1. ตัวเลขทุกตัวอ่านจาก docs/CONFIG.md — ห้าม hardcode (ทำเป็น GameConfigSO ตัวเดียว)
2. ห้าม if (day == X) — ยกเว้น day == 30
3. Building output ต้องคูณ Σ GetEfficiency(workers) — ห้ามนับหัว
4. Hope ห้ามเขียนตรง — ทุกระบบส่ง HopeEntry เข้า HopeLedger
5. Comment เป็นภาษาอังกฤษ · ให้โค้ดเต็มไฟล์ copy-paste ได้ ไม่ใช่ snippet
6. ตั้งชื่อไฟล์ตรงกับ docs/GDD.md §31
```

---

## Sprint 1 — Worker + Hope + Inventory

```
อ่าน docs/GDD.md §17, §17.5, §18 และ docs/CONFIG.md [LOCK] WORKER + [LOCK] HOPE
แล้ว implement Sprint 1 ตาม §32

ไฟล์ที่ต้องสร้าง (ชื่อตรงตาม §31):
  Workers/Worker.cs · WorkerManager.cs · ShiftSystem.cs
  Hope/HopeLedger.cs · HopeEntry.cs · HopeThresholdWatcher.cs
  Hope/UI/HopeBreakdownPanel.cs   ← บังคับมี
  Inventory/InventoryManager.cs + Inventory panel (docs/INVENTORY.md)

จุดที่ห้ามพลาด:
- Daily Tick ต้องเรียงตาม §17 เป๊ะ: ApplyFatigue → ApplyHunger → ApplyRadiation
  → MedBayHeal → RecalcStatus → ProcessDeaths → ReportToHopeLedger (ห้ามสลับ)
- Hunger: คนหิวสุดได้กินก่อน (OrderByDescending) — ห้ามสุ่ม
- hunger_per_day = 30 ไม่ใช่ 20 (บั๊ก #12)
- ShiftSystem ต้องมี lab_deadlock_guard = 92 (บั๊ก #7)
- InventoryManager เป็น view layer อ่านจาก ResourceManager — ห้ามเก็บ state ซ้ำ
- Power/Water floor = 0 ห้ามติดลบ (บั๊ก #17/#18)

Test ที่ต้องผ่าน:
  รัน D1-30 → Hope ไม่ค้าง 100 ตลอด · เห็นคนหิว/ป่วยจริง · Inventory เห็น delta/วัน
```

---

## Sprint 2 — Research + Soft Trigger

```
อ่าน docs/GDD.md §19 · docs/NOTES.md · docs/CONFIG.md [LOCK] RESEARCH + [LOCK] SOFT TRIGGER
แล้ว implement Sprint 2 ตาม §32

ไฟล์ที่ต้องสร้าง:
  Research/ResearchNoteSO.cs · ResearchLeadSO.cs · ResearchLab.cs
  Research/SoftTriggerWatcher.cs · KnowledgeDB.cs
  Research/UI/ResearchQueuePanel.cs · UI/NoteCardPopup.cs

สร้าง ResearchNoteSO asset ครบ 8 ใบจาก docs/NOTES.md — ก๊อป knowledgeBody ตรงๆ ห้ามแต่งใหม่

จุดที่ห้ามพลาด:
- จ่ายค่าวิจัยครั้งเดียวตอนเริ่ม ไม่ใช่ทุกวัน (บั๊ก #2)
- ResearchLab เริ่มเกมเป็นซาก isRuined = true
- re-staff floor: farm/water = 2, mine = 1 — ห้ามดึงต่ำกว่านี้ (บั๊ก #9)
- lead_map ต้องมีครบทั้ง 8 ใบ ขาดใบเดียว = note นั้นถูกข้ามตลอดเกม (บั๊ก #11)
- Soft Trigger ผูก core ไม่ใช่ day

★ บทตอนวิจัยเสร็จ (docs/NOTES.md ตารางท้ายไฟล์):
  ผูก event OnResearchComplete(noteId) → เด้ง knowledgeBody popup → ต่อด้วยบท NPC 1 บรรทัด
  ห้ามใช้ BarkManager — bark มีเพดาน 2/วัน แต่บทนี้ต้องขึ้นเสมอ

Test ที่ต้องผ่าน:
  ต้องติดขัดถ้าไม่วิจัย — สร้าง Extractor ไม่ได้ถ้ายังไม่มี note deuterium
```

---

## Sprint 3 — Mastery + Codex

```
อ่าน docs/GDD.md §21 · docs/QUIZZES.md · docs/CODEX.md
แล้ว implement Sprint 3 ตาม §32

ไฟล์ที่ต้องสร้าง:
  Mastery/MasteryBonusSO.cs · MasteryRegistry.cs   ← singleton
  Codex/CodexEntrySO.cs · CodexManager.cs · UI/CodexPanel.cs

สร้าง QuizQuestionSO 11 ตัว + CodexEntrySO 11 ตัว — ก๊อปข้อความจากไฟล์ตรงๆ

จุดที่ห้ามพลาด:
- requiresApplied ของ q_tritium_breeding ต้องเช็ค flag tritiumEverProduced
  ห้ามเช็ค tritium > 0 (บั๊ก #3 — โดนเผาเป็น 0 ทุกเทิร์น ปลดได้ 1/40 ครั้ง)
- MasteryRegistry ต้อง query ได้จาก: ReactorController · WorkerManager · MedBay
  · Farm · FoodStorage · ZoneB
- ตอบผิด = ไม่มีโทษ ลองใหม่วันถัดไป · แสดง explanation ทุกกรณี
- ควิซไม่บังคับ อยู่ใน Codex ผู้เล่นเปิดเอง · ไม่มีตัวจับเวลา
- Codex entry ที่ยังไม่ปลด แสดง ??? + 🔒 ห้ามซ่อน
- MetaProgress.MasteryBank + UnlockedCodex เก็บถาวรข้ามรอบเล่น

Test ที่ต้องผ่าน:
  ตอบ vs ไม่ตอบ → ต่างกันชัด · Codex นับ x/11 ถูก
```

---

## Sprint 4 — Crisis Cards ★

```
อ่าน docs/GDD.md §25 · docs/CARDS.md · docs/CONFIG.md [LOCK] CARDS
แล้ว implement Sprint 4 ตาม §32

ไฟล์ที่ต้องสร้าง:
  Cards/CrisisCardSO.cs · CardOption.cs · CardManager.cs
  Cards/UI/CrisisCardPanel.cs

สร้าง CrisisCardSO asset ครบ 8 ใบ — ข้อความก๊อปจาก docs/CARDS.md ตรงๆ ห้ามแต่งใหม่

จุดที่ห้ามพลาด:
- ★ ตัวเลือกล็อกต้องแสดงให้เห็น (เทา + 🔒 + บอกว่าต้องวิจัยอะไร) ห้ามซ่อน
  นี่คือกลไกที่ทำให้ผู้เล่นอยากไปวิจัย ซ่อน = เกมพัง
- การ์ด 5 มีแค่ A/B · การ์ด 6 มีแค่ A/B · การ์ด 7/8 ไม่มีตัวเลือกล็อก
- popup การ์ดต้องหยุดเวลา (pause-reason stack ห้ามใช้ Time.timeScale = 0)

★ บทตัวละครชนกัน (การ์ด 4/5/6/7):
  ทำ field dialogueLines[] ใน CrisisCardSO — เรียงตามลำดับในไฟล์ ห้ามสลับ
  บทปิด = CardOption.afterText ผูกกับตัวเลือกที่กด
  ★ ค่าว่าง = เงียบ ถูกต้องแล้ว ไม่ใช่บั๊ก (การ์ด 7 [A] จงใจไม่มีบท)

Test ที่ต้องผ่าน:
  เห็น 🔒 บนตัวเลือกที่ยังไม่วิจัย · วิจัยแล้วกลับมาเจอการ์ดเดิม → กดได้
```

---

## Sprint 5 — Bark + Records + Zone B

```
อ่าน docs/GDD.md §20, §22, §24 · docs/BARKS.md · docs/STORY.md
แล้ว implement Sprint 5 ตาม §32

ไฟล์ที่ต้องสร้าง:
  Barks/BarkSO.cs · BarkManager.cs
  Records/RecordCardSO.cs · DataRecovery.cs · UI/RecordsPanel.cs
  ZoneB/ZoneBController.cs · RadSuitManager.cs

สร้าง BarkSO 62 ตัว (docs/BARKS.md) + RecordCardSO 4 ใบ (docs/STORY.md)

จุดที่ห้ามพลาด:
- Bark เพดาน 2/วัน ห้ามเกิน · conditions AND กันหมด · priority สูงกว่าชนะ
- Bark ผูก state ห้ามผูก day
- DataRecovery: rate = 14.0 + idleResearchers * 10.0 (นักวิจัยว่าง = ได้ Record เร็ว)
- Record #3 คือ Lead เดียวที่ปลด storm_detection → Sensor Array
- Zone B: rotate ออกที่ rad > 32 · ส่งเข้าได้ที่ rad < 30
- Rad Suit คราฟต์แบบ A: หัก labMat ทันที เก็บ stock จริง (docs/INVENTORY.md)
- การ์ด 6 ต้องเช็ค stock ชุด ไม่ใช่หัก cost

Test ที่ต้องผ่าน:
  Death Spiral เกิดจริง — ส่งคนเข้า Zone B ไม่มีชุด → ป่วย → ไม่มีคน → CORE ค้าง
```

---

## Sprint 6 — Storm + Endings

```
อ่าน docs/GDD.md §23, §26 · docs/STORY.md · docs/CONFIG.md [LOCK] STORM
แล้ว implement Sprint 6 ตาม §32

ไฟล์ที่ต้องสร้าง:
  Storm/StormSystem.cs · SensorArray.cs
  Endings/EndingManager.cs + Intro 3 การ์ด + Ending 3 แบบ

จุดที่ห้ามพลาด:
- storm_heat_per_day = 40.0 ไม่ใช่ 12 (บั๊ก #14 — 12 = cooling กลบสนิท พายุไร้ผล)
- ★ core >= 100 → GameWin() ทันที ห้ามรอ Day 30 (บั๊ก #15)
- เช็ค win ทุกวันใน ReactorController หลังคำนวณ core
- Method B gate: core >= 80 && tritium < 5 → gain = 0
- restart รีเซ็ตทุกอย่าง ยกเว้น MetaProgress.MasteryBank + UnlockedCodex

Test ที่ต้องผ่าน:
  รัน sim เทียบ — ไม่ตอบควิซ q_tritium_breeding = แพ้ 100% ทุก playstyle
```

---

## หลัง Sprint 6 — สิ่งที่ต้องเทสกับคนจริง (§33)

```
1. พายุ D22-23 ทุก playstyle — บอทเล่นเหมือนกัน ต้องดูว่าคนจริงกระจายกว่านี้ไหม
2. iron = 200 เท่ากันหมด → D3/D5 เหมือนกัน — แนะนำสุ่ม 160-240
3. การ์ด #8 Decree = 100% ทุกสาย — เกิดแน่นอน ไม่ใช่ "วิกฤต" ต้องเพิ่มเงื่อนไข
4. ระบบเติมประชากร + Shelter — เพิ่มหลัง sim รัน ต้องรัน nrm_sim.py ใหม่ยืนยัน
```

**ห้ามแตะค่าที่มี [LOCK] โดยไม่รัน `nrm_sim.py` ใหม่**
โดยเฉพาะ `boost_core_gain = 3.0` — เปราะมาก (2.3 = แพ้ 100% ทุก playstyle)
