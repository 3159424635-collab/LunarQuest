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

            // ---- 月面（可见但不过亮，有陨石坑细节） ----
            var matMoon = LM("M_MoonLanding", new Color(0.45f, 0.44f, 0.42f));
            var matCrater = LM("M_CraterDeep", new Color(0.28f, 0.27f, 0.26f));
            var matRock = LM("M_MoonRockLand", new Color(0.38f, 0.37f, 0.35f));
            var matTarget = LM("M_LandingTarget", new Color(0.1f, 0.8f, 0.2f));

            var moon = GameObject.CreatePrimitive(PrimitiveType.Plane);
            moon.name = "MoonSurface";
            moon.transform.position = new Vector3(0, -30f, 0);
            moon.transform.localScale = new Vector3(200f, 1f, 200f);
            moon.GetComponent<Renderer>().sharedMaterial = matMoon;

            // 陨石坑（深色碗状+边缘）
            for (int i = 0; i < 15; i++)
            {
                var cr = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cr.name = "Crater_" + i;
                float cx = (i % 5 - 2) * 25f + Random.Range(-8f, 8f);
                float cz = (i / 5 - 1) * 25f + Random.Range(-8f, 8f);
                cr.transform.position = new Vector3(cx, -30.3f, cz);
                float s = 4f + Random.Range(0f, 10f);
                cr.transform.localScale = new Vector3(s, s * 0.3f, s);
                cr.GetComponent<Renderer>().sharedMaterial = matCrater;
                Object.DestroyImmediate(cr.GetComponent<Collider>());
            }

            // 岩石散布
            for (int i = 0; i < 30; i++)
            {
                var rk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rk.name = "Rock_" + i;
                rk.transform.position = new Vector3(Random.Range(-90f, 90f), -29.8f, Random.Range(-90f, 90f));
                rk.transform.localScale = new Vector3(Random.Range(1f, 5f), Random.Range(0.5f, 3f), Random.Range(1f, 5f));
                rk.transform.rotation = Random.rotation;
                rk.GetComponent<Renderer>().sharedMaterial = matRock;
                Object.DestroyImmediate(rk.GetComponent<Collider>());
            }

            // 着陆目标（绿色发光圈）
            var tgt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tgt.name = "LandingTarget";
            tgt.transform.position = new Vector3(0, -29.9f, 0);
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
