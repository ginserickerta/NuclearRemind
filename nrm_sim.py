# -*- coding: utf-8 -*-
"""
nrm_sim.py — ตัวจำลองสมดุลเกม NUCLEAR Re:Mind (Monte-Carlo Balance Simulator)
================================================================================

[TH] หน้าที่: พิสูจน์สมดุลของเกมด้วยการ "เล่นแทนคน" หลายพันรอบ
แทนที่จะเถียงกันปากเปล่าว่าเกมยาก/ง่าย/ชนะได้จริงไหม — เรารันบอท 5 สายการเล่น
สายละหลายพันรอบ (ค่าเริ่มต้น 3,000 รอบ/สาย) แล้วดูสถิติจริง:

    อัตราชนะ · วันที่ชนะเฉลี่ย · สาเหตุการแพ้ · CORE สุดท้ายเฉลี่ย · วันพายุมา

ทุกตัวเลขในไฟล์นี้คัดลอกจาก docs/CONFIG.md (Single Source of Truth ของเกม)
ค่าที่มีเครื่องหมาย 🔒 ใน CONFIG.md ห้ามแก้ในเกมโดยไม่รันไฟล์นี้ซ้ำ
เพราะสมดุลเปราะมาก เช่น boost_core_gain 3.0 → 2.3 = แพ้ 100% ทุกสาย

ผลลัพธ์สำคัญที่ sim นี้พิสูจน์ (หลักออกแบบข้อเดียวของเกม):

    "ความรู้ไม่ใช่รางวัล — มันคือกลไกที่ทำให้ชนะ"

    • ไม่เปิด Zone B เลย        → tritium = 0 → CORE ค้างที่ 80 → แพ้ ~100%
    • เปิด Zone B แต่ไม่ตอบควิซ → ผลิต 3.0/วัน แต่ Boost กิน 9.0/วัน → แพ้ ~100%
    • ตอบควิซ q_tritium_breeding → ผลิต 8.0/วัน → ชนะเป็นส่วนใหญ่

วิธีรัน:
    python tools/nrm_sim.py                     # 3,000 รอบ/สาย ทุกสาย
    python tools/nrm_sim.py --runs 5000         # เพิ่มจำนวนรอบ
    python tools/nrm_sim.py --policy no_zoneb   # รันสายเดียว
    python tools/nrm_sim.py --seed 123          # เปลี่ยน seed (ผลทำซ้ำได้)

ขอบเขต (ตรงตามหมายเหตุใน CONFIG.md):
    • ประชากรคงที่ 14 คน (CONFIG ระบุ "sim รันที่ pop 14 คงที่")
    • การ์ดวิกฤต/บทพูดไม่กระทบตัวเลข จึงไม่จำลอง (เป็น flavor + ทางเลือกผู้เล่นจริง)
    • ค่าที่ CONFIG ไม่ได้กำหนด (เช่น อัตราผลิตของ Extractor) ประกาศไว้ชัดใน
      ส่วน "ASSUMED" ข้างล่าง — แยกให้เห็นว่าตัวไหนคือค่าเกมจริง ตัวไหนคือค่าตัวแทน
"""

import argparse
import random
import statistics
from collections import Counter

# ════════════════════════════════════════════════════════════════════════════
# ค่าคงที่จาก docs/CONFIG.md — คัดลอกตรงทุกตัว (🔒 = ห้ามแก้โดยไม่รัน sim ซ้ำ)
# ════════════════════════════════════════════════════════════════════════════

# ── 🔒 REACTOR ──
CORE_START        = 30.0
CORE_WIN          = 100.0     # เช็คทุกวัน ชนะทันทีที่ถึง (บั๊ก #15: ห้ามรอ D30)
CORE_GAIN_BASE    = 1.05      # โหมด Idle/Normal
BOOST_CORE_GAIN   = 3.0       # 🔒 ต่ำกว่า 2.6 = แพ้ 100% ทุกสาย
MODE_HEAT         = 9.0       # ความร้อนพื้นฐาน/วัน
BOOST_HEAT        = 14.0      # บวกเพิ่มตอน Boost
HEAT_START        = 20.0
HEAT_MELTDOWN     = 100.0     # = Game Over
FUEL_NEED         = 6.0       # fuelEfficiency = min(1, fuel/6)

# ── ★ Method B Gate (ประตูชนะ — หัวใจของเกม) ──
METHOD_B_CORE_GATE   = 80.0   # core >= 80 และ tritium < 5 → CORE ไม่ขึ้นเลย
METHOD_B_TRITIUM_MIN = 5.0
TRITIUM_SOFT_FLOOR   = 6.0    # tritium < 6 → gain คูณ tritium/6

# ── ★ SCRAM (เบรกฉุกเฉิน) ──
SCRAM_HEAT_THRESHOLD = 90.0
SCRAM_HEAT_REDUCE    = 40.0
SCRAM_CORE_PENALTY   = 10.0
SCRAM_WATER_COST     = 30.0
SCRAM_COOLDOWN_DAYS  = 3
SCRAM_HOPE_PENALTY   = 3.0

