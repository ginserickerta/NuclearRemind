using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NuclearReMind
{
    /// <summary>
    /// เอฟเฟกต์ "เด้งขยายจากเล็กไปใหญ่" (scale / pop-in) เมื่อคลิกองค์ประกอบ UI
    /// ใช้ ease-out-back → ย่อลงแล้วสปริงเกินขนาดนิดนึงก่อนกลับเข้าที่ (เด้ง)
    /// ใช้ unscaledDeltaTime → ทำงานแม้เกม pause (แผง CORE TOWER เปิดตอน timeScale 0 ได้)
    ///
    /// ใช้ 2 ทาง:
    ///   • ติดกับปุ่ม → เด้งอัตโนมัติเมื่อคลิก (IPointerClickHandler)
    ///   • เรียก Play()/PlayFrom(scale) เอง เช่น pop-in ทั้งแผงตอนเปิด
    /// </summary>
    public class UIClickPop : MonoBehaviour, IPointerClickHandler
    {
        public float duration = 0.22f;   // ระยะเวลาเด้ง (วินาที)
        public float startScale = 0.72f; // ย่อเริ่มต้น (เล็ก → ใหญ่)
        public float overshoot = 1.9f;   // ความ "เด้งเกิน" ของ ease-out-back (มาก = เด้งแรง)
        public bool playOnClick = true;

        private Vector3 _base = Vector3.one;
        private Coroutine _co;

        private void Awake() => _base = transform.localScale;

        public void OnPointerClick(PointerEventData _)
        {
            if (playOnClick) Play();
        }

        public void Play() => PlayFrom(startScale);

        public void PlayFrom(float from)
        {
            if (!isActiveAndEnabled) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Pop(from));
        }

        private IEnumerator Pop(float from)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                transform.localScale = _base * Mathf.LerpUnclamped(from, 1f, EaseOutBack(x));
                yield return null;
            }
            transform.localScale = _base;
            _co = null;
        }

        // ease-out-back: 1 + c3·p³ + c1·p²  (p = x−1) → เกิน 1 แล้วหล่นกลับ = เด้ง
        private float EaseOutBack(float x)
        {
            float c1 = overshoot, c3 = c1 + 1f, p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static UIClickPop Attach(GameObject go)
        {
            var p = go.GetComponent<UIClickPop>();
            return p != null ? p : go.AddComponent<UIClickPop>();
        }
    }
}
