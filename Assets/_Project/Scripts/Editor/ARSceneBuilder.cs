using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using System.IO;

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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---- 光照 ----
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);

            // ---- AR Session（AR 运行时核心） ----
            var sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();
            var inputManager = sessionGo.AddComponent<ARInputManager>();

            // ---- AR Session Origin（相机+平面检测+放置器） ----
            var originGo = new GameObject("AR Session Origin");
            var cameraGo = new GameObject("AR Camera");
            cameraGo.transform.SetParent(originGo.transform, false);
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0); // 透明背景显示摄像头画面
            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            cameraGo.AddComponent<AudioListener>();

            // 平面检测
            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARPointCloudManager>();
            originGo.AddComponent<ARRaycastManager>();

            // 展品放置器
            var placer = originGo.AddComponent<ARExhibitPlacer>();
            placer.exhibitIds = new[] { "APOLLO_LRV", "YUTU_ROVER", "LUNA25_CAPSULE" };

            // 展品引用（加载模型资产）
            var prefabs = new GameObject[3];
            string[] paths =
            {
                "Assets/_Project/Art/Models/SM_Rover_Apollo.glb",
                "Assets/_Project/Art/Models/Sketchfab/yutu_rover/scene.gltf",
                "Assets/_Project/Art/Models/Sketchfab/luna25_lander/scene.gltf",
            };
            for (int i = 0; i < 3; i++)
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            placer.exhibitPrefabs = prefabs;
            placer.placementScale = new Vector3(0.3f, 0.3f, 0.3f); // 展品缩放为 30cm 大小的桌面模型

            // ---- 保存场景 ----
            const string scenePath = "Assets/_Project/Scenes/S6_AR.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[ARSceneBuilder] S6 AR 场景已创建: {scenePath}");

            // 加入构建场景列表
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == scenePath))
            {
                list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log($"[ARSceneBuilder] 已加入构建列表（共 {list.Count} 个场景）");
        }
}