# ── 🔒 STORM ──
STORM_BASE_RISE      = 0.9
STORM_IGNITION_BONUS = 2.0    # core >= 80
STORM_ZONEB_BONUS    = 0.8    # Zone B เปิดอยู่
STORM_CORE_COEFF     = 1.1    # + (core/100)*1.1
BOOST_DAYS_COEFF     = 0.30   # ยิ่ง Boost หลายวัน พายุยิ่งมาเร็ว
STORM_PRESSURE_MAX   = 100.0
STORM_HEAT_PER_DAY   = 40.0   # 🔒 เดิม 12 = cooling กลบสนิท (บั๊ก #14)
STORM_RAD_PER_DAY    = 3.0
SENSOR_HEAT_ROOM     = 4.0    # มี Sensor Array → ลดความร้อนพายุ

# ── 🔒 METHOD B — Zone B & Tritium ──
ZONEB_TRITIUM_BASE    = 3.0   # 🔒 ไม่ตอบควิซ = ขาด 6/วัน = แพ้ 100%
ZONEB_TRITIUM_MASTERY = 8.0   # 🔒 ตอบควิซ q_tritium_breeding = ชนะ
BOOST_TRITIUM_COST    = 9.0   # Boost ช่วง Method B เผา tritium/วัน
IDLE_TRITIUM_COST     = 6.0   # โหมดปกติช่วง Method B
ZONEB_MIN_STAFF       = 2
ZONEB_ROTATE_RAD      = 32.0  # รังสีถึง 32 → หมุนคนออกก่อนป่วย
ZONEB_SEND_RAD_MAX    = 30.0  # ส่งเฉพาะคนรังสีต่ำกว่า 30

# ── 🔒 RADIATION ──
RAD_ZONEB      = 15.0
RAD_MINE       = 4.0
RAD_HEAT_LEAK  = 5.0          # heat > 85 รั่วใส่ทุกคน
RAD_SUIT_MULT  = 0.4          # ชุดกันรังสีลดเหลือ 40%
DEATH_RAD      = 80.0         # เกิน 80 → เสี่ยงตาย 20%/วัน
DEATH_CHANCE   = 0.20

# ── 🔒 WORKER ──
REST_THRESHOLD   = 70.0       # ล้าถึง 70 → พักเอง
REST_RETURN      = 25.0       # ล้าลงถึง 25 → กลับทำงาน
FATIGUE_WORK     = 12.0
FATIGUE_REST     = -30.0
HUNGER_PER_DAY   = 30.0       # 🔒 บั๊ก #12: เดิม 20 เบาไป
HUNGRY_THRESHOLD = 55.0
EFF_FATIGUE_HARD = 85.0       # ล้าเกิน 85 → ทำงานไม่ได้ (0%)
EFF_RAD_HARD     = 50.0       # รังสีเกิน 50 → ป่วย (0%)
EFF_FATIGUE_SOFT = 60.0       # ล้าเกิน 60 → x0.7
EFF_HUNGER_SOFT  = 55.0       # หิวเกิน 55 → x0.6

# ── 🔒 HOPE (ค่าความหวัง — แพ้ทันทีถ้าถึง 0) ──
HOPE_START          = 70.0
CORE_PROGRESS_BONUS = 1.6     # ต่อ CORE% ที่ขึ้น
HOPE_STRIKE         = 40.0    # ต่ำกว่า 40 → 10% หยุดงาน 2 วัน
HOPE_EXODUS         = 25.0    # ต่ำกว่า 25 → ประชากร -15% ถาวร
STRIKE_RATIO        = 0.10
EXODUS_RATIO        = 0.15
HYSTERESIS          = 8.0     # เหตุการณ์ re-arm เมื่อ hope ขึ้นพ้น threshold + 8

# ── ★ ค่าเริ่มเกม ──
POP_START   = 14              # 🔒 บั๊ก #8: 14 คน ทำ 18 ตำแหน่งไม่ได้ → ต้องเลือก
IRON_START  = 200
FOOD_START  = 40
WATER_START = 90
LABMAT_START= 100

# ── 🔒 ECONOMY / วัน (ทุกค่าคูณ Σ efficiency ไม่ใช่นับหัว — กติกาข้อ 7) ──
FOOD_PER_FARMER   = 9
FOOD_EAT          = 1
WATER_PER_WORKER  = 30
WATER_DRINK       = 1.0
IRON_PER_MINER    = 14
POWER_PER_WORKER  = 45
POWER_CAP         = 400       # 🔒 floor 0 (บั๊ก #17: เคยติดลบ -395)
WATER_CAP         = 200       # 🔒 floor 0 (บั๊ก #18)
LABMAT_PER_DAY    = 5
DRAW_BASE         = 20

# ── 🔒 RESEARCH ──
REPAIR_IRON    = 80
REPAIR_DAYS    = 2
REPAIR_WORKERS = 2
STAFF_RATIO_MIN= 0.5          # นักวิจัยต่ำกว่าครึ่ง → งานไม่เดิน (บั๊ก #7)
# ลำดับโน้ตที่บอทวิจัย (วัน/ใบจาก docs/NOTES.md — จ่ายครั้งเดียวตอนเริ่ม บั๊ก #2)
RESEARCH_PLAN = [
    #  (id,               วัน, ไฟ,  เหล็ก, วัสดุแล็บ)
    ("deuterium",          2,   80,   0,    0),
    ("confinement",        2,   40,  60,    0),   # ปลดคอยล์ → cooling
    ("tritium",            2,  140,  60,    0),   # ★ ประตูชนะ — ปลด Zone B รู้วิธีใช้
    ("nuclear_medicine",   3,    0,   0,   60),
    ("sensor",             2,   60,   0,   30),   # ปลด Sensor Array
]

