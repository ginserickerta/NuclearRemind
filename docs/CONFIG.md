# CONFIG — ค่าเกมทั้งหมด (Single Source of Truth)

> 🔴 **ตัวเลขทุกตัวในเกมอ่านจากไฟล์นี้เท่านั้น — ห้าม hardcode ในโค้ด**
> ทำเป็น `GameConfigSO` ตัวเดียว · ปรับที่เดียวจบ
>
> ⚠ **ห้ามแตะค่าที่มี 🔒 โดยไม่รัน `nrm_sim.py` ใหม่**

---

## 🔒 REACTOR

```python
core_start          = 30.0     # CORE% เริ่มเกม
core_win            = 100.0    # ★ เช็คทุกวัน ห้ามรอ D30
core_gain_base      = 1.05     # Idle/Normal
boost_core_gain     = 3.0      # 🔒 ห้ามต่ำกว่า 2.6 (2.3 = แพ้ 100% ทุก playstyle)
mode_heat           = 9.0      # ความร้อนพื้นฐาน/วัน
boost_heat          = 14.0     # ★ เพิ่มจาก mode_heat ตอน Boost
heat_start          = 0.0      # ★ 2026-07-21: เดิม 20 — เตาเริ่มเย็น ความร้อนมาจากการเดินเครื่องเอง
heat_meltdown       = 100.0    # = Game Over
heat_warn           = 90.0     # UI เตือน + ปลด SCRAM
knowledge_q_max_bonus = 0.20   # ★ 2026-07-22 — Knowledge สเกล Q: gain × (1 + knowledge/100 × ค่านี้)
                               #   บวกอย่างเดียว ไม่มีวันต่ำกว่า ×1.0 → ไม่แตะพื้น 🔒 boost_core_gain
                               #   K20 เริ่มเกม +4% · K100 +20% (แทน KnowBonus v4.1 ที่ไม่เคยถูกต่อ)
manual_cool_water_cost  = 20.0 # ★ 2026-07-22 — ปุ่ม "หล่อเย็นเพิ่ม": จ่ายน้ำครั้งเดียว → ลด HEAT ทันที
manual_cool_heat_reduce = 8.0  #   เตาเย็น (HEAT 0) กดไม่ได้ ไม่เสียน้ำฟรี · น้ำไม่พอ = แจ้งเหตุผล
# ★ 2026-07-22 — Idle/Overdrive กลับมาเป็นโหมดจริง (cutover เคยยุบเหลือ base/boost ทำปุ่มเด้งกลับ)
#   Idle: gain 0 · mode_heat 0 · ไม่เผา fuel/tritium — วันพักเตาให้เย็น (Hope โดนโทษ stall ตามเดิม)
#   บวกอย่างเดียว: เส้น Normal/Boost + 🔒 boost_core_gain ไม่ถูกแตะแม้แต่ตัวเดียว
od_core_gain        = 4.2      # Overdrive = Boost × 1.4 — จบเกมเร็วขึ้นแลกความเสี่ยงจริง
od_heat             = 32.0     # ร้อนรวม 9+32 = 41/วัน (Boost = 23) — อยู่รอดต้องมีคอยล์วิจัย confinement
od_tritium_cost     = 13.0     # หลัง CORE≥80 กิน 13/วัน > Zone B mastery ผลิต 8/วัน → เผาคลังล้วนๆ

# cooling — 🔒 บั๊ก #1: v4.1 ใช้ water/10 ไม่มีเพดาน → HEAT=0 ตลอดเกม
cooling = 6 + min(water/12, 7) + coolWorkers*3 + min(toroidalLv, 3)*9
# + 5 ถ้ามี Mastery("confinement")

poloidal_damp_per_lv     = 6.0
poloidal_mastery_mult    = 1.15   # ถ้ามี Mastery("confinement")

fuel_need           = 6.0      # NORMAL: fuelEfficiency = min(1, fuel/need) — ฐาน sim เดิม ไม่แตะ
# ★ 2026-07-23 (เจ้าของ) — เตากิน Deuterium ตามโหมด: แรงขึ้น = ดื่มมากขึ้น
#   Idle 0 · Normal 6 · Boost 9 · Overdrive 12
#   Extractor เต็มเลเวล 1 โรง = 8/วัน → เดิน Boost เต็มประสิทธิภาพต้องสร้างสกัดเพิ่ม (ตั้งใจ)
#   ⚠ ทำให้เส้นชนะ Boost ตึงขึ้นเล็กน้อย (fe 8/9≈0.89 ถ้าไม่ขยาย) — ควรยืนยันด้วย sim เมื่อมี
fuel_need_boost     = 9.0
fuel_need_od        = 12.0
fuel_eff_bonus      = 0.08     # ต่อ Mastery (deuterium / tritium)
```

