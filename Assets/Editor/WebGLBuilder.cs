using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Build WebGL (iPad Safari/เบราว์เซอร์-friendly) → โฟลเดอร์ Build/WebGL
    ///   • เมนู: NuclearReMind/Build WebGL
    ///   • batch: & "Unity.exe" -quit -batchmode -projectPath "..." -executeMethod NuclearReMind.EditorTools.WebGLBuilder.Build -logFile build.log
    ///
    /// ⚠ ต้องปิด Unity Editor ก่อนรัน batch (project lock) · หลัง build → push ขึ้น itch.io ด้วย butler:
    ///   butler push Build/WebGL &lt;user&gt;/&lt;game&gt;:html5
    /// (ตั้งค่า itch: "This file will be played in the browser" + Mobile friendly ON · frame ~1536×864 หรือ fullscreen)
    /// </summary>
    public static class WebGLBuilder
    {
        private const string BuildPath = "Build/WebGL";
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",   // ฉากเริ่ม (เมนูหลัก)
            "Assets/Scenes/Gamescene.unity",
        };

        [MenuItem("NuclearReMind/Build WebGL")]
        public static void Build()
        {
            // ── Player Settings (WebGL · iPad Safari) ──
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // iOS Safari บาง config อ่าน Brotli header ตรงไม่ได้ → ต้องมี fallback
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; // เดโม่: เล็ก/เร็ว
            PlayerSettings.WebGL.dataCaching = true;             // cache asset ใน IndexedDB — โหลดซ้ำไว
            PlayerSettings.runInBackground = true;

            var opts = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = BuildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[WebGLBuilder] ✅ Build succeeded → {BuildPath} ({s.totalSize / (1024 * 1024)} MB) · " +
                          $"push ต่อ: butler push {BuildPath} <user>/<game>:html5");
            else
                Debug.LogError($"[WebGLBuilder] ❌ Build {s.result} · errors {s.totalErrors}");
        }
    }
}