# ════════════════════════════════════════════════════════════════════════════
# ASSUMED — ค่าตัวแทนที่ CONFIG.md ไม่ได้ระบุ (ประกาศแยกให้เห็นชัด)
# ════════════════════════════════════════════════════════════════════════════
ASSUMED_EXTRACTOR_DEUT   = 8.0   # Extractor ผลิตดิวเทอเรียม/วัน (เผา 6 → เหลือสำรอง)
ASSUMED_EXTRACTOR_IRON   = 40    # ค่าสร้าง Extractor
ASSUMED_COIL_IRON        = 50    # ค่าติดคอยล์ต่อระดับ (toroidal/poloidal)
ASSUMED_SUIT_COUNT       = 5     # ชุดกันรังสีที่บอทคราฟต์ — ตรงเป้า "5 ชุด" ใน CONFIG (30 labMat/ชุด)
ASSUMED_QUIZ_DELAY_DAYS  = 2     # ตอบควิซได้หลัง Zone B ผลิตครบ 2 วัน (ตาม requiresApplied)
MEDBAY_HEAL              = 25.0  # โรงพยาบาลลดรังสี/คน/วัน (CONFIG: heal 25 → 35 เมื่อมี Mastery)
MEDBAY_BEDS              = 2     # จำนวนเตียง (รักษาได้กี่คน/วัน)
DRAW_MEDBAY              = 30    # โหลดไฟของ MedBay (จากสูตร draw ใน CONFIG)


class Worker:
    """[TH] หน้าที่: สถานะรายคน — ความล้า/ความหิว/รังสี กำหนดประสิทธิภาพ (กติกาข้อ 7:
    ผลผลิตอาคาร = ผลรวมประสิทธิภาพ ไม่ใช่จำนวนหัว)"""

    def __init__(self):
        # กระจายความล้าเริ่มต้นให้ต่างกันมาก — จำลอง ShiftSystem ของเกมจริงที่สลับกะ
        # (ถ้าเริ่มเท่ากันหมด ทั้งเมืองจะล้าพร้อมกันแล้วหยุดพักพร้อมกัน 14 คนทุก 7 วัน)
        self.fatigue = random.uniform(0, 55)
        self.hunger = 0.0
        self.rad = 0.0
        self.resting = False
        self.strike_days = 0
        self.alive = True

    def efficiency(self):
        """[TH] ประสิทธิภาพ 0..1 ตามเกณฑ์ CONFIG: ล้า>85 หรือ รังสี>50 = 0,
        ล้า>60 คูณ 0.7, หิว>55 คูณ 0.6"""
        if not self.alive or self.resting or self.strike_days > 0:
            return 0.0
        if self.fatigue > EFF_FATIGUE_HARD or self.rad > EFF_RAD_HARD:
            return 0.0
        eff = 1.0
        if self.fatigue > EFF_FATIGUE_SOFT:
            eff *= 0.7
        if self.hunger > EFF_HUNGER_SOFT:
            eff *= 0.6
        return eff


class Policy:
    """[TH] หน้าที่: นิยาม "สายการเล่น" ของบอท 1 สาย — ธงเปิด/ปิดพฤติกรรมหลัก
    ใช้เทียบกันเพื่อพิสูจน์ว่ากลไกความรู้ (ควิซ/Zone B) ชี้แพ้ชนะจริง"""

    def __init__(self, name, use_zoneb=True, answer_quiz=True,
                 boost_aggression=0.75, farm_workers=3):
        self.name = name
        self.use_zoneb = use_zoneb          # ส่งคนเข้า Zone B ไหม (ปิด = ไม่มี tritium เลย)
        self.answer_quiz = answer_quiz      # ตอบควิซ q_tritium_breeding ไหม (3.0 vs 8.0/วัน)
        self.boost_aggression = boost_aggression  # ยอม Boost เมื่อ heat คาดการณ์ต่ำกว่ากี่ % ของ meltdown
        self.farm_workers = farm_workers    # จำนวนชาวนาขั้นต่ำ (บั๊ก #9: ดึงชาวนาออก = อาหารหมด D22)


# บอท 5 สาย — 3 สายแรกคือหลักฐานชิ้นสำคัญที่ต้องโชว์กรรมการ
POLICIES = [
    Policy("balanced_quiz",                          use_zoneb=True,  answer_quiz=True),
    Policy("no_quiz (เปิด Zone B แต่ไม่ตอบควิซ)",      use_zoneb=True,  answer_quiz=False),
    Policy("no_zoneb (ไม่ปลดล็อค Zone B เลย)",         use_zoneb=False, answer_quiz=False),
    Policy("rush (เร่งเตาแรง)",                        use_zoneb=True,  answer_quiz=True,
           boost_aggression=0.9, farm_workers=2),
    Policy("builder (สายสร้างเมือง Boost ช้า)",          use_zoneb=True,  answer_quiz=True,
           boost_aggression=0.55, farm_workers=4),
]


