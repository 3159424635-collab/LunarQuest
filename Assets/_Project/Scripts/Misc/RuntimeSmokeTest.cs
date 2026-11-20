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
            moonQuest?.OnSamplePoint("crater03_sample_a");
            moonQuest?.OnSamplePoint("crater03_sample_b");
            moonQuest?.OnSamplePoint("crater03_sample_c");
            Check(moonQuest != null && moonQuest.GetState("sample_count") == "3", "三份样本没有正确计数");
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
            Check(player != null && npc != null, "S1 运行时缺少玩家或林岚");
            Check(npc != null && npc.GetComponentInChildren<Collider>() != null,
                "林岚运行时没有交互碰撞体");
            Check(enclosingBox == null || !enclosingBox.enabled,
                "指挥中心入口仍被根级碰撞体封闭");
            Check(player != null && npc != null && Vector3.Distance(player.transform.position, npc.transform.position) <= 6.5f,
                "S1 运行时出生位置未校正");
            Check(GameObject.Find("Key") != null, "交互提示按键文字子物体未创建");
        }

        void ValidateMoonRuntime()
        {
            var npcs = FindObjectsOfType<NpcInteractor>();
            var exhibits = FindObjectsOfType<ExhibitStand>();
            var player = FindObjectOfType<FirstPersonController>();
            var playerCamera = player != null ? player.GetComponentInChildren<Camera>() : null;
            var roverCamera = FindObjectOfType<RoverCamera>(true);
            Check(npcs.Length >= 3 && npcs.All(npc => npc.GetComponentInChildren<Collider>() != null),
                "S4 NPC 运行时交互碰撞体不完整");
            Check(exhibits.Length >= 3 && exhibits.All(exhibit => exhibit.qrTexture != null),
                "S4 展品或二维码引用不完整");
            Check(QuestManager.I != null, "S4 运行时任务管理器未初始化");
            Check(player != null && player.isActiveAndEnabled && playerCamera != null && playerCamera.enabled,
                "S4 进场后玩家或第一人称相机未激活");
            Check(player != null && Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(0f, 112f)) < 1f,
                "S4 玩家没有出现在安全出舱点");
            Check(roverCamera == null || !roverCamera.gameObject.activeInHierarchy,
                "S4 初始状态错误启用了月球车相机");
            Check(!SciFiHUD.ModalOpen, "S4 进场后仍被 HUD 模态状态锁住");
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