### การสกัดดิวเทอเรียม (ResearchLab_System_Spec)

ปลดด้วย **การวิจัย** ไม่ใช่ระดับอาคาร — แต่โรงน้ำต้องใหญ่พอด้วย และผู้เล่นต้องเปิดสวิตช์เอง
(ค่าจริงอยู่ใน `WaterPlant.asset` ฟิลด์ `deuteriumMinLevel` / `deuteriumProductionMinLevel` / `deuteriumProduction`)

```python
deuterium_note        = "deuterium"  # ต้องวิจัยเสร็จก่อน (KnowledgeDB) — เทียบเท่า unlock_deuterium_button
deuterium_min_level   = 2      # โรงน้ำต่ำกว่านี้สกัดไม่ได้แม้วิจัยแล้ว
deuterium_rate_lv2    = 4.0    # /วัน ที่ Lv.2
deuterium_rate_lv3    = 8.0    # /วัน ที่ Lv.3 (ค่าเดิม — ไม่แตะ ยังไม่ได้รัน sim)
deuterium_water_per_1 = 25.0   # น้ำที่ใช้ต่อดิวเทอเรียม 1 หน่วย
deuterium_water_floor = 100.0  # กันน้ำสำรองไว้ ไม่สกัดต่ำกว่านี้
```

> ⚠ สเปกต้นทางเขียน Lv.3 = 9 แต่คงไว้ที่ **8** ตามของเดิม เพราะเป็นแหล่งเชื้อเพลิงหลักและยังยืนยันสมดุลด้วย `nrm_sim.py` ไม่ได้ (ไฟล์ไม่อยู่ในโปรเจกต์) — ของใหม่คือ "สกัดได้ตั้งแต่ Lv.2" ไม่ใช่การเพิ่มเพดาน

### ★ Method B Gate (ประตูชนะ)

```python
method_b_core_gate  = 80.0     # core >= 80 && tritium < 5 → gain = 0
method_b_tritium_min= 5.0
tritium_soft_floor  = 6.0      # tritium < 6 → gain *= tritium/6
```

---

## ★ SCRAM (เบรกฉุกเฉิน)

```python
scram_heat_threshold = 90.0    # กดได้เมื่อ HEAT >= 90
scram_heat_reduce    = 40.0
scram_core_penalty   = 10.0
scram_water_cost     = 30.0
scram_cooldown_days  = 3
scram_hope_penalty   = 3.0
scram_force_idle     = True    # ★ บังคับออกจาก Boost
```

> **หมายเหตุ:** ผล sim MELTDOWN 0–2.8% → บอทแทบไม่ใช้ → ไม่กระทบสมดุลที่ยืนยันไว้
> เก็บไว้เพื่อ (1) กันคนเล่นรอบแรกแพ้ไม่ทันตั้งตัว (2) เป็นความรู้นิวเคลียร์จริง

---

## 🔒 STORM

```python
storm_base_rise      = 0.9
storm_ignition_bonus = 2.0     # core >= 80
storm_zoneb_bonus    = 0.8     # zoneB.isOpen
storm_core_coeff     = 1.1     # + (core/100) * 1.1
boost_days_coeff     = 0.30    # ★ ความก้าวร้าวสะสม
storm_pressure_max   = 100.0   # ถึง 100 → TriggerStorm()

storm_heat_per_day   = 40.0    # 🔒 เดิม 12 = cooling กลบสนิท พายุไร้ผลสิ้นเชิง
storm_rad_per_day    = 3.0
sensor_heat_room     = 4.0     # Sensor Array ให้ห้องหายใจ
```

