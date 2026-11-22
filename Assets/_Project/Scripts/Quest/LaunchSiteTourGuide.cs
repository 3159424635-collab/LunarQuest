using System.Collections;
using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// S1 发射场导览：林岚带玩家参观地面设施，途中介绍中国航天与探月里程碑。
    /// 不依赖 NavMesh，使用有限的安全路点移动；玩家落后时 NPC 会停下等待。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaunchSiteTourGuide : MonoBehaviour
    {
        struct TourStop
        {
            public Vector3 position;
            public Vector3 landmark;
            public string eventName;
            public string routeHint;
            public string cardTitle;
            public string cardBody;

            public TourStop(Vector3 position, Vector3 landmark, string eventName,
                string routeHint, string cardTitle, string cardBody)
            {
                this.position = position;
                this.landmark = landmark;
                this.eventName = eventName;
                this.routeHint = routeHint;
                this.cardTitle = cardTitle;
                this.cardBody = cardBody;
            }
        }

        public float walkSpeed = 3.4f;
        public float waitForPlayerDistance = 9f;
        public float speakDistance = 7f;

        NpcInteractor npc;
        Animator animator;
        Transform player;
        HUDController hud;
        Coroutine tourRoutine;
        bool tourStarted;
        bool tourCompleted;
        bool awaitingContinue;
        bool continueRequested;
        float nextReminderTime;
        string animationState;

        void Awake()
        {
            npc = GetComponent<NpcInteractor>();
            animator = npc != null ? npc.animator : GetComponent<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            hud = FindObjectOfType<HUDController>();
            SetInitialPrompt();
        }

        /// <summary>每次进入 S1 时由 SceneFlow 调用，确保旧场景也能获得新导览。</summary>
        public void ResetTour(Transform playerTransform)
        {
            if (tourRoutine != null) StopCoroutine(tourRoutine);
            tourRoutine = null;
            player = playerTransform;
            hud = player != null ? player.GetComponent<HUDController>() : FindObjectOfType<HUDController>();
            tourStarted = false;
            tourCompleted = false;
            awaitingContinue = false;
            continueRequested = false;
            nextReminderTime = 0f;
            if (SciFiHUD.I != null) SciFiHUD.I.HideCard();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                animationState = null;
                PlayState("idle");
            }
            SetInitialPrompt();
        }

        /// <summary>
        /// true 表示本次交互已由导览系统处理；导览完成后返回 false，
        /// 让 NpcInteractor 继续打开发射确认选项。
        /// </summary>
        public bool HandleInteraction(GameObject who)
        {
            if (tourCompleted) return false;

            if (who != null) player = who.transform;
            if (tourStarted)
            {
                if (awaitingContinue)
                {
                    continueRequested = true;
                    return true;
                }
                if (Time.unscaledTime >= nextReminderTime)
                {
                    nextReminderTime = Time.unscaledTime + 3f;
                    ShowSystemMessage("导览进行中，请跟上林岚。");
                }
                return true;
            }

            tourStarted = true;
            if (npc != null) npc.prompt = "跟随林岚参观发射场";
            tourRoutine = StartCoroutine(RunTour());
            return true;
        }

        IEnumerator RunTour()
        {
            FireEvent("launch_tour_start");
            yield return new WaitForSeconds(6f);

            float y = transform.position.y;
            var stops = new[]
            {
                new TourStop(
                    new Vector3(38f, y, -8f), new Vector3(38f, y, -20f),
                    "launch_tour_control_center", "跟随林岚参观发射场\n沿铺装路线前往指挥中心；她停下后靠近",
                    "中国航天·从卫星到载人",
                    "1970·东方红一号\n中国首颗人造地球卫星成功发射。\n\n2003·神舟五号\n杨利伟完成中国首次载人航天飞行。"),
                new TourStop(
                    new Vector3(54f, y, -10f), new Vector3(60f, y, -20f),
                    "launch_tour_tracking", "跟随林岚参观发射场\n沿铺装路线前往跟踪天线；她停下后靠近",
                    "奔月第一步·嫦娥一号",
                    "2007年10月24日\n中国首颗月球探测器嫦娥一号发射。\n\n跟踪天线和测控系统持续确认飞行器的位置、速度与状态。"),
                new TourStop(
                    new Vector3(7f, y, 15f), new Vector3(0f, y + 30f, -46f),
                    "launch_tour_lunar_milestones", "跟随林岚参观发射场\n沿铺装路线前往观景台；她停下后靠近",
                    "从月球正面到背面",
                    "2013·嫦娥三号 / 玉兔号\n完成月面软着陆和巡视探测。\n\n2019·嫦娥四号\n完成人类探测器首次月球背面软着陆和巡视探测。"),
                new TourStop(
                    new Vector3(0f, y, -15f), new Vector3(0f, y + 40f, -46f),
                    "launch_tour_rocket", "跟随林岚参观发射场\n沿铺装路线前往火箭集合点；她停下后靠近",
                    "探月三步走·绕 落 回",
                    "2020·嫦娥五号\n完成中国首次月面采样返回。\n\n中国探月工程完成“绕、落、回”三步走，你接下来也将亲自经历这三个阶段。"),
            };

            foreach (TourStop stop in stops)
            {
                yield return MoveTo(stop.position, stop.routeHint);
                yield return WaitUntilPlayerIsClose();
                Face(stop.landmark);
                PlayState("point");
                FireEvent(stop.eventName);
                yield return ShowKnowledgeCardAndWait(stop.cardTitle, stop.cardBody);
            }

            FacePlayer();
            PlayState("talk");
            FireEvent("launch_platform_ready");
            yield return new WaitForSeconds(6f);

            tourCompleted = true;
            tourRoutine = null;
            FacePlayer();
            PlayState("idle");
            if (npc != null) npc.prompt = "与林岚确认发射（E）";
        }

        IEnumerator ShowKnowledgeCardAndWait(string title, string body)
        {
            awaitingContinue = true;
            continueRequested = false;
            if (SciFiHUD.I != null)
                SciFiHUD.I.ShowCard(title, body + "\n\n[E / 空格] 继续跟随");

            // 防止到站前的按键被同一帧误读为继续。
            yield return null;
            while (!continueRequested &&
                   !Input.GetKeyDown(KeyCode.E) &&
                   !Input.GetKeyDown(KeyCode.Space) &&
                   !Input.GetKeyDown(KeyCode.Return))
                yield return null;

            awaitingContinue = false;
            continueRequested = false;
            if (SciFiHUD.I != null) SciFiHUD.I.HideCard();
            yield return new WaitForSeconds(0.35f);
        }

        IEnumerator MoveTo(Vector3 target, string routeHint)
        {
            while (HorizontalDistance(transform.position, target) > 0.12f)
            {
                if (player == null)
                {
                    GameObject found = GameObject.FindGameObjectWithTag("Player");
                    if (found != null) player = found.transform;
                }

                if (player != null && HorizontalDistance(transform.position, player.position) > waitForPlayerDistance)
                {
                    PlayState("idle");
                    SetQuest("跟随林岚参观发射场\n林岚正在等你，请沿铺装路线靠近她");
                    yield return null;
                    continue;
                }

                SetQuest(routeHint);
                Vector3 delta = target - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.001f)
                {
                    Quaternion look = Quaternion.LookRotation(delta.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 7f);
                    transform.position = Vector3.MoveTowards(
                        transform.position, target, walkSpeed * Time.deltaTime);
                }
                PlayState("walk");
                yield return null;
            }
            transform.position = target;
            PlayState("idle");
        }

        IEnumerator WaitUntilPlayerIsClose()
        {
            while (player != null && HorizontalDistance(transform.position, player.position) > speakDistance)
            {
                SetQuest("听取林岚讲解\n靠近林岚；出现知识卡后按 E / 空格继续");
                PlayState("idle");
                yield return null;
            }
        }

        void FireEvent(string eventName)
        {
            if (QuestManager.I != null) QuestManager.I.FireEvent(eventName);
        }

        void SetQuest(string text)
        {
            if (hud == null) hud = FindObjectOfType<HUDController>();
            if (hud != null) hud.SetQuest(text);
        }

        void ShowSystemMessage(string text)
        {
            if (SciFiHUD.I != null) SciFiHUD.I.ShowSubtitle("系统", text, 3f);
        }

        void Face(Vector3 target)
        {
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(delta.normalized);
        }

        void FacePlayer()
        {
            if (player != null) Face(player.position);
        }

        void PlayState(string state)
        {
            if (animator == null || !animator.isActiveAndEnabled || animationState == state) return;
            int fullPathHash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, fullPathHash))
            {
                Debug.LogWarning("[LaunchSiteTourGuide] 动画状态不存在: " + state, this);
                return;
            }
            // 只在状态真正改变时切换。逐帧重复 CrossFade 会让动画一直停在过渡首帧，造成“站着平移”。
            animationState = state;
            animator.CrossFadeInFixedTime(fullPathHash, 0.15f, 0, 0f);
        }

        void SetInitialPrompt()
        {
            if (npc != null) npc.prompt = "与林岚交谈，开始发射场参观（E）";
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
            => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
