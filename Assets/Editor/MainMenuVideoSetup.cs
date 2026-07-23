using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// [TH] หน้าที่: สคริปต์ Editor ติดตั้งวิดีโอพื้นหลังให้ซีนเมนูหลัก (ไม่ได้รันในเกมจริง)
    /// ใส่ "คลิปวิดีโอเป็นพื้นหลัง" ให้ MainMenu — VideoPlayer → RenderTexture → RawImage เต็มจอ (หลังปุ่ม)
    ///   • VideoPlayer (MenuVideo): เล่นวน (loop) + playOnAwake + เงียบ (audio None)
    ///   • RawImage (VideoBackground): เต็มจอ อยู่เหนือ Background มืด แต่ใต้ปุ่มเมนู
    /// idempotent: รันซ้ำได้ (reuse ของเดิม + rewire) · เปลี่ยนคลิปได้ที่ค่า ClipPath
    /// รัน: NuclearReMind/Menu/Setup Main Menu Video Background
    /// </summary>
    public static class MainMenuVideoSetup
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string ClipPath = "Assets/Sprites/awsdfasd/805755078.153351.mp4";
        private const string RTPath = "Assets/Video/MenuBackground.renderTexture";

        // เมนูนี้: ใส่วิดีโอวนเป็นพื้นหลังซีนเมนูหลัก (สร้าง VideoPlayer + RawImage ให้อัตโนมัติ)
        [MenuItem("NuclearReMind/Menu/Setup Main Menu Video Background")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != MainMenuPath)
                EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Main Menu Video",
                    "โหลดคลิปไม่ได้:\n" + ClipPath +
                    "\n(ถ้าเพิ่งวางไฟล์ ให้ Unity import ก่อน — คลิก Project window สักครั้ง)", "OK");
                return;
            }

            var canvasGO = GameObject.Find("MenuCanvas");
            if (canvasGO == null)
            {
                EditorUtility.DisplayDialog("Main Menu Video",
                    "ไม่พบ MenuCanvas — รัน 'Setup Menu System' (สร้าง MainMenu) ก่อน", "OK");
                return;
            }

            // ── RenderTexture asset (สร้างถ้ายังไม่มี) ──
            Directory.CreateDirectory("Assets/Video");
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RTPath);
            if (rt == null)
            {
                rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "MenuBackground" };
                AssetDatabase.CreateAsset(rt, RTPath);
            }

            // ── VideoPlayer (offscreen → RenderTexture) ──
            var videoGO = GameObject.Find("MenuVideo") ?? new GameObject("MenuVideo");
            var vp = videoGO.GetComponent<VideoPlayer>() ?? videoGO.AddComponent<VideoPlayer>();
            vp.source = VideoSource.VideoClip;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.isLooping = true;
            vp.playOnAwake = true;
            vp.waitForFirstFrame = true;
            vp.audioOutputMode = VideoAudioOutputMode.None; // พื้นหลังเมนู = เงียบ
            vp.aspectRatio = VideoAspectRatio.Stretch;
            EditorUtility.SetDirty(videoGO);

            // ── RawImage เต็มจอ (โชว์ RT) — เหนือ Background มืด · ใต้ปุ่ม ──
            var t = canvasGO.transform;
            var riTf = t.Find("VideoBackground");
            GameObject riGO = riTf != null ? riTf.gameObject : new GameObject("VideoBackground", typeof(RectTransform));
            if (riTf == null) riGO.transform.SetParent(t, false);
            var rrect = riGO.GetComponent<RectTransform>();
            rrect.anchorMin = Vector2.zero; rrect.anchorMax = Vector2.one;
            rrect.offsetMin = Vector2.zero; rrect.offsetMax = Vector2.zero;
            var ri = riGO.GetComponent<RawImage>() ?? riGO.AddComponent<RawImage>();
            ri.texture = rt;
            ri.color = Color.white;
            ri.raycastTarget = false;

            // วางลำดับ: เหนือ "Background" มืด (ถ้ามี) แต่ใต้ปุ่มที่สร้างทีหลัง
            var bgTf = t.Find("Background");
            riGO.transform.SetSiblingIndex(bgTf != null ? bgTf.GetSiblingIndex() + 1 : 0);

            EditorUtility.SetDirty(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[MainMenuVideoSetup] ✅ ใส่คลิปพื้นหลัง MainMenu แล้ว (VideoPlayer + RawImage เต็มจอ) — กด Play ทดสอบ");
        }
    }
}