---

## 🔒 METHOD B — Zone B & Tritium

```python
zoneb_tritium_base    = 3.0    # 🔒 ★ ไม่ตอบควิซ = ขาด 6/วัน = แพ้ 100%
zoneb_tritium_mastery = 8.0    # 🔒 ★ ตอบควิซ = พอดี = ชนะ
boost_tritium_cost    = 9.0
idle_tritium_cost     = 6.0
zoneb_min_staff       = 2
zoneb_rotate_rad      = 32.0   # ดึงออกก่อนป่วย
zoneb_send_rad_max    = 30.0   # ส่งเฉพาะ rad < 30
```

---

## 🔒 RADIATION

```python
rad_zoneb        = 15.0    # ป่วยใน ~3.5 วัน · มีชุด ~8 วัน · +Mastery ~10 วัน
rad_mine         = 4.0
rad_core         = 6.0
rad_heat_leak    = 5.0     # heat > 85
rad_storm        = 3.0
rad_suit_mult    = 0.4
rad_alara_mult   = 0.8     # Mastery("nuclear_medicine")
suit_cost        = 30      # labMat/ชุด · เป้า 5 ชุด
death_rad        = 80.0    # radiation > 80 → 20%/วัน
death_chance     = 0.20
```

---

## 🔒 WORKER

```python
rest_threshold   = 70.0    # fatigue >= 70 → พักเอง
rest_return      = 25.0    # fatigue <= 25 → กลับทำงาน
fatigue_work     = 12.0    # /วัน
fatigue_rest     = -30.0   # (-45 ถ้ามี Barracks)
fatigue_rest_barracks = -45.0
fatigue_boost_cool    = 6.0   # +6 ถ้า boosting && job=="cool"
hunger_per_day   = 30.0    # 🔒 บั๊ก #12: เดิม 20 = S7 ไม่เคยเกิด
hungry_threshold = 55.0
lab_deadlock_guard = 92.0  # 🔒 บั๊ก #7: labBusy && fatigue < 92 → ไม่ดึงออก

# Status label — คนละเรื่องกับ Efficiency ข้างล่าง (ห้ามรวมกลับเป็นเลขเดียว)
#   ShiftSystem ดึงคนไปพักที่ 70 → ยอดความล้าจริงสูงสุดแค่ 72 ไม่มีวันแตะ 85
#   ถ้าใช้ eff_fatigue_hard ตั้งชื่อสถานะ "หมดแรง" จะไม่ถูกติดให้ใครเลยทั้งเกม
#   และการ์ด 5 overwork (exhaustedWorkers >= 3) ไม่มีทางเกิด
#   ต้องอยู่ระหว่าง eff_fatigue_soft (60) กับ 72 · ในรอบปกติความล้าเป็นพหุคูณของ 12
#   จึงไม่เคยตกในช่วง 61-71 → เลขไหนก็ให้ผลเท่ากัน · เลือก 68 เผื่อเส้นทางที่ไม่ปกติ
#   (คุมหล่อเย็นตอน boost +18/วัน อาจตกที่ 66/69) ให้ติดป้ายเฉพาะตอนใกล้ถูกดึงออกจริง
exhausted_threshold = 68.0
#   tired_threshold: เหตุผลเดียวกัน · ความล้าเดินทีละ 12 (12→24→36→48→60→72)
#   ถ้าใช้ eff_fatigue_soft (60) ตั้งชื่อ ช่วง "ล้า" จะเหลือ 61-68 ซึ่งไม่มีค่าไหนตกลงไปเลย
#   → ผู้เล่นเห็น "ปกติ" ติดกัน 5 วันแล้วจู่ ๆ เป็น "หมดแรง" ไม่มีสัญญาณเตือนล่วงหน้า
#   45 → 48 กับ 60 ติดป้าย "ล้า" (เตือน 2 วัน) และ 42 หลังพักกลับเป็น "ปกติ" อย่างมีความหมาย
tired_threshold     = 45.0

# Efficiency — ไม่ถูกแตะ · ผลผลิตทั้งเกมจึงไม่เปลี่ยนจากการแยกเลขข้างบน
eff_fatigue_hard = 85.0    # > 85 → 0%
eff_rad_hard     = 50.0    # > 50 → 0%
eff_fatigue_soft = 60.0    # > 60 → ×0.7
eff_hunger_soft  = 55.0    # > 55 → ×0.6

# re-staff floor (🔒 บั๊ก #9: ดึงชาวนาไปวิจัย → อาหารหมด D22)
restaff_floor_farm  = 2
restaff_floor_water = 2
restaff_floor_mine  = 1
```

