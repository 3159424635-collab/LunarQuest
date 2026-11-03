using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// HUD 逻辑层：汇集交互提示与车速等信息，是界面数据的唯一来源。
    /// 白盒阶段开启 Show Debug Hud 即可在左上角显示（无需搭建任何 UI）；
    /// 模块四交付正式界面后，把对应的 UGUI Text 拖入字段并关闭调试显示即可切换，
    /// 本脚本逻辑不需要改动。
    /// 挂载要求：场景中新建空物体（建议命名 HUD），挂本脚本。
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("正式 UI 字段（模块四交付后填入，可留空）")]
        public UnityEngine.UI.Text speedText;    // 速度表
        public UnityEngine.UI.Text promptText;   // 交互提示
        public UnityEngine.UI.Text questText;    // 任务提示

        [Header("白盒调试显示")]
        public bool showDebugHud = true;

        PlayerInteractor interactor;
        RoverController rover;
        string prompt;
        string quest;

        void Start()
        {
            interactor = FindObjectOfType<PlayerInteractor>();
            rover = FindObjectOfType<RoverController>();
            if (interactor != null)
                interactor.OnPromptChanged += OnPromptChanged;
        }

        void OnDestroy()
        {
            if (interactor != null)
                interactor.OnPromptChanged -= OnPromptChanged;
        }

        void OnPromptChanged(string text) => prompt = text;

        void Update()
        {
            string speed = rover ? rover.SpeedKmh.ToString("0") + " km/h" : "--";
            if (speedText) speedText.text = speed;
            if (promptText) promptText.text = prompt ?? "";
            if (questText) questText.text = quest;
        }

        /// <summary>任务系统调用：更新当前任务提示。</summary>
        public void SetQuest(string text) => quest = text;

        void OnGUI()
        {
            if (!showDebugHud) return;
            GUILayout.BeginArea(new Rect(16f, 16f, 340f, 160f));
            GUILayout.Label("速度: " + (rover ? rover.SpeedKmh.ToString("0") : "--") + " km/h");
            GUILayout.Label("提示: " + (string.IsNullOrEmpty(prompt) ? "-" : prompt));
            if (!string.IsNullOrEmpty(quest)) GUILayout.Label("任务: " + quest);
            GUILayout.EndArea();
        }
    }
}
