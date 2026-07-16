using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// Build WebGL อัตโนมัติ — ใช้ได้ 2 ทาง:
    ///   • เมนู: NuclearReMind/Build WebGL (Builds/WebGL)
    ///   • batch: Unity.exe -batchmode -projectPath &lt;proj&gt; -executeMethod NuclearReMind.EditorTools.BuildWebGL.Build
    ///     (สคริปต์คู่กัน: build_webgl.ps1 ที่รากโปรเจกต์ — build + butler push ขึ้น itch.io ในคำสั่งเดียว)
    ///
    /// ตั้งค่าบังคับสำหรับ itch.io ให้อัตโนมัติทุกครั้ง: Gzip + Decompression Fallback
    /// (ถ้าไม่เปิด fallback แล้ว server ส่ง Content-Encoding ไม่ครบ จะเจอจอดำ/parse error บน itch)
    /// ซีนเอาจาก Build Settings ตามลำดับ (MainMenu → Gamescene) — build จริงเริ่มที่ MainMenu เสมอ
    /// </summary>
    public static class BuildWebGL
    {
        private const string OutputDir = "Builds/WebGL";

        [MenuItem("NuclearReMind/Build WebGL (Builds/WebGL)")]
        public static void BuildFromMenu() => Run(exitWhenDone: false);

        /// <summary>เรียกจาก command line (-executeMethod) — จบแล้ว Exit code 0/1 ให้ฝั่ง PowerShell เช็คได้</summary>
        public static void Build() => Run(exitWhenDone: Application.isBatchMode);

        private static void Run(bool exitWhenDone)
        {
            // ★ กติกา itch.io — ตั้งซ้ำทุกครั้งกันค่าถูกมือแก้เพี้ยน
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildWebGL] ไม่มีซีนใน Build Settings — build ไม่ได้");
                if (exitWhenDone) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[BuildWebGL] ▶ เริ่ม build WebGL → {OutputDir}\n  ซีน: {string.Join(", ", scenes)}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            bool ok = report.summary.result == BuildResult.Succeeded;
            if (ok)
                Debug.Log($"[BuildWebGL] ✅ สำเร็จ — {report.summary.totalSize / (1024 * 1024)} MB · " +
                          $"{report.summary.totalTime.TotalMinutes:0.0} นาที · output: {OutputDir}");
            else
                Debug.LogError($"[BuildWebGL] ❌ ล้มเหลว — {report.summary.result} " +
                               $"(errors: {report.summary.totalErrors})");

            if (exitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
