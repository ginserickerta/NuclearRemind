using System.Collections;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// เอฟเฟกต์อัพเกรดอาคาร (2D sprite) — เล่นตอน OnBuildingUpgraded ผ่าน [[BuildingVisualSpawner]]:
    ///   กระแทกสั่น → ฝุ่นระเบิด + flash ขาว → สลับ sprite เป็นเลเวลใหม่ "กลางฝุ่น" → เด้ง (squash/bounce)
    ///
    /// ปรับจากสคริปต์ต้นแบบ (3D model + NPC + ParticleSystem + Inspector) ให้เข้ากับระบบ runtime ของเกม:
    ///   • ทำงานกับ SpriteRenderer ตัวเดียวที่ BuildingVisualSpawner สร้าง (ไม่ใช่ GameObject model แยก)
    ///   • ฝุ่น/flash วาดเป็นสไปรต์กลมโปรซีเยอรัล cache ไว้ (ไม่ต้องลาก Inspector · ไม่ใช้ ParticleSystem)
    ///   • ไม่มี NPC ช่างก่อสร้าง (เพิ่มทีหลังได้) · สลับ sprite ตอนฝุ่นทึบสุดให้รอยต่อเนียน
    ///   • ใช้ unscaledDeltaTime — เล่นลื่นแม้อัพเกรดช่วง Planning ที่นาฬิกาเกมหยุด (§15)
    /// เรียกผ่าน static BuildingUpgradeEffect.Play(go, sr, newSprite)
    /// </summary>
    public class BuildingUpgradeEffect : MonoBehaviour
    {
        // ── จังหวะ (ปรับในโค้ด — เอฟเฟกต์ runtime ไม่ผ่าน Inspector) ──
        private const float ShakeDuration = 0.18f;  // กระแทกก่อนระเบิด (แทน NPC ตี)
        private const float ShakeAmount   = 0.06f;
        private const float DustPeakDelay = 0.12f;   // รอฝุ่นทึบก่อนสลับ sprite
        private const float BounceDuration = 0.34f;
        private const float BounceHeight   = 0.18f;
        private const int   DustCount      = 7;

        private static Sprite _dustSprite;   // พัฟกลมนุ่ม (ใช้ทั้งฝุ่น/flash · tint ตอน runtime)

        private SpriteRenderer _sr;
        private Sprite _newSprite;

        /// <summary>เล่นเอฟเฟกต์อัพเกรดบน target (ที่มี SpriteRenderer ตัวอาคาร) แล้วสลับเป็น newSprite กลางอนิเมชัน</summary>
        public static void Play(GameObject target, SpriteRenderer sr, Sprite newSprite)
        {
            if (target == null || sr == null) return;

            // กันซ้อน: กำลังเล่นอยู่แล้ว → สลับ sprite ทันที ไม่เริ่มใหม่ (อัพเกรดรัว ๆ ไม่ค้าง/ไม่ซ้อนเอฟเฟกต์)
            if (target.GetComponent<BuildingUpgradeEffect>() != null)
            {
                if (newSprite != null) sr.sprite = newSprite;
                return;
            }

            var fx = target.AddComponent<BuildingUpgradeEffect>();
            fx._sr = sr;
            fx._newSprite = newSprite;
            fx.StartCoroutine(fx.Run());
        }

        private IEnumerator Run()
        {
            Transform tf = transform;
            Vector3 baseScale = tf.localScale;
            Vector3 basePos = tf.position;

            // 1) กระแทก: สั่นแนวนอนหน่วงลง (ให้รู้สึกโดนทุบ — แทน NPC)
            float t = 0f;
            while (t < ShakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float damp = 1f - (t / ShakeDuration);
                float dx = Mathf.Sin(t * 90f) * ShakeAmount * damp;
                tf.position = basePos + new Vector3(dx, 0f, 0f);
                yield return null;
            }
            tf.position = basePos;

            // 2) (เอาเอฟเฟกต์ควัน/ฝุ่น+flash ออกตามคำขอ — เหลือ สั่น→สลับสไปรต์→เด้ง)
            //    ถ้าอยากเปิดกลับ: เรียก SpawnDustBurst(basePos, baseScale, layer, baseSort + 5);
            //    และ SpawnFlash(basePos, baseScale, layer, baseSort + 6); ตรงนี้

            // 3) หน่วงเล็กน้อยแล้วสลับ sprite เป็นเลเวลใหม่
            yield return WaitUnscaled(DustPeakDelay);
            if (_sr != null && _newSprite != null) _sr.sprite = _newSprite;

            // 4) เด้ง: squash → พุ่งขึ้น → กลับปกติ
            yield return Bounce(tf, baseScale, basePos);

            tf.position = basePos;
            tf.localScale = baseScale;
            Destroy(this);
        }

        private IEnumerator Bounce(Transform tf, Vector3 baseScale, Vector3 basePos)
        {
            Vector3 squash = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.86f, baseScale.z);

            float squashDur = BounceDuration * 0.28f;
            float t = 0f;
            while (t < squashDur)
            {
                t += Time.unscaledDeltaTime;
                tf.localScale = Vector3.Lerp(baseScale, squash, t / squashDur);
                yield return null;
            }

            float riseDur = BounceDuration * 0.72f;
            t = 0f;
            while (t < riseDur)
            {
                t += Time.unscaledDeltaTime;
                float p = t / riseDur;
                tf.position = basePos + Vector3.up * (Mathf.Sin(p * Mathf.PI) * BounceHeight);
                tf.localScale = Vector3.Lerp(squash, baseScale, p);
                yield return null;
            }
        }

        // ── ฝุ่น: สปอว์นพัฟกลมหลายก้อน กระจายรอบ ๆ แล้วขยาย+ลอย+จาง ──
        private void SpawnDustBurst(Vector3 center, Vector3 baseScale, string layer, int sort)
        {
            float footprint = Mathf.Max(0.6f, (baseScale.x + baseScale.y) * 0.5f);
            for (int i = 0; i < DustCount; i++)
            {
                float ang = (360f / DustCount) * i + Random.Range(-15f, 15f);
                float rad = ang * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad) * 0.6f, 0f); // แบนแนวนอนตาม iso
                var puff = MakeSpriteObject("Dust", GetPuffSprite(), layer, sort);
                puff.transform.position = center + new Vector3(0f, 0.15f * footprint, 0f);
                float maxSize = Random.Range(0.4f, 0.8f) * footprint;
                StartCoroutine(AnimateDust(puff, dir * footprint * 0.7f, maxSize));
            }
        }

        private IEnumerator AnimateDust(GameObject go, Vector3 travel, float maxSize)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            Vector3 start = go.transform.position;
            Color c = new Color(0.82f, 0.76f, 0.66f, 0.9f); // ฝุ่นสีทราย
            float life = Random.Range(0.45f, 0.7f);
            float t = 0f;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float p = t / life;
                float ease = Mathf.SmoothStep(0f, 1f, p);
                go.transform.position = start + travel * ease + Vector3.up * (0.25f * p);
                float s = Mathf.Lerp(0.2f, maxSize, ease);
                go.transform.localScale = new Vector3(s, s, 1f);
                c.a = 0.9f * (1f - p);
                if (sr != null) sr.color = c;
                yield return null;
            }
            Destroy(go);
        }

        // ── flash ขาว: พัฟกลมนุ่มตรงกลาง เข้าไว-ออกไว ──
        private void SpawnFlash(Vector3 center, Vector3 baseScale, string layer, int sort)
        {
            float footprint = Mathf.Max(0.6f, (baseScale.x + baseScale.y) * 0.5f);
            var flash = MakeSpriteObject("Flash", GetPuffSprite(), layer, sort);
            flash.transform.position = center + new Vector3(0f, 0.4f * footprint, 0f);
            StartCoroutine(AnimateFlash(flash, 1.6f * footprint));
        }

        private IEnumerator AnimateFlash(GameObject go, float maxSize)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            Color c = Color.white;
            const float inDur = 0.06f, outDur = 0.22f;

            float t = 0f;
            while (t < inDur)
            {
                t += Time.unscaledDeltaTime;
                float p = t / inDur;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxSize, p);
                c.a = Mathf.Lerp(0f, 0.9f, p);
                if (sr != null) sr.color = c;
                yield return null;
            }
            t = 0f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                float p = t / outDur;
                go.transform.localScale = Vector3.one * Mathf.Lerp(maxSize, maxSize * 1.3f, p);
                c.a = Mathf.Lerp(0.9f, 0f, p);
                if (sr != null) sr.color = c;
                yield return null;
            }
            Destroy(go);
        }

        private GameObject MakeSpriteObject(string name, Sprite sprite, string layer, int sort)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = sort;
            return go;
        }

        private IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // พัฟกลมนุ่ม: ตรงกลางทึบ ขอบจาง (alpha falloff) — สร้างครั้งเดียว cache ไว้ (idiom เดียวกับ BuildingVisualSpawner.GetShadowSprite)
        private static Sprite GetPuffSprite()
        {
            if (_dustSprite != null) return _dustSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 กลาง → 1 ขอบ
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f);                // ขอบนุ่ม
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            _dustSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _dustSprite;
        }
    }
}
