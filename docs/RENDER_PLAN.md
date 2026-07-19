# RENDER_PLAN — "ASHFALL DAWN" Art-Direction Cutover

> เป้าหมาย: เปลี่ยนลุคเกม NUCLEAR Re:Mind จาก **prototype daytime สว่างจัด เขียวสด flat** (Before)
> ให้เป็น **post-apocalyptic dawn ภาพยนตร์ โทน orange-teal · god rays · หมอก · แสงอุ่นจากหน้าต่าง/เทียน · vignette หนัก** (After — สไตล์ *They Are Billions / Endzone / Frostpunk*)
>
> **เขียนเพื่อให้ Fable 5 อ่านแล้วทำตามได้ 100%** — ทุกค่าเป็นตัวเลขจริง, ทุก step ตรวจได้, ทุกไฟล์เป็น full file
> Stack: Unity 6 (6000.3.6f1) · URP + **URP 2D Renderer (Light2D)** · Legacy Input · WebGL/Windows target
> วันที่: 2026-07-18

---

## 0) กติกาสำหรับ Fable 5 (อ่านก่อนแตะอะไร)

1. **ห้ามแตะ gameplay/logic** — งานนี้เป็น **visual layer ล้วน** ห้ามแก้ Manager, สูตร, SaveData, sorting math (`GridManager.IsoToWorld/SortOrder`)
2. **ยึดค่าจาก CONFIG.md/GDD.md เดิม** — งานนี้ไม่มีตัวเลข gameplay ใหม่ ถ้าเจอ conflict ให้ถาม Rut
3. **Editor setup ทุกตัวต้อง idempotent** — รันซ้ำห้ามสร้างซ้ำ (ลบของเก่าชื่อเดียวกันก่อนสร้าง) + ลงทะเบียนใน `RunAllSetups.cs`
4. **Comment ในโค้ดเป็นภาษาอังกฤษทั้งหมด** · ให้ full file copy-paste ได้
5. **ปิด Unity Editor ก่อนรัน batch compile** (`error CS` ต้อง = 0) — ดูคำสั่งใน CLAUDE.md
6. **UI ต้องอ่านออกเสมอ** — mood ห้ามชนะ legibility ของตัวเลข/ปุ่ม (UI เป็น Screen-Space-Overlay = post ไม่โดน อยู่แล้ว ดู §9)
7. **ทำเป็นเฟส A→B→C** ตาม §11 · จบเฟส A ต้องเห็นความต่าง 70% แล้ว (ถ้าไม่เห็น = ตั้งค่าผิด หยุดแล้วดีบัก อย่าทำเฟสต่อ)

---

## 1) วิเคราะห์ Before → After (ต้องเข้าใจก่อนทำ)

| มิติ | รูป 1 (Before) | รูป 2 (After — เป้าหมาย) |
|---|---|---|
| **Global light** | ขาว 1.15 → สว่างเต็ม ไม่มีทิศ | ดิม + เย็น (teal) → twilight, มีทิศแสงอุ่นจากขอบฟ้า |
| **Saturation** | สูงมาก (เขียว/เหลืองสด) | ต่ำลง ~40–50% เหลือ muted olive/brown/grey |
| **Value/contrast** | สว่างแบน mid-high ทั้งจอ | crushed shadows (แต่ไม่ดำสนิท) + highlight อุ่นพุ่ง |
| **สีหลัก (grading)** | neutral | **orange highlight / teal shadow** (cinematic split-tone) |
| **Bloom** | ไม่มี | มี — หน้าต่าง/เทียน/ขอบฟ้าเรืองฟุ้ง |
| **Vignette** | ไม่มี | หนัก มุมจอมืด ดึงสายตาเข้ากลาง |
| **หมอก/Atmosphere** | ไม่มี — เห็นขอบ grid → ดินน้ำตาล → ดำ | หมอกจาง + atmospheric perspective, ขอบเบลอเข้าฉากหลัง |
| **ฉากหลัง (backdrop)** | ไม่มี (ดำ) | ท้องฟ้า dramatic + god rays + เงาตึกร้าง (skyline silhouette) |
| **แสง local** | ไม่มี | หน้าต่างเรืองส้ม, เทียนรอบอนุสรณ์, ควันจากปล่อง |
| **Ground** | tile เขียว checkerboard เด่น | พื้นเทา/น้ำตาลเข้ม cracked มืด, checkerboard จม |
| **Shadow grounding** | ไม่มีเงาใต้วัตถุ → ลอย | มี contact/blob shadow ใต้อาคาร/คน |
| **Particles** | ไม่มี | ควัน, เถ้า/ฝุ่นลอย, ประกายไฟ (embers) |

