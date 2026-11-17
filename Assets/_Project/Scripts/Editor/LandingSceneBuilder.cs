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

            // ---- 月面：与 S4 完全相同的 Terrain（同参数同贴图同世界位置，保证两场景月面一致） ----
            var td = new TerrainData { heightmapResolution = 1025, size = new Vector3(500f, 100f, 500f) };
            var hts = new float[1025, 1025];
            for (int z = 0; z < 1025; z++)
                for (int x = 0; x < 1025; x++)
                {
                    float wx = x / 1024f * 500f - 250f;
                    float wz = z / 1024f * 500f - 250f;
                    float h = 2f
                        + 3.0f * Mathf.PerlinNoise(wx * 0.008f + 100f, wz * 0.008f + 100f)
                        - 1.5f
                        + 1.2f * Mathf.PerlinNoise(wx * 0.03f + 200f, wz * 0.03f + 200f)
                        + 0.4f * Mathf.PerlinNoise(wx * 0.12f + 300f, wz * 0.12f + 300f);
                    h -= Bowl(wx, wz, 80f, 140f, 30f, 7f);
                    h -= Bowl(wx, wz, -150f, -60f, 25f, 5f);
                    h -= Bowl(wx, wz, -40f, -170f, 40f, 6f);
                    h = Mathf.Lerp(h, 2f, FlatF(Dist2(wx, wz, -8f, 188f), 80f));
                    h = Mathf.Lerp(h, 2f, FlatF(Dist2(wx, wz, 120f, -40f), 28f));
                    hts[z, x] = h / 100f;
                }
            td.SetHeights(0, 0, hts);
            var lroc = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Cinematics/Module4/Textures/NASA_Moon_LROC.jpg");
            var tl = new TerrainLayer { diffuseTexture = lroc, tileSize = new Vector2(40f, 40f) };
            td.terrainLayers = new[] { tl };
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-250f, 0f, -250f);

            // 着陆目标（绿色发光圈）
            var tgt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tgt.name = "LandingTarget";
            tgt.transform.position = new Vector3(0f, 2.6f, 0f);
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
            dustGo.transform.position = new Vector3(0f, 2.2f, 0f);
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
            // SciFiHUD 不加入 S3——着陆小游戏自带仪表，避免底部面板重合

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


        static float Bowl(float x, float z, float cx, float cz, float r, float d)
        {
            float dd = Dist2(x, z, cx, cz);
            if (dd >= r * 1.9f) return 0f;
            if (dd < r) return d * (0.55f + 0.45f * Mathf.Cos(Mathf.PI * dd / r));
            return -d * 0.2f * Mathf.Sin(Mathf.PI * (dd - r) / (0.9f * r));
        }
        static float FlatF(float d, float r) => 1f - Mathf.SmoothStep(r * 0.65f, r, d);
        static float Dist2(float x, float z, float cx, float cz) => Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));

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
