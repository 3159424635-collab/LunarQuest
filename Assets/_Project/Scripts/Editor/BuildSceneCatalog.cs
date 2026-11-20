using System.IO;
using UnityEditor;
using UnityEngine;

namespace LunarQuest.EditorTools
{
    /// <summary>项目唯一的构建场景清单，避免各场景生成器互相覆盖。</summary>
    public static class BuildSceneCatalog
    {
        public const string LaunchScene = "Assets/_Project/Scenes/S1_LaunchSite.unity";
        public const string ShowcaseScene = "Assets/_Project/Cinematics/Module4/Demo/Module4Showcase.unity";
        public const string LandingScene = "Assets/_Project/Scenes/S3_Landing.unity";
        public const string MoonScene = "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity";
        public const string ARScene = "Assets/_Project/Scenes/S6_AR.unity";

        public static string[] DesktopScenes => new[] { LaunchScene, ShowcaseScene, LandingScene, MoonScene };

        [MenuItem("工具/月征/修复构建场景列表")]
        public static void ConfigureProjectScenes()
        {
            ValidateRequiredScenes();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LaunchScene, true),
                new EditorBuildSettingsScene(ShowcaseScene, true),
                new EditorBuildSettingsScene(LandingScene, true),
                new EditorBuildSettingsScene(MoonScene, true),
                // AR 使用独立 APK 构建器；保留在列表中但不混入 Windows 主流程。
                new EditorBuildSettingsScene(ARScene, false),
            };
            Debug.Log("[BuildSceneCatalog] 已恢复桌面主流程，S6 作为独立 AR 场景保留。");
        }

        public static void ValidateRequiredScenes()
        {
            foreach (string scene in new[] { LaunchScene, ShowcaseScene, LandingScene, MoonScene, ARScene })
                if (!File.Exists(scene)) throw new FileNotFoundException("缺少构建场景", scene);
        }
    }
}
