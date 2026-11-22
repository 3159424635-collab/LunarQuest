using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest.Module4
{
    public sealed class Module4HudView : MonoBehaviour
    {
        [Header("精简演出显示")]
        public bool showSubtitles;
        public bool showTitleCards;
        [Header("可直接交给现有 HUDController 绑定")]
        public Text speedText;
        public Text promptText;
        public Text questText;
        [Header("演出界面")]
        public Text chapterText;
        public Text phaseText;
        public Text missionClock;
        public Text altitudeText;
        public Text oxygenText;
        public Text subtitleText;
        public Text speakerText;
        public Text titleText;
        public Text titleEnglish;
        public Text footerText;
        public Image progress;
        public Image fade;
        public GameObject subtitlePanel;
        public GameObject titlePanel;
        public RectTransform minimapArrow;
        public GameObject dialoguePanel;
        public Text dialogueText;
        public Text landingText;
        public GameObject landingPanel;

        void Awake()
        {
            // 旧场景和旧预制体曾把 Canvas 根节点序列化成 (0,0,0)，导致整套 HUD 不可见。
            if (transform.localScale.sqrMagnitude < 0.01f)
                transform.localScale = Vector3.one;
        }

        public void SetSubtitle(string speaker, string text)
        {
            if (subtitlePanel) subtitlePanel.SetActive(showSubtitles && !string.IsNullOrEmpty(text));
            if (subtitleText) subtitleText.text = text;
            if (speakerText) speakerText.text = speaker;
        }

        public void SetTitle(string chinese, string english, bool visible)
        {
            if (titlePanel) titlePanel.SetActive(showTitleCards && visible);
            if (titleText) titleText.text = chinese;
            if (titleEnglish) titleEnglish.text = english;
        }

        public void SetChapter(string number, string name, string english)
        {
            if (chapterText) chapterText.text = number + "  /  " + name;
            if (phaseText) phaseText.text = english;
        }

        public void SetTelemetry(float speed, float height, float oxygen, string quest, string prompt)
        {
            if (speedText) speedText.text = speed.ToString("0.0") + " km/h";
            if (altitudeText) altitudeText.text = height.ToString("0") + " m";
            if (oxygenText) oxygenText.text = oxygen.ToString("0") + "%";
            if (questText) questText.text = quest;
            if (promptText) promptText.text = prompt;
        }

        public void SetFade(float amount)
        {
            if (!fade) return;
            var c = fade.color;
            c.a = Mathf.Clamp01(amount);
            fade.color = c;
        }
    }
}