**สรุป lever ที่ให้ผลมากสุด → น้อยสุด** (ทำตามลำดับนี้):
`Post-processing Volume (grading+bloom+vignette)` → `Global Light2D ดิม+เย็น` → `Local warm lights` → `หมอก overlay` → `Ground tint` → `Particles` → `Backdrop skyline + god rays` → `Blob shadows`

---

## 2) หลักการเทคนิค URP 2D (สิ่งที่ต้องรู้ ไม่งั้นทำผิดแน่)

- **Post-processing ใน URP 2D ทำผ่าน Volume framework** (`Volume` component + `VolumeProfile` asset) — ทำงานกับ 2D Renderer ได้ ต้องเปิด 2 จุด: Camera `Post Processing = ON` และ Renderer2D มี post enabled (default เปิด)
- **Light2D จะมีผลกับ sprite เฉพาะที่ใช้ material แบบ "Lit"** (`Sprite-Lit-Default` หรือ shader `Universal Render Pipeline/2D/Sprite-Lit-Default`) — sprite ที่ใช้ `Sprite-Unlit-Default` จะ **ไม่โดนแสง = ไม่มืดตาม** (เจอบ่อย: ตั้ง global มืดแล้วบางชิ้นยังสว่างโพลน → เพราะมัน Unlit)
- **สถานะปัจจุบันในซีน (ตรวจแล้ว):** มี `Global Light 2D` (สีขาว, intensity **1.15**), `Fill Light 2D` ×2 (type Global), `Reactor Glow Light 2D` (type Point). **ยังไม่มี Volume ในซีนเลย.** URP asset `m_SupportsHDR = 1` (bloom พร้อม) แต่ `m_ColorGradingMode = 0` (LDR — ต้องเปลี่ยนเป็น HDR ดู §3)
- **UI = Screen-Space-Overlay** → render หลัง post-processing → **grading/vignette/bloom ไม่โดน UI** (ดี, UI อ่านออกเสมอ). ยกเว้นถ้ามี Canvas เป็น Screen-Space-Camera/World → อันนั้นจะโดน (โปรเจกต์นี้ใช้ Overlay ทั้งหมด = ปลอดภัย)
- **WebGL:** bloom/vignette/grading ใช้ได้ แต่ต้องคุมต้นทุน — bloom ตั้ง moderate, ปิด DoF/Motion Blur (2D ไม่ต้องใช้)

---

## 3) STEP 1 — Pipeline & Camera settings (แก้ 3 assets)

### 3.1 `Assets/Settings/UniversalRP.asset`
- `m_ColorGradingMode: 0` → **`1`** (LDR → **HDR** — จำเป็นเพื่อ split-tone/bloom ให้ได้ผลนุ่ม)
- `m_ColorGradingLutSize`: ตั้ง **32** (default) พอ
- คง `m_SupportsHDR: 1` ไว้
> ⚠ แก้ผ่าน Inspector: เลือก UniversalRP.asset → Quality → HDR ✓ · Post-processing → Grading Mode = **High Dynamic Range**, LUT size = 32

### 3.2 Main Camera (ใน`Gamescene.unity`)
- `Rendering > Post Processing` = **ON** (`m_RenderPostProcessing: 1`)
- `Anti-aliasing` = **FXAA** (ถูก, เหมาะ WebGL) — SMAA ก็ได้ถ้าเดสก์ท็อป
- `Background Type` = **Solid Color**, สี **#0B0E12** (เผื่อ backdrop ยังไม่มา ขอบจอจะได้ไม่ดำสนิทโพลน)
- ห้ามแตะ: Projection (Orthographic), Size, Culling — เป็นค่ากล้องไอโซเมตริก

