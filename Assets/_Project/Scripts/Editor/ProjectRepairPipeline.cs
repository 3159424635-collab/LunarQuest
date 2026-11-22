using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LunarQuest.Module4;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest.EditorTools
{
    /// <summary>重建受影响场景并执行课程项目关键链路的静态验收。</summary>
    public static class ProjectRepairPipeline
    {
        [MenuItem("工具/月征/修复并验证项目")]
        public static void Run()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            // Unity 的未保存 Scene 会继承当前场景的序列化格式；先打开已是 YAML 的 S4。
            EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            WhiteboxBuilder.BuildLaunchSite();
            EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            LandingSceneBuilder.Build();
            RepairMoonNpcColliders();
            ARSceneBuilder.Build();
            BuildSceneCatalog.ConfigureProjectScenes();
            AssetDatabase.SaveAssets();
            ForceTextSceneSerialization();
            AssetDatabase.Refresh();
            Validate();
        }

        [MenuItem("工具/月征/修复场景视觉一致性")]
        public static void RepairVisualConsistency()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            WhiteboxBuilder.BuildLaunchSite();
            LandingSceneBuilder.Build();
            WhiteboxBuilder.BuildAll();
            RepairShowcaseHudScale();
            BuildSceneCatalog.ConfigureProjectScenes();
            AssetDatabase.SaveAssets();
            ForceTextSceneSerialization();
            AssetDatabase.Refresh();
            Validate();
        }

        [MenuItem("工具/月征/修复 S2 HUD 缩放")]
        public static void RepairShowcaseHudScale()
        {
            const string prefabPath = "Assets/_Project/UI/Prefabs/Module4HUD.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                prefabRoot.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // 预制体编辑会临时切换内部场景，所以最后再修复并保存正式场景实例。
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.ShowcaseScene, OpenSceneMode.Single);
            Module4HudView sceneHud = FindAll<Module4HudView>(scene).FirstOrDefault();
            if (sceneHud != null)
            {
                var transformData = new SerializedObject(sceneHud.GetComponent<RectTransform>());
                transformData.FindProperty("m_LocalScale").vector3Value = Vector3.one;
                transformData.ApplyModifiedPropertiesWithoutUndo();
                sceneHud.transform.localScale = Vector3.one;
                EditorUtility.SetDirty(sceneHud.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[ProjectRepairPipeline] S2 HUD 场景实例与预制体缩放已恢复为 1。");
        }

        static void ForceTextSceneSerialization()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            string[] paths = { BuildSceneCatalog.LaunchScene, BuildSceneCatalog.LandingScene };
            AssetDatabase.ForceReserializeAssets(paths,
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            Debug.Log("[ProjectRepairPipeline] S1/S3 已强制转换为文本序列化。");
        }

        static void RepairMoonNpcColliders()
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            int repaired = 0;
            foreach (NpcInteractor npc in FindAll<NpcInteractor>(scene))
            {
                int before = npc.GetComponentsInChildren<Collider>().Length;
                npc.EnsureInteractionCollider();
                if (npc.GetComponentsInChildren<Collider>().Length > before) repaired++;
                EditorUtility.SetDirty(npc);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ProjectRepairPipeline] S4 NPC 新增交互碰撞体: " + repaired);
        }

        public static void Validate()
        {
            var report = new StringBuilder();
            var failures = new List<string>();

            ValidateBuildScenes(report, failures);
            ValidateLaunchScene(report, failures);
            ValidateShowcaseScene(report, failures);
            ValidateLandingScene(report, failures);
            ValidateMoonScene(report, failures);
            ValidateARScene(report, failures);
            ValidateDialogue(report, failures);
            ValidateSceneSerialization(report, failures);

            Directory.CreateDirectory("Logs");
            report.AppendLine(failures.Count == 0 ? "RESULT=PASS" : "RESULT=FAIL");
            foreach (string failure in failures) report.AppendLine("FAIL: " + failure);
            File.WriteAllText("Logs/project_validation.txt", report.ToString());
            Debug.Log("[ProjectRepairPipeline]\n" + report);

            if (failures.Count > 0)
                throw new Exception("项目验证失败：" + string.Join("；", failures));
        }

        static void ValidateBuildScenes(StringBuilder report, List<string> failures)
        {
            var scenes = EditorBuildSettings.scenes;
            string[] expected =
            {
                BuildSceneCatalog.LaunchScene,
                BuildSceneCatalog.ShowcaseScene,
                BuildSceneCatalog.LandingScene,
                BuildSceneCatalog.MoonScene,
                BuildSceneCatalog.ARScene,
            };
            bool orderOk = scenes.Length == expected.Length &&
                           scenes.Select(scene => scene.path).SequenceEqual(expected);
            bool flagsOk = scenes.Length == 5 && scenes.Take(4).All(scene => scene.enabled) && !scenes[4].enabled;
            Check(orderOk && flagsOk, "BUILD_SCENES", "构建场景顺序或启用状态不正确", report, failures);
        }

        static void ValidateLaunchScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.LaunchScene, OpenSceneMode.Single);
            GameObject player = FindAll<FirstPersonController>(scene).Select(c => c.gameObject).FirstOrDefault();
            NpcInteractor npc = FindAll<NpcInteractor>(scene)
                .FirstOrDefault(item => item.characterId == "ground_flight_controller");
            GameObject controlCenter = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "ControlCenter");
            GameObject ground = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "LaunchGroundInfrastructure");
            Terrain terrain = FindAll<Terrain>(scene).FirstOrDefault(item => item.name == "EarthTerrain");
            GameObject rocket = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "MissionRocket_LQ01");
            GameObject tower = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "LaunchServiceTower");
            GameObject vab = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "VAB");

            Check(player != null, "S1_PLAYER", "S1 缺少玩家", report, failures);
            Check(npc != null, "S1_NPC", "S1 缺少林岚", report, failures);
            Check(npc != null && ColliderOverlapsRenderer(npc),
                "S1_NPC_COLLIDER", "林岚的交互碰撞体没有覆盖角色模型", report, failures);
            Check(controlCenter != null && controlCenter.GetComponent<BoxCollider>() == null,
                "S1_OPEN_BUILDING", "指挥中心仍有封闭整栋建筑的根级碰撞体", report, failures);
            Check(player != null && npc != null && Vector3.Distance(player.transform.position, npc.transform.position) <= 6.5f,
                "S1_START_DISTANCE", "玩家与林岚出生距离过远", report, failures);
            Check(FindAll<QuestManager>(scene).Any(), "S1_QUEST", "S1 缺少任务管理器", report, failures);
            Check(ground != null && ground.transform.Find("LaunchApron") != null &&
                  ground.transform.Find("FlameTrench") != null && ground.transform.Find("CoastalSea") != null,
                "S1_ENGINEERED_GROUND", "S1 缺少发射坪、导流槽或滨海远景", report, failures);
            Check(ground != null && ground.transform.Find("TourRoute_Start") != null &&
                  ground.transform.Find("TourRoute_Tracking") != null &&
                  ground.transform.Find("TourRoute_Viewing") != null &&
                  ground.transform.Find("TourRoute_Rocket") != null,
                "S1_TOUR_ROADS", "S1 导览路线没有完整铺装", report, failures);
            Check(terrain != null && terrain.terrainData != null && terrain.terrainData.terrainLayers.Length >= 2,
                "S1_TERRAIN_LAYERS", "S1 地形仍缺少滨海土壤混合层", report, failures);
            float rocketHeight = RendererBounds(rocket).size.y;
            Check(rocket != null && rocketHeight >= 95f,
                "S1_MISSION_ROCKET", "S1 缺少与 S2 一致的 LQ-01 火箭或火箭尺寸异常", report, failures);
            Check(tower != null && RendererBounds(tower).size.y >= rocketHeight * 0.9f,
                "S1_SERVICE_TOWER", "S1 发射勤务塔与火箭高度不协调", report, failures);
            Check(vab != null && RendererBounds(vab).size.y >= 110f,
                "S1_VAB_SCALE", "S1 总装厂房相对火箭仍然过矮", report, failures);
        }

        static void ValidateShowcaseScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.ShowcaseScene, OpenSceneMode.Single);
            Module4HudView hud = FindAll<Module4HudView>(scene).FirstOrDefault();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/UI/Prefabs/Module4HUD.prefab");
            bool sceneScaleSaved = hud != null && SerializedTransformScaleIsOne(
                BuildSceneCatalog.ShowcaseScene, hud.GetComponent<RectTransform>());
            bool prefabScaleSaved = prefab != null && prefab.transform.localScale.sqrMagnitude > 0.5f;
            Check(hud != null && sceneScaleSaved && prefabScaleSaved,
                "S2_HUD_SCALE", "S2 HUD 根节点缩放为零或 HUD 缺失", report, failures);
        }

        static void ValidateLandingScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.LandingScene, OpenSceneMode.Single);
            ParticleSystem dust = FindAll<ParticleSystem>(scene).FirstOrDefault(item => item.name == "EngineDust");
            GameObject target = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "LandingTarget");
            ParticleSystemRenderer dustRenderer = dust != null ? dust.GetComponent<ParticleSystemRenderer>() : null;
            Check(dustRenderer != null && dustRenderer.sharedMaterial != null,
                "S3_DUST_MATERIAL", "S3 引擎扬尘缺少粒子材质", report, failures);
            Check(target != null && target.GetComponentsInChildren<LineRenderer>(true).Length >= 2 &&
                  target.GetComponentsInChildren<MeshRenderer>(true).Length == 0,
                "S3_TARGET_RING", "S3 着陆目标仍是实体色块或全息环缺失", report, failures);
        }

        static void ValidateMoonScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            var npcs = FindAll<NpcInteractor>(scene).ToArray();
            var exhibits = FindAll<ExhibitStand>(scene).ToArray();
            NpcInteractor technician = npcs.FirstOrDefault(npc => npc.characterId == "maintenance_technician");
            RoverController rover = FindAll<RoverController>(scene).FirstOrDefault();
            RoverSampleInteractor roverSample = rover != null ? rover.GetComponent<RoverSampleInteractor>() : null;
            Rigidbody roverBody = rover != null ? rover.GetComponent<Rigidbody>() : null;
            FirstPersonController player = FindAll<FirstPersonController>(scene).FirstOrDefault();
            FieldTabletNavigator tablet = FindAll<FieldTabletNavigator>(scene).FirstOrDefault();
            SampleRock[] sampleRocks = FindAll<SampleRock>(scene).ToArray();
            Terrain terrain = FindAll<Terrain>(scene).FirstOrDefault();
            TerrainLayer moonLayer = terrain != null && terrain.terrainData != null &&
                terrain.terrainData.terrainLayers.Length > 0
                    ? terrain.terrainData.terrainLayers[0]
                    : null;
            Animator playerAnimator = player != null && player.thirdPersonBody != null
                ? player.thirdPersonBody.GetComponentInChildren<Animator>(true)
                : null;
            Check(npcs.Length >= 3, "S4_NPCS", "S4 NPC 数量不足", report, failures);
            Check(npcs.All(ColliderOverlapsRenderer),
                "S4_NPC_COLLIDERS", "S4 存在未被交互碰撞体覆盖的 NPC", report, failures);
            Check(exhibits.Length >= 3, "S4_EXHIBITS", "S4 展品数量不足", report, failures);
            Check(exhibits.All(exhibit => exhibit.qrTexture != null),
                "S4_QR_REFERENCES", "S4 存在没有二维码引用的展品", report, failures);
            Check(exhibits.Any(exhibit => exhibit.exhibitTitle.Contains("阿波罗载人登月舱")),
                "S4_REAL_LANDER_EXHIBIT", "S4 尚未用 NASA 官方登月舱模型替换错误展品", report, failures);
            Check(exhibits.Any(exhibit => exhibit.exhibitTitle.Contains("月球勘测轨道器")),
                "S4_REAL_LRO_EXHIBIT", "S4 缺少 NASA 官方 LRO 真实展品", report, failures);
            bool technicianInPressurizedDome = technician != null &&
                Vector2.Distance(new Vector2(technician.transform.position.x, technician.transform.position.z),
                    new Vector2(-4f, 178f)) <= 15f;
            Check(technicianInPressurizedDome,
                "S4_TECHNICIAN_INSIDE", "未穿舱外服的陈越仍被放在月球基地外", report, failures);
            Check(rover != null && rover.name.Contains("SEV") && rover.maxSpeed <= 8f && rover.downForce <= 6f,
                "S4_SEV_ROVER", "S4 仍在使用阿波罗月球车或车辆参数不稳定", report, failures);
            Check(rover != null && RendererBounds(rover.gameObject).size.magnitude > 5f,
                "S4_SEV_MODEL", "S4 探测车缺少有效渲染模型", report, failures);
            Check(rover != null && roverBody != null && roverBody.constraints == RigidbodyConstraints.None &&
                  roverBody.isKinematic &&
                  rover.groundAlignSpeed >= 5f && rover.stallAssistDelay <= 1f,
                "S4_ROVER_TERRAIN", "S4 月球车越障参数或无人驾驶停驻状态不正确", report, failures);
            Check(roverSample != null && roverSample.interactRange >= 6f,
                "S4_ROVER_SAMPLE_INTERACTION", "S4 月球车缺少车载月壤采样交互", report, failures);
            Check(player != null && player.thirdPersonBody != null && playerAnimator != null &&
                  playerAnimator.runtimeAnimatorController != null,
                "S4_THIRD_PERSON_BODY", "S4 第三人称角色模型或动画控制器缺失", report, failures);
            Check(player != null && player.thirdPersonDistance >= 3.8f &&
                  player.thirdPersonHeight <= 1f && player.thirdPersonFocusHeight <= 0.5f,
                "S4_THIRD_PERSON_CAMERA", "S4 第三人称镜头仍可能只拍到角色头部", report, failures);
            var locomotionController = playerAnimator != null
                ? playerAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController
                : null;
            bool hasLocomotionStates = locomotionController != null && locomotionController.layers.Length > 0 &&
                locomotionController.layers[0].stateMachine.states.Any(state => state.state.name == "idle" && state.state.motion != null) &&
                locomotionController.layers[0].stateMachine.states.Any(state => state.state.name == "walk" && state.state.motion != null);
            Check(hasLocomotionStates,
                "S4_PLAYER_LOCOMOTION", "S4 主角缺少有效的 idle/walk 动画状态", report, failures);
            Check(tablet != null && tablet.player == player && tablet.rover != null &&
                  tablet.toggleKey == KeyCode.Q && tablet.zoomKey == KeyCode.M && tablet.showBaseSchematic,
                "S4_TABLET_NAV", "S4 缺少 Q 键手持平板导航或目标引用不完整", report, failures);
            Check(new[] { "A", "B", "C" }.All(suffix => GameObject.Find("SamplePoint_" + suffix) != null),
                "S4_SAMPLE_NAV_ANCHORS", "S4 月壤点缺少独立导航锚点，采集后可能导致地图标记失效", report, failures);
            Check(moonLayer != null && moonLayer.diffuseTexture != null &&
                  moonLayer.diffuseTexture.name == "NASA_Moon_LROC" &&
                  moonLayer.tileSize.x >= 499f && moonLayer.tileSize.y >= 499f,
                "S4_TERRAIN_TEXTURE_SCALE", "S4 月面贴图未按 S3 的 500m 整幅尺度铺设", report, failures);
            Check(!FindAll<MinimapController>(scene).Any(),
                "S4_NO_OVERHEAD_MINIMAP", "S4 仍在使用额外俯视相机，而非平板雷达导航", report, failures);
            float maxSampleSeparation = 0f;
            for (int i = 0; i < sampleRocks.Length; i++)
                for (int j = i + 1; j < sampleRocks.Length; j++)
                    maxSampleSeparation = Mathf.Max(maxSampleSeparation,
                        Vector3.Distance(sampleRocks[i].transform.position, sampleRocks[j].transform.position));
            Check(sampleRocks.Length == 3 && sampleRocks.Select(sample => sample.pointId).Distinct().Count() == 3 &&
                  maxSampleSeparation >= 55f,
                "S4_SAMPLE_SPREAD", "S4 三处月壤点未分散布置或任务 ID 不唯一", report, failures);
            Check(sampleRocks.All(sample => sample.GetComponentsInChildren<Collider>(true)
                    .All(collider => collider.isTrigger)),
                "S4_SAMPLE_NOT_OBSTACLE", "S4 存在会卡住月球车的实体样本碰撞体", report, failures);
            Check(FindAll<QuestManager>(scene).Any(), "S4_QUEST", "S4 缺少任务管理器", report, failures);
            GameObject lander = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "LandedMissionLander");
            Check(lander != null && lander.GetComponentsInChildren<Renderer>(true).Length >= 10,
                "S4_MISSION_LANDER", "S4 缺少正确的月面任务着陆器", report, failures);
            Check(!scene.GetRootGameObjects().Any(go => go.name == "LunarTerrainVisual" || go.name == "LandedCapsule"),
                "S4_NO_OVERLAP", "S4 仍存在重叠高模地形或错误的阿波罗指令舱", report, failures);
            Check(FindAll<Terrain>(scene).Count() == 1,
                "S4_SINGLE_TERRAIN", "S4 物理地形数量不是 1", report, failures);
        }

        static void ValidateARScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.ARScene, OpenSceneMode.Single);
            var cameras = FindAll<Camera>(scene).ToArray();
            var listeners = FindAll<AudioListener>(scene).ToArray();
            XROrigin origin = FindAll<XROrigin>(scene).FirstOrDefault();
            ARExhibitPlacer placer = FindAll<ARExhibitPlacer>(scene).FirstOrDefault();

            Check(cameras.Length == 1 && cameras[0].CompareTag("MainCamera"),
                "S6_CAMERA", "S6 必须且只能有一个 MainCamera", report, failures);
            Check(listeners.Length == 1, "S6_AUDIO", "S6 必须且只能有一个 AudioListener", report, failures);
            Check(origin != null && origin.Camera != null, "S6_XR_ORIGIN", "S6 缺少有效 XR Origin", report, failures);
            Check(placer != null && placer.exhibitIds != null && placer.exhibitIds.Length == 3,
                "S6_IDS", "S6 展品编号配置不完整", report, failures);
            Check(placer != null && placer.exhibitIds != null && placer.exhibitIds.Contains("APOLLO_LM"),
                "S6_APOLLO_LM", "S6 AR 端尚未同步替换后的阿波罗登月舱", report, failures);
            Check(placer != null && placer.exhibitPrefabs != null && placer.exhibitPrefabs.Length == 3 &&
                  placer.exhibitPrefabs.All(prefab => prefab != null),
                "S6_PREFABS", "S6 三个展品模型引用不完整", report, failures);
            Check(AndroidBuilder.IsARCoreConfigured(),
                "S6_ARCORE_LOADER", "Android 未启用 ARCore Loader，APK 将缺少相机权限和真实 AR 支持",
                report, failures);
        }

        static void ValidateDialogue(StringBuilder report, List<string> failures)
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Characters/dialogue.json");
            DialogueDatabase db = DialogueDatabase.Load(source);
            Check(db.Nodes.Any(node => node.@event == "interact_ground_flight_controller"),
                "DIALOGUE_LAUNCH", "缺少林岚发射确认节点", report, failures);
            string[] moonNpcEvents =
            {
                "interact_base_coordinator",
                "interact_lunar_scientist",
                "interact_maintenance_technician",
            };
            Check(moonNpcEvents.All(eventName => db.Nodes.Any(node => node.@event == eventName)),
                "DIALOGUE_MOON_NPCS", "S4 存在没有对话节点的主要 NPC", report, failures);
            string[] launchTourEvents =
            {
                "launch_tour_start",
                "launch_tour_control_center",
                "launch_tour_tracking",
                "launch_tour_lunar_milestones",
                "launch_tour_rocket",
                "launch_platform_ready",
            };
            Check(launchTourEvents.All(eventName => db.Nodes.Any(node => node.@event == eventName)),
                "DIALOGUE_LAUNCH_TOUR", "S1 发射场导览剧情节点不完整", report, failures);
            Check(db.Nodes.Any(node => node.on_complete_effects != null &&
                                     node.on_complete_effects.Any(effect => effect.field == "exhibit_complete")),
                "DIALOGUE_EXHIBIT", "缺少展品完成状态效果", report, failures);
            Check(db.Nodes.Any(node => node.choices != null && node.choices.Any(choice =>
                                     choice.effects != null && choice.effects.Any(effect => effect.type == "deliver_samples_once"))),
                "DIALOGUE_DELIVERY", "缺少样本交付效果", report, failures);
            DlgTask stationTask = db.Tasks.FirstOrDefault(task => task.id == "T30_eva");
            DlgTask deliveryTask = db.Tasks.FirstOrDefault(task => task.id == "T33_sample_delivery");
            Check(db.Tasks.Count >= 11 && db.Tasks.All(task => !string.IsNullOrWhiteSpace(task.objective)) &&
                  stationTask != null && stationTask.objective.Contains("周航") &&
                  deliveryTask != null && deliveryTask.objective.Contains("许青"),
                "DIALOGUE_TASK_GUIDANCE", "任务追踪缺少明确地点、人物或操作指引", report, failures);
        }

        static void ValidateSceneSerialization(StringBuilder report, List<string> failures)
        {
            bool launchText = StartsWithYamlHeader(BuildSceneCatalog.LaunchScene);
            bool landingText = StartsWithYamlHeader(BuildSceneCatalog.LandingScene);
            Check(launchText && landingText, "SCENE_SERIALIZATION",
                "S1 或 S3 仍是二进制序列化", report, failures);
        }

        static bool StartsWithYamlHeader(string path)
        {
            byte[] expected = Encoding.ASCII.GetBytes("%YAML");
            using (var stream = File.OpenRead(path))
            {
                for (int i = 0; i < expected.Length; i++)
                    if (stream.ReadByte() != expected[i]) return false;
            }
            return true;
        }

        static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T component in root.GetComponentsInChildren<T>(true))
                    yield return component;
        }

        static bool ColliderOverlapsRenderer(NpcInteractor npc)
        {
            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>();
            Collider[] colliders = npc.GetComponentsInChildren<Collider>();
            if (renderers.Length == 0 || colliders.Length == 0) return false;

            Bounds renderBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) renderBounds.Encapsulate(renderers[i].bounds);
            return colliders.Any(collider => collider.enabled && collider.bounds.Intersects(renderBounds));
        }

        static Bounds RendererBounds(GameObject gameObject)
        {
            if (gameObject == null) return new Bounds();
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(gameObject.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static bool SerializedTransformScaleIsOne(string assetPath, Transform transform)
        {
            if (transform == null || !File.Exists(assetPath)) return false;
            ulong objectId = GlobalObjectId.GetGlobalObjectIdSlow(transform).targetObjectId;
            string text = File.ReadAllText(assetPath);
            string marker = "--- !u!224 &" + objectId;
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return false;
            int end = text.IndexOf("\n--- !u!", start + marker.Length, StringComparison.Ordinal);
            string block = end >= 0 ? text.Substring(start, end - start) : text.Substring(start);
            return block.Contains("m_LocalScale: {x: 1, y: 1, z: 1}");
        }

        static void Check(bool ok, string id, string message, StringBuilder report, List<string> failures)
        {
            report.AppendLine(id + "=" + (ok ? "PASS" : "FAIL"));
            if (!ok) failures.Add(message);
        }
    }
}
