using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.IO;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// Android APK 构建器：构建 S6 AR 场景为可安装的 APK。
    /// 批处理入口：LunarQuest.EditorTools.AndroidBuilder.BuildAPK
    /// </summary>
    public static class AndroidBuilder
    {
        public static void BuildAPK()
        {
            BuildSceneCatalog.ValidateRequiredScenes();

            string apkPath = CommandLineValue("-apkPath") ??
                Path.GetFullPath(Path.Combine("Build", "Android", "LunarQuest_AR.apk"));
            string outputDirectory = Path.GetDirectoryName(apkPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            UIOrientation previousOrientation = PlayerSettings.defaultInterfaceOrientation;
            AndroidSdkVersions previousMinSdk = PlayerSettings.Android.minSdkVersion;
            AndroidSdkVersions previousTargetSdk = PlayerSettings.Android.targetSdkVersion;

            try
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.companyName = "LunarQuest";
                PlayerSettings.productName = "月征AR";
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.lunarquest.ar");
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

                var options = new BuildPlayerOptions
                {
                    // AR APK 必须只包含 S6，避免错误地从 Module4Showcase 或 S1 启动。
                    scenes = new[] { BuildSceneCatalog.ARScene },
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                };

                Debug.Log($"[AndroidBuilder] 开始构建独立 AR 场景 → {apkPath}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"[AndroidBuilder] 构建结果: {report.summary.result}, " +
                          $"大小: {report.summary.totalSize / 1e6:F1}MB, " +
                          $"耗时: {report.summary.totalTime.TotalSeconds:F0}s, " +
                          $"错误: {report.summary.totalErrors}");

                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("AR APK 构建失败: " + report.summary.result);

                Debug.Log($"[AndroidBuilder] APK 已生成: {apkPath}");
            }
            finally
            {
                PlayerSettings.companyName = previousCompany;
                PlayerSettings.productName = previousProduct;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, previousIdentifier);
                PlayerSettings.defaultInterfaceOrientation = previousOrientation;
                PlayerSettings.Android.minSdkVersion = previousMinSdk;
                PlayerSettings.Android.targetSdkVersion = previousTargetSdk;
                BuildSceneCatalog.ConfigureProjectScenes();
            }
        }

        static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