### 3.3 `Assets/Settings/Renderer2D.asset`
- ตรวจว่า **Post Processing** ติ๊กเปิด (default) · ไม่ต้องเพิ่ม Renderer Feature ใดๆ

**✅ Verify STEP 1:** compile ผ่าน · เข้า Play ยังเห็นภาพเดิม (ยังไม่มี Volume จึงยังไม่เปลี่ยนลุค) แต่ Camera inspector โชว์ Post Processing = ON

---

## 4) STEP 2 — Global Volume + Profile (หัวใจของลุค · ค่าตัวเลขเป๊ะ)

สร้าง GameObject **`__ASHFALL_Volume`** (Global Volume, `Is Global = ✓`, Priority `10`, Weight `1`)
ผูก VolumeProfile asset ใหม่ **`Assets/Settings/Ashfall_Dawn.asset`** ใส่ override ตามตารางนี้ **ทั้งหมด**:

### 4.1 Tonemapping
| field | ค่า |
|---|---|
| Mode | **ACES** (ให้ film-look + roll-off highlight นุ่ม) |

### 4.2 White Balance
| field | ค่า |
|---|---|
| Temperature | **+18** (อุ่นขึ้น — ดวงอาทิตย์ยามเช้า) |
| Tint | **+4** (magenta จางๆ กัน sickly green) |

### 4.3 Color Adjustments
| field | ค่า |
|---|---|
| Post Exposure | **-0.35** (ดึงลงจาก daylight) |
| Contrast | **+14** |
| Color Filter | **#FFE9CE** (ฟิล์มอุ่นบางๆ) |
| Hue Shift | **0** |
| Saturation | **-42** (ฆ่าเขียว/เหลืองสดให้ muted) |

### 4.4 Split Toning (สร้าง orange-teal — ขั้นที่ให้ "ลุคหนัง" มากสุด)
| field | ค่า |
|---|---|
| Shadows | **#2E5A66** (teal เย็น) |
| Highlights | **#E9A24B** (ส้มอุ่น) |
| Balance | **+12** (ดัน crossover ไปทาง highlight เล็กน้อย) |

### 4.5 Shadows / Midtones / Highlights
| field | ค่า |
|---|---|
| Shadows | RGB (0.85, 0.95, 1.05) — ยกเงาเป็น teal เล็กน้อย (กัน crush เป็นดำตาย) |
| Midtones | (1.0, 1.0, 1.0) |
| Highlights | (1.08, 1.02, 0.92) — highlight อุ่น |
| Shadows Start/End, Highlights Start/End | default (0 / 0.3 / 0.55 / 1.0) |

### 4.6 Bloom
| field | ค่า |
|---|---|
| Threshold | **0.9** (ต้อง HDR ถึงจะได้เฉพาะแหล่งแสงจริง ไม่ฟุ้งทั้งจอ) |
| Intensity | **1.0** (WebGL) / 1.3 (เดสก์ท็อป) |
| Scatter | **0.62** |
| Tint | **#FFDFAF** (ฟุ้งอุ่น) |
| High Quality Filtering | ✓ (ปิดถ้า WebGL เฟรมตก) |

### 4.7 Vignette
| field | ค่า |
|---|---|
| Color | **#070A0D** |
| Intensity | **0.44** |
| Smoothness | **0.42** |
| Rounded | off |

### 4.8 Film Grain
| field | ค่า |
|---|---|
| Type | Medium 3 |
| Intensity | **0.20** |
| Response | **0.8** |

### 4.9 (optional เดสก์ท็อป) Chromatic Aberration `0.08` · Lens Distortion `-0.05` — ปิดบน WebGL

> **Fable 5 note:** สร้าง Volume + Profile นี้ด้วย editor script §10 (idempotent) หรือมือก็ได้ แต่ค่าต้องตรงตารางเป๊ะ ถ้าลุคเพี้ยน ให้ไล่ทีละ override (ปิด Split Toning ก่อนถ้าสีจัดไป)

