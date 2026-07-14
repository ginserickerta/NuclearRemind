using System.Collections;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// เด้งขยายจากเล็กไปใหญ่ (scale pop-in) อัตโนมัติ "ทุกครั้งที่ถูกเปิด" (OnEnable) — สำหรับแผงที่เปิด/ปิดได้
    ///   • ease-out-back (เด้งเกินขนาดแล้วหล่นเข้าที่) · ใช้ unscaledDeltaTime → ทำงานแม้เกม pause
    ///   • หา "กล่อง" ให้เอง: ถ้าแปะบนฉากหลัง dim เต็มจอ (anchor 0..1 + offset 0) จะเด้ง "กล่องลูก" แทน (ไม่เด้ง dim)
    ///   • OnDisable คืนสเกลปกติ (กันค้างเล็ก) · idempotent ด้วย Ensure()
    /// วิธีใช้: เรียก UIPopIn.Ensure(objTHatToggles) ก่อน SetActive(true) ในเมธอดเปิดของแต่ละ controller
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPopIn : MonoBehaviour
    {
        public float duration   = 0.26f;
        public float startScale = 0.6f;
        public float overshoot  = 1.7f;

        private Transform _target;
        private Vector3 _base = Vector3.one;
        private bool _captured;
        private Coroutine _co;

        private void OnEnable()
        {
            if (_target == null) _target = ResolveTarget();
            if (!_captured) { _base = _target.localScale; _captured = true; }
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Pop());
        }

        private void OnDisable()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (_target != null && _captured) _target.localScale = _base;
        }

        // ตัวเองเป็นกล่อง → เด้งตัวเอง · ตัวเองเป็นฉาก dim เต็มจอ → เด้งกล่องลูกตัวแรกที่ไม่ใช่ dim
        private Transform ResolveTarget()
        {
            var rt = transform as RectTransform;
            if (rt == null || !IsBackdrop(rt)) return transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i) as RectTransform;
                if (c != null && c.gameObject.activeSelf && !IsBackdrop(c)) return c;
            }
            return transform;
        }

        // ฉาก dim = ยืดเต็ม parent (anchor 0..1) + offset ~0 (ไม่มี inset) · กล่องเนื้อหาจะมี inset/anchor กลาง → false
        private static bool IsBackdrop(RectTransform rt)
            => Approx(rt.anchorMin, Vector2.zero) && Approx(rt.anchorMax, Vector2.one)
               && rt.offsetMin.sqrMagnitude < 1f && rt.offsetMax.sqrMagnitude < 1f;

        private static bool Approx(Vector2 a, Vector2 b)
            => Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;

        private IEnumerator Pop()
        {
            float t = 0f;
            _target.localScale = _base * startScale;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                _target.localScale = _base * Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(x));
                yield return null;
            }
            _target.localScale = _base;
            _co = null;
        }

        // ease-out-back: 1 + c3·p³ + c1·p² (p = x−1) → เกิน 1 แล้วหล่นกลับ = เด้ง
        private float EaseOutBack(float x)
        {
            float c1 = overshoot, c3 = c1 + 1f, p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static UIPopIn Ensure(GameObject go)
            => go == null ? null : (go.GetComponent<UIPopIn>() ?? go.AddComponent<UIPopIn>());
    }
}
