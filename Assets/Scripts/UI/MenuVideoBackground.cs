using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace NuclearReMind
{
    /// <summary>
    /// พื้นหลังวิดีโอเมนู — เบ็ดเสร็จในคอมโพเนนต์เดียว (ใส่ที่ "MenuCanvas" แล้วกด Play จบ)
    ///   • หา VideoClip เอง: ช่อง Clip → ไม่มีก็ไปหยิบจาก VideoPlayer ตัวใดก็ได้ในซีนที่ตั้ง clip ไว้แล้ว
    ///   • ปิด VideoPlayer มือที่ค้างทุกตัว (กันตัวที่ Target Texture=None มากวน)
    ///   • สร้าง VideoPlayer + RenderTexture + RawImage เต็มจอ (เหนือ Background · ใต้ปุ่ม) เองทั้งหมด
    /// ไม่ต้องแตะ Target Texture / ไม่ต้องเซฟ scene / ไม่ต้องรันเมนู
    /// </summary>
    public class MenuVideoBackground : MonoBehaviour
    {
        [Tooltip("คลิปพื้นหลัง — เว้นว่างได้ (จะไปหยิบจาก VideoPlayer ในซีนเอง)")]
        [SerializeField] private VideoClip clip;

        [SerializeField] private int renderWidth = 1280;
        [SerializeField] private int renderHeight = 720;

        private RenderTexture _rt;

        private void Start()
        {
            // 1) หา clip: ช่องนี้ก่อน → ไม่มีก็ไปหยิบจาก VideoPlayer ในซีน
            var chosen = clip != null ? clip : FindClipInScene();
            if (chosen == null)
            {
                Debug.LogError("[MenuVideoBackground] ไม่พบ VideoClip — ลาก clip ใส่ช่อง Clip ของคอมโพเนนต์นี้");
                return;
            }

            // 2) ปิด VideoPlayer มือที่ค้างทุกตัว (พวก Target Texture=None ที่กวน)
            foreach (var stray in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
                stray.enabled = false;

            // 3) สร้าง RenderTexture (ตอนรัน — ไม่ต้องมี asset)
            _rt = new RenderTexture(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);
            _rt.Create();

            // 4) VideoPlayer ใหม่ (คุมเองทั้งหมด)
            var vp = gameObject.AddComponent<VideoPlayer>();
            vp.source = VideoSource.VideoClip;
            vp.clip = chosen;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = _rt;
            vp.isLooping = true;
            vp.playOnAwake = false;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.aspectRatio = VideoAspectRatio.Stretch;
            vp.waitForFirstFrame = true;

            // 5) RawImage เต็มจอ (เหนือ Background · ใต้ปุ่ม)
            var canvas = GetComponent<Canvas>();                              // ห้ามใช้ ?? กับ Unity object
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            var parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("VideoBackground", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var ri = go.AddComponent<RawImage>();
            ri.texture = _rt;
            ri.color = Color.white;
            ri.raycastTarget = false;

            var bg = parent.Find("Background");
            go.transform.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);

            // 6) เตรียมแล้วเล่น (กันจอดำเฟรมแรก) + ดัก error
            vp.errorReceived += (s, m) => Debug.LogError("[MenuVideoBackground] เล่นไม่ได้: " + m);
            vp.prepareCompleted += _ => vp.Play();
            vp.Prepare();

            Debug.Log("[MenuVideoBackground] ต่อท่อครบ กำลังเตรียมคลิป: " + chosen.name);
        }

        private static VideoClip FindClipInScene()
        {
            foreach (var v in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
                if (v.clip != null) return v.clip;
            return null;
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
