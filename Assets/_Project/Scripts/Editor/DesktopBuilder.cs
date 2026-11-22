using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LunarQuest.EditorTools
{
    public static class DesktopBuilder
    {
        [MenuItem("工具/月征/构建 Windows 完整版")]
        public static void BuildWindows()
        {
            BuildSceneCatalog.ConfigureProjectScenes();
            string output = Path.GetFullPath(Path.Combine("Build", "Windows", "LunarQuest.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            PlayerSettings.companyName = "LunarQuest";
            PlayerSettings.productName = "月征：绕·落·回";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = BuildSceneCatalog.DesktopScenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows 构建失败: " + report.summary.result);
            Debug.Log("[DesktopBuilder] Windows 完整版已生成: " + output);
        }
    }
}
