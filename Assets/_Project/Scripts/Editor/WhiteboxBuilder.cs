using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// S4 月面白盒场景的一次性搭建工具：程序化地形、模型归一化摆放、
    /// 模块一脚本挂载、小地图与调试 HUD。批处理入口 BuildAll。
    /// </summary>
    public static class WhiteboxBuilder
    {
        const float MapSize = 500f;
        const float HeightRange = 100f;

        // 地形要素使用世界坐标；地形对象置于 (-250,0,-250)，世界原点即地图中心
        static readonly Vector2 CraterMain = new Vector2(80f, 140f);   // 3 号陨石坑（采样任务区）
        const float CraterMainR = 30f, CraterMainD = 7f;
        static readonly Vector2 CraterB = new Vector2(-150f, -60f);
        const float CraterBR = 25f, CraterBD = 5f;
        static readonly Vector2 CraterC = new Vector2(-40f, -170f);
        const float CraterCR = 40f, CraterCD = 6f;
        static readonly Vector2 BaseFlat = new Vector2(0f, 170f);      // 广寒站平整区
        const float BaseFlatR = 62f;
        static readonly Vector2 LandingFlat = new Vector2(120f, -40f); // 着陆区
        const float LandingFlatR = 28f;

        const string MoonMatPath = "Assets/_Project/Cinematics/Module4/Materials/MoonGround.mat";
        const string ModelDir = "Assets/_Project/Art/Models";
        const string CharDir = ModelDir + "/Characters";

        static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("工具/月征/搭建 S4 白盒场景")]
        public static void BuildAll()
        {
            Report.Clear();
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            Terrain terrain = BuildTerrain();
            float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

            var player = BuildPlayer(new Vector3(0f, 0f, 150f), Ground);
            BuildRover(new Vector3(24f, 0f, 156f), Ground, player.transform);
            PlaceModel(ModelDir + "/月球基地.fbx", 46f, new Vector3(-6f, 0f, 178f), Ground, "MoonBase", true);
            PlaceModel(ModelDir + "/毅力号.fbx", 2.7f, new Vector3(14f, 0f, 150f), Ground, "Exhibit_Perseverance", false);
            PlaceNpc("base_coordinator", "周航_协调员", new Vector3(2f, 0f, 158f), Ground);
            PlaceNpc("lunar_scientist", "许青_科学家", new Vector3(-9f, 0f, 162f), Ground);
            PlaceNpc("maintenance_technician", "陈越_维护员", new Vector3(18f, 0f, 150f), Ground);
            BuildSampleRocks(Ground);
            BuildRockField(Ground);
            BuildMinimap(player.transform);

            player.GetComponent<HUDController>().SetQuest("熟悉广寒站：走近提示按 E 交互；月球车按 F 上下车");

            const string scenePath = "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            SetBuildScenes(scenePath);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/whitebox_report.txt", Report.ToString());
            Debug.Log("[WhiteboxBuilder] 搭建完成\n" + Report);
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");
            Directory.CreateDirectory("Assets/_Project/Art/Textures");
            Directory.CreateDirectory("Assets/_Project/Art/Animations");
            AssetDatabase.Refresh();
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.22f);
        }

        static Terrain BuildTerrain()
        {
            var td = new TerrainData
            {
                heightmapResolution = 513,
                size = new Vector3(MapSize, HeightRange, MapSize),
            };
            var heights = new float[513, 513];
            for (int z = 0; z < 513; z++)
                for (int x = 0; x < 513; x++)
                {
                    float wx = x / 512f * MapSize - MapSize * 0.5f;
                    float wz = z / 512f * MapSize - MapSize * 0.5f;
                    heights[z, x] = SampleHeight(wx, wz) / HeightRange;
                }
            td.SetHeights(0, 0, heights);
            td.terrainLayers = new[] { BuildTerrainLayer() };

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.position = new Vector3(-MapSize * 0.5f, 0f, -MapSize * 0.5f);
            AssetDatabase.CreateAsset(td, "Assets/_Project/Scenes/S4_TerrainData.asset");
            Report.AppendLine($"[地形] 500x500m；3号坑@({CraterMain.x},{CraterMain.y}) r={CraterMainR} d={CraterMainD}m；基地平区@({BaseFlat.x},{BaseFlat.y}) r={BaseFlatR}m；着陆区@({LandingFlat.x},{LandingFlat.y}) r={LandingFlatR}m");
            return go.GetComponent<Terrain>();
        }

        static float SampleHeight(float x, float z)
        {
            float h = 2f
                + 0.55f * (Mathf.Sin(x * 0.021f + 1.7f) + Mathf.Sin(z * 0.017f + 0.4f))
                + 0.35f * Mathf.Sin(x * 0.052f) * Mathf.Sin(z * 0.043f);
            h -= Bowl(x, z, CraterMain, CraterMainR, CraterMainD);
            h -= Bowl(x, z, CraterB, CraterBR, CraterBD);
            h -= Bowl(x, z, CraterC, CraterCR, CraterCD);
            h = Mathf.Lerp(h, 2f, FlatFactor(Dist(x, z, BaseFlat), BaseFlatR));
            h = Mathf.Lerp(h, 2f, FlatFactor(Dist(x, z, LandingFlat), LandingFlatR));
            return h;
        }

        // 陨石坑剖面：坑内余弦碗形下沉，坑沿一圈低矮隆起
        static float Bowl(float x, float z, Vector2 c, float r, float d)
        {
            float dd = Dist(x, z, c);
            if (dd >= r * 1.9f) return 0f;
            if (dd < r) return d * (0.55f + 0.45f * Mathf.Cos(Mathf.PI * dd / r));
            return -d * 0.2f * Mathf.Sin(Mathf.PI * (dd - r) / (0.9f * r));
        }

        static float FlatFactor(float d, float r) => 1f - Mathf.SmoothStep(r * 0.65f, r, d);
        static float Dist(float x, float z, Vector2 c) => Vector2.Distance(new Vector2(x, z), c);

        static TerrainLayer BuildTerrainLayer()
        {
            const string texPath = "Assets/_Project/Art/Textures/T_MoonFlat.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGB24, false);
                for (int i = 0; i < 64; i++) tex.SetPixel(i % 8, i / 8, new Color32(134, 132, 138, 255));
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
            }
            var layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
                tileSize = new Vector2(30f, 30f),
            };
            AssetDatabase.CreateAsset(layer, "Assets/_Project/Art/Textures/TL_MoonGray.asset");
            return layer;
        }

        static GameObject BuildPlayer(Vector3 pos, Func<Vector3, float> ground)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            player.AddComponent<FirstPersonController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<HUDController>();
            player.transform.position = new Vector3(pos.x, ground(pos) + 1.05f, pos.z);

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Report.AppendLine("[玩家] 出生于广寒站中央；第一人称 + E 交互 + OnGUI 调试 HUD");
            return player;
        }

        static void BuildRover(Vector3 pos, Func<Vector3, float> ground, Transform player)
        {
            string path = ModelDir + "/阿波罗月球车.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject rover;
            float scale = 1f;
            if (prefab != null)
            {
                rover = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rover.transform.position = Vector3.zero;
                rover.transform.localScale = Vector3.one;
                Bounds b0 = LocalBounds(rover.transform);
                scale = 3.2f / Mathf.Max(Mathf.Max(b0.size.x, b0.size.z), 0.01f);
                rover.transform.localScale = Vector3.one * scale;
                Report.AppendLine($"[月球车] 阿波罗 FBX 包围盒 {b0.size:F1}m → 缩放 {scale:F3} 至 3.2m");
            }
            else
            {
                rover = BuildProceduralRover();
                Report.AppendLine("[月球车] 阿波罗 FBX 待瘦身（导入超时已摘除），改用白盒车身，脚本挂载完全一致");
            }
            rover.name = "Rover";
            Bounds b1 = LocalBounds(rover.transform);
            rover.transform.position = new Vector3(pos.x, ground(pos) - b1.min.y + 0.05f, pos.z);

            var box = rover.AddComponent<BoxCollider>();
            box.center = new Vector3(b1.center.x / scale, b1.center.y / scale, b1.center.z / scale);
            box.size = new Vector3(b1.size.x / scale, b1.size.y / scale, b1.size.z / scale);
            var rb = rover.AddComponent<Rigidbody>();
            rb.mass = 800f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rover.AddComponent<RoverController>();

            var camGo = new GameObject("RoverCamera");
            camGo.transform.SetParent(rover.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            var rc = camGo.AddComponent<RoverCamera>();
            rc.target = rover.transform;

            var enterExit = rover.AddComponent<VehicleEnterExit>();
            enterExit.player = player;
            enterExit.roverCamera = rc;

            PrefabUtility.SaveAsPrefabAsset(rover, "Assets/_Project/Prefabs/PF_Rover.prefab");
        }

        // 白盒月球车：模块二正式模型就位前的驾驶验证用车身
        static GameObject BuildProceduralRover()
        {
            var root = new GameObject("Rover");
            var bodyMat = LoadOrCreateMat("M_RoverBody", new Color(0.82f, 0.44f, 0.16f));
            var darkMat = LoadOrCreateMat("M_RoverDark", new Color(0.18f, 0.20f, 0.24f));

            void Part(PrimitiveType type, string name, Vector3 scale, Vector3 localPos, Material mat,
                bool keepCollider = false, Vector3? rot = null)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = name;
                p.transform.SetParent(root.transform, false);
                p.transform.localScale = scale;
                p.transform.localPosition = localPos;
                if (rot.HasValue) p.transform.localRotation = Quaternion.Euler(rot.Value);
                p.GetComponent<Renderer>().sharedMaterial = mat;
                if (!keepCollider)
                {
                    var col = p.GetComponent<Collider>();
                    if (col != null) UnityEngine.Object.DestroyImmediate(col);
                }
            }

            Part(PrimitiveType.Cube, "Body", new Vector3(1.6f, 0.45f, 2.4f), new Vector3(0f, 0.72f, 0f), bodyMat);
            Part(PrimitiveType.Cube, "Chassis", new Vector3(1.2f, 0.3f, 2.0f), new Vector3(0f, 0.45f, 0f), darkMat);
            Part(PrimitiveType.Cube, "Seat", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.1f, 0.35f), darkMat);
            Part(PrimitiveType.Cube, "AntennaMast", new Vector3(0.06f, 1.0f, 0.06f), new Vector3(-0.6f, 1.35f, -0.9f), darkMat);
            Part(PrimitiveType.Cube, "AntennaDish", new Vector3(0.5f, 0.06f, 0.5f), new Vector3(-0.6f, 1.85f, -0.9f), darkMat);
            for (int i = 0; i < 4; i++)
            {
                Part(PrimitiveType.Cylinder, "Wheel_" + (i + 1),
                    new Vector3(0.72f, 0.12f, 0.72f),
                    new Vector3(i % 2 == 0 ? -0.95f : 0.95f, 0.36f, i < 2 ? 0.85f : -0.85f),
                    darkMat, false, new Vector3(0f, 0f, 90f));
            }
            return root;
        }

        static Material LoadOrCreateMat(string name, Color color)
        {
            string p = $"Assets/_Project/Art/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) return m;
            Directory.CreateDirectory("Assets/_Project/Art/Materials");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static GameObject PlaceModel(string path, float targetMax, Vector3 pos,
            Func<Vector3, float> ground, string name, bool addCollider)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[WhiteboxBuilder] 找不到 " + path);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;
            Bounds b0 = LocalBounds(go.transform);
            float s = targetMax / Mathf.Max(Mathf.Max(b0.size.x, b0.size.z), 0.01f);
            go.transform.localScale = Vector3.one * s;
            Bounds b1 = LocalBounds(go.transform);
            go.transform.position = new Vector3(pos.x, ground(pos) - b1.min.y, pos.z);
            if (addCollider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(b1.center.x / s, b1.center.y / s, b1.center.z / s);
                box.size = new Vector3(b1.size.x / s, b1.size.y / s, b1.size.z / s);
            }
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            Report.AppendLine($"[{name}] 包围盒 {b0.size:F1}m → 缩放 {s:F3} 后 {b1.size:F1}m，落点 ({pos.x:F0},{pos.z:F0})");
            return go;
        }

        static void PlaceNpc(string fileId, string display, Vector3 pos, Func<Vector3, float> ground)
        {
            string path = $"{CharDir}/{fileId}.glb";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[WhiteboxBuilder] 找不到 " + path);
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "NPC_" + display;
            Bounds b = LocalBounds(go.transform);
            go.transform.position = new Vector3(pos.x, ground(pos) - b.min.y, pos.z);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = BuildNpcController(fileId, path);
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Report.AppendLine($"[NPC] {display} @ ({pos.x:F0},{pos.z:F0})，idle 循环");
        }

        static RuntimeAnimatorController BuildNpcController(string fileId, string glbPath)
        {
            string ctrlPath = $"Assets/_Project/Art/Animations/AC_{fileId}_Idle.controller";
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
            if (existing != null) return existing;

            AnimationClip idle = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glbPath))
                if (o is AnimationClip c && c.name.ToLower().Contains("idle")) { idle = c; break; }
            if (idle == null)
            {
                Debug.LogWarning("[WhiteboxBuilder] " + glbPath + " 中未找到 idle 动画");
                return null;
            }
            TrySetLoop(idle);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddMotion(idle);
            return ctrl;
        }

        static void TrySetLoop(AnimationClip clip)
        {
            try
            {
                var s = AnimationUtility.GetAnimationClipSettings(clip);
                s.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, s);
                EditorUtility.SetDirty(clip);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WhiteboxBuilder] 设置动画循环失败: " + e.Message);
            }
        }

        static void BuildSampleRocks(Func<Vector3, float> ground)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MoonMatPath);
            Vector3[] pts =
            {
                new Vector3(72f, 0f, 132f), new Vector3(88f, 0f, 148f), new Vector3(76f, 0f, 154f),
            };
            string[] tags = { "A", "B", "C" };
            for (int i = 0; i < pts.Length; i++)
            {
                var marker = new GameObject("SamplePoint_" + tags[i]);
                marker.transform.position = pts[i];
                var p = pts[i];

                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "SampleRock_" + tags[i];
                rock.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);
                rock.transform.position = new Vector3(p.x, ground(p) + 0.25f, p.z);
                rock.transform.rotation = Quaternion.Euler(6f * i, 40f * i, 5f * i);
                if (mat != null) rock.GetComponent<Renderer>().sharedMaterial = mat;
                rock.AddComponent<SampleRock>().prompt = $"按 E 采集月壤样本（{i + 1}/3）";
            }
            Report.AppendLine("[采样] 3 号坑内 3 个采样点 + 可交互样本石");
        }

        static void BuildRockField(Func<Vector3, float> ground)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MoonMatPath);
            var rng = new System.Random(20261115);
            int placed = 0, tries = 0;
            while (placed < 26 && tries < 200)
            {
                tries++;
                float x = 150f + (float)(rng.NextDouble() * 2 - 1) * 45f;
                float z = -80f + (float)(rng.NextDouble() * 2 - 1) * 45f;
                if (Dist(x, z, LandingFlat) < LandingFlatR + 8f) continue;
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"Rock_{placed + 1:00}";
                float sx = 0.5f + (float)rng.NextDouble() * 2.5f;
                float sy = 0.35f + (float)rng.NextDouble() * 1.05f;
                rock.transform.localScale = new Vector3(sx, sy, sx * (0.7f + (float)rng.NextDouble() * 0.6f));
                var p = new Vector3(x, 0f, z);
                rock.transform.position = new Vector3(x, ground(p) + sy * 0.4f, z);
                rock.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 10f, rng.Next(0, 180), (float)rng.NextDouble() * 8f);
                if (mat != null) rock.GetComponent<Renderer>().sharedMaterial = mat;
                placed++;
            }
            Report.AppendLine($"[乱石区] 东侧 26 块带碰撞岩石（{placed} 块落位）");
        }

        static void BuildMinimap(Transform player)
        {
            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "RT_Minimap",
                antiAliasing = 4,
            };
            AssetDatabase.CreateAsset(rt, "Assets/_Project/UI/RT_Minimap.renderTexture");

            var camGo = new GameObject("MinimapCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 60f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.depth = -10f;
            cam.targetTexture = rt;
            var mm = camGo.AddComponent<MinimapController>();
            mm.target = player;
            Report.AppendLine("[小地图] 正交相机 512 RenderTexture，M 键切换缩放；界面呈现由模块四接入");
        }

        static void SetBuildScenes(string s4Path)
        {
            var list = new List<EditorBuildSettingsScene>();
            const string showcase = "Assets/_Project/Cinematics/Module4/Demo/Module4Showcase.unity";
            if (File.Exists(showcase)) list.Add(new EditorBuildSettingsScene(showcase, true));
            list.Add(new EditorBuildSettingsScene(s4Path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static Bounds LocalBounds(Transform root)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