---

## 🔒 HOPE

```python
hope_start           = 70.0
hope_max             = 100.0
hope_min             = 0.0     # = Game Over
core_progress_bonus  = 1.6     # ต่อ CORE% ที่ขึ้น

# Threshold events (hysteresis: reset เมื่อ current > threshold + 8)
hope_bark_low        = 60.0
hope_strike          = 40.0    # 10% หยุดงาน 2 วัน
hope_exodus          = 25.0    # ประชากร −15% ถาวร
hope_gameover        = 0.0
strike_ratio         = 0.10
exodus_ratio         = 0.15
```

### Hope Sources

| sourceKey | เงื่อนไข | ค่า |
|---|---|---|
| `worker.exhausted` | /คน/วัน | −1 |
| `worker.hungry` | /คน/วัน | −2 |
| `worker.sick` | /คน/วัน | −3 |
| `worker.dying` | /คน/วัน | −5 |
| `worker.death` | ครั้งเดียว | −8 |
| `food.surplus` | food > pop×6 | +2 |  ★ แก้ 2026-07-21 (เดิม pop×3 / +3)
| `food.empty` | food == 0 | −6 |
| `water.shortage` | water < pop | −4 |
| `power.blackout` | ★ ใช้ flag ไม่ใช่ power<0 | −5 |
| `heat.critical` | HEAT > 90 | −3 |
| `storm.active` | พายุ | −1 |
| `core.progress` | /CORE% ที่ขึ้น | +1.6 |
| `core.stalled` | ไม่ขึ้น 3 วันติด | −4 |
| `research.complete` | /ใบ | +6 |
| `memorial.visited` | ครั้งแรก | +2 |
| `alara.compliant` | Zone B ชุดครบ | +2 |
| `reactor.scram` | ★ ใช้ SCRAM | −3 |
| `card.*` | ดู `CARDS.md` | ตามการ์ด |

> ★ **แก้ 2026-07-21 — Hope เฟ้อ ขึ้นอย่างเดียวไม่ลง**
>
> **1) แหล่งที่ไม่เคยถูกเขียนโค้ด** — `heat.critical` · `storm.active` · `core.stalled` · `core.progress` ·
> `alara.compliant` มีค่าอยู่ใน `GameConfigSO` ครบ แต่ **ไม่มีที่ไหนในโปรเจกต์เรียกเลยสักจุด**
> ที่เจ็บคือ 3 ใน 5 เป็นค่าลบ → ระบบมีแต่แรงขึ้น ไม่มีแรงลง ตอนนี้ต่อครบแล้ว
>
> **2) `food.surplus` เป็นรายได้ถาวร** — เดิมจ่าย +3 **ทุกวัน** ที่ `food > pop×3`
> ซึ่งเป็นเส้นเดียวกับเกณฑ์เพิ่มประชากร แปลว่า **เมืองไหนที่โตได้ = ได้ Hope ฟรีทุกวันตลอดเกม**
> เปลี่ยนเป็น `pop×6` (สต็อกจริงราวหนึ่งสัปดาห์) และลดเป็น **+2** ให้ต่ำกว่าวิจัยสำเร็จ (+6)
> — คือ "ตุนได้จริง" ไม่ใช่แค่ "ไม่อดตาย"
>
> **ยังไม่ผ่าน `nrm_sim.py`** (ไฟล์ไม่อยู่ในโปรเจกต์) — ต้องเล่นจริง D1–D30 ยืนยัน

---

