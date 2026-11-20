using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            Check(player != null, "S1_PLAYER", "S1 缺少玩家", report, failures);
            Check(npc != null, "S1_NPC", "S1 缺少林岚", report, failures);
            Check(npc != null && ColliderOverlapsRenderer(npc),
                "S1_NPC_COLLIDER", "林岚的交互碰撞体没有覆盖角色模型", report, failures);
            Check(controlCenter != null && controlCenter.GetComponent<BoxCollider>() == null,
                "S1_OPEN_BUILDING", "指挥中心仍有封闭整栋建筑的根级碰撞体", report, failures);
            Check(player != null && npc != null && Vector3.Distance(player.transform.position, npc.transform.position) <= 6.5f,
                "S1_START_DISTANCE", "玩家与林岚出生距离过远", report, failures);
            Check(FindAll<QuestManager>(scene).Any(), "S1_QUEST", "S1 缺少任务管理器", report, failures);
        }

        static void ValidateMoonScene(StringBuilder report, List<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(BuildSceneCatalog.MoonScene, OpenSceneMode.Single);
            var npcs = FindAll<NpcInteractor>(scene).ToArray();
            var exhibits = FindAll<ExhibitStand>(scene).ToArray();
            Check(npcs.Length >= 3, "S4_NPCS", "S4 NPC 数量不足", report, failures);
            Check(npcs.All(ColliderOverlapsRenderer),
                "S4_NPC_COLLIDERS", "S4 存在未被交互碰撞体覆盖的 NPC", report, failures);
            Check(exhibits.Length >= 3, "S4_EXHIBITS", "S4 展品数量不足", report, failures);
            Check(exhibits.All(exhibit => exhibit.qrTexture != null),
                "S4_QR_REFERENCES", "S4 存在没有二维码引用的展品", report, failures);
            Check(FindAll<QuestManager>(scene).Any(), "S4_QUEST", "S4 缺少任务管理器", report, failures);
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
            Check(placer != null && placer.exhibitPrefabs != null && placer.exhibitPrefabs.Length == 3 &&
                  placer.exhibitPrefabs.All(prefab => prefab != null),
                "S6_PREFABS", "S6 三个展品模型引用不完整", report, failures);
        }

        static void ValidateDialogue(StringBuilder report, List<string> failures)
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Content/Characters/dialogue.json");
            DialogueDatabase db = DialogueDatabase.Load(source);
            Check(db.Nodes.Any(node => node.@event == "interact_ground_flight_controller"),
                "DIALOGUE_LAUNCH", "缺少林岚发射确认节点", report, failures);
            Check(db.Nodes.Any(node => node.on_complete_effects != null &&
                                     node.on_complete_effects.Any(effect => effect.field == "exhibit_complete")),
                "DIALOGUE_EXHIBIT", "缺少展品完成状态效果", report, failures);
            Check(db.Nodes.Any(node => node.choices != null && node.choices.Any(choice =>
                                     choice.effects != null && choice.effects.Any(effect => effect.type == "deliver_samples_once"))),
                "DIALOGUE_DELIVERY", "缺少样本交付效果", report, failures);
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

        static void Check(bool ok, string id, string message, StringBuilder report, List<string> failures)
        {
            report.AppendLine(id + "=" + (ok ? "PASS" : "FAIL"));
            if (!ok) failures.Add(message);
        }
    }
}
