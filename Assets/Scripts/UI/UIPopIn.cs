using System;
using System.Collections;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// แอนิเมชันเปิด/ปิดหน้าต่างสไตล์ Windows 11 — นุ่ม เร็ว ไม่เด้ง (scale + fade, ease)
    ///   • เปิด (อัตโนมัติทุกครั้งที่ OnEnable): scale 0.92→1.0 + fade 0→1 · ease-out · ~0.16s
    ///   • ปิด (เรียก UIPopIn.PlayClose): scale 1.0→0.94 + fade 1→0 · ease-in · ~0.12s แล้วค่อย SetActive(false)
    ///   • fade ผ่าน CanvasGroup (ไม่กระทบ raycast/interactable — คืน alpha=1 เสมอตอนจบ)
    ///   • หา "กล่อง" ให้เอง: ถ้าแปะบนฉากหลัง dim เต็มจอ (anchor 0..1 + offset 0) จะเด้ง "กล่องลูก" แทน (ไม่เด้ง dim)
    ///   • ใช้ unscaledDeltaTime → ทำงานแม้เกม pause · idempotent ด้วย Ensure()
    ///
    /// วิธีใช้:
    ///   เปิด → UIPopIn.Ensure(panel) ก่อน panel.SetActive(true) (เหมือนเดิม)
    ///   ปิด → UIPopIn.PlayClose(panel) แทน panel.SetActive(false) (เล่นหุบออกแล้วปิดเอง)
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPopIn : MonoBehaviour
    {
        [Header("เปิด (Windows 11 — นุ่ม ไม่เด้ง)")]
        public float openDuration = 0.16f;
        public float openStartScale = 0.92f;

        [Header("ปิด (หุบออก)")]
        public float closeDuration = 0.12f;
        public float closeEndScale = 0.94f;

        [Tooltip("true = เด้ง 'ตัวเอง' เสมอ (ไม่ไปหากล่องลูก) — สำหรับแผงเต็มจอที่อยากเด้งทั้งชิ้น เช่น dialogue overlay")]
        public bool animateSelf;

        private Transform _target;
        private CanvasGroup _group;
        private Vector3 _base = Vector3.one;
        private bool _captured;
        private Coroutine _co;
        private bool _closing;

        private void OnEnable()
        {
            _closing = false;
            EnsureRefs();
            if (!_captured) { _base = _target.localScale; _captured = true; }
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(OpenRoutine());
        }

        private void OnDisable()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            _closing = false;
            // คืนสภาพปกติ (กันค้างเล็ก/จาง) — รอบเปิดถัดไปเริ่มจากศูนย์
            if (_target != null && _captured) _target.localScale = _base;
            if (_group != null) _group.alpha = 1f;
        }

        private void EnsureRefs()
        {
            if (_target == null) _target = ResolveTarget();
            if (_group == null)
            {
                _group = _target.GetComponent<CanvasGroup>();
                if (_group == null) _group = _target.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // ตัวเองเป็นกล่อง → เด้งตัวเอง · ตัวเองเป็นฉาก dim เต็มจอ → เด้งกล่องลูกตัวแรกที่ไม่ใช่ dim
        private Transform ResolveTarget()
        {
            if (animateSelf) return transform; // บังคับเด้งทั้งชิ้น (dialogue overlay ฯลฯ)
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

        private IEnumerator OpenRoutine()
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, openDuration);
            _target.localScale = _base * openStartScale;
            if (_group != null) _group.alpha = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / dur);
                float e = EaseOutCubic(x);
                _target.localScale = _base * Mathf.LerpUnclamped(openStartScale, 1f, e);
                if (_group != null) _group.alpha = e;
                yield return null;
            }
            _target.localScale = _base;
            if (_group != null) _group.alpha = 1f;
            _co = null;
        }

        // หุบออกแล้วปิด GameObject (เรียกผ่าน static PlayClose) — onDone หลังปิดจริง
        private IEnumerator CloseRoutine(Action onDone)
        {
            _closing = true;
            float t = 0f;
            float dur = Mathf.Max(0.01f, closeDuration);
            float startAlpha = _group != null ? _group.alpha : 1f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / dur);
                float e = EaseInCubic(x);
                _target.localScale = _base * Mathf.LerpUnclamped(1f, closeEndScale, e);
                if (_group != null) _group.alpha = Mathf.LerpUnclamped(startAlpha, 0f, e);
                yield return null;
            }
            _co = null;
            _closing = false;
            gameObject.SetActive(false); // OnDisable จะคืน scale/alpha ให้เอง
            onDone?.Invoke();
        }

        private static float EaseOutCubic(float x) { float p = 1f - x; return 1f - p * p * p; }
        private static float EaseInCubic(float x)  => x * x * x;

        public static UIPopIn Ensure(GameObject go)
            => go == null ? null : (go.GetComponent<UIPopIn>() ?? go.AddComponent<UIPopIn>());

        /// <summary>เหมือน Ensure แต่บังคับเด้ง "ตัวเอง" (ไม่ไปหากล่องลูก) — สำหรับแผงเต็มจอ เช่น dialogue overlay</summary>
        public static UIPopIn EnsureSelf(GameObject go)
        {
            var p = Ensure(go);
            if (p != null) p.animateSelf = true;
            return p;
        }

        /// <summary>
        /// เปิดแบบปลอดภัยต่อการ "ปิดแล้วเปิดซ้ำทันที" (เช่น dialogue เล่นต่อเนื่อง): ถ้ากำลังหุบปิดอยู่
        /// จะยกเลิก close แล้วเล่น open ใหม่ · ถ้ายังปิดสนิทก็ SetActive(true) ให้ OnEnable เล่นเอง
        /// </summary>
        public static void PlayOpen(GameObject go, bool self = false)
        {
            if (go == null) return;
            var p = self ? EnsureSelf(go) : Ensure(go);
            if (go.activeSelf) // active อยู่ (น่าจะกำลังหุบปิด) → ยกเลิก close แล้วเล่น open ทับ
            {
                if (p._co != null) p.StopCoroutine(p._co);
                p._closing = false;
                p.EnsureRefs();
                if (!p._captured) { p._base = p._target.localScale; p._captured = true; }
                p._co = p.StartCoroutine(p.OpenRoutine());
            }
            else go.SetActive(true); // ปิดสนิท → OnEnable เล่น open ให้เอง
        }

        /// <summary>
        /// หุบหน้าต่างออก (scale ลง + fade) แล้ว SetActive(false) ให้เอง — เรียกแทน go.SetActive(false)
        /// go ต้อง active อยู่ (ไม่งั้นปิดทันทีแบบเดิม) · เรียกซ้ำระหว่างปิดอยู่ = ไม่ทำอะไร (กันชน)
        /// </summary>
        public static void PlayClose(GameObject go, Action onDone = null)
        {
            if (go == null) return;
            if (!go.activeInHierarchy) { go.SetActive(false); onDone?.Invoke(); return; }

            var pop = Ensure(go);
            if (pop._closing) return; // กำลังหุบอยู่แล้ว
            pop.EnsureRefs();
            if (!pop._captured) { pop._base = pop._target.localScale; pop._captured = true; }
            if (pop._co != null) pop.StopCoroutine(pop._co);
            pop._co = pop.StartCoroutine(pop.CloseRoutine(onDone));
        }
    }
}
