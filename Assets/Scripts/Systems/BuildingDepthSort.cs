using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// จัดลำดับความลึกอาคาร iso แบบ "ส่วนบน/ส่วนฐาน" (ตามดีไซน์ผู้ใช้) — แก้ปัญหาสไปรต์อาคารซ้อนกันผิดลำดับ
    ///
    /// แต่ละอาคารมี BoxCollider2D 2 โซน: baseCollider (ฐาน) + topCollider (บน) + overlay renderer ของ "ส่วนบน"
    ///   rule 1: ถ้า topCollider ของ A ทับ baseCollider ของ B → ยก "ส่วนบนของ A" ให้วาดเหนือ "ฐาน" B
    ///   rule 2: ถ้า topCollider ของ A ทับ "ทั้ง base และ top" ของ B (A คร่อม B หมด = A อยู่หน้า) → ยอด A ต้องเหนือ "ยอด" B ด้วย
    ///           (A อยู่หน้าเสมอ · เช่น top ของ core tower ยาวคร่อมโรงน้ำข้างหลัง → core tower บังโรงน้ำทั้งหลัง)
    ///
    /// overlay = สำเนาแถบบนของสไปรต์ (sub-rect) · ปกติปิดอยู่ · ยก sortingOrder เหนืออาคารที่บังเฉพาะตอนจำเป็น
    /// (main SpriteRenderer เต็มใบไม่ถูกแตะ → upgrade สลับ sprite / click collider / เงา ทำงานเหมือนเดิม)
    /// recompute เฉพาะตอนวาง/ทุบ/อัปเกรด/โหลด (อาคารนิ่ง ไม่ต้องเช็คทุกเฟรม) · ไม่ยุ่ง grid/placement/worker เลย
    ///
    /// ★ ออกแบบให้ต่อ rule เพิ่มได้ (rule 2/3) โดยไม่ต้องรื้อ: เพิ่มเงื่อนไขใน RecomputeAll
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingDepthSort : MonoBehaviour
    {
        private static readonly List<BuildingDepthSort> All = new List<BuildingDepthSort>();

        private SpriteRenderer _overlay;      // สำเนา "แถบบน" — ยกเหนืออาคารที่บัง
        private BoxCollider2D _baseCollider;  // โซนฐาน (trigger)
        private BoxCollider2D _topCollider;   // โซนบน (trigger)

        // ★ กล่องชุดที่สอง สำหรับ "worker ทับอาคาร" โดยเฉพาะ — แยกจาก base/top ด้านบนที่ตัดสิน "อาคารทับอาคาร"
        //   เดิม TryGetRaiseOrder ยืม _baseCollider มาใช้ กล่องเดียวจึงต้องรับสองหน้าที่ที่ต้องการรูปทรงคนละแบบ
        //   จูนให้อาคารเรียงถูก worker ก็ลอย · จูนให้ worker ถูก อาคารก็เรียงผิด
        private BoxCollider2D _workerBase;    // แถบพื้นเตี้ย ๆ — เท้าต่ำกว่ากึ่งกลางแถบนี้ = ยืนหน้าอาคาร
        private BoxCollider2D _workerTop;     // โซน "ยืนตรงนี้ = อยู่หลังอาคาร" → ห้ามยกขึ้นมา

        /// <summary>สัดส่วนความสูงเริ่มต้นของ worker base (แถบพื้น) เมื่อยังไม่ได้จูนเอง — เตี้ยไว้ก่อน กัน worker ดูลอย</summary>
        public const float DefaultWorkerBaseFraction = 0.08f;
        private int _isoOrder;                // sortingOrder ฐาน (iso depth) ของ main
        private string _sortingLayer = "Buildings";

        [Tooltip("BuildingData ของอาคารนี้ — ใช้ override กล่องฐาน (baseColliderSize/Offset) ถ้า overrideBaseCollider = true")]
        public BuildingData data;

        /// <summary>สร้างส่วนบน/ฐาน + collider จาก main sprite (เรียกตอน spawn)</summary>
        public void Setup(SpriteRenderer main, int isoOrder, string sortingLayer, float baseFraction, BuildingData buildingData)
        {
            _isoOrder = isoOrder;
            data = buildingData;
            if (!string.IsNullOrEmpty(sortingLayer)) _sortingLayer = sortingLayer;
            EnsureParts();
            Rebuild(main != null ? main.sprite : null, baseFraction);
        }

        private void EnsureParts()
        {
            if (_overlay == null)
            {
                var topGO = new GameObject("TopOverlay");
                topGO.transform.SetParent(transform, false);
                _overlay = topGO.AddComponent<SpriteRenderer>();
                _overlay.sortingLayerName = _sortingLayer;
                _overlay.enabled = false;
            }
            // colliders อยู่บน GO อาคารเอง — hit ใด ๆ ยัง GetComponent<BuildingClickTarget> บน GO เดิมได้ (ไม่รบกวนคลิก)
            if (_baseCollider == null)
            {
                _baseCollider = gameObject.AddComponent<BoxCollider2D>();
                _baseCollider.isTrigger = true;
            }
            if (_topCollider == null)
            {
                _topCollider = gameObject.AddComponent<BoxCollider2D>();
                _topCollider.isTrigger = true;
            }
            if (_workerBase == null)
            {
                _workerBase = gameObject.AddComponent<BoxCollider2D>();
                _workerBase.isTrigger = true;
            }
            if (_workerTop == null)
            {
                _workerTop = gameObject.AddComponent<BoxCollider2D>();
                _workerTop.isTrigger = true;
            }
        }

        /// <summary>สร้างแถบบน + วาง collider ใหม่ตามสไปรต์ปัจจุบัน (เรียกซ้ำได้ตอนอัปเกรดสลับ sprite)</summary>
        public void Rebuild(Sprite fullSprite, float baseFraction)
        {
            if (fullSprite == null) return;
            EnsureParts();

            Bounds b = fullSprite.bounds;            // local space (คิด pivot+ppu แล้ว · ยังไม่คูณ transform scale)
            float ppu = fullSprite.pixelsPerUnit;
            int rectH = Mathf.Max(2, Mathf.RoundToInt(fullSprite.rect.height));

            float baseTopY;   // Y (local) ของ "ขอบบนโซนฐาน" = จุดตัด base/top → คุม top collider + overlay ให้สอดคล้อง
            if (data != null && data.overrideBaseCollider && data.baseColliderSize != Vector2.zero)
            {
                // กล่องฐานกำหนดเอง (ลากด้วย Base Collider Editor) — พิกัด local สไปรต์
                _baseCollider.size = data.baseColliderSize;
                _baseCollider.offset = data.baseColliderOffset;
                baseTopY = data.baseColliderOffset.y + data.baseColliderSize.y * 0.5f;
            }
            else
            {
                // อัตโนมัติ: ฐาน = แถบล่าง baseFraction ของสไปรต์ เต็มความกว้าง
                float frac = Mathf.Clamp01(baseFraction);
                int baseHpx0 = Mathf.Clamp(Mathf.RoundToInt(fullSprite.rect.height * frac), 1, rectH - 1);
                float baseWorldH = baseHpx0 / ppu;
                _baseCollider.size = new Vector2(b.size.x, baseWorldH);
                _baseCollider.offset = new Vector2(b.center.x, b.min.y + baseWorldH * 0.5f);
                baseTopY = b.min.y + baseWorldH;
            }

            // top collider = ส่วนเหนือขอบบนโซนฐาน (สำหรับ rule 2 · อาคารคร่อมกัน)
            float topWorldH = Mathf.Max(0f, b.max.y - baseTopY);
            _topCollider.size = new Vector2(b.size.x, topWorldH);
            _topCollider.offset = new Vector2(b.center.x, baseTopY + topWorldH * 0.5f);

            // ── กล่องชุด worker (จูนแยกจากชุดอาคาร) ──
            float wBaseTopY;
            if (data != null && data.overrideWorkerCollider && data.workerBaseColliderSize != Vector2.zero)
            {
                _workerBase.size = data.workerBaseColliderSize;
                _workerBase.offset = data.workerBaseColliderOffset;
                wBaseTopY = data.workerBaseColliderOffset.y + data.workerBaseColliderSize.y * 0.5f;
            }
            else
            {
                // ยังไม่จูน → แถบพื้นเตี้ย ๆ เต็มความกว้าง (อย่างน้อย 1px เพื่อไม่ให้กล่องแบนเป็นศูนย์)
                float wh = Mathf.Max(1f / ppu, b.size.y * DefaultWorkerBaseFraction);
                _workerBase.size = new Vector2(b.size.x, wh);
                _workerBase.offset = new Vector2(b.center.x, b.min.y + wh * 0.5f);
                wBaseTopY = b.min.y + wh;
            }

            if (data != null && data.overrideWorkerCollider && data.workerTopColliderSize != Vector2.zero)
            {
                _workerTop.size = data.workerTopColliderSize;
                _workerTop.offset = data.workerTopColliderOffset;
            }
            else
            {
                float wth = Mathf.Max(0f, b.max.y - wBaseTopY);
                _workerTop.size = new Vector2(b.size.x, wth);
                _workerTop.offset = new Vector2(b.center.x, wBaseTopY + wth * 0.5f);
            }

            // overlay = แถบบนของ texture (sub-rect) เหนือขอบบนโซนฐาน · pivot มุมล่างซ้าย → วางทับแถบบนของ main พอดี
            int baseHpx = Mathf.Clamp(Mathf.RoundToInt((baseTopY - b.min.y) * ppu), 1, rectH - 1);
            Rect r = fullSprite.rect;
            var topRect = new Rect(r.x, r.y + baseHpx, r.width, r.height - baseHpx);
            _overlay.sprite = Sprite.Create(fullSprite.texture, topRect, Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
            _overlay.transform.localPosition = new Vector3(b.min.x, baseTopY, 0f);
            _overlay.sortingLayerName = _sortingLayer;
            _overlay.sortingOrder = _isoOrder;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>
        /// คำนวณลำดับ overlay ใหม่ทั้งหมด — เรียกหลังวาง/ทุบ/อัปเกรด/โหลด
        /// rule 1: topCollider(A) ทับ baseCollider(B) → ยก overlay(A) เหนือ "ฐาน" B (order = B.iso+1)
        /// rule 2: topCollider(A) ทับ "ทั้ง base และ top" ของ B → ยก overlay(A) เหนือ "ยอด(overlay)" B ด้วย (A หน้าเสมอ)
        /// relaxation ดัน order ขึ้นจนคงที่ (รองรับ chain A→B→C) · overlay เปิดเฉพาะตอนถูกยกเหนือ base ตัวเอง
        /// </summary>
        public static void RecomputeAll()
        {
            Physics2D.SyncTransforms(); // ให้ bounds ของ collider ที่เพิ่งสร้าง/ย้ายอัปเดตก่อนอ่าน

            int n = All.Count;
            if (n == 0) return;

            // order[i] = sortingOrder ที่ overlay อาคาร i ต้องอยู่ (เริ่มที่ base order ของตัวเอง แล้วค่อยดันขึ้นตาม rule)
            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = All[i] != null ? All[i]._isoOrder : int.MinValue;

            // relaxation: วนดัน order ขึ้นจนไม่มีอะไรเปลี่ยน (cap = n รอบ พอสำหรับ chain ยาวสุด + กันลูปไม่จบกรณี overlap วนกัน)
            for (int iter = 0; iter < n; iter++)
            {
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    var a = All[i];
                    if (a == null || a._topCollider == null) continue;
                    Bounds aTop = a._topCollider.bounds;

                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        var b = All[j];
                        if (b == null || b._baseCollider == null) continue;

                        // rule 1: ยอด A ทับ "ฐาน" B → ยอด A ต้องเหนือฐาน B
                        if (!aTop.Intersects(b._baseCollider.bounds)) continue;
                        int want = b._isoOrder + 1;

                        // rule 2: ยอด A ทับ "ยอด" B ด้วย (A คร่อม B ทั้งฐานและยอด = A อยู่หน้า) → ยอด A ต้องเหนือ "ยอด(overlay)" B
                        if (b._topCollider != null && aTop.Intersects(b._topCollider.bounds))
                            want = Mathf.Max(want, order[j] + 1);

                        if (want > order[i])
                        {
                            order[i] = want;
                            changed = true;
                        }
                    }
                }
                if (!changed) break;
            }

            // apply — overlay เปิดเฉพาะเมื่อถูกยกเหนือ base ตัวเอง (ไม่งั้นปล่อย main SR วาดเต็มใบพอ)
            for (int i = 0; i < n; i++)
            {
                var a = All[i];
                if (a == null || a._overlay == null) continue;
                a._overlay.sortingOrder = order[i];
                a._overlay.enabled = order[i] > a._isoOrder;
            }
        }

        /// <summary>
        /// ตัดสิน sortingOrder สุดท้ายของ worker เทียบกับอาคารทุกหลัง (เรียกทุกเฟรมจาก WorkerView.UpdateSorting)
        ///
        /// ★ ทำไมต้อง "กดลง" ไม่ใช่แค่ "งดยก":
        ///   พื้นถูกวาง y = (col+row)·tileHeight/2 → ช่องที่ไกลออกไปอยู่ "สูงขึ้นบนจอ"
        ///   แต่ SortOrder = (col+row)·SortScale → ช่องที่ไกลออกไป "วาดทับ" ของที่ใกล้กว่า
        ///   สองอย่างนี้สวนกัน ผลคือคนงานที่ยืนหลังอาคาร (สูงบนจอ) ถูกดันมาข้างหน้าตั้งแต่ต้น
        ///   โดยไม่ต้องมีกฎอะไรมาช่วยเลย → กฎที่บอกแค่ "ไม่ต้องยก" จึงไม่มีผลใด ๆ ทั้งสิ้น
        ///   กล่อง top ต้องกดคนงานลงไปใต้อาคารจริง ๆ ถึงจะเห็นผล
        ///
        ///   (ต้นเหตุจริงอยู่ที่เครื่องหมายของแกนความลึกในสูตรกลาง แต่สูตรนั้นถูกใช้โดยอาคาร/แร่/ของตกแต่ง
        ///    และ overlay ของอาคารก็ถูกจูนทับความเพี้ยนนั้นมาแล้ว — แก้ตรงนี้คือแก้เฉพาะผู้ใช้ที่ยังไม่มีตัวกลบ)
        ///
        /// ลำดับการบังคับ: ยกก่อน แล้วค่อยกด — "ถูกบัง" สำคัญกว่า "ได้ทับ" เวลาสองเงื่อนไขขัดกัน
        /// (ยืนหลังตึก A แต่หน้าตึก B พร้อมกัน) เพราะคนโผล่ทะลุตึกสะดุดตากว่าคนที่ถูกบังเกินจริงหนึ่งชั้น
        /// </summary>
        public static int ResolveWorkerOrder(Bounds workerBounds, Vector3 feet, int naturalOrder)
        {
            int raise = naturalOrder;   // สูงสุดที่ควรถูกยก (ยืนหน้าอาคาร)
            int ceiling = int.MaxValue; // ต่ำสุดที่ต้องไม่เกิน (ยืนหลังอาคาร)

            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (b == null || b._workerBase == null) continue;

                // ยืนในโซนบน = อยู่หลังอาคารหลังนี้ → ต้องอยู่ใต้มัน
                // ★ เช็คด้วย "จุดที่เท้าเหยียบ" ไม่ใช่กรอบสไปรต์ทั้งตัว — คนที่ยืนหน้าอาคารสูง หัวก็ยังกิน
                //   พื้นที่ Y เดียวกับครึ่งบนของอาคาร ถ้าเช็คด้วยกรอบทั้งตัวจะโดนกดลงกันหมดทุกคน
                if (b._workerTop != null && ContainsXY(b._workerTop.bounds, feet))
                {
                    ceiling = Mathf.Min(ceiling, b._isoOrder - 1);
                    continue; // หลังเดียวกันจะเป็นทั้ง "ยืนหลัง" และ "ยืนหน้า" ไม่ได้
                }

                Bounds bb = b._workerBase.bounds;
                if (!workerBounds.Intersects(bb)) continue;
                if (feet.y >= bb.center.y) continue; // อยู่หลังฐาน → อาคารบังตามเดิม
                int top = (b._overlay != null && b._overlay.enabled) ? b._overlay.sortingOrder : b._isoOrder;
                raise = Mathf.Max(raise, Mathf.Max(b._isoOrder, top) + 1);
            }

            return ceiling == int.MaxValue ? raise : Mathf.Min(raise, ceiling);
        }

        // Bounds.Contains ต้องให้ z อยู่ในช่วงด้วย แต่ collider 2D มีความหนา z = 0 → เทียบเฉพาะ XY
        private static bool ContainsXY(Bounds b, Vector3 p)
            => p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;

        // ─────────────────────────────────────────
        //  Gizmos — เห็นกล่องจริงตอนเล่น
        // ─────────────────────────────────────────

        /// <summary>
        /// วาดกรอบ collider ทั้ง 4 ใบตอน Play (เปิด/ปิดจากเมนู NuclearReMind → Tools → Show Depth Boxes)
        ///
        /// จำเป็นเพราะกล่องพวกนี้ถูก "คำนวณตอนรันไทม์" จาก sprite bounds — ค่าที่เห็นใน BuildingData ไม่ได้
        /// บอกว่ากล่องไปโผล่ที่ไหนจริง ๆ บนจอ เวลา worker เรียงผิดจึงแยกไม่ออกว่าเป็นเพราะกล่องวางผิดที่
        /// หรือเพราะกฎการยกลำดับ — อันนี้ตอบได้ด้วยการมอง ไม่ต้องเดา
        /// </summary>
        public static bool DrawGizmos;

        private void OnDrawGizmos()
        {
            if (!DrawGizmos) return;
            Box(_baseCollider, new Color(0.30f, 1.00f, 0.40f)); // เขียว — อาคารทับอาคาร
            Box(_topCollider,  new Color(0.20f, 0.65f, 0.30f)); // เขียวเข้ม — ยอดอาคาร
            Box(_workerBase,   new Color(0.35f, 0.70f, 1.00f)); // ฟ้า — "ยืนหน้า" ของ worker
            Box(_workerTop,    new Color(1.00f, 0.60f, 0.25f)); // ส้ม — "ยืนหลัง" ของ worker
        }

        private static void Box(BoxCollider2D c, Color color)
        {
            if (c == null) return;
            Gizmos.color = color;
            var b = c.bounds;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, 0f));
        }
    }
}
