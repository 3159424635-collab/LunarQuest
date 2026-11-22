using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.ARCore;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Linq;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// Android APK 构建器：构建 S6 AR 场景为可安装的 APK。
    /// 批处理入口：LunarQuest.EditorTools.AndroidBuilder.BuildAPK
    /// </summary>
    public static class AndroidBuilder
    {
        static string menuBuildPathOverride;

        [MenuItem("工具/月征/构建 S6 AR 测试包")]
        public static void BuildAPKFromMenu()
        {
            menuBuildPathOverride = Path.GetFullPath(
                Path.Combine("Build", "Android", "LunarQuest_AR_2026-11-21.apk"));
            BuildAPK();
        }

        public static void BuildAPK()
        {
            EnsureARCoreConfigured();
            BuildSceneCatalog.ValidateRequiredScenes();

            string apkPath = menuBuildPathOverride ?? CommandLineValue("-apkPath") ??
                Path.GetFullPath(Path.Combine("Build", "Android", "LunarQuest_AR.apk"));
            menuBuildPathOverride = null;
            string outputDirectory = Path.GetDirectoryName(apkPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            string previousCompany = PlayerSettings.companyName;
            string previousProduct = PlayerSettings.productName;
            string previousIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            UIOrientation previousOrientation = PlayerSettings.defaultInterfaceOrientation;
            AndroidSdkVersions previousMinSdk = PlayerSettings.Android.minSdkVersion;
            AndroidSdkVersions previousTargetSdk = PlayerSettings.Android.targetSdkVersion;
            bool previousDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            GraphicsDeviceType[] previousGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);

            try
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.companyName = "LunarQuest";
                PlayerSettings.productName = "月征AR";
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.lunarquest.ar");
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                // ARCore 5.1 不支持 Vulkan；独立 AR 包构建期间仅使用 OpenGLES3。
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                    new[] { GraphicsDeviceType.OpenGLES3 });

                var options = new BuildPlayerOptions
                {
                    // AR APK 必须只包含 S6，避免错误地从 Module4Showcase 或 S1 启动。
                    scenes = new[] { BuildSceneCatalog.ARScene },
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    // ARCore/Depth 的 required→optional 会改变合并后的 AndroidManifest；
                    // 清理构建缓存，避免 Gradle 沿用上一次的 required feature。
                    options = BuildOptions.CleanBuildCache,
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
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, previousDefaultGraphicsApis);
                if (!previousDefaultGraphicsApis && previousGraphicsApis != null && previousGraphicsApis.Length > 0)
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, previousGraphicsApis);
                BuildSceneCatalog.ConfigureProjectScenes();
            }
        }

        public static void EnsureARCoreConfigured()
        {
            const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            XRGeneralSettingsPerBuildTarget perTarget =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (perTarget == null)
                throw new InvalidOperationException("缺少 Android XR General Settings 资产: " + settingsPath);

            const BuildTargetGroup targetGroup = BuildTargetGroup.Android;
            if (!perTarget.HasSettingsForBuildTarget(targetGroup))
                perTarget.CreateDefaultSettingsForBuildTarget(targetGroup);
            if (!perTarget.HasManagerSettingsForBuildTarget(targetGroup))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(targetGroup);

            var general = perTarget.SettingsForBuildTarget(targetGroup);
            var manager = perTarget.ManagerSettingsForBuildTarget(targetGroup);
            general.InitManagerOnStart = true;
            if (!manager.activeLoaders.Any(loader => loader is ARCoreLoader) &&
                !XRPackageMetadataStore.AssignLoader(manager, typeof(ARCoreLoader).FullName, targetGroup))
                throw new InvalidOperationException("无法为 Android 构建目标启用 ARCore Loader");

            // 国内 Android 设备经常没有可更新的“Google Play 服务（AR）”。
            // 将 ARCore 声明为可选：支持 ARCore 的手机仍使用真实平面检测，
            // 不支持或未安装运行环境的手机则由场景进入兼容 3D 展示模式。
            var arcoreSettings = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            arcoreSettings.requirement = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            arcoreSettings.depth = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcoreSettings);

            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidBuilder] Android XR 已启用 ARCore Loader（ARCore 可选，支持兼容展示模式）");
        }

        public static bool IsARCoreConfigured()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildTargetGroup.Android);
            return general != null && general.InitManagerOnStart && general.Manager != null &&
                   general.Manager.activeLoaders.Any(loader => loader is ARCoreLoader);
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
