# GROUND_PLAN — พื้นหิน/ลาน/ทางเดิน แบบ reference (ต่อจาก RENDER_PLAN เฟส C ข้อ 4B)

> เป้าหมาย: พื้นแบบภาพ reference — **หญ้าหม่นเป็นฐาน + ลานหินแผ่น (plaza) รอบจุดสำคัญ +
> ทางเดินหินเชื่อมอาคาร + แผ่นแตก/มีมอสส์แซม + เศษปูหลงเหลือเป็นหย่อม**
> สี/แสง/grading ทำแล้ว (Ashfall เฟส A/B) — งานนี้คือ "ชนิดพื้น" อย่างเดียว
> วันที่: 2026-07-18 · กติกาเดิม: visual layer ล้วน · deterministic (Hash ไม่ใช่ Random) · ห้ามแตะ IsoToWorld/SortOrder/gameplay

---

## 0) วิเคราะห์ reference — พื้นมี 4 ชั้นความหมาย

| ชั้น | ในภาพ | สัดส่วนพื้นที่ |
|---|---|---|
| 1. หญ้าฐาน | เขียวหม่นมี variation เป็นหย่อม (มีอยู่แล้ว) | ~70% |
| 2. ลานหิน (plaza) | แผ่นสี่เหลี่ยมสีเทา ปูรอบ CORE TOWER / อนุสรณ์ / อาคาร | ~15% |
| 3. ทางเดิน (path) | แนวหินสีจางกว่า เชื่อมอาคารเป็นแกนตั้ง/นอน กว้าง 1–2 ช่อง | ~10% |
| 4. เศษ/รอยแตก | แผ่นแตก, มอสส์กินขอบ, หย่อมปูเก่าโผล่กลางหญ้า | ~5% |

หลักสำคัญจาก reference: **หินไม่ได้เป็นบล็อกเดียวใหญ่ๆ** — ขอบลานแหว่งแบบสุ่ม (แผ่นหาย 1–2 ช่อง),
แผ่นมี 3–4 เฉดสลับกัน, ทางเดินสีอ่อนกว่าลาน

---

## 1) ของที่มี vs ของที่ขาด

**มีแล้ว (reuse — ห้ามเขียนซ้ำ):**
- `IsoGroundPainter.Hash / Hash01 / VariantIndex` — deterministic pattern ([Assets/Scripts/Systems/IsoGroundPainter.cs](../Assets/Scripts/Systems/IsoGroundPainter.cs))
- `GroundTileFlattener` — เทคนิคตัด rhombus หน้าบน + สร้าง Tile asset ([Assets/Editor/GroundTileFlattener.cs](../Assets/Editor/GroundTileFlattener.cs))
- `GridSpriteFiller` — โครงการปู Tilemap ตามกริด 43×43 + apron ([Assets/Editor/GridSpriteFiller.cs](../Assets/Editor/GridSpriteFiller.cs))
- `TileColorPainter` override + Ashfall groundTint — ระบบ tint ต่อช่อง
- ตำแหน่ง CORE TOWER/อนุสรณ์/อาคาร pre-placed — คำนวณจากกริดได้ (แบบ RenderingSetup)

**ขาด (ต้องทำใหม่):**
- ❌ **Tile "แผ่นหินปูพื้น"** — IsoNature มีแต่ดิน/หญ้า/ก้อนหิน ไม่มี slab/pavement
- ❌ Tilemap ชั้นหิน + ตัววางตามกฎ (plaza/path/patch)

---

## 2) STEP 1 — Tile หินปูพื้น 10 ใบ (32×32 top-face rhombus สไตล์เดียวกับ flat_XXX)

