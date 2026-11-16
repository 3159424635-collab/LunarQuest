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

        [MenuItem("工具/月征/搭建 S1 发射场")]
        public static void BuildLaunchSite()
        {
            Report.Clear();
            EnsureFolders();
            FixKenneyMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 地球白天氛围：程序化蓝天 + 暖阳光
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(62f, 25f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            const string skyPath = "Assets/_Project/Art/Materials/Skybox_Earth.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Skybox_Earth" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;

            // 草原地面 800×800
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            ground.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMat("M_EarthGround", new Color(0.42f, 0.50f, 0.34f));
            float Flat(Vector3 p) => 0f;

            // 发射台基座
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "LaunchPad";
            pad.transform.localScale = new Vector3(36f, 0.5f, 36f);
            pad.transform.position = new Vector3(0f, 0.5f, -46f);
            pad.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMat("M_Concrete", new Color(0.62f, 0.62f, 0.60f));

            // 土星五号（竖直构件按高度归一化至 110m）
            PlaceModelHeight(ModelDir + "/土星五号火箭.fbx", 110f, new Vector3(0f, 0.9f, -46f), Flat, "SaturnV", true);

            // 发射塔（Kenney 桁架两节）+ 安全区围栏
            PlaceKenneyHeight("metalStructure", 24f, new Vector3(13f, 0f, -46f), Flat, "TowerA", false);
            PlaceKenneyHeight("metalStructure", 24f, new Vector3(13f, 0f, -54f), Flat, "TowerB", false);
            (Vector3 p, float r)[] fences =
            {
                (new Vector3(0f, 0f, -22f), 180f), (new Vector3(0f, 0f, -70f), 0f),
                (new Vector3(26f, 0f, -46f), 270f), (new Vector3(-26f, 0f, -46f), 90f),
            };
            for (int i = 0; i < fences.Length; i++)
                PlaceKenney("metalFence", 24f, fences[i].p, Flat, "PadFence_" + i, false, new Vector3(0f, fences[i].r, 0f));

            // 指挥中心与控制台
            PlaceKenney("buildingOpen", 12f, new Vector3(38f, 0f, -20f), Flat, "ControlCenter", true, new Vector3(0f, -90f, 0f));
            PlaceKenney("console", 2.6f, new Vector3(35f, 0f, -17f), Flat, "Console_A", false, null);
            PlaceKenney("console", 2.6f, new Vector3(41f, 0f, -17f), Flat, "Console_B", false, null);

            // 林岚与玩家
            PlaceNpc("ground_flight_controller", "林岚_飞控", new Vector3(31f, 0f, -11f), Flat,
                "欢迎加入任务。跟我来，先熟悉操作，再去看发射。");
            var player = BuildPlayer(new Vector3(35f, 0f, -7f), Flat);
            player.GetComponent<HUDController>().SetQuest("走近飞控林岚按 E 交谈，完成发射前检查");

            const string scenePath = "Assets/_Project/Scenes/S1_LaunchSite.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            RenderPreviewsAt("s1", new[]
            {
                (new Vector3(75f, 32f, 2f), new Vector3(0f, 45f, -46f)),   // 火箭全景
                (new Vector3(35f, 4f, -2f), new Vector3(8f, 18f, -40f)),    // 玩家视角望向火箭
            });
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/s1_report.txt", Report.ToString());
            Debug.Log("[WhiteboxBuilder] S1 发射场搭建完成\n" + Report);
        }

        [MenuItem("工具/月征/搭建全部场景")]
        public static void BuildEverything()
        {
            BuildLaunchSite();
            BuildAll();
        }

        // 按高度归一化摆放（竖直构件：火箭、塔架等）
        static GameObject PlaceModelHeight(string path, float targetHeight, Vector3 pos,
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
            float s = targetHeight / Mathf.Max(b0.size.y, 0.01f);
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
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            Report.AppendLine($"[{name}] 高度归一 {b0.size:F1}m ×{s:F3} → {b1.size:F1}m，落点 ({pos.x:F0},{pos.z:F0})");
            return go;
        }

        [MenuItem("工具/月征/搭建 S4 白盒场景")]
        public static void BuildAll()
        {
            Report.Clear();
            EnsureFolders();
            FixKenneyMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildSkybox();
            // 月面物理：全局重力设为 1/6（月球车与掉落物一致，同时大幅降低摩擦拖滞）
            Physics.gravity = new Vector3(0f, -9.81f / 6f, 0f);
            Terrain terrain = BuildTerrain();
            float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

            var player = BuildPlayer(new Vector3(0f, 0f, 150f), Ground);
            BuildRover(new Vector3(10f, 0f, 168f), Ground, player.transform);
            BuildKenneyBase(Ground);
            BuildExhibitHall(Ground);
            PlaceNpc("base_coordinator", "周航_协调员", new Vector3(2f, 0f, 171f), Ground, "去 3 号陨石坑，采三份月壤样本回来交给我。");
            PlaceNpc("lunar_scientist", "许青_科学家", new Vector3(-9f, 0f, 162f), Ground, "样本先放我这里，参观完科普厅再走。");
            PlaceNpc("maintenance_technician", "陈越_维护员", new Vector3(14f, 0f, 166f), Ground, "车就在外面，月面路滑，慢点开。");
            BuildSampleRocks(Ground);
            BuildRockField(Ground);
            BuildMinimap(player.transform);

            player.GetComponent<HUDController>().SetQuest("E 开门/交谈/查看展品 · F 上下月球车 · V 切换视角");

            const string scenePath = "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            SetBuildScenes(scenePath);
            RenderPreviews();
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
            RenderSettings.ambientLight = new Color(0.13f, 0.14f, 0.18f); // 月面夜空环境光
        }

        // 星空天空盒：Poly Haven CC0 纯星空全景图（无地面）
        static void BuildSkybox()
        {
            const string texPath = "Assets/_Project/Art/Textures/Stars_PureSky_2k.hdr";
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[WhiteboxBuilder] 找不到星空贴图 " + texPath);
                return;
            }
            const string matPath = "Assets/_Project/Art/Materials/Skybox_Stars.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic")) { name = "Skybox_Stars" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Exposure", 0.85f);
            RenderSettings.skybox = mat;
        }

        // Kenney 包自带材质是 Built-in 管线 shader，URP 下渲染为洋红色——统一转换为 URP Lit
        static void FixKenneyMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Kenney Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == shader) continue;
                var tex = mat.HasProperty("_MainTex") ? mat.mainTexture : null;
                var col = mat.HasProperty("_Color") ? mat.color : Color.white;
                mat.shader = shader;
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", col);
                EditorUtility.SetDirty(mat);
                fixedCount++;
            }
            if (fixedCount > 0) AssetDatabase.SaveAssets();
            Report.AppendLine($"[材质] Kenney → URP 转换 {fixedCount} 个（已转换的自动跳过）");
        }

        const string KenneyDir = "Assets/Kenney Assets/Space Kit/Models";
        const string StationDir = "Assets/Kenney Assets/Space Station Kit/Models/FBX format";

        static GameObject PlaceStation(string model, float targetMax, Vector3 pos,
            Func<Vector3, float> ground, string name, bool addCollider, Vector3? rotEuler = null)
        {
            return PlaceModel($"{StationDir}/{model}.fbx", targetMax, pos, ground, name, addCollider, false, rotEuler);
        }

        // 低摩擦物理材质：月球车 BoxCollider 在地形上滑行而非蹭地卡死
        static PhysicMaterial LoadOrCreatePhysicMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/PM_Slip.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicMaterial("PM_Slip")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounciness = 0f,
            };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        // 展览区并入穹顶大厅：滑动门装在穹顶南门，展台环绕全息台。
        // （原绿色指挥室内景已整体拆除——昏暗拥挤，观感不合格）
        static void BuildExhibitHall(Func<Vector3, float> ground)
        {
            const float cx = -4f, cz = 178f;      // 穹顶中心
            float y0 = ground(new Vector3(cx, 0f, cz));

            // 滑动门：穹顶南门洞（z = cz-7.5）
            var panelMat = LoadOrCreateMat("M_DoorPanel", new Color(0.80f, 0.45f, 0.15f));
            var doorRoot = new GameObject("DomeDoor");
            doorRoot.transform.position = new Vector3(cx, y0, cz - 7.35f);
            GameObject Panel(string n, float xOff)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n;
                p.transform.SetParent(doorRoot.transform, false);
                p.transform.localPosition = new Vector3(xOff, 1.3f, 0f);
                p.transform.localScale = new Vector3(1.45f, 2.6f, 0.14f);
                p.GetComponent<Renderer>().sharedMaterial = panelMat;
                return p;
            }
            var pl = Panel("PanelL", -0.75f);
            var pr = Panel("PanelR", 0.75f);
            var sd = doorRoot.AddComponent<SlidingDoor>();
            sd.panelL = pl.transform;
            sd.panelR = pr.transform;
            sd.autoRange = 4.5f;
            sd.prompt = "按 E 开门（靠近自动开启）";

            // 气闸大门：玻璃罩南口 6m 自动感应双扇
            var gateRoot = new GameObject("AirlockGate");
            gateRoot.transform.position = new Vector3(-7.7f, y0, 154.0f);
            GameObject GatePanel(string n, float xOff)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n;
                p.transform.SetParent(gateRoot.transform, false);
                p.transform.localPosition = new Vector3(xOff, 2.1f, 0f);
                p.transform.localScale = new Vector3(3.1f, 4.2f, 0.3f);
                p.GetComponent<Renderer>().sharedMaterial = panelMat;
                return p;
            }
            var gl = GatePanel("GateL", -1.6f);
            var gr = GatePanel("GateR", 1.6f);
            var gate = gateRoot.AddComponent<SlidingDoor>();
            gate.panelL = gl.transform;
            gate.panelR = gr.transform;
            gate.openOffset = 3.0f;
            gate.speed = 1.6f;
            gate.autoRange = 8f;
            gate.prompt = "气闸门（靠近自动开启）";


            // 展台 ×3 环绕全息台（避开南门/西走廊/北走廊三个方向）
            PlaceStand(0, new Vector3(-1f, 0f, 181.5f), y0,
                "阿波罗月球车（LRV）",
                "1971–1972 年阿波罗 15/16/17 号任务使用，电池驱动四轮月面车，" +
                "月面最高时速约 18 公里，三台车累计行驶约 90 公里。",
                ModelDir + "/SM_Rover_Apollo.glb", 1.7f);
            PlaceStand(1, new Vector3(-1f, 0f, 174.8f), y0,
                "玉兔二号月球车（占位模型）",
                "嫦娥四号任务 2019 年实现人类首次月球背面软着陆巡视，" +
                "玉兔二号设计寿命 3 个月，实际工作超过 5 年。",
                KenneyDir + "/spaceCraft1.fbx", 1.3f);
            PlaceStand(2, new Vector3(-8.4f, 0f, 180.2f), y0,
                "嫦娥五号返回舱（占位模型）",
                "2020 年嫦娥五号完成我国首次地外天体采样返回，" +
                "带回月壤样品 1731 克，本作 HUD 数据卡同款数字。",
                KenneyDir + "/spaceCraft5.fbx", 1.3f);

            Report.AppendLine("[展览区] 并入穹顶大厅：南门滑动门 + 展台×3 环绕全息台；绿色指挥室内景已拆除");
        }

        static void PlaceStand(int i, Vector3 pos, float baseY, string title, string desc,
            string miniPath, float miniSize)
        {
            var ped = PlaceStation("table-display", 1.8f, pos, _ => baseY, $"ExhibitStand_{i}", true, null);
            if (ped == null) return;
            Bounds pb = LocalBounds(ped.transform);
            float topY = pb.max.y + 0.05f;
            if (!string.IsNullOrEmpty(miniPath))
                PlaceModel(miniPath, miniSize, pos, _ => topY, $"ExhibitMini_{i}", false, false);

            var screenMat = LoadOrCreateMat("M_QRScreen", new Color(0.92f, 0.92f, 0.88f));
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = $"QRScreen_{i}";
            screen.transform.position = new Vector3(pos.x, baseY + 1.6f, pos.z - 0.6f);
            screen.transform.localScale = new Vector3(0.7f, 0.55f, 0.06f);
            screen.GetComponent<Renderer>().sharedMaterial = screenMat;
            UnityEngine.Object.DestroyImmediate(screen.GetComponent<BoxCollider>());

            var stand = ped.AddComponent<ExhibitStand>();
            stand.exhibitTitle = title;
            stand.exhibitDesc = desc;
            stand.prompt = $"按 E 查看：{title}";
        }

        // 广寒站主体：自建参数化基地（Blender 生成，米制原尺寸，穹顶+双舱段+走廊可进入）
        static void BuildKenneyBase(Func<Vector3, float> ground)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Art/Models/Sketchfab/SM_MoonBase_Custom.glb");
            if (prefab == null)
            {
                Debug.LogError("[WhiteboxBuilder] 找不到自建基地 SM_MoonBase_Custom.glb");
                return;
            }
            var baseGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            baseGo.name = "MoonBase_Custom";
            Bounds b = LocalBounds(baseGo.transform);
            baseGo.transform.position = new Vector3(-4f, ground(new Vector3(-4f, 0f, 178f)) - b.min.y, 178f);
            // 全网格碰撞：墙体/地板/门洞均为真实几何，MeshCollider 直接可用
            int colliders = 0;
            foreach (var mf in baseGo.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<MeshCollider>() == null && mf.sharedMesh != null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    colliders++;
                }
            }
            Report.AppendLine($"[基地] 自建参数化广寒站（穹顶+双舱段+走廊，73 构件，米制）落位 (-4,178)，网格碰撞 {colliders} 处");

            // 穹顶内景：成品科幻家具（控制台/椅子）+ 内部光源
            float by = ground(new Vector3(-4f, 0f, 178f));
            PlaceStation("computer-wide", 2.6f, new Vector3(-8.5f, 0f, 174.5f), ground, "DomeConsole_1", true, new Vector3(0f, 45f, 0f));
            PlaceStation("computer-wide", 2.6f, new Vector3(4.5f, 0f, 173f), ground, "DomeConsole_2", true, new Vector3(0f, 135f, 0f));
            PlaceStation("computer-wide", 2.6f, new Vector3(8f, 0f, 181f), ground, "DomeConsole_3", true, new Vector3(0f, 225f, 0f));
            PlaceStation("chair", 1.0f, new Vector3(-7.2f, 0f, 173.2f), ground, "DomeChair_1", false, new Vector3(0f, 45f, 0f));
            PlaceStation("chair", 1.0f, new Vector3(3.4f, 0f, 171.8f), ground, "DomeChair_2", false, new Vector3(0f, 135f, 0f));
            var domeLight = new GameObject("DomeInteriorLight").AddComponent<Light>();
            domeLight.type = LightType.Point;
            domeLight.intensity = 2.6f;
            domeLight.range = 18f;
            domeLight.color = new Color(1f, 0.98f, 0.92f);
            domeLight.transform.position = new Vector3(-4f, by + 4.9f, 178f);
            var domeLight2 = new GameObject("DomeInteriorLight2").AddComponent<Light>();
            domeLight2.type = LightType.Point;
            domeLight2.intensity = 1.4f;
            domeLight2.range = 12f;
            domeLight2.color = new Color(0.95f, 0.97f, 1f);
            domeLight2.transform.position = new Vector3(-4f, by + 2.2f, 178f);
            Report.AppendLine("[基地] 穹顶内景：控制台×3+椅子×2（Kenney 成品件）+ 暖光点光源");
        }

        // 基地原材质同为 Built-in 管线：按零件名映射到 URP 平色材质
        static void RecolorByPartName(GameObject go)
        {
            if (go == null) return;
            var white = LoadOrCreateMat("M_HullWhite", new Color(0.88f, 0.89f, 0.92f));
            var grey = LoadOrCreateMat("M_HullGrey", new Color(0.55f, 0.57f, 0.60f));
            var orange = LoadOrCreateMat("M_AccentOrange", new Color(0.85f, 0.45f, 0.16f));
            var dark = LoadOrCreateMat("M_MetalDark", new Color(0.18f, 0.20f, 0.24f));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    mats[i] = n.Contains("white") || n.Contains("hull") && !n.Contains("grey") ? white
                        : n.Contains("grey") || n.Contains("gray") ? grey
                        : n.Contains("orange") || n.Contains("accent") || n.Contains("beacon") ? orange
                        : dark;
                }
                r.sharedMaterials = mats;
            }
        }

        // 按高度归一化：用于竖直造型的构件（塔、天线杆等），避免按横向归一化时高度失控
        static GameObject PlaceKenneyHeight(string model, float targetHeight, Vector3 pos,
            Func<Vector3, float> ground, string name, bool addCollider)
        {
            string path = $"{KenneyDir}/{model}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[WhiteboxBuilder] 找不到 " + path);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = Vector3.zero;
            Bounds b0 = LocalBounds(go.transform);
            float s = targetHeight / Mathf.Max(b0.size.y, 0.01f);
            go.transform.localScale = Vector3.one * s;
            Bounds b1 = LocalBounds(go.transform);
            go.transform.position = new Vector3(pos.x, ground(pos) - b1.min.y, pos.z);
            if (addCollider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(b1.center.x / s, b1.center.y / s, b1.center.z / s);
                box.size = new Vector3(b1.size.x / s, b1.size.y / s, b1.size.z / s);
            }
            Report.AppendLine($"[{name}] 高度归一 {b0.size:F1}m ×{s:F2} → {b1.size:F1}m，落点 ({pos.x:F0},{pos.z:F0})");
            return go;
        }

        static GameObject PlaceKenney(string model, float targetMax, Vector3 pos,
            Func<Vector3, float> ground, string name, bool addCollider, Vector3? rotEuler)
        {
            return PlaceModel($"{KenneyDir}/{model}.fbx", targetMax, pos, ground, name, addCollider, false, rotEuler);
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
            const string terrainAsset = "Assets/_Project/Scenes/S4_TerrainData.asset";
            if (File.Exists(terrainAsset))
            {
                File.Delete(terrainAsset);
                File.Delete(terrainAsset + ".meta");
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateAsset(td, terrainAsset);
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
            const string layerPath = "Assets/_Project/Art/Textures/TL_MoonGray.asset";
            var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existingLayer != null) return existingLayer;

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
            AssetDatabase.CreateAsset(layer, layerPath);
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

            // 第三人称身体：第一人称隐藏，V 键切换显示
            var bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "/player_astronaut.glb");
            if (bodyPrefab != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyPrefab);
                body.name = "AstronautBody";
                body.transform.SetParent(player.transform, false);
                Bounds bb = LocalBounds(body.transform);
                float bodyScale = 1.8f / Mathf.Max(bb.size.y, 0.01f);
                body.transform.localScale = Vector3.one * bodyScale;
                bb = LocalBounds(body.transform);
                body.transform.localPosition += Vector3.up * ((player.transform.position.y - 0.9f) - bb.min.y);
                body.SetActive(false);
                player.GetComponent<FirstPersonController>().thirdPersonBody = body;
            }
            Report.AppendLine("[玩家] 出生于广寒站中央；第一人称 + E 交互 + OnGUI 调试 HUD + V 第三人称");
            return player;
        }

        static void BuildRover(Vector3 pos, Func<Vector3, float> ground, Transform player)
        {
            // 优先用经 Blender 转换瘦身的阿波罗真模型（SM_Rover_Apollo.glb），其次原始 FBX，最后白盒
            string path = ModelDir + "/SM_Rover_Apollo.glb";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                path = ModelDir + "/阿波罗月球车.fbx";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
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
                Report.AppendLine($"[月球车] 阿波罗真模型 包围盒 {b0.size:F1}m → 缩放 {scale:F3} 至 3.2m");
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
            box.sharedMaterial = LoadOrCreatePhysicMaterial();
            var rb = rover.AddComponent<Rigidbody>();
            rb.mass = 800f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rover.AddComponent<RoverController>();

            var camGo = new GameObject("RoverCamera");
            camGo.transform.SetParent(rover.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            camGo.tag = "MainCamera";
            var rc = camGo.AddComponent<RoverCamera>();
            rc.target = rover.transform;

            var enterExit = rover.AddComponent<VehicleEnterExit>();
            enterExit.player = player;
            enterExit.roverCamera = rc;

            var door = rover.AddComponent<VehicleDoor>();
            door.vehicle = enterExit;
            door.prompt = "按 F 或 E 上下车";

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
            Func<Vector3, float> ground, string name, bool addCollider, bool stripGround = false, Vector3? rotEuler = null)
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
            if (stripGround)
            {
                // 摘除内嵌的巨大地台/地形片（横向超大而厚度小的渲染器），避免整体按最大边缩放时建筑被压扁。
                // 阈值放宽为横向>150m 且厚度<8m，覆盖单体大地台与拼接地形两种情况。
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    float horiz = Mathf.Max(r.bounds.size.x, r.bounds.size.z);
                    if (horiz > 150f && r.bounds.size.y < 8f)
                    {
                        Report.AppendLine($"[{name}] 摘除内嵌地台: {r.name} ({r.bounds.size.x:F0}x{r.bounds.size.y:F0}x{r.bounds.size.z:F0})");
                        r.gameObject.SetActive(false);
                    }
                }
            }
            Bounds b0 = LocalBounds(go.transform);
            float s = targetMax / Mathf.Max(Mathf.Max(b0.size.x, b0.size.z), 0.01f);
            go.transform.localScale = Vector3.one * s;
            Bounds b1 = LocalBounds(go.transform);
            go.transform.position = new Vector3(pos.x, ground(pos) - b1.min.y, pos.z);
            if (rotEuler.HasValue) go.transform.rotation = Quaternion.Euler(rotEuler.Value);
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

        static void PlaceNpc(string fileId, string display, Vector3 pos, Func<Vector3, float> ground, string line)
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
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var stub = go.AddComponent<NpcDialogueStub>();
            stub.animator = anim;
            stub.line = line;
            stub.prompt = $"与{display.Split('_')[0]}交谈（E）";
            Report.AppendLine($"[NPC] {display} @ ({pos.x:F0},{pos.z:F0})，idle/talk，E 可交互");
        }

        static RuntimeAnimatorController BuildNpcController(string fileId, string glbPath)
        {
            string ctrlPath = $"Assets/_Project/Art/Animations/AC_{fileId}_Idle.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            AnimationClip idle = null, talk = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glbPath))
            {
                if (!(o is AnimationClip c)) continue;
                string n = c.name.ToLower();
                if (idle == null && n.Contains("idle")) idle = c;
                else if (talk == null && n.Contains("talk")) talk = c;
            }
            if (idle == null)
            {
                Debug.LogWarning("[WhiteboxBuilder] " + glbPath + " 中未找到 idle 动画");
                return ctrl;
            }
            TrySetLoop(idle);

            // 确保控制器里有命名规范的 idle/talk 状态（重建时对旧控制器补齐）
            var sm = ctrl.layers[0].stateMachine;
            void EnsureState(string name, AnimationClip clip)
            {
                if (clip == null) return;
                foreach (var s in sm.states)
                    if (s.state.name == name) return;
                var st = sm.AddState(name);
                st.motion = clip;
                if (name == "idle" && sm.defaultState == null) sm.defaultState = st;
            }
            EnsureState("idle", idle);
            EnsureState("talk", talk);
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

                var rock = PlaceKenney("rocksSmall", 0.9f, p, ground, "SampleRock_" + tags[i], true,
                    new Vector3(0f, 37f * i, 0f));
                if (rock != null)
                {
                    rock.AddComponent<SampleRock>().prompt = $"按 E 采集月壤样本（{i + 1}/3）";
                }
            }
            Report.AppendLine("[采样] 3 号坑内 3 个采样点 + 可交互样本石（Kenney 岩石模型）");
        }

        static readonly string[] RockVariants = { "rocks", "rocksSmall", "rocksTall", "rocksOre", "meteorHalf" };

        static void BuildRockField(Func<Vector3, float> ground)
        {
            var rng = new System.Random(20261115);
            int placed = 0, tries = 0;
            while (placed < 26 && tries < 200)
            {
                tries++;
                float x = 150f + (float)(rng.NextDouble() * 2 - 1) * 45f;
                float z = -80f + (float)(rng.NextDouble() * 2 - 1) * 45f;
                if (Dist(x, z, LandingFlat) < LandingFlatR + 8f) continue;
                string variant = RockVariants[rng.Next(RockVariants.Length)];
                float size = 1.2f + (float)rng.NextDouble() * 2.3f;
                var p = new Vector3(x, 0f, z);
                PlaceKenney(variant, size, p, ground, $"Rock_{placed + 1:00}", true,
                    new Vector3(0f, rng.Next(0, 360), 0f));
                placed++;
            }
            Report.AppendLine($"[乱石区] 东侧 26 块带碰撞岩石（{placed} 块落位，Kenney 变体）");
        }

        static void BuildMinimap(Transform player)
        {
            const string rtPath = "Assets/_Project/UI/RT_Minimap.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RT_Minimap",
                    antiAliasing = 4,
                };
                AssetDatabase.CreateAsset(rt, rtPath);
            }

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

        // 构建产物预览：S4 基地鸟瞰 + 出生点视角
        static void RenderPreviews()
        {
            RenderPreviewsAt("scene", new[]
            {
                (new Vector3(-4f, 55f, 120f), new Vector3(-4f, 2f, 172f)),       // 基地鸟瞰
                (new Vector3(0f, 8f, 140f), new Vector3(-4f, 3f, 170f)),         // 出生点望向基地
                (new Vector3(-4f, 2.2f, 148f), new Vector3(-4f, 1.5f, 162f)),    // 展览舱门口
                (new Vector3(-4f, 3.4f, 173.5f), new Vector3(-4f, 2.4f, 179.5f)),// 穹顶内部
                (new Vector3(-4f, 2.6f, 168.5f), new Vector3(-4f, 2.4f, 176f)),  // 穿南门看大厅
                (new Vector3(-7.7f, 6f, 146.5f), new Vector3(-7.7f, 3f, 158f)),  // 气闸门外望罩内
            });
        }

        static void RenderPreviewsAt(string tag, (Vector3 pos, Vector3 look)[] views)
        {
            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 55f;
            cam.targetTexture = rt;
            for (int i = 0; i < views.Length; i++)
            {
                cam.transform.position = views[i].pos;
                cam.transform.LookAt(views[i].look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{tag}_preview_{i}.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        static void SetBuildScenes(string s4Path)
        {
            var list = new List<EditorBuildSettingsScene>();
            const string showcase = "Assets/_Project/Cinematics/Module4/Demo/Module4Showcase.unity";
            if (File.Exists(showcase)) list.Add(new EditorBuildSettingsScene(showcase, true));
            const string s1 = "Assets/_Project/Scenes/S1_LaunchSite.unity";
            if (File.Exists(s1)) list.Add(new EditorBuildSettingsScene(s1, true));
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