**✅ Verify STEP 2:** เข้า Play → ภาพต้อง desaturate + อุ่น-เย็นแยกโทน + มุมมืด + หน้าต่าง/glow เริ่มฟุ้ง **ทันที** (นี่คือ ~50% ของลุค). ยังสว่างไปเพราะ global light ยัง 1.15 → ไป STEP 3

---

## 5) STEP 3 — ปรับ Light2D ที่มีอยู่ + เพิ่มแสง local (twilight + warmth)

### 5.1 แก้ `Global Light 2D` (มีอยู่แล้ว)
| field | จาก | → เป็น |
|---|---|---|
| Color | (1, 1, 1) ขาว | **#5A6B78** (teal-grey เย็น) |
| Intensity | **1.15** | **0.72** |
> นี่คือการเปลี่ยน "เที่ยงวัน" → "รุ่งสาง" ในสวิตช์เดียว

### 5.2 เพิ่ม `Sun Key Light 2D` (Global light ตัวใหม่ — แทน sun warmth)
- Light Type **Global**, Blend Style index 0
- Color **#FFB765** (ส้มอุ่น), Intensity **0.38**
> Global light 2 ตัวจะ blend กัน = ฐานเย็น + คีย์อุ่น → ได้ ambient แบบ orange-teal ตรงกับ grading

### 5.3 `Fill Light 2D` ×2 (มีอยู่) — ลด intensity ~30% (เช่น 1.0 → 0.7) กันสว่างเกิน หรือปรับ color ให้เย็นตาม §5.1

### 5.4 หน้าต่างอาคารเรืองแสง — `Window Ember Light`
- Point Light2D ที่อาคารหลัก (โรงไฟฟ้า/reactor/ที่อยู่อาศัยที่มีไฟ): Color **#FFB24D**, Intensity **1.1**, Outer Radius **1.8**, Inner **0.4**, Falloff **0.6**
- ผูกเป็นลูกของ prefab อาคาร หรือ spawn ผ่าน `BuildingVisualSpawner` (ดู §8) เฉพาะอาคาร "มีไฟ" (`SESSION_2026-06-17` มี logic dim อาคารไม่มีไฟอยู่แล้ว → ผูก light on/off ตาม power state เดียวกัน)

### 5.5 เทียนรอบอนุสรณ์ (memorial) — `Candle Light` + flicker
- Point Light2D เล็ก: Color **#FF8A3A**, Intensity **0.85**, Outer Radius **0.9**
- ใส่สคริปต์ flicker (full file §7)

**✅ Verify STEP 3:** ฉากเป็น twilight เย็น + หลุมแสงอุ่นที่หน้าต่าง/เทียน + reactor glow เด่นขึ้น. เทียบรูป 2 ควร "ใกล้" แล้ว (~70%). **ถ้ามีชิ้นไหนยังสว่างโพลนไม่ยอมมืด = มันเป็น Sprite-Unlit → เปลี่ยน material เป็น Sprite-Lit-Default** (ดู §2)

---

## 6) STEP 4 — Ground / Tile ให้มืด-หม่น (checkerboard เขียวต้องจม)

grading + global light ช่วยได้ระดับหนึ่ง แต่เขียวอิ่มมากอาจยังโผล่ ทำ 2 ทางเลือก (ทำ A ก่อน เร็ว+reversible):

**ทางเลือก A (เร็ว — tint):** ตั้ง **Tilemap `Color`** ของเลเยอร์ "Ground" เป็น **#7C7A5E** (olive-grey, multiply) → กดเขียวสดให้เป็นดินหม่น. ปรับ `IsoGroundPainter` ถ้ามันเซ็ตสี tile ตอน spawn ให้ใช้ tint นี้ (อย่าแก้ math การวาง)

**ทางเลือก B (คุณภาพเต็ม — ภายหลัง):** เปลี่ยน tile sprite เป็นชุด "cracked pavement / rubble / dead grass" โทนเทา-น้ำตาล (asset §12). import ตาม convention `BuildingArtSetup.ConfigureImporter` (Point filter, PPU ตาม tile เดิม)

