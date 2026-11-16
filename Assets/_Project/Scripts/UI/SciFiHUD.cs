using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 赛博风 HUD 组件（字幕 / 选项 / 数据卡 / 左上信息），替代 OnGUI 灰框。
    /// 自建画布；font 字段由搭建脚本填思源黑体（模块四交付字体），留空回退内置。
    /// 各模块后续换正式美术时可直接改这里，逻辑层不受影响。
    /// </summary>
    public class SciFiHUD : MonoBehaviour
    {
        public static SciFiHUD I { get; private set; }
        public Font font;

        CanvasGroup subGroup, cardGroup;
        Text subSpeaker, subText, cardTitle, cardBody, hudSpeed, hudQuest;
        RectTransform choicesRoot;
        readonly List<GameObject> choiceObjs = new List<GameObject>();
        Action<int> choiceCb;
        float subUntil;
        float subTarget, cardTarget;

        void Awake()
        {
            I = this;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panelSprite = SciFiGUI.RoundedPanel(96, 14f, 2f, SciFiGUI.PanelFill,
                new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.75f));

            // ---- 字幕（底部居中） ----
            var sub = SciFiGUI.Rect("Subtitle", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 96f));
            sub.sizeDelta = new Vector2(860f, 96f);
            subGroup = sub.gameObject.AddComponent<CanvasGroup>();
            subGroup.alpha = 0f;
            subGroup.blocksRaycasts = false;
            var subPanel = sub.gameObject.AddComponent<Image>();
            subPanel.sprite = panelSprite;
            subPanel.type = Image.Type.Sliced;
            subPanel.pixelsPerUnitMultiplier = 100f / 96f;
            var accent = SciFiGUI.Rect("Accent", sub, new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            accent.sizeDelta = new Vector2(5f, 64f);
            accent.gameObject.AddComponent<Image>().color = SciFiGUI.Cyan;
            accent.pivot = new Vector2(0f, 0.5f);
            var spk = SciFiGUI.Rect("Speaker", sub, new Vector2(0f, 1f), new Vector2(26f, -12f));
            spk.sizeDelta = new Vector2(500f, 28f);
            spk.pivot = new Vector2(0f, 1f);
            subSpeaker = spk.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subSpeaker, font, 20, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            var stx = SciFiGUI.Rect("Text", sub, new Vector2(0f, 1f), new Vector2(26f, -44f));
            stx.sizeDelta = new Vector2(806f, 44f);
            stx.pivot = new Vector2(0f, 1f);
            subText = stx.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subText, font, 22, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);

            // ---- 选项（字幕上方） ----
            choicesRoot = SciFiGUI.Rect("Choices", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 216f));
            choicesRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var choiceHolder = choicesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceHolder.spacing = 10f;
            choiceHolder.childAlignment = TextAnchor.MiddleCenter;
            choiceHolder.childForceExpandHeight = false;
            choiceHolder.childForceExpandWidth = false;

            // ---- 数据卡（右侧） ----
            var card = SciFiGUI.Rect("Card", canvas.transform, new Vector2(1f, 0.5f), new Vector2(-36f, 0f));
            card.sizeDelta = new Vector2(470f, 540f);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            var cardPanel = card.gameObject.AddComponent<Image>();
            cardPanel.sprite = panelSprite;
            cardPanel.type = Image.Type.Sliced;
            cardPanel.pixelsPerUnitMultiplier = 100f / 96f;
            var ct = SciFiGUI.Rect("Title", card, new Vector2(0f, 1f), new Vector2(26f, -20f));
            ct.sizeDelta = new Vector2(418f, 34f);
            ct.pivot = new Vector2(0f, 1f);
            cardTitle = ct.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardTitle, font, 25, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            var div = SciFiGUI.Rect("Divider", card, new Vector2(0f, 1f), new Vector2(26f, -64f));
            div.sizeDelta = new Vector2(418f, 2f);
            div.pivot = new Vector2(0f, 1f);
            div.gameObject.AddComponent<Image>().color = new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.45f);
            var cb = SciFiGUI.Rect("Body", card, new Vector2(0f, 1f), new Vector2(26f, -84f));
            cb.sizeDelta = new Vector2(418f, 380f);
            cb.pivot = new Vector2(0f, 1f);
            cardBody = cb.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardBody, font, 21, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.UpperLeft);
            cardBody.lineSpacing = 1.35f;

            // ---- 左上信息（速度/任务） ----
            var hud = SciFiGUI.Rect("Readout", canvas.transform, new Vector2(0f, 1f), new Vector2(28f, -24f));
            hud.sizeDelta = new Vector2(360f, 84f);
            var hudPanel = hud.gameObject.AddComponent<Image>();
            hudPanel.sprite = panelSprite;
            hudPanel.type = Image.Type.Sliced;
            hudPanel.pixelsPerUnitMultiplier = 100f / 96f;
            var spd = SciFiGUI.Rect("Speed", hud, new Vector2(0f, 1f), new Vector2(20f, -10f));
            spd.sizeDelta = new Vector2(320f, 32f);
            spd.pivot = new Vector2(0f, 1f);
            hudSpeed = spd.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudSpeed, font, 26, FontStyle.Bold, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);
            var qst = SciFiGUI.Rect("Quest", hud, new Vector2(0f, 1f), new Vector2(20f, -46f));
            qst.sizeDelta = new Vector2(320f, 26f);
            qst.pivot = new Vector2(0f, 1f);
            hudQuest = qst.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudQuest, font, 19, FontStyle.Normal, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
        }

        void Update()
        {
            if (subGroup != null)
            {
                float t = Time.time < subUntil ? 1f : 0f;
                subGroup.alpha = Mathf.MoveTowards(subGroup.alpha, t, Time.deltaTime * 6f);
            }
            if (cardGroup != null)
                cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, cardTarget, Time.deltaTime * 6f);
        }

        // ---------------- 对外 API ----------------

        public void ShowSubtitle(string speaker, string text, float duration)
        {
            subSpeaker.text = speaker;
            subText.text = text;
            subUntil = Time.time + duration;
        }

        public void SetSpeed(string text) => hudSpeed.text = text;
        public void SetQuest(string text) => hudQuest.text = text;

        public void ShowCard(string title, string body)
        {
            cardTitle.text = title;
            cardBody.text = body;
            cardTarget = 1f;
            cardGroup.blocksRaycasts = true;
        }

        public void HideCard()
        {
            cardTarget = 0f;
            cardGroup.blocksRaycasts = false;
        }

        public void ShowChoices(List<string> options, Action<int> onPick)
        {
            HideChoices();
            choiceCb = onPick;
            var panelSprite = SciFiGUI.RoundedPanel(72, 10f, 2f,
                new Color(0.02f, 0.06f, 0.1f, 0.85f), new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.6f));
            for (int i = 0; i < options.Count; i++)
            {
                int idx = i;
                var chip = SciFiGUI.Rect("Choice_" + i, choicesRoot, Vector2.zero, Vector2.zero);
                chip.sizeDelta = new Vector2(560f, 46f);
                var img = chip.gameObject.AddComponent<Image>();
                img.sprite = panelSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 100f / 72f;
                var btn = chip.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.45f, 0.85f, 0.95f, 0.35f);
                colors.pressedColor = new Color(0.3f, 0.7f, 0.85f, 0.55f);
                btn.colors = colors;
                var lbl = SciFiGUI.Rect("Label", chip, new Vector2(0.5f, 0.5f), Vector2.zero);
                lbl.sizeDelta = new Vector2(540f, 44f);
                var txt = lbl.gameObject.AddComponent<Text>();
                SciFiGUI.StyleText(txt, font, 21, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleCenter);
                txt.text = options[i];
                btn.onClick.AddListener(() => { HideChoices(); choiceCb?.Invoke(idx); });
                choiceObjs.Add(chip.gameObject);
            }
        }

        public void HideChoices()
        {
            foreach (var go in choiceObjs) Destroy(go);
            choiceObjs.Clear();
        }
    }
}
