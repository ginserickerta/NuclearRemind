using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearReMind
{
    /// <summary>
    /// Staggered intro ของ MainMenu — ไล่ให้ชิ้นเมนู (โลโก้/ปุ่ม) ค่อยๆ fade + เลื่อนขึ้นทีละชิ้น
    /// ใส่คอมโพเนนต์นี้ที่ "MenuCanvas" (หรือที่ไหนก็ได้ในซีน มันหา Canvas เอง) แล้วกด Play
    ///   • เว้นช่อง Targets ว่าง = เก็บลูกตรงของ MenuCanvas เอง (ข้าม Background/วิดีโอ/พาเนล)
    ///   • ปรับจังหวะ/ระยะ/ย่อ ได้ใน Inspector
    /// กัน "โผล่พรึบ": clamp deltaTime + อุ่นเครื่อง 1 เฟรม (เฟรมแรกหลังโหลดมักกระโดด)
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuIntroAnimator : MonoBehaviour
    {
        [Header("เป้าหมาย (เว้นว่าง = เก็บลูกของ MenuCanvas อัตโนมัติ)")]
        [SerializeField] private RectTransform[] targets;

        [Header("จังหวะ")]
        [SerializeField] private float startDelay = 0.2f;
        [SerializeField] private float stagger = 0.14f;
        [SerializeField] private float duration = 0.5f;

        [Header("ท่าเข้า")]
        [SerializeField] private float slideUp = 60f;
        [SerializeField] private float startScale = 0.92f;

        private const float MaxStep = 1f / 30f; // clamp deltaTime กันเฟรมกระโดด

        private static readonly string[] Exclude = { "background", "video", "backdrop", "panel", "auto" };

        private void Start()
        {
            var list = CollectTargets();
            if (list.Count == 0)
            {
                Debug.LogWarning("[MainMenuIntro] ไม่พบชิ้นเมนูให้แอนิเมต — ลูกทั้งหมด: " + DumpChildren());
                return;
            }
            var sb = new StringBuilder();
            foreach (var t in list) sb.Append(t.name).Append(", ");
            Debug.Log("[MainMenuIntro] เริ่มแอนิเมต " + list.Count + " ชิ้น: " + sb);
            StartCoroutine(Play(list));
        }

        private Transform ResolveRoot()
        {
            // ให้ทำงานได้แม้ใส่คอมโพเนนต์ผิดตัว: ตัวเอง→พาเรนต์→ทั้งซีน หา Canvas
            // NOTE: ห้ามใช้ ?? กับ Unity object — ต้องเช็ก == null (Unity override == แต่ไม่ override ??)
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform : transform;
        }

        private List<RectTransform> CollectTargets()
        {
            var list = new List<RectTransform>();
            if (targets != null && targets.Length > 0)
            {
                foreach (var t in targets) if (t != null) list.Add(t);
                return list;
            }
            foreach (Transform child in ResolveRoot())
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.GetComponentInChildren<Graphic>() == null) continue; // มีอะไรให้เห็นไหม
                string n = child.name.ToLowerInvariant();
                bool skip = false;
                foreach (var e in Exclude) if (n.Contains(e)) { skip = true; break; }
                if (skip) continue;
                if (child is RectTransform rt) list.Add(rt);
            }
            return list;
        }

        private string DumpChildren()
        {
            var sb = new StringBuilder();
            foreach (Transform c in ResolveRoot()) sb.Append(c.name).Append(c.gameObject.activeInHierarchy ? "" : "(inactive)").Append(", ");
            return sb.ToString();
        }

        private IEnumerator Play(List<RectTransform> list)
        {
            int n = list.Count;
            var groups = new CanvasGroup[n];
            var homePos = new Vector2[n];
            var homeScale = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                var rt = list[i];
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>(); // ห้ามใช้ ?? กับ Unity object
                groups[i] = cg;
                homePos[i] = rt.anchoredPosition;
                homeScale[i] = rt.localScale;
                cg.alpha = 0f;
                rt.anchoredPosition = homePos[i] + Vector2.down * slideUp;
                rt.localScale = homeScale[i] * startScale;
            }

            // อุ่นเครื่อง: ทิ้งเฟรมแรกที่ deltaTime มักกระโดดหลังโหลด scene
            yield return null;
            yield return null;

            if (startDelay > 0f) yield return WaitClamped(startDelay);

            for (int i = 0; i < n; i++)
            {
                StartCoroutine(Animate(list[i], groups[i], homePos[i], homeScale[i]));
                if (stagger > 0f && i < n - 1) yield return WaitClamped(stagger);
            }
        }

        private IEnumerator Animate(RectTransform rt, CanvasGroup cg, Vector2 home, Vector3 scale)
        {
            Vector2 from = home + Vector2.down * slideUp;
            Vector3 fromScale = scale * startScale;
            float t = 0f;
            while (t < duration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxStep); // clamp กันกระโดด
                float k = Mathf.Clamp01(t / duration);
                float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                cg.alpha = e;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, home, e);
                rt.localScale = Vector3.LerpUnclamped(fromScale, scale, e);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = home;
            rt.localScale = scale;
        }

        private static IEnumerator WaitClamped(float s)
        {
            float t = 0f;
            while (t < s) { t += Mathf.Min(Time.unscaledDeltaTime, MaxStep); yield return null; }
        }
    }
}