> ขอบ grid (ดินน้ำตาล→ดำ ใน Before): ให้ **หมอก §7.3 + backdrop §10.5** กลืนขอบ อย่าปล่อยดำโพลน

**✅ Verify STEP 4:** พื้นไม่ "เขียวสด" อีก · checkerboard ยังเห็นจางๆ ได้ (grid อ่านออกเพื่อ gameplay) แต่ไม่แย่งซีน

---

## 7) STEP 5 — Atmosphere: หมอก + Particles + flicker (full-file scripts)

### 7.1 `Assets/Scripts/Rendering/LightFlicker.cs` (ใหม่ — เทียน/ไฟ)
```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind
{
    // Cheap deterministic-ish flicker for candle / fire Light2D.
    // Attach to a GameObject that has a Light2D (point light).
    [RequireComponent(typeof(Light2D))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 0.85f;
        [SerializeField] private float amplitude = 0.18f;   // how much it dips
        [SerializeField] private float speed = 9f;          // flicker frequency
        [SerializeField] private float smooth = 12f;        // response smoothing

        private Light2D _light;
        private float _seed;
        private float _current;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _seed = Random.value * 100f;   // per-instance phase so candles don't sync
            _current = baseIntensity;
        }

        private void Update()
        {
            // Perlin noise gives organic, non-repeating flicker
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            float target = baseIntensity - amplitude * (1f - n);
            _current = Mathf.Lerp(_current, target, Time.deltaTime * smooth);
            _light.intensity = _current;
        }
    }
}
```

### 7.2 `Assets/Scripts/Rendering/ChimneySmoke` — ParticleSystem params (ตั้งใน setup)
- Shape: Cone แคบ (angle 8°) ที่ปากปล่อง · Start Lifetime **4–6s** · Start Speed **0.4** · Start Size **1.2→3.0** (grow) · Start Color **#3A3A3E α160** → over lifetime fade α→0 + จางเป็น **#6B6E74**
- Emission rate **6/s** · Simulation Space **World** · Gravity **-0.05** (ลอยขึ้น) · Noise ✓ (strength 0.3, freq 0.4) ให้ควันบิด
- Sorting Layer **"Buildings"**, order สูงกว่าปล่อง · material additive-soft หรือ alpha-blended smoke puff

### 7.3 หมอก/Atmosphere overlay — `Assets/Scripts/Rendering/FogLayer` (2 ชั้น)
- **Ground haze:** sprite นุ่มกว้าง (soft gradient, ขาว-เทา α ต่ำ) วางคลุมขอบฉากด้านไกล, Sorting Layer **"Buildings"** order รองจากอาคารไกล, สี **#8A94A0 α ~70**, scroll ช้าๆ (UV pan 0.01/s)
- **Depth fog band:** แถบ gradient horizontal ที่ "เส้นขอบฟ้า" ให้ตึกไกลจางเข้าฟ้า (atmospheric perspective) — สี = สีฟ้า backdrop
- Particle "floating ash/dust": จุดเล็ก 20–30 ตัว, drift ช้า, α ต่ำ, additive จางๆ, World space

### 7.4 Embers รอบไฟ/เทียน
- ParticleSystem จุดส้ม **#FFB24D** ลอยขึ้น, size 0.03–0.06, lifetime 2s, emission 3/s, additive, Noise เบา

**✅ Verify STEP 5:** เห็นควันลอยจากปล่อง · เทียนวูบไหว · หมอกจางที่ขอบทำให้ฉากมี depth · เถ้าลอยในอากาศ

---

## 8) STEP 6 — Blob / Contact shadows (กัน "วัตถุลอย")

- เพิ่ม child sprite **เงารูปวงรีนุ่ม** (soft ellipse, ดำ α ~120) ใต้ base ของอาคาร/คน/decor
- Sorting: layer **"Buildings"** แต่ order **ต่ำกว่า** ตัววัตถุ (ใช้ `SortTier` ต่ำกว่า 1 tier) หรือ layer "Ground" order สูงสุด → เงาอยู่ใต้ pivot
- ผูกใน `BuildingVisualSpawner` / `WorkerVisualSpawner` / `DecorSpawner` ตอน spawn: instantiate `blobShadow` เป็นลูก, scale ตามขนาด footprint
- material เงา = **Sprite-Unlit** (เงาไม่ควรโดนแสงเปลี่ยนสี) — ข้อยกเว้นเดียวที่ใช้ Unlit โดยตั้งใจ