| ไฟล์ | คือ | ใช้ที่ |
|---|---|---|
| `stone_full_0..2` (3) | แผ่นเต็ม 3 เฉดเทา (สลับกันด้วย Hash) | ลาน |
| `stone_crack_0..1` (2) | แผ่นแตกร้าว | แซมลาน ~25% |
| `stone_moss_0..1` (2) | แผ่นมีมอสส์กินขอบ (เขียวหม่นแซม) | แซมลาน ~15% + ขอบลานติดหญ้า |
| `stone_worn_0..1` (2) | แผ่นสึกสีอ่อน | ทางเดิน |
| `stone_edge_0` (1) | แผ่นแหว่ง/ขอบพัง | ขอบลานที่ติดหญ้า |

**แหล่งที่มา — 2 ทาง (ทำ A ก่อน ไม่บล็อกงาน):**
- **A. Generate procedural** (ทันที ใช้ pattern `EnsureRadialTexture` ของ AshfallAtmosphereSetup แต่เป็น pixel-art):
  วาดใน 32×32 → mask rhombus สูตรเดียวกับ GroundTileFlattener (`|x-cx|/(W/2)+|y-cy|/(H/4) ≤ 1`) →
  พื้นเทา `#8A8F87` ±noise ต่อ pixel → เส้นร่องขอบแผ่นเข้ม `#5E625B` → รอยแตก = random-walk line จาก Hash →
  มอสส์ = speckle เขียว `#5E6B4A` เกาะมุม → PPU 32, Point filter, uncompressed (convention BuildingArtSetup)
- **B. ทีม art วาดแทนทีหลัง** — ใช้**ชื่อไฟล์เดิม** วางทับ PNG แล้วรันเมนู re-import → โค้ดไม่ต้องแก้เลย
  (สเปคให้ทีม: 32×32 top-face rhombus 2:1 เต็มช่อง โปร่งรอบนอก โทนเทา `#8A8F87` เข้ากับ palette Ashfall)

เก็บที่ `Assets/Sprites/Art/Tiles/Stone/` + สร้าง Tile asset ข้างกัน (แบบ GroundTileFlattener)

---

## 3) STEP 2 — Tilemap ชั้นใหม่ "StonePave" (ห้ามยุ่ง Ground เดิม)

- GameObject ใต้ Grid เดียวกับ Ground: **`StonePave`** — Tilemap + TilemapRenderer
- Sorting: layer **Ground**, order = order ของ Ground **+1** (ทับหญ้า แต่ใต้ทุกอย่างบน layer Buildings)
- Material: **Sprite-Lit-Default** (โดนแสง/มู้ดเหมือนพื้น) + รับ groundTint จาก AshfallMoodController (เพิ่ม field `stoneTilemap` + `stoneTint` แยกจากหญ้า — หินควรอ่อนกว่า เช่น `#B8BCB4`)
- เหตุที่แยก tilemap: (1) `Fill Grids` re-run ไม่ล้างงานหิน (2) เปิด/ปิดทั้งชั้นได้ (3) TileColorPainter เดิมไม่ชน

---

## 4) STEP 3 — กฎการวาง (deterministic ทั้งหมด — Hash เดิม ไม่มี Random)

ทำเป็น editor menu **`NuclearReMind/Setup Stone Ground (Plaza + Paths)`** (idempotent: ล้าง StonePave แล้ววางใหม่ทุกครั้ง):

### 4.1 Plaza (ลานหิน)
- **CORE TOWER**: จาก origin กลางกริด — วาง slab ใน diamond radius **4** (`|dx|+|dy| ≤ 4` รอบ footprint)
- **อนุสรณ์ + อาคาร pre-placed** (lab/hospital): pad รอบ footprint **1 ช่อง**
- **ขอบแหว่งธรรมชาติ**: ช่องที่ `distance == radius` วางเฉพาะเมื่อ `Hash01(c,r) < 0.55` · ช่องที่แหว่งติดลาน → โอกาส 0.3 ใช้ `stone_edge`

