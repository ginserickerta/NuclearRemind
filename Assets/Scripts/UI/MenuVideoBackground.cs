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

        [Tooltip("ชื่อไฟล์วิดีโอใน StreamingAssets ที่ใช้บน WebGL (VideoClip เล่นบน WebGL ไม่ได้ — ต้องสตรีมจากไฟล์จริง)")]
        [SerializeField] private string webglFileName = "menu.mp4";

        [SerializeField] private int renderWidth = 1280;
        [SerializeField] private int renderHeight = 720;

        private RenderTexture _rt;

        private void Start()
        {
            // WebGL: VideoPlayer เล่นผ่าน <video> ของเบราว์เซอร์ → รองรับเฉพาะ URL จริง
            //        VideoClip ที่ฝังในบิลด์เล่นไม่ได้ (จอดำ) → ต้องชี้ไปไฟล์ใน StreamingAssets
            bool webgl = Application.platform == RuntimePlatform.WebGLPlayer;

            // 1) หา clip (ใช้บน platform อื่น) — บน WebGL ไม่จำเป็นถ้ามี webglFileName
            var chosen = clip != null ? clip : FindClipInScene();
            if (chosen == null && !webgl)
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
            if (webgl)
            {
                vp.source = VideoSource.Url; // เบราว์เซอร์ fetch ไฟล์จริงจาก StreamingAssets
                vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, webglFileName);
            }
            else
            {
                vp.source = VideoSource.VideoClip;
                vp.clip = chosen;
            }
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = _rt;
            vp.isLooping = true;
            vp.playOnAwake = false;
            vp.audioOutputMode = VideoAudioOutputMode.None; // เงียบ → เบราว์เซอร์อนุญาต autoplay
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

            Debug.Log("[MenuVideoBackground] ต่อท่อครบ กำลังเตรียม: " + (webgl ? vp.url : chosen.name));
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
