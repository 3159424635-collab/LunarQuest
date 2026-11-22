using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

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
                const string skyPath = "Assets/_Project/Art/Materials/Skybox_Stars_Landing.mat";
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (skyMat == null)
                {
                    skyMat = new Material(Shader.Find("Skybox/Panoramic")) { name = "Skybox_Stars" };
                    AssetDatabase.CreateAsset(skyMat, skyPath);
                }
                skyMat.SetTexture("_MainTex", skyTex);
                skyMat.SetFloat("_Exposure", 0.8f);
                EditorUtility.SetDirty(skyMat);
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
            var matTarget = TransparentUnlit("M_LandingTarget", new Color(0.15f, 0.9f, 1f, 0.82f));

            // ---- 月面：与 S4 完全相同的 Terrain（同参数同贴图同世界位置，保证两场景月面一致） ----
            const string terrainDataPath = "Assets/_Project/Scenes/S3_TerrainData.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, terrainDataPath);
            }
            td.heightmapResolution = 1025;
            td.size = new Vector3(500f, 100f, 500f);
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
            const string terrainLayerPath = "Assets/_Project/Art/Textures/TL_MoonLanding.asset";
            var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(terrainLayerPath);
            if (tl == null)
            {
                tl = new TerrainLayer();
                AssetDatabase.CreateAsset(tl, terrainLayerPath);
            }
            tl.diffuseTexture = lroc;
            tl.tileSize = new Vector2(500f, 500f);
            td.terrainLayers = new[] { tl };
            EditorUtility.SetDirty(tl);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-250f, 0f, -250f);

            // 着陆目标：透明全息环，不再用一整块亮绿色实体圆柱遮挡月面。
            var tgt = new GameObject("LandingTarget");
            float targetY = tgo.GetComponent<Terrain>().SampleHeight(Vector3.zero) +
                            tgo.transform.position.y + 0.10f;
            tgt.transform.position = new Vector3(0f, targetY, 0f);
            AddRing(tgt, "OuterRing", 10f, 0.16f, matTarget);
            AddRing(tgt, "InnerRing", 4.5f, 0.09f, matTarget);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tick = new GameObject("TargetTick_" + i).AddComponent<LineRenderer>();
                tick.transform.SetParent(tgt.transform, false);
                ConfigureLine(tick, 0.18f, matTarget);
                tick.positionCount = 2;
                tick.SetPosition(0, dir * 7.8f);
                tick.SetPosition(1, dir * 10.8f);
            }

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
            var dustRenderer = dustGo.GetComponent<ParticleSystemRenderer>();
            dustRenderer.sharedMaterial = ParticleMaterial(
                "M_EngineDust", new Color(0.62f, 0.58f, 0.52f, 0.48f));
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
            EditorSettings.serializationMode = SerializationMode.ForceText;
            // 仅替换场景数据文件，保留 .meta 中的稳定 GUID。
            if (File.Exists(sp)) File.Delete(sp);
            EditorSceneManager.SaveScene(scene, sp);
            Debug.Log("[LandingSceneBuilder] S3(星空+窗框+动态) 已创建: " + sp);

            BuildSceneCatalog.ConfigureProjectScenes();
        }


        static float Bowl(float x, float z, float cx, float cz, float r, float d)
        {
            float dd = Dist2(x, z, cx, cz);
            if (dd >= r * 1.9f) return 0f;
            if (dd < r) return d * (0.55f + 0.45f * Mathf.Cos(Mathf.PI * dd / r));
            return -d * 0.2f * Mathf.Sin(Mathf.PI * (dd - r) / (0.9f * r));
        }
        static float FlatF(float d, float r)
        {
            float t = Mathf.Clamp01((d - r * 0.65f) / (r * 0.35f));
            return 1f - t * t * (3f - 2f * t);
        }
        static float Dist2(float x, float z, float cx, float cz) => Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));

        static void AddRing(GameObject parent, string name, float radius, float width, Material material)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent.transform, false);
            ConfigureLine(line, width, material);
            line.loop = true;
            line.positionCount = 72;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        static void ConfigureLine(LineRenderer line, float width, Material material)
        {
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        static Material TransparentUnlit(string name, Color color)
        {
            string path = "Assets/_Project/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Sprites/Default");
            material.SetColor("_Color", color);
            material.SetColor("_BaseColor", color);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material ParticleMaterial(string name, Color color)
        {
            string path = "Assets/_Project/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
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