## ★ ค่าเริ่มเกม (Day 1 Start)

```python
population       = 14      # 🔒 บั๊ก #8: 14 คน ทำ 18 ตำแหน่งไม่ได้
iron             = 200     # ⚠ §33: แนะนำสุ่ม 160-240 (D3/D5 เหมือนกันหมด)
power            = 0
food             = 40
water            = 90
lab_mat          = 100
core             = 30.0
heat             = 0.0     # ★ 2026-07-21: เดิม 20 (ตาม heat_start)
fuel             = 0.0
tritium          = 0.0
hope             = 70.0
knowledge        = 20.0    # ★ เพิ่ม 2026-07-19 — Auren รู้พื้นฐานอยู่แล้ว (tier Novice ยังไม่ข้าม 30)
                           # ★ 2026-07-22 — เริ่ม 20 เสมอทุกรอบ: ยกเลิก MetaProgress บวก KnowledgeBank ทับ
                           #   (เดิมรอบใหม่เริ่ม 20+ค่าสูงสุดรอบก่อน → K สูง = ได้โบนัส Q ฟรีตั้งแต่ D1)
                           #   "ความรู้ไม่มีวันหาย" = Codex ที่ปลดแล้วยังอยู่ข้ามรอบ ไม่ใช่ตัวเลข K

research_lab_is_ruined = True   # ★ เริ่มเกมเป็นซาก
memorial_clickable     = True   # ★ เปิดได้ตั้งแต่ D1
```

### ตำแหน่งงานเริ่มต้น (pop 14)

| สาย | farm | power | water | mine | lab |
|---|---|---|---|---|---|
| เร่งเตา | 2 | 2 | 2 | 4 | 4 |
| สร้างเมือง | 3 | 2 | 2 | 3 | 3 |
| สมดุล | 3 | 2 | 2 | 4 | 3 |

---

## 🔒 ECONOMY / วัน

```python
food  += farmWorkers  * 9     # 🔒 เดิม 11 (ลด 18% ตอน balance pass)
food  -= aliveWorkers * 1     # 1/คน/วัน
water += waterWorkers * 30
water -= pop * 1.0            # cap 200, 🔒 floor 0 (บั๊ก #18)
iron  += mineWorkers  * 14
raw    = power + powerWorkers * 45 - draw
blackout = (raw < 0)
power  = clamp(raw, 0, 400)   # 🔒 floor 0 (บั๊ก #17: −395 → สูตรเพี้ยนทั้งระบบ)
lab_mat += 5

# ★ ทุกค่าต้องคูณ Σ GetEfficiency(workers) ไม่ใช่นับหัว

draw = 20 + (extractor?15:0) + (medbay?30:0) + (zoneB?25:0) + (co60?20:0)

spoil = 0.05 + (avgRad/100) * 0.15
if co60:    spoil *= 0.3
if granary: spoil *= 0.6
```

---

## ★ POPULATION GROWTH

```python
growth_food_ratio  = 3      # food > pop * 3   ★ ลดจาก 5 (2026-07-20)
growth_chance      = 0.35   # 35%/วัน          ★ เพิ่มจาก 0.25 (2026-07-20)
growth_food_cost   = 20
growth_amount      = 1
# cap = Shelter level (ไม่ผูก Hope — ต่างจาก v4.1)
```

> ★ **แก้ 2026-07-20 — ระบบเติมประชากรเพิ่งถูก implement จริงครั้งแรก**
> ก่อนหน้านี้สูตรนี้ไม่เคยทำงานเลย (โค้ดอยู่ใน `PopulationManager` ที่ถูก guard ปิดไว้)
> พอเปิดใช้จริงพบว่าเล่นแล้วประชากรแทบไม่ขึ้นในเกม 30 วัน สาเหตุคือ **เส้นอาหารขยับตามจำนวนคน**
> (ได้คน +1 → เส้นสูงขึ้น 5 ทันที) ทำให้ตันตั้งแต่คนแรก
> ที่ 14 คน เส้นเดิม = 70 แต่อาหารเริ่มเกมมีแค่ 40 → ต้องมีชาวนา 4 คนถึงจะไล่ทันใน 2 วัน
> ลดเป็น ×3 → เส้นเหลือ 42 · ชาวนา 2-3 คนก็พอ · โอกาส 35% ทำให้เห็นผลใน ~3 วันแทน 4
> **ยังไม่ผ่าน `nrm_sim.py`** (ไฟล์ไม่อยู่ในโปรเจกต์) — ไม่ใช่ค่า 🔒 แต่กระทบ pop ที่ sim เดิมตรึงไว้ที่ 14 (ดู §33)

