using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest.Module4
{
    public sealed class Module4HudView : MonoBehaviour
    {
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

        public void SetSubtitle(string speaker, string text)
        {
            if (subtitlePanel) subtitlePanel.SetActive(!string.IsNullOrEmpty(text));
            if (subtitleText) subtitleText.text = text;
            if (speakerText) speakerText.text = speaker;
        }

        public void SetTitle(string chinese, string english, bool visible)
        {
            titlePanel.SetActive(visible);
            titleText.text = chinese;
            titleEnglish.text = english;
        }

        public void SetChapter(string number, string name, string english)
        {
            chapterText.text = number + "  /  " + name;
            phaseText.text = english;
        }

        public void SetTelemetry(float speed, float height, float oxygen, string quest, string prompt)
        {
            speedText.text = speed.ToString("0.0") + "  km/h";
            altitudeText.text = height.ToString("0.0") + "  m";
            oxygenText.text = oxygen.ToString("0") + "%";
            questText.text = quest;
            promptText.text = prompt;
        }

        public void SetFade(float amount)
        {
            var c = fade.color;
            c.a = Mathf.Clamp01(amount);
            fade.color = c;
        }
    }
}
