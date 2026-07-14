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
        /// worker ที่ "ตัวสไปรต์ทับโซนฐาน" ของอาคาร และยืน "หน้า" ฐาน (feetY ต่ำกว่ากึ่งกลางฐาน) → ต้องวาดเหนืออาคารนั้น
        /// (แก้ปัญหา worker เดินหน้าอาคารแล้วถูกบังหมด — Unit tier ต่ำกว่า Building เสมอ) · อยู่หลังฐาน = ปล่อยให้อาคารบังตามเดิม
        /// คืน order สูงสุดที่ควรยก worker ขึ้นไป (เหนือทั้งฐานและยอดของอาคารที่ทับ) · false = ไม่ทับอาคารไหน
        /// เรียกทุกเฟรมจาก WorkerView.UpdateSorting (อาคารนิ่ง — bounds ฐานคงที่ ไม่ต้อง SyncTransforms ที่นี่)
        /// </summary>
        public static bool TryGetRaiseOrder(Bounds workerBounds, float feetY, out int order)
        {
            order = int.MinValue;
            bool any = false;
            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (b == null || b._baseCollider == null) continue;
                Bounds bb = b._baseCollider.bounds;
                if (!workerBounds.Intersects(bb)) continue;
                if (feetY >= bb.center.y) continue; // worker อยู่หลังฐาน → อาคารบังตามปกติ
                int top = (b._overlay != null && b._overlay.enabled) ? b._overlay.sortingOrder : b._isoOrder;
                int cand = Mathf.Max(b._isoOrder, top) + 1;
                if (cand > order) { order = cand; any = true; }
            }
            return any;
        }
    }
}
