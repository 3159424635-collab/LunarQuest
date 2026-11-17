using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
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
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);

            // AR Session
            var sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            // AR Session Origin（相机+平面检测+放置器）
            var originGo = new GameObject("AR Session Origin");
            var cameraGo = new GameObject("AR Camera");
            cameraGo.transform.SetParent(originGo.transform, false);
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            cameraGo.AddComponent<AudioListener>();

            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARRaycastManager>();

            var placer = originGo.AddComponent<ARExhibitPlacer>();
            placer.exhibitIds = new[] { "APOLLO_LRV", "YUTU_ROVER", "LUNA25_CAPSULE" };

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
            placer.placementScale = new Vector3(0.3f, 0.3f, 0.3f);

            const string scenePath = "Assets/_Project/Scenes/S6_AR.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[ARSceneBuilder] S6 AR 场景已创建: " + scenePath);

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == scenePath))
            {
                list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log("[ARSceneBuilder] 构建列表共 " + list.Count + " 个场景");
        }
    }
}