**✅ Verify STEP 6:** อาคาร/คน "นั่ง" บนพื้น ไม่ลอย · เงาไม่บังของข้างหลังผิด iso

---

## 9) UI — ตรวจว่าไม่โดน grading (ควรผ่านอยู่แล้ว)

- Canvas ทั้งหมด = **Screen-Space-Overlay** → render หลัง post → **ไม่โดน vignette/grading** (ยืนยันจาก lastplan.md: ScaleWithScreenSize ref 1536×864). **ไม่ต้องแก้อะไร**
- ถ้าเจอ Canvas ใดเป็น Screen-Space-Camera → มันจะโดนหมอก/มืด → ย้ายเป็น Overlay หรือ exclude
- ถ้าต้องการให้ HUD "เข้าธีม" เพิ่ม (optional): ปรับสี panel/overlay ใน UI เอง ไม่ใช่ผ่าน post

---

## 10) STEP 7 — Backdrop diorama: ท้องฟ้า + god rays + skyline (art lift หนักสุด → ทำท้าย)

นี่คือส่วนที่ทำให้ "เหมือนรูป 2 เป๊ะ" แต่ต้องการ art asset → แยกเป็นเฟส C

### 10.1 โครง layer (หลัง→หน้า) — สร้าง Sorting Layers เพิ่มถ้ายังไม่มี
```
[Sky]        ← ท้องฟ้า gradient + ดวงอาทิตย์ break (order ต่ำสุด, ไกลสุด)
[SkyRays]    ← god-ray light shafts (additive sprite) พาดจากดวงอาทิตย์
[Skyline]    ← เงาตึกร้าง silhouette (2–3 ชั้น parallax, ชั้นไกล = จาง/ฟ้าลง)
[FarFog]     ← หมอกคั่นระหว่าง skyline กับ playfield
Ground / Buildings / Units ... (ของเดิม ห้ามแตะ order)
[NearDust]   ← ฝุ่น/เถ้า หน้า playfield
(post: vignette/grain คลุมทั้งหมด)
```
> เพิ่ม 3 sorting layer ใหม่ **ก่อน** "Ground": `Sky`, `SkyRays`, `Skyline`, `FarFog` (แก้ `ProjectSettings/TagManager.asset` — เพิ่มท้าย ห้ามแทรกกลางของเดิม)

