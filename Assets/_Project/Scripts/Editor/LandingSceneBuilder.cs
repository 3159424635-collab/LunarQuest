using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LunarQuest.EditorTools
{
    public static class LandingSceneBuilder
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 星空天空盒（与 S4 相同） ----
            var skyTex = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/_Project/Art/Textures/Stars_PureSky_2k.hdr");
            if (skyTex != null)
            {
                var skyMat = new Material(Shader.Find("Skybox/Panoramic")) { name = "Skybox_Stars" };
                skyMat.SetTexture("_MainTex", skyTex);
                skyMat.SetFloat("_Exposure", 0.8f);
                AssetDatabase.CreateAsset(skyMat, "Assets/_Project/Art/Materials/Skybox_Stars.mat");
                RenderSettings.skybox = skyMat;
            }

            // ---- 光照：月面阳光 + 深空环境 ----
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.9f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(45, 30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f); // 深空冷调

            var matMoon = LM("M_MoonLanding", new Color(0.45f, 0.44f, 0.42f));
            var matTarget = LM("M_LandingTarget", new Color(0.1f, 0.8f, 0.2f));

            // ---- 月面：与 S4 相同的 Terrain 系统（统一视觉） ----
            var td = new TerrainData { heightmapResolution = 513, size = new Vector3(500f, 60f, 500f) };
            var heights = new float[513, 513];
            for (int z = 0; z < 513; z++)
                for (int x = 0; x < 513; x++)
                {
                    float wx = x / 512f * 500f - 250f;
                    float wz = z / 512f * 500f - 250f;
                    float h = 8f * Mathf.PerlinNoise(wx * 0.01f + 50f, wz * 0.01f + 50f)
                             + 3f * Mathf.PerlinNoise(wx * 0.04f + 150f, wz * 0.04f + 150f)
                             + 1f * Mathf.PerlinNoise(wx * 0.15f + 250f, wz * 0.15f + 250f);
                    // 着陆区平坦
                    float dCenter = Mathf.Sqrt(wx * wx + wz * wz);
                    float flat = 1f - Mathf.SmoothStep(30f, 60f, dCenter);
                    h = Mathf.Lerp(h, 5f, flat);
                    heights[z, x] = h / 60f;
                }
            td.SetHeights(0, 0, heights);

            // 与 S4 相同的 NASA LROC 贴图
            var lrocTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Cinematics/Module4/Textures/NASA_Moon_LROC.jpg");
            var layer = new TerrainLayer
            {
                diffuseTexture = lrocTex,
                tileSize = new Vector2(40f, 40f),
            };
            td.terrainLayers = new[] { layer };

            var terrainGO = Terrain.CreateTerrainGameObject(td);
            terrainGO.name = "LandingTerrain_Physics"; // 只做碰撞，不渲染
            terrainGO.transform.position = new Vector3(-250f, -35f, -250f);
            // 隐藏 Terrain 的视觉（只保留碰撞）
            var tCollider = terrainGO.GetComponent<TerrainCollider>();
            if (tCollider != null) tCollider.enabled = true;
            foreach (var mr in terrainGO.GetComponentsInChildren<MeshRenderer>())
                mr.enabled = false;

            // 阿波罗 11 着陆点模型（视觉主地形，NASA 数据）
            // 注意：glTFast 资产用 Object.Instantiate 深拷贝（PrefabUtility.InstantiatePrefab 对它只生成空壳）
            var apollo = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Art/Models/Sketchfab/apollo11_landing_site/scene.gltf");
            if (apollo != null)
            {
                var am = Object.Instantiate(apollo);
                am.name = "Apollo11Terrain";
                am.transform.position = new Vector3(0, -30f, 0);
                am.transform.localScale = Vector3.one * 5f;
                int meshCount = am.GetComponentsInChildren<MeshFilter>().Length;
                Debug.Log($"[LandingSceneBuilder] Apollo 11 地形已放置（含 {meshCount} 个网格）");
            }
            else
            {
                Debug.LogError("[LandingSceneBuilder] Apollo 模型加载失败！");
            }

            // 着陆目标（绿色发光圈）
            var tgt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tgt.name = "LandingTarget";
            tgt.transform.position = new Vector3(0, -30f, 0);
            tgt.transform.localScale = new Vector3(10f, 0.02f, 10f);
            tgt.GetComponent<Renderer>().sharedMaterial = matTarget;
            Object.DestroyImmediate(tgt.GetComponent<Collider>());

            // ---- 引擎扬尘粒子系统 ----
            var dustGo = new GameObject("EngineDust");
            var ps = dustGo.AddComponent<ParticleSystem>();
            var psMain = ps.main;
            psMain.startLifetime = 2f;
            psMain.startSpeed = 8f;
            psMain.startSize = 0.5f;
            psMain.startColor = new Color(0.5f, 0.48f, 0.45f, 0.6f);
            psMain.maxParticles = 200;
            psMain.loop = true;
            psMain.playOnAwake = false;
            var psShape = ps.shape;
            psShape.shapeType = ParticleSystemShapeType.Cone;
            psShape.angle = 30f;
            var psEmit = ps.emission;
            psEmit.rateOverTime = 50f;
            dustGo.transform.position = new Vector3(0, -29f, 0);
            dustGo.SetActive(false); // 由 LandingMinigame 控制启用

            // ---- 相机（向下看月面，星空在四周） ----
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // 用天空盒（星空）
            cam.fieldOfView = 65f;
            cam.transform.rotation = Quaternion.Euler(65, 0, 0); // 向下看但能看到星空边缘
            cam.farClipPlane = 800f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();

            // ---- 舱内窗框（四角暗色板，营造透过窗户看的感觉） ----
            var matFrame = LM("M_WindowFrame", new Color(0.04f, 0.05f, 0.07f));
            // 上方窗框
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "WindowTop";
            top.transform.SetParent(camGo.transform, false);
            top.transform.localPosition = new Vector3(0, 1.8f, 0.5f);
            top.transform.localScale = new Vector3(6f, 1.5f, 0.1f);
            top.GetComponent<Renderer>().sharedMaterial = matFrame;
            Object.DestroyImmediate(top.GetComponent<Collider>());
            // 左右窗框
            for (int side = 0; side < 2; side++)
            {
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "WindowSide_" + side;
                frame.transform.SetParent(camGo.transform, false);
                frame.transform.localPosition = new Vector3(side == 0 ? -2.8f : 2.8f, 0, 0.5f);
                frame.transform.localScale = new Vector3(1f, 5f, 0.1f);
                frame.GetComponent<Renderer>().sharedMaterial = matFrame;
                Object.DestroyImmediate(frame.GetComponent<Collider>());
            }
            // 底部窗框
            var bot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bot.name = "WindowBottom";
            bot.transform.SetParent(camGo.transform, false);
            bot.transform.localPosition = new Vector3(0, -1.8f, 0.5f);
            bot.transform.localScale = new Vector3(6f, 1f, 0.1f);
            bot.GetComponent<Renderer>().sharedMaterial = matFrame;
            Object.DestroyImmediate(bot.GetComponent<Collider>());

            // ---- 着陆小游戏 ----
            var mg = new GameObject("LandingMinigame").AddComponent<LandingMinigame>();
            mg.mainCam = cam;
            mg.startAltitude = 500f;
            mg.dustEffect = dustGo;

            // ---- QuestManager + SciFiHUD ----
            var qm = new GameObject("QuestManager").AddComponent<QuestManager>();
            qm.database = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Characters/dialogue.json");
            qm.startEvent = "orbit_complete";
            var sf = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_Project/UI/Fonts/NotoSansCJKsc-Regular.otf");
            var sci = new GameObject("SciFiHUD").AddComponent<SciFiHUD>();
            sci.font = sf;

            // ---- 保存 ----
            const string sp = "Assets/_Project/Scenes/S3_Landing.unity";
            EditorSceneManager.SaveScene(scene, sp);
            Debug.Log("[LandingSceneBuilder] S3(星空+窗框+动态) 已创建: " + sp);

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == sp))
            {
                int i4 = list.FindIndex(s => s.path.Contains("S4"));
                if (i4 >= 0) list.Insert(i4, new EditorBuildSettingsScene(sp, true));
                else list.Add(new EditorBuildSettingsScene(sp, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        static Material LM(string n, Color c)
        {
            string p = "Assets/_Project/Art/Materials/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
            AssetDatabase.CreateAsset(m, p);
            return m;
        }
    }
}