### 4.2 Path (ทางเดิน)
- เส้นแกน: จากขอบ plaza กลาง → ทิศ N/S/E/W ตามแนว col/row จนถึงอาคาร pre-placed หรือสุดระยะ **10 ช่อง**
- กว้าง 1 ช่อง + ช่องข้างเคียงโอกาส 0.35 (Hash) → ดูกว้าง 1–2 ไม่เป็นเส้นตรงเป๊ะ
- ใช้ `stone_worn` เป็นหลัก แซม `stone_crack` 20%
- **ห้ามวางทับช่องแร่ A/B** (เช็ค `OreDepositManager`) — ที่เหลือวางได้ (พื้นเป็น visual ไม่มีผล placement)

### 4.3 Patch (ซากปูเก่ากลางหญ้า)
- seed: ช่องในกริดที่ `Hash(c+53,r+97) % 1000 < 12` (~1.2%) → งอกเป็น blob 3–6 ช่อง (เดินสุ่มจาก Hash)
- ใช้ `stone_crack`/`stone_moss` ล้วน (ซากเก่า ไม่ใช่ลานใหม่)

### 4.4 เลือก variant ต่อช่อง (ในลาน)
```
h = Hash(c,r)/7 % 100 →  <60: full(เฉดจาก h%3) · <85: crack · ≤99: moss
ช่องลานที่ "ติดหญ้า" (เพื่อนบ้าน 4 ทิศไม่มี slab) → บังคับ moss/edge 50%  ← ขอบกินมอสส์แบบ reference
```

### 4.5 (เฟสถัดไป — ยังไม่ทำรอบนี้) Apron ตอนวางอาคารใหม่
- ฟัง `OnBuildingPlaced` → ปูรอบ footprint 1 วง runtime (visual ล้วน) — ทำหลังลานหลักผ่านตาแล้ว

---

## 5) STEP 4 — ผูกเข้าระบบ

1. ลงทะเบียน `RunAllSetups.cs`: **หลัง** `Fill Grids (Ground + Fog)` / ก่อน `Setup Decor`
2. `AshfallMoodController` เพิ่ม `stoneTilemap` + `stoneTint` (ปรับสดใน Inspector เหมือน groundTint)
3. `AshfallRenderSetup.WireMoodController` wire `StonePave` ให้อัตโนมัติ

---

## 6) Verify

- [ ] compile error CS = 0 · เมนูรันซ้ำไม่ซ้อน (ล้าง StonePave ก่อนวางเสมอ)
- [ ] ลานรอบ CORE TOWER + อนุสรณ์ ขอบแหว่งไม่เป็นสี่เหลี่ยมเป๊ะ · ทางเดิน 4 ทิศเชื่อมอาคาร
- [ ] เฉดแผ่นสลับ 3 เฉด + แตก/มอสส์แซม · ขอบลานติดหญ้ามีมอสส์/แผ่นแหว่งมากกว่ากลางลาน
- [ ] รัน `Fill Grids` ซ้ำ → งานหินไม่หาย (คนละ tilemap)
- [ ] tint หินปรับได้จาก `__ASHFALL_Volume` Inspector · โดนแสง/หมอกเหมือนพื้นหญ้า
- [ ] ไม่มีผลต่อ placement/pathing/gameplay ใดๆ (StonePave ไม่มี collider ไม่มี logic)

## ลำดับทำ

| # | งาน | ผลที่เห็น |
|---|---|---|
| 1 | Generator tile หิน 10 ใบ + import + Tile assets | ไฟล์ `Stone/` ครบ เปิดดูใน Project ได้ |
| 2 | สร้าง StonePave tilemap + เมนูวาง plaza/path/patch | พื้นแบบ reference ในซีน |
| 3 | ผูก RunAllSetups + MoodController tint | จูนสีหินสดได้ |
| 4 | (ทีหลัง) art ทีมวาดทับ + apron ตอนวางอาคาร | อัปเกรดคุณภาพโดยไม่แก้โค้ด |