class Run:
    """[TH] หน้าที่: จำลองเกม 1 รอบ (30 วัน) ตามสายการเล่นที่กำหนด
    โครงสร้างวันเดียวกับเกมจริง: จัดคน → ผลิต → เตา → พายุ → Hope → เช็คแพ้ชนะ"""

    def __init__(self, policy, rng):
        self.p = policy
        self.rng = rng
        self.workers = [Worker() for _ in range(POP_START)]
        self.food, self.water = float(FOOD_START), float(WATER_START)
        self.iron, self.labmat = float(IRON_START), float(LABMAT_START)
        self.power = 0.0
        self.core, self.heat = CORE_START, HEAT_START
        self.fuel = 0.0                 # ดิวเทอเรียมคงคลัง
        self.tritium = 0.0
        self.hope = HOPE_START
        self.storm_pressure = 0.0
        self.storm_active = False
        self.storm_day = None
        self.boost_days = 0             # วันที่ Boost สะสม (ป้อนสูตรพายุ)
        self.core_stall_days = 0
        self.scram_cooldown = 0
        self.suits = 0
        # สถานะวิจัย: ซ่อมแล็บก่อน แล้วไล่ตาม RESEARCH_PLAN ทีละใบ
        self.lab_repaired = False
        self.repair_progress = 0
        self.repair_paid = False
        self.research_done = set()
        self.research_idx = 0
        self.research_days_left = 0
        self.extractor = False
        self.toroidal_lv = 0
        self.poloidal_lv = 0
        self.sensor = False
        self.zoneb_open = False
        self.zoneb_days_produced = 0
        self.quiz_mastered = False      # q_tritium_breeding: Zone B 3.0 → 8.0/วัน
        self.strike_armed = True
        self.exodus_armed = True
        self.exodus_done = False
        self.violations = 0             # ตัวนับ invariant (ทรัพยากรติดลบ ฯลฯ — ต้องเป็น 0)

    # ── ตัวช่วย ──
    def alive_workers(self):
        return [w for w in self.workers if w.alive]

    def check_invariants(self):
        """[TH] ยามเฝ้าบั๊ก #17/#18: ค่าเหล่านี้ห้ามติดลบเด็ดขาด — ถ้าติดลบ = สูตรเพี้ยน"""
        for v in (self.food, self.water, self.power, self.iron,
                  self.labmat, self.fuel, self.tritium, self.heat):
            if v < -1e-9:
                self.violations += 1

    # ── ขั้นที่ 1: จัดคนเข้าตำแหน่ง (14 คน / งานมากกว่านั้น — ต้องเลือก) ──
    def assign(self):
        avail = [w for w in self.alive_workers()
                 if not w.resting and w.strike_days == 0 and w.rad <= EFF_RAD_HARD]
        jobs = {"farm": [], "power": [], "water": [], "mine": [],
                "lab": [], "cool": [], "zoneb": []}
        avail.sort(key=lambda w: w.fatigue)   # คนสดทำงานก่อน

        def take(n):
            out = []
            while avail and len(out) < n:
                out.append(avail.pop(0))
            return out

        # Zone B ก่อน (ต้อง 2 คน รังสีต่ำ) — เฉพาะสายที่เปิดใช้และประตูเปิดแล้ว
        if self.zoneb_open and self.p.use_zoneb:
            cands = [w for w in avail if w.rad < ZONEB_SEND_RAD_MAX]
            for w in cands[:ZONEB_MIN_STAFF]:
                avail.remove(w)
                jobs["zoneb"].append(w)
        # โครงพื้นฐานตามตาราง "สมดุล" ใน CONFIG (farm 3 / power 2 / water 2 / mine 4 / lab 3)
        jobs["farm"] = take(self.p.farm_workers)
        # ไฟ: อาคารเปิดเพิ่ม (Extractor/MedBay/Zone B) → โหลดขึ้น ต้องเพิ่มคนแผนกไฟ
        power_need = 2
        if self.extractor and ("nuclear_medicine" in self.research_done or self.zoneb_open):
            power_need = 3
        jobs["power"] = take(power_need)
        # น้ำ: สต็อกเกิน 120 แล้วคนเดียวพอ (ผลิต 30 ดื่ม 14/วัน) — คืนแรงงานให้แผนกอื่น
        jobs["water"] = take(1 if self.water > 120 else 2)
        # ช่วงซ่อมแล็บ/วิจัย ต้องมีคนแล็บ · ช่วงความร้อนสูง/พายุต้องมีคน cooling
        lab_need = REPAIR_WORKERS if not self.lab_repaired else 3
        jobs["lab"] = take(lab_need if self.research_idx < len(RESEARCH_PLAN)
                           or not self.lab_repaired else 0)
        if self.storm_active:
            jobs["cool"] = take(3)      # เงื่อนไขเดียวกับการ์ด #8: พายุมา cooling ต้อง ≥ 3
        elif self.heat > 45:
            jobs["cool"] = take(2 if self.heat > 65 else 1)
        jobs["mine"] = take(4)
        return jobs

    # ── ขั้นที่ 2: เศรษฐกิจรายวัน (ทุกอย่างคูณ Σ efficiency — กติกาข้อ 7) ──
    def economy(self, jobs):
        eff = {k: sum(w.efficiency() for w in v) for k, v in jobs.items()}
        alive = len(self.alive_workers())

        self.food += eff["farm"] * FOOD_PER_FARMER
        self.water = min(WATER_CAP, self.water + eff["water"] * WATER_PER_WORKER)
        self.iron += eff["mine"] * IRON_PER_MINER
        self.labmat += LABMAT_PER_DAY

        # ไฟ: ผลิต - โหลด · floor 0 (บั๊ก #17) · blackout ใช้ธง ไม่ใช่ค่าติดลบ
        draw = (DRAW_BASE + (15 if self.extractor else 0)
                + (DRAW_MEDBAY if "nuclear_medicine" in self.research_done else 0)
                + (25 if self.zoneb_open else 0))
        raw = eff["power"] * POWER_PER_WORKER - draw
        blackout = raw < 0
        self.power = max(0.0, min(POWER_CAP, self.power + raw))

        # กิน/ดื่ม · เน่าเสียตามรังสีเฉลี่ย
        avg_rad = statistics.mean([w.rad for w in self.alive_workers()] or [0])
        spoil = 0.05 + (avg_rad / 100.0) * 0.15
        self.food = max(0.0, self.food * (1 - spoil) - alive * FOOD_EAT)
        water_short = self.water < alive * WATER_DRINK
        self.water = max(0.0, self.water - alive * WATER_DRINK)

        # ความหิว: มีอาหาร = อิ่ม รีเซ็ต · ไม่มี = สะสม 30/วัน (บั๊ก #12)
        for w in self.alive_workers():
            if self.food <= 0:
                w.hunger += HUNGER_PER_DAY
            else:
                w.hunger = 0.0
        return eff, blackout, water_short, avg_rad

    # ── ขั้นที่ 3: ซ่อมแล็บ + คิววิจัย (จ่ายครั้งเดียวตอนเริ่ม — บั๊ก #2) ──
    def research(self, jobs):
        completed = 0
        if not self.lab_repaired:
            if not self.repair_paid and self.iron >= REPAIR_IRON:
                self.iron -= REPAIR_IRON
                self.repair_paid = True
            if self.repair_paid and len(jobs["lab"]) >= REPAIR_WORKERS:
                self.repair_progress += 1
                if self.repair_progress >= REPAIR_DAYS:
                    self.lab_repaired = True
            return 0
        if self.research_idx >= len(RESEARCH_PLAN):
            return 0
        rid, days, c_pow, c_iron, c_mat = RESEARCH_PLAN[self.research_idx]
        if self.research_days_left == 0:      # ยังไม่เริ่มใบนี้ → จ่ายก่อน (ครั้งเดียว)
            if self.power >= c_pow and self.iron >= c_iron and self.labmat >= c_mat:
                self.power -= c_pow
                self.iron -= c_iron
                self.labmat -= c_mat
                self.research_days_left = days
                # เกมจริงเริ่มงานวันเดียวกับที่กดจ่าย — ไม่ return เพื่อให้วันนี้นับด้วย
            else:
                return 0
        # งานเดินเฉพาะเมื่อนักวิจัยพอ (staff_ratio_min 0.5 — กันบั๊ก #7 ค้างตลอดกาล)
        if len(jobs["lab"]) >= max(1, int(3 * STAFF_RATIO_MIN)):
            self.research_days_left -= 1
            if self.research_days_left == 0:
                self.research_done.add(rid)
                self.research_idx += 1
                completed = 1
                # ผลของโน้ตแต่ละใบ → ปลดอาคาร/ความสามารถ
                if rid == "deuterium" and self.iron >= ASSUMED_EXTRACTOR_IRON:
                    self.iron -= ASSUMED_EXTRACTOR_IRON
                    self.extractor = True
                if rid == "confinement" and self.iron >= ASSUMED_COIL_IRON * 2:
                    self.iron -= ASSUMED_COIL_IRON * 2
                    self.toroidal_lv, self.poloidal_lv = 1, 1
        # อัปเกรดคอยล์ต่อจนถึงระดับ 3 (วันละระดับ ถ้าเหล็กพอ) — สูตร cooling ใน CONFIG
        # ให้ toroidal สูงสุด min(lv,3)*9 = 27 · poloidal damp 6/ระดับ — นี่คือเกราะกันพายุ
        if "confinement" in self.research_done and self.iron >= ASSUMED_COIL_IRON:
            if self.toroidal_lv < 3 and self.toroidal_lv <= self.poloidal_lv:
                self.toroidal_lv += 1
                self.iron -= ASSUMED_COIL_IRON
            elif self.poloidal_lv < 3:
                self.poloidal_lv += 1
                self.iron -= ASSUMED_COIL_IRON
                if rid == "sensor":
                    self.sensor = True
                if rid == "nuclear_medicine":
                    # คราฟต์ชุดกันรังสี 30 labMat/ชุด
                    n = min(ASSUMED_SUIT_COUNT, int(self.labmat // 30))
                    self.labmat -= n * 30
                    self.suits = n
        return completed

    # ── ขั้นที่ 4: เตาปฏิกรณ์ (หัวใจเกม — สูตรตรง CONFIG ทุกบรรทัด) ──
    def reactor(self, jobs):
        # Extractor เติมดิวเทอเรียม · เตาเผา 6/วัน → fuelEfficiency
        if self.extractor:
            self.fuel += ASSUMED_EXTRACTOR_DEUT
        fuel_eff = min(1.0, self.fuel / FUEL_NEED) if self.fuel > 0 else 0.0
        self.fuel = max(0.0, self.fuel - FUEL_NEED)

        # Zone B ผลิต tritium — 3.0 พื้นฐาน / 8.0 เมื่อตอบควิซถูก (จุดชี้แพ้ชนะ)
        if self.zoneb_open and len(jobs["zoneb"]) >= ZONEB_MIN_STAFF:
            rate = ZONEB_TRITIUM_MASTERY if self.quiz_mastered else ZONEB_TRITIUM_BASE
            self.tritium += rate
            self.zoneb_days_produced += 1
            # ควิซปลดได้หลัง Zone B ผลิตจริง >= 2 วัน (requiresApplied — กันบั๊ก #3)
            if (self.p.answer_quiz and not self.quiz_mastered
                    and self.zoneb_days_produced >= ASSUMED_QUIZ_DELAY_DAYS):
                self.quiz_mastered = True

        # ตัดสินใจ Boost: เชื้อเพลิงพอ + ความร้อนคาดการณ์ไม่ชนเพดาน + (ช่วง Method B ต้องมี tritium)
        cooling = (6 + min(self.water / 12, 7) + len(jobs["cool"]) * 3
                   + min(self.toroidal_lv, 3) * 9)
        damp = self.poloidal_lv * 6.0
        # คาดการณ์ความร้อนพรุ่งนี้ถ้า Boost — รวมความร้อนพายุด้วย (บอทไม่ตาบอด)
        projected = self.heat + MODE_HEAT + BOOST_HEAT - cooling - damp
        if self.storm_active:
            projected += STORM_HEAT_PER_DAY - (SENSOR_HEAT_ROOM if self.sensor else 0)
        # ช่วง Method B: เกมจริงเผา tritium เท่าที่มี (floor 0) — บอทจึงยอม Boost
        # เมื่อ tritium หลังผลิตพ้น soft floor (6) ไม่ต้องรอเต็ม 9
        # → ตอบควิซ (ผลิต 8/วัน) = Boost ต่อเนื่องได้ · ไม่ตอบ (3/วัน) = ไม่มีวันถึง
        want_boost = (fuel_eff >= 0.9
                      and projected < HEAT_MELTDOWN * self.p.boost_aggression
                      and (self.core < METHOD_B_CORE_GATE
                           or self.tritium >= TRITIUM_SOFT_FLOOR + 1.0))
        boosting = want_boost

        # ความร้อน: พื้นฐาน + Boost − cooling − poloidal + พายุ (Sensor ให้ห้องหายใจ)
        delta_heat = MODE_HEAT + (BOOST_HEAT if boosting else 0) - cooling - damp
        if self.storm_active:
            delta_heat += STORM_HEAT_PER_DAY - (SENSOR_HEAT_ROOM if self.sensor else 0)
        self.heat = max(0.0, self.heat + delta_heat)

        # SCRAM: เบรกฉุกเฉินเมื่อจะ meltdown (แลก CORE −10, น้ำ −30, Hope −3)
        scrammed = False
        if (self.heat >= HEAT_MELTDOWN and self.scram_cooldown == 0
                and self.water >= SCRAM_WATER_COST):
            self.heat -= SCRAM_HEAT_REDUCE
            self.core = max(0.0, self.core - SCRAM_CORE_PENALTY)
            self.water -= SCRAM_WATER_COST
            self.scram_cooldown = SCRAM_COOLDOWN_DAYS
            boosting = False
            scrammed = True
        self.scram_cooldown = max(0, self.scram_cooldown - 1)

        # CORE ขึ้น: gain พื้นฐาน/Boost × fuelEff · ประตู Method B ที่ 80
        gain = (BOOST_CORE_GAIN if boosting else CORE_GAIN_BASE) * fuel_eff
        if self.core >= METHOD_B_CORE_GATE:
            if self.tritium < METHOD_B_TRITIUM_MIN:
                gain = 0.0                     # ★ ไม่มี tritium = CORE ค้างที่ 80 ตรงนี้เอง
            elif self.tritium < TRITIUM_SOFT_FLOOR:
                gain *= self.tritium / TRITIUM_SOFT_FLOOR
            # ช่วง Method B เตาเผา tritium ทุกวัน (Boost 9 / ปกติ 6)
            self.tritium = max(0.0, self.tritium -
                               (BOOST_TRITIUM_COST if boosting else IDLE_TRITIUM_COST))
        prev = self.core
        self.core = min(CORE_WIN, self.core + gain)
        if boosting:
            self.boost_days += 1
        # เปิด Zone B เมื่อเข้า Phase 4 (core >= 80) — เฉพาะสายที่ใช้
        if self.core >= METHOD_B_CORE_GATE and self.p.use_zoneb:
            self.zoneb_open = True
        self.core_stall_days = self.core_stall_days + 1 if self.core <= prev else 0
        return self.core - prev, boosting, scrammed

    # ── ขั้นที่ 5: รังสี + ความล้า + ความตาย ──
    def bodies(self, jobs):
        deaths = 0
        suit_pool = self.suits
        for job, ws in jobs.items():
            for w in ws:
                w.fatigue = min(100, w.fatigue + FATIGUE_WORK)
                if job == "zoneb":
                    mult = RAD_SUIT_MULT if suit_pool > 0 else 1.0
                    if suit_pool > 0:
                        suit_pool -= 1
                    w.rad += RAD_ZONEB * mult
                elif job == "mine":
                    w.rad += RAD_MINE
        for w in self.alive_workers():
            if w.resting:
                w.fatigue = max(0, w.fatigue + FATIGUE_REST)
                if w.fatigue <= REST_RETURN:
                    w.resting = False
            elif w.fatigue >= REST_THRESHOLD:
                w.resting = True
            if self.heat > 85:
                w.rad += RAD_HEAT_LEAK       # เตารั่ว — โดนทุกคน
            if self.storm_active:
                w.rad += STORM_RAD_PER_DAY
            if w.strike_days > 0:
                w.strike_days -= 1
            # ตาย: รังสีเกิน 80 → 20%/วัน
            if w.rad > DEATH_RAD and self.rng.random() < DEATH_CHANCE:
                w.alive = False
                deaths += 1

        # โรงพยาบาล: หลังวิจัยการแพทย์นิวเคลียร์ รักษาคนรังสีสูงสุด 2 คน/วัน (−25 รังสี)
        # ระบบนี้คือเหตุที่เกมจริงไม่เข้า death spiral — คนงานเหมือง/Zone B ฟื้นได้
        if "nuclear_medicine" in self.research_done:
            for w in sorted(self.alive_workers(), key=lambda x: -x.rad)[:MEDBAY_BEDS]:
                w.rad = max(0.0, w.rad - MEDBAY_HEAL)
        return deaths

    # ── ขั้นที่ 6: พายุ (นาฬิกาบังคับจบเกม — ยิ่ง Boost มาก ยิ่งมาเร็ว) ──
    def storm(self):
        if self.storm_active:
            return
        rise = (STORM_BASE_RISE
                + (STORM_IGNITION_BONUS if self.core >= METHOD_B_CORE_GATE else 0)
                + (STORM_ZONEB_BONUS if self.zoneb_open else 0)
                + (self.core / 100.0) * STORM_CORE_COEFF
                + self.boost_days * BOOST_DAYS_COEFF)
        self.storm_pressure += rise

    def storm_check(self, day):
        if not self.storm_active and self.storm_pressure >= STORM_PRESSURE_MAX:
            self.storm_active = True
            self.storm_day = day

    # ── ขั้นที่ 7: Hope Ledger (กติกาข้อ 8 — ทุกระบบส่งรายการเข้าบัญชีเดียว) ──
    def hope_tick(self, core_gain, deaths, blackout, water_short,
                  research_completed, scrammed):
        alive = self.alive_workers()
        delta = 0.0
        delta += core_gain * CORE_PROGRESS_BONUS
        delta -= sum(1 for w in alive if w.fatigue >= REST_THRESHOLD) * 1     # exhausted
        delta -= sum(1 for w in alive if w.hunger > HUNGRY_THRESHOLD) * 2     # hungry
        delta -= sum(1 for w in alive if EFF_RAD_HARD < w.rad <= DEATH_RAD) * 3  # sick
        delta -= sum(1 for w in alive if w.rad > DEATH_RAD) * 5               # dying
        delta -= deaths * 8
        if self.food > len(alive) * 3:
            delta += 3
        if self.food <= 0:
            delta -= 6
        if water_short:
            delta -= 4
        if blackout:
            delta -= 5
        if self.heat > 90:
            delta -= 3
        if self.storm_active:
            delta -= 1
        if self.core_stall_days >= 3:
            delta -= 4
        delta += research_completed * 6
        if scrammed:
            delta -= SCRAM_HOPE_PENALTY
        self.hope = max(0.0, min(100.0, self.hope + delta))

        # เหตุการณ์ threshold (มี hysteresis — ไม่ยิงรัวทุกวัน)
        if self.hope < HOPE_STRIKE and self.strike_armed:
            self.strike_armed = False
            n = max(1, int(len(alive) * STRIKE_RATIO))
            for w in self.rng.sample(alive, min(n, len(alive))):
                w.strike_days = 2
        elif self.hope > HOPE_STRIKE + HYSTERESIS:
            self.strike_armed = True
        if self.hope < HOPE_EXODUS and self.exodus_armed and not self.exodus_done:
            self.exodus_done = True
            n = max(1, int(len(alive) * EXODUS_RATIO))
            for w in self.rng.sample(alive, min(n, len(alive))):
                w.alive = False

    # ── เกม 1 รอบเต็ม ──
    def play(self, trace=False):
        """[TH] คืนผล: (ผลลัพธ์, วันจบ, core สุดท้าย, วันพายุ, จำนวน invariant พัง)
        trace=True → พิมพ์สถานะรายวัน (ใช้ debug/สาธิตกลไกให้กรรมการดูทีละวัน)"""
        for day in range(1, 31):
            jobs = self.assign()
            eff, blackout, water_short, avg_rad = self.economy(jobs)
            completed = self.research(jobs)
            core_gain, boosting, scrammed = self.reactor(jobs)
            deaths = self.bodies(jobs)
            self.storm()
            self.storm_check(day)
            self.hope_tick(core_gain, deaths, blackout, water_short,
                           completed, scrammed)
            self.check_invariants()
            if trace:
                print(f"  D{day:>2} core {self.core:5.1f} heat {self.heat:5.1f} "
                      f"tri {self.tritium:4.1f} {'BOOST' if boosting else '     '} "
                      f"food {self.food:5.0f} hope {self.hope:5.1f} "
                      f"zoneB {len(jobs['zoneb'])} lab {len(jobs['lab'])} "
                      f"rest {sum(1 for w in self.alive_workers() if w.resting)} "
                      f"research#{self.research_idx}")

            # เช็คแพ้ชนะทุกวัน (บั๊ก #15: CORE ถึง 100 ต้องจบทันที ไม่รอ D30)
            if self.core >= CORE_WIN:
                return "WIN", day, self.core, self.storm_day, self.violations
            if self.heat >= HEAT_MELTDOWN:
                return "MELTDOWN", day, self.core, self.storm_day, self.violations
            if self.hope <= 0:
                return "HOPE_ZERO", day, self.core, self.storm_day, self.violations
            if not self.alive_workers():
                return "ALL_DEAD", day, self.core, self.storm_day, self.violations
        return "D30_CORE_STALL", 30, self.core, self.storm_day, self.violations


def simulate(policy, runs, seed):
    """[TH] หน้าที่: รันสายการเล่นเดียวซ้ำ N รอบ แล้วสรุปสถิติ"""
    results = Counter()
    win_days, final_cores, storm_days = [], [], []
    violations = 0
    for i in range(runs):
        rng = random.Random(seed * 100003 + i)   # seed ต่อรอบ → ผลทำซ้ำได้ 100%
        random.seed(seed * 100003 + i)
        run = Run(policy, rng)
        outcome, day, core, storm_day, viol = run.play()
        results[outcome] += 1
        final_cores.append(core)
        violations += viol
        if outcome == "WIN":
            win_days.append(day)
        if storm_day:
            storm_days.append(storm_day)
    return results, win_days, final_cores, storm_days, violations


def main():
    ap = argparse.ArgumentParser(description="NUCLEAR Re:Mind balance simulator")
    ap.add_argument("--runs", type=int, default=3000, help="จำนวนรอบต่อสาย (ค่าเริ่มต้น 3000)")
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--policy", type=str, default=None,
                    help="รันเฉพาะสายที่ชื่อขึ้นต้นด้วยข้อความนี้ เช่น no_zoneb")
    ap.add_argument("--trace", action="store_true",
                    help="พิมพ์สถานะรายวันของรอบแรก (สาธิต/หาจุดคอขวด)")
    args = ap.parse_args()

    if args.trace:
        for p in [p for p in POLICIES
                  if args.policy is None or p.name.startswith(args.policy)]:
            print(f"\n══ trace: {p.name} ══")
            rng = random.Random(args.seed * 100003)
            random.seed(args.seed * 100003)
            outcome = Run(p, rng).play(trace=True)
            print(f"  → {outcome[0]} วันที่ {outcome[1]} · CORE {outcome[2]:.1f}")
        return

    chosen = [p for p in POLICIES
              if args.policy is None or p.name.startswith(args.policy)]
    print(f"NUCLEAR Re:Mind — nrm_sim · {args.runs:,} รอบ/สาย · seed {args.seed}")
    print("=" * 78)
    for p in chosen:
        results, win_days, cores, storms, viol = simulate(p, args.runs, args.seed)
        total = sum(results.values())
        win = results.get("WIN", 0)
        print(f"\n▶ {p.name}")
        print(f"   ชนะ {win}/{total} ({100.0*win/total:5.1f}%)"
              + (f" · วันชนะเฉลี่ย D{statistics.mean(win_days):.1f}" if win_days else ""))
        for cause, n in results.most_common():
            if cause != "WIN":
                print(f"   แพ้ {cause:<15} {n:5} ({100.0*n/total:5.1f}%)")
        print(f"   CORE สุดท้ายเฉลี่ย {statistics.mean(cores):5.1f}"
              + (f" · พายุมาเฉลี่ย D{statistics.mean(storms):.1f}" if storms else " · พายุไม่มา"))
        print(f"   invariant พัง (ค่าติดลบ ฯลฯ): {viol}  {'✓ ผ่าน' if viol == 0 else '✗ มีบั๊ก!'}")
    print("\n" + "=" * 78)
    print("ข้อพิสูจน์: no_zoneb / no_quiz ต้องแพ้ ~100% (CORE ค้างแถว 80)")
    print("           balanced_quiz ต้องชนะเป็นส่วนใหญ่ — ความรู้คือกลไกชนะ ไม่ใช่รางวัล")


if __name__ == "__main__":
    main()