### 10.2 Sky
- Sprite gradient ฟ้าเทา-อมส้มที่ขอบฟ้า (เช่น บน **#3A4653** → กลาง **#6E6A5E** → ขอบฟ้า **#D9A867** รอบดวงอาทิตย์)
- วาง billboard ใหญ่คลุมเต็ม view, ตรึงกับกล้อง (parallax factor 0 = ไม่เลื่อนตามหรือเลื่อนช้ามาก)

### 10.3 God rays
- Additive sprite "light shafts" 2–3 แฉกพาดจากตำแหน่งดวงอาทิตย์ · animate α ขึ้นลงช้า (breathing 0.6↔0.9 ทุก ~6s) · tint **#FFD9A0**
- (ทางเลือกคุณภาพ: radial-blur shader แต่ sprite additive พอสำหรับ 2D + WebGL)

### 10.4 Skyline silhouette
- เงาตึกร้าง (ตึกสูงพัง, ปล่อง, เครน) เป็น silhouette เข้ม · 2 ชั้น parallax: ชั้นไกลจางกว่า+ฟ้าอมกว่า (atmospheric perspective), ชั้นใกล้เข้มกว่า
- มี smoke plume ไกลๆ 1–2 จุด (particle เบา)

### 10.5 กลืนขอบ playfield → backdrop
- ขอบ grid (เดิมเป็นดินน้ำตาล/ดำ) ให้ **FarFog** + vignette กลืน — อย่าให้เห็นเส้นตัดคม
- ถ้าจำเป็น ลด apron/decor ขอบนอกให้จางลงไปทางขอบ (alpha ramp)

**✅ Verify STEP 7:** ด้านบนจอเป็นท้องฟ้า dramatic + god rays · ขอบฟ้ามีเงาเมืองพัง · playfield กลืนเข้าฉากหลังด้วยหมอก = ตรงรูป 2

---

## 11) ลำดับทำ (เฟส — Fable 5 ทำตามนี้เป๊ะ) + acceptance

| เฟส | ทำ | ต้นทุน art | ผลลัพธ์ที่ต้องเห็น |
|---|---|---|---|
| **A — Grade & Light (โค้ด/setup ล้วน ไม่ต้องมี art ใหม่)** | STEP 1→2→3 (+4A tint) | 0 | **~70% ของลุค**: desaturate + orange-teal + twilight + bloom หน้าต่าง + vignette |
| **B — Atmosphere (asset เบา)** | STEP 5 (หมอก/ควัน/particle/flicker) + STEP 6 (blob shadow) | เบา (soft sprites, particle) | ฉากมีชีวิต+depth: ควัน, เถ้า, เทียนไหว, เงาใต้วัตถุ |
| **C — Diorama (asset หนัก)** | STEP 7 (sky/god rays/skyline) + STEP 4B (dark tiles) | หนัก (ต้องวาด/หา asset) | "เหมือนรูป 2" เต็มตัว |

**เริ่มที่เฟส A เสมอ** — ถ้าจบ A แล้วไม่เห็นความต่างชัด แปลว่า Volume/Camera/Light ตั้งผิด → หยุดดีบัก อย่าทำ B/C ทับ

**Acceptance criteria รวม (ตรวจก่อนบอกว่าเสร็จ):**
1. compile `error CS = 0` (ปิด Unity → batch)
2. เทียบ screenshot กับรูป 2: โทน orange-teal ✓, มุมมืด ✓, หน้าต่าง/เทียนเรือง ✓
3. **UI/ตัวเลขทุกตัวยังอ่านออก** (Hope, Knowledge, resource bars, ปุ่มอาคาร, task panel)
4. **คน/อาคารยังแยกออกจากพื้นได้** (ไม่มืดจนกลืน) — worker ผมเหลืองยังเห็น
5. Play D1→หลายวัน ไม่มี null/error, ไม่มีชิ้น "สว่างโพลนหลุดธีม" (= ยังเป็น Unlit)
6. WebGL build ผ่าน + เฟรมรับได้ (ถ้าตก → ลด Bloom HQ filtering, ปิด grain/CA)
7. setup รันซ้ำไม่สร้างซ้อน (idempotent)

---

## 12) STEP 8 — Editor automation (idempotent, ลงใน RunAllSetups)

สร้าง `Assets/Editor/AshfallRenderSetup.cs`:
- `[MenuItem("NuclearReMind/Setup Ashfall Render")]`
- idempotent: หา/ลบ `__ASHFALL_Volume`, `Sun Key Light 2D`, fog/particle roots เก่าก่อนสร้างใหม่
- สร้าง `Ashfall_Dawn.asset` VolumeProfile + ใส่ override ทุกตัวตาม §4 ด้วยโค้ด (`VolumeProfile.Add<Bloom>()` ฯลฯ) แล้ว set ค่า + `overrideState = true` ทุก field ที่กำหนด
- แก้ Global Light 2D (§5.1), เพิ่ม Sun Key (§5.2), set Camera post ON (§3.2)
- สร้าง fog/particle/backdrop roots (เฟส B/C) ถ้ามี asset
- ท้ายฟังก์ชัน: `EditorSceneManager.MarkSceneDirty` + `SaveScene`, `AssetDatabase.SaveAssets`
- เพิ่มชื่อเมนูใน `Assets/Editor/RunAllSetups.cs` `MenuOrder` (วางท้ายสุด — visual layer ทำหลัง gameplay setup)

> asset ที่ต้องมีก่อน (เฟส C): sky/godray/skyline/fog/smoke/ember/blob sprites → เก็บใน `Assets/Sprites/Backdrop/` + `Assets/Sprites/VFX/` (world art ไม่ใช่ Resources), import Point filter + alphaIsTransparency ตาม `BuildingArtSetup.ConfigureImporter`

---

## 13) Art shopping list (สำหรับเฟส B/C)

| asset | ใช้ที่ | สเปค |
|---|---|---|
| Sky gradient + sun break | §10.2 | 2048×1024, ฟ้าเทา→ส้มขอบฟ้า, PNG |
| God-ray shafts | §10.3 | additive, soft, โปร่ง, 2–3 แฉก |
| Skyline silhouette ×2 | §10.4 | ตึกร้าง PNG โปร่ง, ชั้นไกล/ใกล้ |
| Ground haze / fog | §7.3 | soft radial/gradient, α ต่ำ, tileable |
| Smoke puff | §7.2 | soft round, greyscale |
| Ember / dust dot | §7.4 | จุดนุ่ม 8–16px |
| Blob shadow | §8 | soft ellipse, ดำโปร่ง |
| Window glow overlay | §5.4 | additive จุดเรืองส้ม (ถ้าอยากได้ core เรืองชัดกว่าแค่ light) |
| (เฟส 4B) dark ground tiles | §6B | cracked pavement/rubble/dead-grass ชุด iso |

---

## 14) Pitfalls (ห้ามพลาด — เจอบ่อย)

1. **ตั้ง global light มืดแล้วบางชิ้นยังสว่างโพลน** → ชิ้นนั้นเป็น `Sprite-Unlit-Default` เปลี่ยนเป็น **Sprite-Lit-Default** (§2)
2. **Bloom ฟุ้งทั้งจอ / เละ** → เพราะ Grading Mode ยัง LDR (§3.1) หรือ threshold ต่ำไป → HDR + threshold 0.9
3. **UI มืด/หมอกจับ** → มี Canvas เป็น Screen-Space-Camera → เปลี่ยนเป็น Overlay (§9)
4. **มืดจนเล่นไม่ได้** → global intensity อย่าต่ำกว่า 0.65 · เพิ่ม Sun Key/Fill · legibility ชนะ mood เสมอ
5. **สีเขียว/เหลืองยังจัด** → Saturation ยังไม่พอ (ลองถึง -50) + Tilemap tint (§6A)
6. **WebGL เฟรมตก** → ปิด Bloom HQ filtering, ปิด Film Grain/Chromatic, ลด particle count
7. **เงา blob บังของหลัง iso ผิด** → เงาต้อง order ต่ำกว่าตัววัตถุ, ไม่เกิน 1 tier (§8)
8. **แก้ sorting layer แล้วของเดิมเพี้ยน** → เพิ่ม layer ใหม่ **ต่อท้าย** เท่านั้น ห้ามแทรกกลาง (จะ shift order ของเดิมทั้งหมด)
9. **เพิ่ม field ใน SaveData** → **ไม่มีในงานนี้เลย** (visual layer ไม่ persist) ถ้าเผลอเพิ่ม = ผิด scope

---

## 15) TL;DR สำหรับ Fable 5

> ทำ **เฟส A ก่อน** = 3 อย่าง: (1) URP Grading Mode → HDR + Camera Post ON, (2) สร้าง Global Volume `Ashfall_Dawn` ใส่ override ตาม §4 เป๊ะ, (3) Global Light 2D: ขาว1.15 → **#5A6B78 @ 0.72** + เพิ่ม Sun Key อุ่น. แค่นี้ได้ ~70% ของรูป 2 โดยไม่ต้องมี art ใหม่.
> แล้วค่อย **เฟส B** (หมอก/ควัน/เทียนไหว/เงา) → **เฟส C** (ท้องฟ้า+god rays+skyline) ซึ่งต้องการ art asset.
> ทุก setup ทำผ่าน `AshfallRenderSetup.cs` (idempotent, ลงใน RunAllSetups). **ห้ามแตะ gameplay/sorting math/SaveData. UI ห้ามอ่านไม่ออก.**
