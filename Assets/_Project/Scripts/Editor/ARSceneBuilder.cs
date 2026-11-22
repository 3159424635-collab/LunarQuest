using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// S6 AR 场景搭建：ARSession + ARPlaneManager + 展品放置器。
    /// 批处理入口：LunarQuest.EditorTools.ARSceneBuilder.Build
    /// </summary>
    public static class ARSceneBuilder
    {
        public static void Build()
        {
            AndroidBuilder.EnsureARCoreConfigured();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);

            // AR Session
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            // 不让 AR Foundation 自动弹出 Google Play AR 服务安装页；
            // ARExhibitPlacer 会在运行环境缺失时进入兼容展示模式。
            session.attemptUpdate = false;
            sessionGo.AddComponent<ARInputManager>();

            // XR Origin（相机+平面检测+放置器）
            var originGo = new GameObject("XR Origin");
            var origin = originGo.AddComponent<XROrigin>();
            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);
            var cameraGo = new GameObject("AR Camera");
            cameraGo.transform.SetParent(offsetGo.transform, false);
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            cameraGo.AddComponent<AudioListener>();
            // 项目交互层统一使用旧 Input API；ARPoseDriver 直接读取 XR InputDevice，
            // 避免 Android 上启用 "Both" 输入后端产生兼容性警告。
#pragma warning disable 0618
            cameraGo.AddComponent<ARPoseDriver>();
#pragma warning restore 0618

            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = cam;

            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARRaycastManager>();

            var placer = originGo.AddComponent<ARExhibitPlacer>();
            placer.exhibitIds = new[] { "APOLLO_LM", "YUTU_ROVER", "LRO" };

            var prefabs = new GameObject[3];
            string[] paths =
            {
                "Assets/_Project/Art/Models/NASA/Apollo Lunar Module/Apollo Lunar Module.glb",
                "Assets/_Project/Art/Models/Sketchfab/yutu_rover/scene.gltf",
                "Assets/_Project/Art/Models/NASA/Lunar Reconnaissance Orbiter/Lunar Reconnaissance Orbiter.glb",
            };
            for (int i = 0; i < 3; i++)
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            placer.exhibitPrefabs = prefabs;
            placer.placementScale = new Vector3(0.3f, 0.3f, 0.3f);

            const string scenePath = "Assets/_Project/Scenes/S6_AR.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[ARSceneBuilder] S6 AR 场景已创建: " + scenePath);
            BuildSceneCatalog.ConfigureProjectScenes();
        }
    }
}