### ★ Shelter (เพดานประชากร)

| ระดับ | เพดาน | ต้นทุน | ปลดล็อก |
|---|---|---|---|
| L1 | 14 | เริ่มมี | Phase 1 |
| L2 | 20 | Iron 60 + Power 100 | Phase 2 |
| L3 | 28 | Iron 120 + Power 250 | Phase 3 |

> ⚠ **sim รันที่ pop 14 คงที่** — เปิดระบบเติมประชากร = ค่าอาจ drift → ต้องรัน sim ใหม่

---

## ★ PHASE (ผูก `core%` ไม่ผูก `day`)

```python
phase_2_core = 40.0
phase_3_core = 60.0
phase_4_core = 80.0
```

| Phase | เงื่อนไข | ปลดล็อก |
|---|---|---|
| 1 | `core < 40` | อาคารผลิต L1 · Shelter L2 |
| 2 | `core >= 40` | ซ่อมห้องวิจัย · อาคารผลิต L2 · Shelter L3 |
| 3 | `core >= 60` | Med Bay · Co-60 · Water/Farm L3 |
| 4 | `core >= 80` | ★ Zone B · Power L3 · Mine L2 |

> ❌ **ห้ามใช้ Phase เป็น trigger ของการ์ด/ควิซ** — ใช้แค่ UI + building unlock

---

## 🔒 SOFT TRIGGER (ผูก `core` ไม่ใช่ `day`)

```python
soft_heat_base   = 35.0    # heat > 35 - (core/100)*10   → 35→25
soft_heat_slope  = 10.0
soft_rad_base    = 25.0    # rad  > 25 - (core/100)*6    → 25→19
soft_rad_slope   = 6.0
soft_food_base   = 45.0    # food > 45 - (core/100)*8
soft_food_slope  = 8.0
soft_rad2_base   = 8.0     # avgRad > 8 - (core/100)*4
soft_rad2_slope  = 4.0
soft_lithium_core= 45.0    # core >= 45 → lithium_breeding
soft_hungry_count    = 3   # ★ S7
soft_exhausted_count = 3   # ★ S8
```

---

## 🔒 RESEARCH

```python
researcher_slots_lv1 = 3   # Lv2→5, Lv3→8
queue_capacity_lv1   = 1   # Lv2→2, Lv3→3
repair_iron          = 80
repair_days          = 2
repair_workers       = 2
staff_ratio_min      = 0.5  # < 0.5 → progress ไม่เดิน
# 🔒 บั๊ก #2: จ่ายค่าวิจัยครั้งเดียวตอนเริ่ม ไม่ใช่ทุกวัน
```

---

## 🔒 DATA RECOVERY

```python
data_recovery_passive = 14.0   # /วัน (แม้ไม่มีคนว่าง)
data_recovery_rate    = 10.0   # ต่อนักวิจัยว่าง 1 คน
data_recovery_lv2_mult= 1.3
data_recovery_target  = 100.0  # ถึง 100 → ปลด Record ถัดไป → reset 0
```

---

## 🔒 CARDS

| # | cardId | trigger | cooldown | unlockedBy |
|---|---|---|---|---|
| 1 | `heat` | `heat > 62` | 3 | `confinement` |
| 2 | `sick` | `sickWorkers >= 3` | 5 | `nuclear_medicine` + MedBay |
| 3 | `spoil` | `food > 30 && avgRadiation > 6` | 6 | `irradiation` |
| 4 | `hunger` | `hungryWorkers >= 3` | 4 | `food_logistics` → Granary |
| 5 | `overwork` | `exhaustedWorkers >= 3` | 4 | `shift_management` → Barracks |
| 6 | `zoneb` | `zoneB.isOpen && zoneBWorkers < 2` | 4 | — |
| 7 | `triage` | `sickWorkers > medBayCapacity && sickWorkers >= 5` | 6 | — |
| 8 | `decree` | `stormActive && coolingWorkers < 3` | ครั้งเดียว | — |

