using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest
{
    /// <summary>
    /// 连接 S1、模块四出发演出、S3、S4 与返航演出。
    /// 同时对旧版 S1 场景做一次运行时安全校正，保证玩家与林岚之间没有封闭碰撞体。
    /// </summary>
    public static class SceneFlow
    {
        public const string LaunchScene = "S1_LaunchSite";
        public const string ShowcaseScene = "Module4Showcase";
        public const string LandingScene = "S3_Landing";
        public const string MoonScene = "S4_MoonSurface_Whitebox";

        const string ShowcaseModeKey = "LunarQuest.ShowcaseMode";
        const string OutboundMode = "outbound";
        const string ReturnMode = "return";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallRuntime()
        {
            if (SceneFlowRuntime.Instance != null) return;
            var go = new GameObject("SceneFlowRuntime");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SceneFlowRuntime>();
        }

        public static void StartOutboundSequence(float delay = 0f)
            => SceneFlowRuntime.Instance.ScheduleShowcase(OutboundMode, delay);

        public static void StartReturnSequence(float delay = 0f)
            => SceneFlowRuntime.Instance.ScheduleShowcase(ReturnMode, delay);

        public static void LoadLanding()
            => SceneManager.LoadScene(LandingScene);

        public static string ConsumeShowcaseMode()
        {
            string mode = PlayerPrefs.GetString(ShowcaseModeKey, "full");
            PlayerPrefs.DeleteKey(ShowcaseModeKey);
            return mode;
        }

        internal static void SetShowcaseMode(string mode)
        {
            PlayerPrefs.SetString(ShowcaseModeKey, mode);
            PlayerPrefs.Save();
        }
    }

    sealed class SceneFlowRuntime : MonoBehaviour
    {
        public static SceneFlowRuntime Instance { get; private set; }

        Coroutine pendingLoad;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public void ScheduleShowcase(string mode, float delay)
        {
            SceneFlow.SetShowcaseMode(mode);
            if (pendingLoad != null) StopCoroutine(pendingLoad);
            pendingLoad = StartCoroutine(LoadShowcaseAfter(Mathf.Max(0f, delay)));
        }

        IEnumerator LoadShowcaseAfter(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            pendingLoad = null;
            SceneManager.LoadScene(SceneFlow.ShowcaseScene);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneFlow.LaunchScene)
                StartCoroutine(FixLaunchSiteAfterAwake());
            else if (scene.name == SceneFlow.MoonScene)
                StartCoroutine(PrepareMoonSurfaceAfterAwake());
        }

        IEnumerator PrepareMoonSurfaceAfterAwake()
        {
            // 等场景中所有 Awake 完成，再统一修正相机、车辆与玩家状态。
            yield return null;

            foreach (var vehicle in Object.FindObjectsOfType<VehicleEnterExit>(true))
                vehicle.ForceWalkingState();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) yield break;

            player.SetActive(true);
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            const float spawnX = 0f;
            const float spawnZ = 112f;
            Vector3 spawn = new Vector3(spawnX, GroundY(spawnX, spawnZ) + 1.05f, spawnZ);
            Vector3 towardAirlock = new Vector3(4f, spawn.y, 129f) - spawn;
            player.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(towardAirlock.normalized));

            if (controller != null) controller.enabled = true;
            var movement = player.GetComponent<FirstPersonController>();
            if (movement != null) movement.enabled = true;
            var interaction = player.GetComponent<PlayerInteractor>();
            if (interaction != null) interaction.enabled = true;

            var playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }

            Physics.SyncTransforms();
            SciFiHUD.ResetModalState();

            if (SciFiHUD.I != null)
                SciFiHUD.I.ShowSubtitle("系统", "出舱完成。WASD 移动，前往前方气闸；靠近月球车可按 E 或 F 上车。", 7f);
        }

        IEnumerator FixLaunchSiteAfterAwake()
        {
            yield return null;

            var controlCenter = GameObject.Find("ControlCenter");
            if (controlCenter != null)
            {
                var enclosingBox = controlCenter.GetComponent<BoxCollider>();
                if (enclosingBox != null) enclosingBox.enabled = false;
            }

            NpcInteractor flightController = null;
            foreach (var npc in Object.FindObjectsOfType<NpcInteractor>())
                if (npc.characterId == "ground_flight_controller")
                {
                    flightController = npc;
                    break;
                }

            // 仅校正旧场景：固定在建筑外的平坦区域，玩家朝 +Z，林岚位于正前方 5 米。
            if (flightController != null && HorizontalDistance(flightController.transform.position, 35f, 0f) > 0.1f)
                MoveNpcToGround(flightController, 35f, 0f);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && HorizontalDistance(player.transform.position, 35f, -5f) > 0.1f)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.transform.SetPositionAndRotation(
                    new Vector3(35f, GroundY(35f, -5f) + 1.05f, -5f),
                    Quaternion.identity);
                if (controller != null) controller.enabled = true;
            }
        }

        static void MoveNpcToGround(NpcInteractor npc, float x, float z)
        {
            Collider[] colliders = npc.GetComponentsInChildren<Collider>();
            var enabledStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                enabledStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }

            float ground = GroundY(x, z);
            Vector3 position = npc.transform.position;
            npc.transform.position = new Vector3(x, position.y, z);
            Physics.SyncTransforms();

            Renderer[] renderers = npc.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                npc.transform.position += Vector3.up * (ground - bounds.min.y);
            }

            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = enabledStates[i];
        }

        static float HorizontalDistance(Vector3 position, float x, float z)
            => Vector2.Distance(new Vector2(position.x, position.z), new Vector2(x, z));

        static float GroundY(float x, float z)
        {
            var ray = new Ray(new Vector3(x, 100f, z), Vector3.down);
            return Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 0f;
        }
    }
}
