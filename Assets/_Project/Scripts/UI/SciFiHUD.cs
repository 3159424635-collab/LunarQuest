using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 赛博风 HUD：字幕/选项/数据卡(含二维码)/左上信息。
    /// 不用 CanvasScaler（避免分辨率不匹配导致偏移），直接像素坐标。
    /// </summary>
    public class SciFiHUD : MonoBehaviour
    {
        public static SciFiHUD I { get; private set; }
        public Font font;

        CanvasGroup subGroup, cardGroup;
        Text subSpeaker, subText, cardTitle, cardBody, hudSpeed, hudQuest;
        RawImage cardQR;
        RectTransform choicesRoot, cardRect;
        readonly List<GameObject> choiceObjs = new List<GameObject>();
        Action<int> choiceCb;
        float subUntil;
        float subTarget, cardTarget;

        void Awake()
        {
            I = this;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            Rect safe = Screen.safeArea;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvas.pixelPerfect = true;
            // 不加 CanvasScaler —— 直接像素坐标适配任何分辨率

            var panelSprite = SciFiGUI.RoundedPanel(96, 14f, 2f, SciFiGUI.PanelFill,
                new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.75f));

            // ---- 字幕（底部居中，宽度限制在屏宽 80% 以内） ----
            var sub = SciFiGUI.Rect("Subtitle", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f));
            sub.sizeDelta = new Vector2(Mathf.Min(safe.width * 0.7f, 700f), 84f);
            subGroup = sub.gameObject.AddComponent<CanvasGroup>();
            subGroup.alpha = 0f;
            subGroup.blocksRaycasts = false;
            var subPanel = sub.gameObject.AddComponent<Image>();
            subPanel.sprite = panelSprite;
            subPanel.type = Image.Type.Sliced;
            var accent = SciFiGUI.Rect("Accent", sub, new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            accent.sizeDelta = new Vector2(5f, 60f);
            accent.gameObject.AddComponent<Image>().color = SciFiGUI.Cyan;
            var spk = SciFiGUI.Rect("Speaker", sub, new Vector2(0f, 1f), new Vector2(24f, -10f));
            spk.sizeDelta = new Vector2(500f, 26f);
            spk.pivot = new Vector2(0f, 1f);
            subSpeaker = spk.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subSpeaker, font, 20, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            var stx = SciFiGUI.Rect("Text", sub, new Vector2(0f, 1f), new Vector2(24f, -40f));
            stx.sizeDelta = new Vector2(Mathf.Min(750f, Screen.width * 0.75f), 42f);
            stx.pivot = new Vector2(0f, 1f);
            subText = stx.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subText, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);

            // ---- 选项（字幕上方） ----
            choicesRoot = SciFiGUI.Rect("Choices", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 200f));
            choicesRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var choiceHolder = choicesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceHolder.spacing = 8f;
            choiceHolder.childAlignment = TextAnchor.MiddleCenter;
            choiceHolder.childForceExpandHeight = false;
            choiceHolder.childForceExpandWidth = false;

            // ---- 数据卡（右侧，含二维码） ----
            cardRect = SciFiGUI.Rect("Card", canvas.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f));
            float cardW = Mathf.Min(safe.width * 0.35f, 380f);
            float cardH = Mathf.Min(safe.height * 0.8f, 500f);
            cardRect.sizeDelta = new Vector2(cardW, cardH);
            cardGroup = cardRect.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            var cardPanel = cardRect.gameObject.AddComponent<Image>();
            cardPanel.sprite = panelSprite;
            cardPanel.type = Image.Type.Sliced;

            var ct = SciFiGUI.Rect("Title", cardRect, new Vector2(0f, 1f), new Vector2(22f, -16f));
            ct.sizeDelta = new Vector2(cardW - 44f, 32f);
            ct.pivot = new Vector2(0f, 1f);
            cardTitle = ct.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardTitle, font, 24, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);

            var div = SciFiGUI.Rect("Divider", cardRect, new Vector2(0f, 1f), new Vector2(22f, -56f));
            div.sizeDelta = new Vector2(cardW - 44f, 2f);
            div.pivot = new Vector2(0f, 1f);
            div.gameObject.AddComponent<Image>().color = new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.45f);

            var cb = SciFiGUI.Rect("Body", cardRect, new Vector2(0f, 1f), new Vector2(22f, -72f));
            cb.sizeDelta = new Vector2(cardW - 44f, cardH - 260f);
            cb.pivot = new Vector2(0f, 1f);
            cardBody = cb.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardBody, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.UpperLeft);
            cardBody.lineSpacing = 1.3f;

            // 二维码区域（卡片底部，居中）
            float qrSize = 160f;
            var qrRect = SciFiGUI.Rect("QRCode", cardRect, new Vector2(0.5f, 0f), new Vector2(0f, 20f));
            qrRect.sizeDelta = new Vector2(qrSize, qrSize);
            cardQR = qrRect.gameObject.AddComponent<RawImage>();
            cardQR.raycastTarget = false;

            // 二维码下方提示文字
            var qrHint = SciFiGUI.Rect("QRHint", cardRect, new Vector2(0.5f, 0f), new Vector2(0f, 6f));
            qrHint.sizeDelta = new Vector2(cardW - 40f, 18f);
            var qrHintText = qrHint.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(qrHintText, font, 14, FontStyle.Normal,
                new Color(0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            qrHintText.text = "↑ 手机扫码查看 AR 模型";

            // ---- 左上信息 ----
            var hud = SciFiGUI.Rect("Readout", canvas.transform, new Vector2(0f, 1f), new Vector2(16f, -16f));
            hud.sizeDelta = new Vector2(Mathf.Min(safe.width * 0.28f, 300f), 72f);
            var hudPanel = hud.gameObject.AddComponent<Image>();
            hudPanel.sprite = panelSprite;
            hudPanel.type = Image.Type.Sliced;
            var spd = SciFiGUI.Rect("Speed", hud, new Vector2(0f, 1f), new Vector2(16f, -8f));
            spd.sizeDelta = new Vector2(300f, 30f);
            spd.pivot = new Vector2(0f, 1f);
            hudSpeed = spd.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudSpeed, font, 24, FontStyle.Bold, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);
            var qst = SciFiGUI.Rect("Quest", hud, new Vector2(0f, 1f), new Vector2(16f, -42f));
            qst.sizeDelta = new Vector2(300f, 24f);
            qst.pivot = new Vector2(0f, 1f);
            hudQuest = qst.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudQuest, font, 18, FontStyle.Normal, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
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

        public void ShowCard(string title, string body, Texture2D qrTexture = null)
        {
            cardTitle.text = title;
            cardBody.text = body;
            if (cardQR != null)
            {
                cardQR.texture = qrTexture;
                cardQR.enabled = qrTexture != null;
            }
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
                chip.sizeDelta = new Vector2(Mathf.Min(Screen.safeArea.width * 0.45f, 440f), 40f);
                var img = chip.gameObject.AddComponent<Image>();
                img.sprite = panelSprite;
                img.type = Image.Type.Sliced;
                var btn = chip.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.45f, 0.85f, 0.95f, 0.35f);
                btn.colors = colors;
                var lbl = SciFiGUI.Rect("Label", chip, new Vector2(0.5f, 0.5f), Vector2.zero);
                lbl.sizeDelta = new Vector2(Mathf.Min(500f, Screen.width * 0.48f), 40f);
                var txt = lbl.gameObject.AddComponent<Text>();
                SciFiGUI.StyleText(txt, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleCenter);
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
