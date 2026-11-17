using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

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
            // 收集启用场景
            var sceneList = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) sceneList.Add(s.path);
            Debug.Log($"[AndroidBuilder] 构建场景数: {sceneList.Count}");

            // Android 构建配置
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // ARCore 最低 7.0
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.companyName = "LunarQuest";
            PlayerSettings.productName = "月征AR";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.lunarquest.ar");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            

            

            string apkPath = @"D:\下载\LunarQuest_AR.apk";

            var options = new BuildPlayerOptions
            {
                scenes = sceneList.ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            Debug.Log($"[AndroidBuilder] 开始构建 → {apkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[AndroidBuilder] 构建结果: {report.summary.result}, " +
                      $"大小: {report.summary.totalSize / 1e6:F1}MB, " +
                      $"耗时: {report.summary.totalTime.TotalSeconds:F0}s, " +
                      $"错误: {report.summary.totalErrors}");

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[AndroidBuilder] ✓ APK 已生成: {apkPath}");
            else
                Debug.LogError($"[AndroidBuilder] ✗ 构建失败: {report.summary.result}");
        }
    }
}