> **Research Lead ปลดครั้งเดียวตลอดเกม · การ์ดเกิดซ้ำได้ตาม cooldown**

---

## MASTERY BONUS

| quizId | ต้องทำก่อน (`requiresApplied`) | Bonus |
|---|---|---|
| `q_deuterium` | Extractor เดิน 1 วัน | `fuelEfficiency +0.08` |
| `q_plasma` | ติดคอยล์ ≥1 | `cooling +5` |
| `q_magnetic_pair` | ติดครบ 2 ชนิด | `poloidalDamp ×1.15` |
| `q_nuclear_medicine` | รักษา ≥1 คน | `heal 25→35` · `radiation ×0.8` |
| `q_alara` | ส่งคนเข้าโซนเสี่ยง | (Codex only) |
| `q_mutation` | Mutation Lab เดิน | `farmYield +15%` |
| `q_food_irradiation` | Co-60 เดิน | `spoil −50%` |
| `q_dt_fuel` | ป้อน Tritium สำเร็จ | `fuelEfficiency +0.08` |
| **`q_tritium_breeding`** | Zone B ผลิต ≥2 วัน | 🔒 **★ Zone B 3.0 → 8.0/วัน** |
| `q_fusion` | CORE ≥ 95 | `Boost heat −10%` |
| `q_clean_energy` | ถึง ending | (ending stat) |

> 🔒 **บั๊ก #3:** `requiresApplied` ห้ามเช็ค `tritium > 0` (โดนเผาเป็น 0 ทุกเทิร์น → ปลดได้ 1/40 ครั้ง)
> ต้องใช้ flag ถาวร `tritiumEverProduced`

---

## TIME & MAP

```python
planning_phase_seconds = 30
live_phase_seconds     = 60
total_days             = 30    # Day 1 = tutorial
grid_size              = 43    # 43 × 43
cooling_water_pool     = "shared"   # ใช้คลังน้ำเดียวกับน้ำดื่ม
```

---

## PERSISTENCE

```python
# เก็บนอกเซฟปกติ (PlayerPrefs / ไฟล์ meta)
MetaProgress.MasteryBank   # ★ Mastery ที่ปลดแล้ว
MetaProgress.UnlockedCodex # ★ Codex entry ที่ปลดแล้ว
# restart รีเซ็ตทุกอย่างยกเว้น 2 ค่านี้
```

---

## ⚠ ค่าที่ยังต้องแก้ (§33 — ยอมรับตรงๆ)

| # | ปัญหา | สถานะ |
|---|---|---|
| 1 | **พายุ D22-23 ทุก playstyle** — บอทเล่นเหมือนกัน (boost 19-20 วัน) | ⏳ ต้องทดสอบกับคนจริง |
| 2 | **D3/D5 เหมือนกันหมด** — `iron = 200` เท่ากัน | 💡 แนะนำสุ่ม 160–240 |
| 3 | **การ์ด #8 Decree = 100% ทุกสาย** — เกิดแน่นอน ไม่ใช่ "วิกฤต" | ⏳ ต้องเพิ่มเงื่อนไข |
| 4 | **`boost_core_gain` เปราะ** | 🔒 ห้ามแตะโดยไม่รัน sim |
| 5 | **★ ระบบเติมประชากร + Shelter** — ใหม่ ยังไม่ผ่าน sim | ⏳ ต้องรัน sim ใหม่ |
| 6 | **★ SCRAM** — ใหม่ ยังไม่ผ่าน sim (แต่ MELTDOWN 0-2.8% → คาดว่าไม่กระทบ) | ⏳ ควรยืนยัน |

---

*CONFIG · อ้างอิง `nrm_sim.py` · 3,000 runs · 0 invariant violations · drift ≤5.5%*
