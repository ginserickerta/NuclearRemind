using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NuclearReMind.EditorTools
{
    /// <summary>
    /// เมนู Build เกมเป็น Windows Standalone (x64) แบบ downloadable — กดจาก Unity ที่เปิดอยู่ได้เลย
    ///   • ใช้ scene จาก Build Settings (ที่ enabled) · ไม่มีก็ fallback เป็น MainMenu + Gamescene
    ///   • ออกที่ <โปรเจกต์>/Builds/Windows/NSC2026.exe (อยู่นอก Assets — Unity ไม่ import)
    ///   • เมนู "…+ Run" build เสร็จเปิดเกมเลย · "Reveal" เปิดโฟลเดอร์ build
    ///
    /// อัปเดตบ่อย: กดเมนูซ้ำได้ — Unity ทำ incremental build (เร็วกว่าครั้งแรกมาก) ทับโฟลเดอร์เดิม
    /// </summary>
    public static class GameBuilder
    {
        const string ProductFileName = "NSC2026.exe";
        const string SubDir = "Builds/Windows";

        [MenuItem("NuclearReMind/Build/Build Windows (x64)")]
        public static void BuildWindows() => Build(run: false);

        [MenuItem("NuclearReMind/Build/Build Windows (x64) + Run")]
        public static void BuildWindowsAndRun() => Build(run: true);

        /// <summary>
        /// Batch entry point — same build, no dialogs, real exit code so a CI/PowerShell driver can
        /// tell success from failure (-quit alone reports 0 even for a failed build).
        ///   Unity.exe -quit -batchmode -projectPath "..." -executeMethod NuclearReMind.EditorTools.GameBuilder.BuildWindowsBatch
        /// ⚠ Unity Editor must be CLOSED (project lock).
        /// </summary>
        public static void BuildWindowsBatch() => Build(run: false);

        [MenuItem("NuclearReMind/Build/Reveal Builds Folder")]
        public static void RevealBuilds()
        {
            var dir = OutputDir();
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        static void Build(bool run)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[GameBuilder] ไม่มี scene ใน Build Settings และหา MainMenu/Gamescene ไม่เจอ");
                if (Application.isBatchMode) { EditorApplication.Exit(1); return; }
                EditorUtility.DisplayDialog("Build Windows",
                    "ไม่มี scene ใน Build Settings และหา MainMenu/Gamescene ไม่เจอ — เปิด File → Build Settings เพิ่ม scene ก่อน", "OK");
                return;
            }

            string dir = OutputDir();
            Directory.CreateDirectory(dir);
            string exePath = Path.Combine(dir, ProductFileName);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };

            Debug.Log($"[GameBuilder] เริ่ม build Windows x64 → {exePath}\n scenes:\n  " + string.Join("\n  ", scenes));

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                double mb = s.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[GameBuilder] ✅ build สำเร็จ — {mb:F1} MB · ใช้เวลา {s.totalTime.TotalSeconds:F0}s\n{exePath}");
                if (!run && !Application.isBatchMode)
                    EditorUtility.RevealInFinder(exePath); // เปิดโฟลเดอร์ให้เห็นไฟล์ (โหมด Run เปิดเกมแทน)
            }
            else
            {
                Debug.LogError($"[GameBuilder] ❌ build {s.result} — errors: {s.totalErrors} · ดู Console/log ด้านบน");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Build Windows",
                        $"Build ไม่สำเร็จ ({s.result}) — errors {s.totalErrors}\nดูรายละเอียดใน Console", "OK");
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        // scene ที่ enabled ใน Build Settings · ไม่มีเลย → fallback หา MainMenu + Gamescene ในโปรเจกต์
        static string[] EnabledScenes()
        {
            var fromSettings = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
            if (fromSettings.Length > 0) return fromSettings;

            var found = new System.Collections.Generic.List<string>();
            foreach (var name in new[] { "MainMenu", "Gamescene" })
            {
                var guids = AssetDatabase.FindAssets($"{name} t:Scene");
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name && !found.Contains(p)) { found.Add(p); break; }
                }
            }
            return found.ToArray();
        }

        static string OutputDir()
        {
            // Application.dataPath = <โปรเจกต์>/Assets → ขึ้นไป 1 ชั้น = รากโปรเจกต์
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, SubDir);
        }
    }
}
