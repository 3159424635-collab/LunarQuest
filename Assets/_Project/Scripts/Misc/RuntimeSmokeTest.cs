using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LunarQuest.Module4;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest
{
    /// <summary>
    /// 完整构建的无人值守烟雾测试。只有命令行包含 -lqSmoke 时才启动，不影响正常游戏。
    /// </summary>
    public sealed class RuntimeSmokeTest : MonoBehaviour
    {
        readonly List<string> failures = new List<string>();
        readonly List<string> runtimeErrors = new List<string>();
        string outputPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void TryStart()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-lqSmoke");
            if (index < 0) return;

            var go = new GameObject("RuntimeSmokeTest");
            DontDestroyOnLoad(go);
            var smoke = go.AddComponent<RuntimeSmokeTest>();
            smoke.outputPath = index < args.Length - 1
                ? args[index + 1]
                : Path.GetFullPath("lunarquest-smoke.txt");
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
            Time.timeScale = 50f;
            AudioListener.pause = true;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeErrors.Add(type + ": " + condition + "\n" + stackTrace);
        }

        IEnumerator Run()
        {
            yield return null;
            yield return null;

            Check(SceneManager.GetActiveScene().name == SceneFlow.LaunchScene,
                "启动场景不是 S1");
            ValidateLaunchRuntime();

            var launchQuest = QuestManager.I;
            launchQuest?.OnNpc("ground_flight_controller");
            yield return null;
            Check(SciFiHUD.ModalOpen, "林岚交互没有打开发射确认选项");
            var firstChoice = GameObject.Find("Choice_0")?.GetComponent<RectTransform>();
            var firstChoiceText = firstChoice != null
                ? firstChoice.GetComponentInChildren<UnityEngine.UI.Text>()
                : null;
            Check(firstChoice != null && firstChoice.rect.width >= 300f && firstChoice.rect.height >= 50f,
                "发射选项按钮尺寸异常");
            Check(firstChoiceText != null && firstChoiceText.preferredHeight <= firstChoice.rect.height - 8f,
                "发射选项文字超出按钮");
            SciFiHUD.I?.HideChoices();
            Check(launchQuest != null && launchQuest.TrySelectChoice("launch_departure_offer", "confirm_launch"),
                "发射确认选项执行失败");
            yield return WaitForScene(SceneFlow.ShowcaseScene, 5f);
            yield return WaitForScene(SceneFlow.LandingScene, 12f);
            Check(SceneManager.GetActiveScene().name == SceneFlow.LandingScene,
                "发射/环月演出没有进入 S3");

            SceneManager.LoadScene(SceneFlow.MoonScene);
            yield return null;
            yield return null;
            ValidateMoonRuntime();

            var moonQuest = QuestManager.I;
            Check(moonQuest != null && moonQuest.TrySelectChoice("coordinator_mission_offer", "accept_sample_mission"),
                "采样任务接取失败");
            var moonHud = FindObjectOfType<HUDController>();
            Check(moonHud != null && moonHud.CurrentQuest.Contains("0/3") &&
                  moonHud.CurrentQuest.Contains("按 Q"),
                "采样任务追踪没有显示进度或导航操作");
            var tablet = FindObjectOfType<FieldTabletNavigator>();
            tablet?.SetOpen(true);
            yield return null;
            Check(tablet != null && tablet.IsOpen && tablet.VisibleSampleMarkerCount == 3 &&
                  tablet.SchematicLineCount >= 80,
                "Q 键平板导航未显示三处月壤标记或基地建筑线框");
            tablet?.SetOpen(false);
            var vehicleMode = FindObjectOfType<VehicleEnterExit>();
            var roverBody = vehicleMode != null ? vehicleMode.GetComponent<Rigidbody>() : null;
            Check(roverBody != null && roverBody.isKinematic,
                "无人驾驶时月球车没有进入稳定停驻状态");
            vehicleMode?.Toggle();
            tablet?.SetOpen(true);
            yield return null;
            Check(vehicleMode != null && vehicleMode.IsInVehicle && roverBody != null && !roverBody.isKinematic &&
                  tablet != null && tablet.IsOpen,
                "驾驶月球车时无法打开 Q 键平板导航");
            vehicleMode?.Toggle();
            tablet?.SetOpen(false);
            yield return null;
            Check(roverBody != null && roverBody.isKinematic,
                "下车后月球车没有恢复稳定停驻状态");
            moonQuest?.OnSamplePoint("crater03_sample_a");
            Check(moonHud != null && moonHud.CurrentQuest.Contains("1/3"),
                "采集第一份样本后任务进度没有更新");
            moonQuest?.OnSamplePoint("crater03_sample_b");
            moonQuest?.OnSamplePoint("crater03_sample_c");
            Check(moonQuest != null && moonQuest.GetState("sample_count") == "3", "三份样本没有正确计数");
            Check(moonHud != null && moonHud.CurrentQuest.Contains("许青"),
                "收齐样本后任务追踪没有明确交付对象");
            Check(moonQuest != null && moonQuest.TrySelectChoice("scientist_delivery_offer", "deliver_three_samples"),
                "三份样本交付失败");
            Check(moonQuest != null && moonQuest.GetState("sample_delivered") == "true",
                "sample_delivered 没有更新");
            moonQuest?.OnExhibitOpened();
            moonQuest?.OnExhibitClosed();
            Check(moonQuest != null && moonQuest.GetState("exhibit_complete") == "true",
                "exhibit_complete 没有更新");
            Check(moonQuest != null && moonQuest.TrySelectChoice("coordinator_return_ready", "confirm_return"),
                "返航确认选项执行失败");
            yield return WaitForScene(SceneFlow.ShowcaseScene, 5f);
            yield return null;
            var showcase = FindObjectOfType<Module4ShowcaseController>();
            Check(showcase != null && showcase.clock >= 140d,
                "返航模式没有从返航段开始");

            float deadline = Time.realtimeSinceStartup + 8f;
            while (showcase != null && !showcase.paused && Time.realtimeSinceStartup < deadline)
                yield return null;
            Check(showcase != null && showcase.paused && showcase.clock >= showcase.totalDuration - 0.1d,
                "返航演出没有正常结束");

            foreach (string error in runtimeErrors.Distinct())
                failures.Add("运行时日志错误：" + error);
            Finish();
        }

        IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
                yield return null;
            Check(SceneManager.GetActiveScene().name == sceneName,
                "等待场景超时：" + sceneName);
        }

        void ValidateLaunchRuntime()
        {
            var player = FindObjectOfType<FirstPersonController>();
            var npc = FindObjectsOfType<NpcInteractor>()
                .FirstOrDefault(item => item.characterId == "ground_flight_controller");
            var controlCenter = GameObject.Find("ControlCenter");
            var enclosingBox = controlCenter != null ? controlCenter.GetComponent<BoxCollider>() : null;
            var guide = npc != null ? npc.GetComponent<LaunchSiteTourGuide>() : null;
            var ground = GameObject.Find("LaunchGroundInfrastructure");
            var terrain = FindObjectOfType<Terrain>();
            Check(player != null && npc != null, "S1 运行时缺少玩家或林岚");
            Check(npc != null && npc.GetComponentInChildren<Collider>() != null,
                "林岚运行时没有交互碰撞体");
            Check(enclosingBox == null || !enclosingBox.enabled,
                "指挥中心入口仍被根级碰撞体封闭");
            Check(player != null && npc != null && Vector3.Distance(player.transform.position, npc.transform.position) <= 5.5f,
                "S1 运行时出生位置未校正");
            Check(player != null && npc != null &&
                  Vector3.Dot(player.transform.forward,
                      (npc.transform.position - player.transform.position).normalized) > 0.8f &&
                  Vector3.Dot(npc.transform.forward,
                      (player.transform.position - npc.transform.position).normalized) > 0.8f,
                "S1 玩家与林岚没有面对面出生");
            Check(guide != null, "S1 运行时缺少林岚发射场导览系统");
            Check(GameObject.Find("Key") != null, "交互提示按键文字子物体未创建");
            Check(ground != null && ground.transform.Find("LaunchApron") != null &&
                  ground.transform.Find("FlameTrench") != null && ground.transform.Find("CoastalSea") != null,
                "S1 工程化发射地面或滨海远景不完整");
            Check(ground != null && ground.transform.Find("TourRoute_Start") != null &&
                  ground.transform.Find("TourRoute_Tracking") != null &&
                  ground.transform.Find("TourRoute_Viewing") != null &&
                  ground.transform.Find("TourRoute_Rocket") != null,
                "S1 导览铺装道路不完整");
            Check(terrain != null && terrain.terrainData != null && terrain.terrainData.terrainLayers.Length >= 2,
                "S1 地形材质层不完整");
            Check(Mathf.Abs(Physics.gravity.y + 9.81f) < 0.05f,
                "S1 没有恢复地球重力");
        }

        void ValidateMoonRuntime()
        {
            var npcs = FindObjectsOfType<NpcInteractor>();
            var exhibits = FindObjectsOfType<ExhibitStand>();
            var player = FindObjectOfType<FirstPersonController>();
            var playerCamera = player != null ? player.GetComponentInChildren<Camera>() : null;
            var roverCamera = FindObjectOfType<RoverCamera>(true);
            var rover = FindObjectOfType<RoverController>();
            var roverSample = rover != null ? rover.GetComponent<RoverSampleInteractor>() : null;
            var roverBody = rover != null ? rover.GetComponent<Rigidbody>() : null;
            var interaction = player != null ? player.GetComponent<PlayerInteractor>() : null;
            var tablet = FindObjectOfType<FieldTabletNavigator>();
            var samples = FindObjectsOfType<SampleRock>();
            var technician = npcs.FirstOrDefault(npc => npc.characterId == "maintenance_technician");
            var thirdPersonAnimator = player != null && player.thirdPersonBody != null
                ? player.thirdPersonBody.GetComponentInChildren<Animator>(true)
                : null;
            Check(npcs.Length >= 3 && npcs.All(npc => npc.GetComponentInChildren<Collider>() != null),
                "S4 NPC 运行时交互碰撞体不完整");
            Check(exhibits.Length >= 3 && exhibits.All(exhibit => exhibit.qrTexture != null),
                "S4 展品或二维码引用不完整");
            Check(exhibits.Any(exhibit => exhibit.exhibitTitle.Contains("阿波罗载人登月舱")) &&
                  exhibits.Any(exhibit => exhibit.exhibitTitle.Contains("月球勘测轨道器")),
                "S4 官方真实展品未正确加载");
            Check(technician != null && Vector2.Distance(
                    new Vector2(technician.transform.position.x, technician.transform.position.z),
                    new Vector2(-4f, 178f)) <= 15f,
                "陈越仍未位于加压大厅内");
            Check(rover != null && rover.name.Contains("SEV") && rover.maxSpeed <= 8f && rover.downForce <= 6f,
                "S4 未正确加载稳定化的 NASA SEV 探测车");
            Check(rover != null && roverBody != null && roverBody.constraints == RigidbodyConstraints.None &&
                  rover.groundAlignSpeed >= 5f && rover.stallAssistDelay <= 1f &&
                  roverSample != null && roverSample.interactRange >= 6f,
                "S4 月球车越野姿态或车载样本交互未正确配置");
            Check(interaction != null && interaction.interactRange >= 6f && interaction.aimAssistRadius >= 0.15f,
                "S4 NPC 交互距离或准星容错未生效");
            Check(player != null && player.thirdPersonBody != null && thirdPersonAnimator != null &&
                  thirdPersonAnimator.runtimeAnimatorController != null && player.thirdPersonDistance >= 3.8f &&
                  player.thirdPersonHeight <= 1f && player.thirdPersonFocusHeight <= 0.5f,
                "S4 第三人称镜头或主角移动动画未正确配置");
            float spread = 0f;
            for (int i = 0; i < samples.Length; i++)
                for (int j = i + 1; j < samples.Length; j++)
                    spread = Mathf.Max(spread,
                        Vector3.Distance(samples[i].transform.position, samples[j].transform.position));
            Check(tablet != null && tablet.player == player && tablet.rover != null &&
                  samples.Length == 3 && spread >= 55f && samples.All(sample =>
                      sample.GetComponentsInChildren<Collider>(true).All(collider => collider.isTrigger)),
                "S4 平板导航或分散月壤点未正确配置");
            Check(QuestManager.I != null, "S4 运行时任务管理器未初始化");
            Check(player != null && player.isActiveAndEnabled && playerCamera != null && playerCamera.enabled,
                "S4 进场后玩家或第一人称相机未激活");
            Check(player != null && Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(0f, 112f)) < 1f,
                "S4 玩家没有出现在安全出舱点");
            Check(roverCamera == null || !roverCamera.gameObject.activeInHierarchy,
                "S4 初始状态错误启用了月球车相机");
            Check(!SciFiHUD.ModalOpen, "S4 进场后仍被 HUD 模态状态锁住");
            Check(Mathf.Abs(Physics.gravity.y + 9.81f / 6f) < 0.05f,
                "S4 没有切换为月球重力");
        }

        void Check(bool passed, string message)
        {
            if (!passed) failures.Add(message);
        }

        void Finish()
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllLines(outputPath,
                new[] { failures.Count == 0 ? "RESULT=PASS" : "RESULT=FAIL" }
                    .Concat(failures.Select(item => "FAIL: " + item)));
            int exitCode = failures.Count == 0 ? 0 : 2;
            Debug.Log("[RuntimeSmokeTest] " + (exitCode == 0 ? "PASS" : string.Join(" | ", failures)));
            Application.Quit(exitCode);
        }
    }
}
