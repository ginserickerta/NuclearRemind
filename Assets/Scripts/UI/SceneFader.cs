using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Transition จอดำข้ามซีน — เรียก SceneFader.FadeToScene("Gamescene") แทน SceneManager.LoadScene ตรง ๆ
    ///   เฟดมืด → โหลดซีน (จอดำสนิทระหว่างโหลด) → เฟดสว่าง
    ///
    /// • สร้างตัวเอง on-demand: canvas overlay ดำเต็มจอ sortingOrder สูงสุด + DontDestroyOnLoad (รอดข้ามซีน
    ///   ให้ coroutine เฟดต่อจนจบ) — ไม่ต้อง wire อะไรในซีน
    /// • ใช้ unscaledDeltaTime → ทำงานแม้ timeScale = 0 (เรียกจาก Pause/GameOver ได้)
    /// • ระหว่างเฟด raycastTarget เปิด = บล็อกคลิกทุกอย่าง (กันดับเบิลคลิกปุ่ม/คลิกทะลุตอนเปลี่ยนซีน)
    ///   เรียกซ้ำระหว่างกำลังเฟดอยู่ = เมิน (กันกดรัว)
    /// • ผู้เรียก: MainMenuController.NewGame · PauseMenuController.GoToMainMenu · GameManager.Restart
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        private const float DefaultFadeOut = 0.35f; // มืดก่อนโหลด
        private const float DefaultFadeIn  = 0.45f; // สว่างหลังโหลด

        private static SceneFader _inst;

        private Image _black;
        private bool _busy;

        /// <summary>เฟดจอดำแล้วโหลดซีนตามชื่อ (แทน SceneManager.LoadScene)</summary>
        public static void FadeToScene(string sceneName,
            float fadeOut = DefaultFadeOut, float fadeIn = DefaultFadeIn)
            => Ensure().Begin(() => SceneManager.LoadSceneAsync(sceneName), fadeOut, fadeIn);

        /// <summary>เฟดจอดำแล้วโหลดซีนตาม build index (สำหรับ reload ซีนเดิม เช่น Restart)</summary>
        public static void FadeToScene(int buildIndex,
            float fadeOut = DefaultFadeOut, float fadeIn = DefaultFadeIn)
            => Ensure().Begin(() => SceneManager.LoadSceneAsync(buildIndex), fadeOut, fadeIn);

        private static SceneFader Ensure()
        {
            if (_inst != null) return _inst;

            var go = new GameObject("[SceneFader]");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // เหนือทุกแผง (GameUIStack ฐาน 200 / Pause 100)
            go.AddComponent<GraphicRaycaster>(); // จำเป็น — raycastTarget ของ Image ถึงจะบล็อกคลิกจริง

            var imgGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = imgGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false; // ตอน idle ห้ามบังคลิก

            _inst = go.AddComponent<SceneFader>();
            _inst._black = img;
            return _inst;
        }

        private void Begin(System.Func<AsyncOperation> load, float outDur, float inDur)
        {
            if (_busy) return; // กำลังเปลี่ยนซีนอยู่ — กันกดรัว
            StartCoroutine(Run(load, outDur, inDur));
        }

        private IEnumerator Run(System.Func<AsyncOperation> load, float outDur, float inDur)
        {
            _busy = true;
            _black.raycastTarget = true;

            yield return Fade(0f, 1f, outDur);

            var op = load();
            while (op != null && !op.isDone) yield return null;

            yield return Fade(1f, 0f, inDur);

            _black.raycastTarget = false;
            _busy = false;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            dur = Mathf.Max(0.01f, dur);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            _black.color = new Color(0f, 0f, 0f, to);
        }
    }
}
