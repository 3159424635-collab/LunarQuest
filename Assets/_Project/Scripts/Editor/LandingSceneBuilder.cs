using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// S3 着陆场景搭建：舱内视角 + 月面远景 + 着陆小游戏。
    /// 批处理入口：LunarQuest.EditorTools.LandingSceneBuilder.Build
    /// </summary>
    public static class LandingSceneBuilder
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 光照：舱内暗 + 环境光微弱 ----
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.3f;
            sun.color = new Color(0.5f, 0.6f, 0.8f);
            sun.transform.rotation = Quaternion.Euler(60, 30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f);

            // ---- 舱内结构（暗色圆筒壁+底部窗口框） ----
            var matWall = LoadOrCreateMat("M_CabinWall", new Color(0.08f, 0.09f, 0.12f));
            var matFrame = LoadOrCreateMat("M_CabinFrame", new Color(0.15f, 0.17f, 0.22f));
            var matScreen = LoadOrCreateMat("M_CabinScreen", new Color(0.05f, 0.15f, 0.1f));
            var matMoon = LoadOrCreateMat("M_MoonSurface", new Color(0.35f, 0.34f, 0.32f));

            // 圆筒舱壁（围住相机）
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "CabinWall";
            tube.transform.position = Vector3.zero;
            tube.transform.localScale = new Vector3(3f, 2f, 3f); // 直径3m 高4m
            tube.GetComponent<Renderer>().sharedMaterial = matWall;
            // 翻转法线（从内往外看）
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            // 用第二个圆柱做内壁
            var tubeIn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tubeIn.name = "CabinWallInner";
            tubeIn.transform.position = Vector3.zero;
            tubeIn.transform.localScale = new Vector3(2.9f, 2f, 2.9f);
            tubeIn.GetComponent<Renderer>().sharedMaterial = matWall;
            Object.DestroyImmediate(tubeIn.GetComponent<Collider>());
            tube.SetActive(false); // 只用内壁

            // 窗口框（底部一圈）
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                var strut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strut.name = $"WindowStrut_{i}";
                float r = 1.45f;
                strut.transform.position = new Vector3(r * Mathf.Cos(ang), -1.8f, r * Mathf.Sin(ang));
                strut.transform.rotation = Quaternion.Euler(0, -ang * Mathf.Rad2Deg, 0);
                strut.transform.localScale = new Vector3(0.12f, 0.4f, 0.3f);
                strut.GetComponent<Renderer>().sharedMaterial = matFrame;
                Object.DestroyImmediate(strut.GetComponent<Collider>());
            }

            // 底部仪表台发光条
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI / 2f + Mathf.PI / 4f;
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = $"InstrumentPanel_{i}";
                float r = 1.3f;
                panel.transform.position = new Vector3(r * Mathf.Cos(ang), -1.5f, r * Mathf.Sin(ang));
                panel.transform.rotation = Quaternion.Euler(30, -ang * Mathf.Rad2Deg, 0);
                panel.transform.localScale = new Vector3(0.5f, 0.35f, 0.08f);
                panel.GetComponent<Renderer>().sharedMaterial = matScreen;
                Object.DestroyImmediate(panel.GetComponent<Collider>());
            }

            // ---- 月面远景（灰色平面，从远处渐近） ----
            var moonSurface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            moonSurface.name = "MoonSurface";
            moonSurface.transform.position = new Vector3(0, -20f, 0); // 初始 20m 下方
            moonSurface.transform.localScale = new Vector3(100f, 1f, 100f); // 1000×1000m
            moonSurface.GetComponent<Renderer>().sharedMaterial = matMoon;

            // 陨石坑（几个半球凹陷）
            var matCrater = LoadOrCreateMat("M_Crater", new Color(0.25f, 0.24f, 0.23f));
            for (int i = 0; i < 5; i++)
            {
                var crater = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crater.name = $"Crater_{i}";
                float cx = (i - 2) * 8f + Random.Range(-2f, 2f);
                float cz = Random.Range(-5f, 5f);
                crater.transform.position = new Vector3(cx, -20.2f, cz);
                float s = 2f + Random.Range(0f, 3f);
                crater.transform.localScale = new Vector3(s, 0.5f, s);
                crater.GetComponent<Renderer>().sharedMaterial = matCrater;
                Object.DestroyImmediate(crater.GetComponent<Collider>());
            }

            // ---- 主相机（舱内向下看月面） ----
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            cam.fieldOfView = 75f;
            cam.transform.position = new Vector3(0, 0, 0);
            cam.transform.rotation = Quaternion.Euler(80, 0, 0); // 向下看
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();

            // ---- 着陆小游戏 ----
            var minigameGo = new GameObject("LandingMinigame");
            var minigame = minigameGo.AddComponent<LandingMinigame>();
            minigame.mainCam = cam;
            minigame.startAltitude = 2000f;
            minigame.gravity = 1.62f;
            minigame.maxThrust = 30f;
            minigame.safeVerticalSpeed = 5f;
            minigame.safeHorizontalSpeed = 2f;
            minigame.fuelCapacity = 100f;

            // ---- QuestManager 接入 ----
            var qmGo = new GameObject("QuestManager");
            var qm = qmGo.AddComponent<QuestManager>();
            qm.database = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Characters/dialogue.json");
            qm.startEvent = "orbit_complete"; // 绕月完成→准备着陆

            // ---- SciFiHUD ----
            var sciFont = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_Project/UI/Fonts/NotoSansCJKsc-Regular.otf");
            var sci = new GameObject("SciFiHUD").AddComponent<SciFiHUD>();
            sci.font = sciFont;
            var prompt = new GameObject("InteractionPromptUI").AddComponent<InteractionPromptUI>();
            prompt.font = sciFont;

            // ---- 保存 ----
            const string scenePath = "Assets/_Project/Scenes/S3_Landing.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[LandingSceneBuilder] S3 着陆场景已创建: " + scenePath);

            // 加入构建列表
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == scenePath))
            {
                // 插在 S4 之前
                int s4Idx = list.FindIndex(s => s.path.Contains("S4"));
                if (s4Idx >= 0) list.Insert(s4Idx, new EditorBuildSettingsScene(scenePath, true));
                else list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        static Material LoadOrCreateMat(string name, Color color)
        {
            string p = $"Assets/_Project/Art/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }
    }
}
