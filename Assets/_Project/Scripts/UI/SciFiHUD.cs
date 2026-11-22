using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public static bool ModalOpen { get; private set; }
        public Font font;

        CanvasGroup subGroup, cardGroup, choicesGroup;
        Text subSpeaker, subText, cardTitle, cardBody, cardQrHint, hudSpeed, hudQuestTitle, hudQuestDetail;
        RawImage cardQR;
        RectTransform choicesRoot, cardRect, subtitleRect, subtitleTextRect, subtitleAccent;
        readonly List<GameObject> choiceObjs = new List<GameObject>();
        Action<int> choiceCb;
        CursorLockMode cursorLockBeforeChoices;
        bool cursorVisibleBeforeChoices;
        bool choicesOpen;
        float subUntil;
        float subTarget, cardTarget;

        void Awake()
        {
            I = this;
            // 每个场景的 HUD 都从非模态状态开始，避免跨场景残留的静态标记锁住玩家输入。
            ModalOpen = false;
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } }

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvas = gameObject.AddComponent<Canvas>();
            Rect safe = Screen.safeArea;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvas.pixelPerfect = true;
            // 不加 CanvasScaler —— 直接像素坐标适配任何分辨率

            var panelSprite = SciFiGUI.RoundedPanel(96, 14f, 2f, SciFiGUI.PanelFill,
                new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.75f));

            // ---- 字幕（底部居中，宽度限制在屏宽 80% 以内） ----
            var sub = SciFiGUI.RectP("Subtitle", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(0.5f, 0f));
            sub.sizeDelta = new Vector2(Mathf.Min(safe.width * 0.82f, 820f), 96f);
            subtitleRect = sub;
            subGroup = sub.gameObject.AddComponent<CanvasGroup>();
            subGroup.alpha = 0f;
            subGroup.blocksRaycasts = false;
            var subPanel = sub.gameObject.AddComponent<Image>();
            subPanel.sprite = panelSprite;
            subPanel.type = Image.Type.Sliced;
            var accent = SciFiGUI.RectP("Accent", sub, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(0f, 0.5f));
            accent.sizeDelta = new Vector2(5f, 72f);
            subtitleAccent = accent;
            accent.gameObject.AddComponent<Image>().color = SciFiGUI.Cyan;
            var spk = SciFiGUI.RectP("Speaker", sub, new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(0f, 1f));
            spk.sizeDelta = new Vector2(sub.sizeDelta.x - 48f, 26f);
            spk.pivot = new Vector2(0f, 1f);
            subSpeaker = spk.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subSpeaker, font, 20, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            var stx = SciFiGUI.RectP("Text", sub, new Vector2(0f, 1f), new Vector2(24f, -40f), new Vector2(0f, 1f));
            stx.sizeDelta = new Vector2(sub.sizeDelta.x - 48f, 48f);
            stx.pivot = new Vector2(0f, 1f);
            subtitleTextRect = stx;
            subText = stx.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(subText, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);
            subText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subText.verticalOverflow = VerticalWrapMode.Truncate;
            subText.resizeTextForBestFit = true;
            subText.resizeTextMinSize = 16;
            subText.resizeTextMaxSize = 20;

            // ---- 选项（字幕上方） ----
            choicesRoot = SciFiGUI.RectP("Choices", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(0.5f, 0f));
            choicesRoot.sizeDelta = new Vector2(Mathf.Min(safe.width * 0.72f, 620f), 0f);
            choicesGroup = choicesRoot.gameObject.AddComponent<CanvasGroup>();
            choicesGroup.blocksRaycasts = false;
            var choiceHolder = choicesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceHolder.spacing = 8f;
            choiceHolder.padding = new RectOffset(8, 8, 8, 8);
            choiceHolder.childAlignment = TextAnchor.MiddleCenter;
            choiceHolder.childControlHeight = true;
            choiceHolder.childControlWidth = true;
            choiceHolder.childForceExpandHeight = false;
            choiceHolder.childForceExpandWidth = true;
            var choiceFitter = choicesRoot.gameObject.AddComponent<ContentSizeFitter>();
            choiceFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            choiceFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- 数据卡（右侧，含二维码） ----
            cardRect = SciFiGUI.RectP("Card", canvas.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(1f, 0.5f));
            float cardW = Mathf.Min(safe.width * 0.35f, 380f);
            float cardH = Mathf.Min(safe.height * 0.8f, 500f);
            cardRect.sizeDelta = new Vector2(cardW, cardH);
            cardGroup = cardRect.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            var cardPanel = cardRect.gameObject.AddComponent<Image>();
            cardPanel.sprite = panelSprite;
            cardPanel.type = Image.Type.Sliced;

            var ct = SciFiGUI.RectP("Title", cardRect, new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(0f, 1f));
            ct.sizeDelta = new Vector2(cardW - 44f, 32f);
            ct.pivot = new Vector2(0f, 1f);
            cardTitle = ct.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardTitle, font, 24, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);

            var div = SciFiGUI.RectP("Divider", cardRect, new Vector2(0f, 1f), new Vector2(22f, -56f), new Vector2(0f, 1f));
            div.sizeDelta = new Vector2(cardW - 44f, 2f);
            div.pivot = new Vector2(0f, 1f);
            div.gameObject.AddComponent<Image>().color = new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.45f);

            var cb = SciFiGUI.RectP("Body", cardRect, new Vector2(0f, 1f), new Vector2(22f, -72f), new Vector2(0f, 1f));
            cb.sizeDelta = new Vector2(cardW - 44f, cardH - 260f);
            cb.pivot = new Vector2(0f, 1f);
            cardBody = cb.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardBody, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.UpperLeft);
            cardBody.lineSpacing = 1.3f;

            // 二维码区域（卡片底部，居中）
            float qrSize = 160f;
            var qrRect = SciFiGUI.RectP("QRCode", cardRect, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(0.5f, 0f));
            qrRect.sizeDelta = new Vector2(qrSize, qrSize);
            cardQR = qrRect.gameObject.AddComponent<RawImage>();
            cardQR.raycastTarget = false;

            // 二维码下方提示文字
            var qrHint = SciFiGUI.RectP("QRHint", cardRect, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(0.5f, 0f));
            qrHint.sizeDelta = new Vector2(cardW - 40f, 18f);
            cardQrHint = qrHint.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(cardQrHint, font, 14, FontStyle.Normal,
                new Color(0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            cardQrHint.text = "↑ 手机扫码查看 AR 模型";

            // ---- 左上信息 ----
            var hud = SciFiGUI.RectP("Readout", canvas.transform, new Vector2(0f, 1f), new Vector2(safe.x + 12f, -safe.y - 12f), new Vector2(0f, 1f));
            float hudWidth = Mathf.Min(safe.width - 32f, Mathf.Clamp(safe.width * 0.36f, 360f, 460f));
            hud.sizeDelta = new Vector2(hudWidth, 152f);
            var hudPanel = hud.gameObject.AddComponent<Image>();
            hudPanel.sprite = panelSprite;
            hudPanel.type = Image.Type.Sliced;

            var trackerLabel = SciFiGUI.RectP("TrackerLabel", hud, new Vector2(0f, 1f),
                new Vector2(16f, -9f), new Vector2(0f, 1f));
            trackerLabel.sizeDelta = new Vector2(150f, 24f);
            trackerLabel.pivot = new Vector2(0f, 1f);
            Text labelText = trackerLabel.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(labelText, font, 15, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            labelText.text = "任务追踪";

            var spd = SciFiGUI.RectP("Speed", hud, new Vector2(1f, 1f), new Vector2(-16f, -9f), new Vector2(1f, 1f));
            spd.sizeDelta = new Vector2(170f, 24f);
            spd.pivot = new Vector2(1f, 1f);
            hudSpeed = spd.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudSpeed, font, 17, FontStyle.Bold, SciFiGUI.BodyWhite, TextAnchor.MiddleRight);

            var divider = SciFiGUI.RectP("TrackerDivider", hud, new Vector2(0f, 1f),
                new Vector2(16f, -37f), new Vector2(0f, 1f));
            divider.sizeDelta = new Vector2(hudWidth - 32f, 1f);
            divider.pivot = new Vector2(0f, 1f);
            divider.gameObject.AddComponent<Image>().color =
                new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.38f);

            var title = SciFiGUI.RectP("QuestTitle", hud, new Vector2(0f, 1f),
                new Vector2(16f, -44f), new Vector2(0f, 1f));
            title.sizeDelta = new Vector2(hudWidth - 32f, 28f);
            title.pivot = new Vector2(0f, 1f);
            hudQuestTitle = title.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudQuestTitle, font, 19, FontStyle.Bold, SciFiGUI.BodyWhite, TextAnchor.MiddleLeft);

            var detail = SciFiGUI.RectP("QuestDetail", hud, new Vector2(0f, 1f),
                new Vector2(16f, -74f), new Vector2(0f, 1f));
            detail.sizeDelta = new Vector2(hudWidth - 32f, 68f);
            detail.pivot = new Vector2(0f, 1f);
            hudQuestDetail = detail.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(hudQuestDetail, font, 16, FontStyle.Normal, SciFiGUI.Cyan, TextAnchor.UpperLeft);
            hudQuestDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            hudQuestDetail.verticalOverflow = VerticalWrapMode.Truncate;
            hudQuestDetail.resizeTextForBestFit = true;
            hudQuestDetail.resizeTextMinSize = 14;
            hudQuestDetail.resizeTextMaxSize = 16;
            hudQuestDetail.lineSpacing = 1.08f;
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

            if (choicesOpen)
            {
                for (int i = 0; i < Mathf.Min(choiceObjs.Count, 9); i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                        Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                    {
                        Action<int> callback = choiceCb;
                        HideChoices();
                        callback?.Invoke(i);
                        break;
                    }
            }
        }

        // ---------------- 对外 API ----------------

        public void ShowSubtitle(string speaker, string text, float duration)
        {
            subSpeaker.text = speaker;
            subText.text = text ?? string.Empty;
            if (subtitleRect != null && subtitleTextRect != null)
            {
                Canvas.ForceUpdateCanvases();
                float preferred = Mathf.Max(24f, subText.preferredHeight);
                float maxHeight = Mathf.Clamp(Screen.safeArea.height * 0.42f, 120f, 230f);
                float panelHeight = Mathf.Clamp(preferred + 58f, 96f, maxHeight);
                subtitleRect.sizeDelta = new Vector2(subtitleRect.sizeDelta.x, panelHeight);
                subtitleTextRect.sizeDelta = new Vector2(subtitleRect.sizeDelta.x - 48f, panelHeight - 50f);
                if (subtitleAccent != null)
                    subtitleAccent.sizeDelta = new Vector2(5f, panelHeight - 24f);
            }
            subUntil = Time.time + duration;
        }

        public void SetSpeed(string text) => hudSpeed.text = text;
        public void SetQuest(string text)
        {
            text = text ?? string.Empty;
            int split = text.IndexOf('\n');
            if (split < 0)
            {
                hudQuestTitle.text = text;
                hudQuestDetail.text = string.Empty;
                return;
            }
            hudQuestTitle.text = text.Substring(0, split).Trim();
            hudQuestDetail.text = text.Substring(split + 1).Trim();
        }

        /// <summary>切换场景后恢复步行输入，用于处理旧 HUD 被销毁时的模态状态残留。</summary>
        public static void ResetModalState()
        {
            ModalOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ShowCard(string title, string body, Texture2D qrTexture = null)
        {
            cardTitle.text = title;
            cardBody.text = body;
            bool hasQr = qrTexture != null;
            if (cardQR != null)
            {
                cardQR.texture = qrTexture;
                cardQR.gameObject.SetActive(hasQr);
            }
            if (cardQrHint != null) cardQrHint.gameObject.SetActive(hasQr);
            if (cardRect != null && cardBody != null)
            {
                float bodyHeight = hasQr ? cardRect.rect.height - 260f : cardRect.rect.height - 104f;
                cardBody.rectTransform.sizeDelta = new Vector2(
                    cardRect.rect.width - 44f, Mathf.Max(120f, bodyHeight));
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
            cursorLockBeforeChoices = Cursor.lockState;
            cursorVisibleBeforeChoices = Cursor.visible;
            choicesOpen = true;
            ModalOpen = true;
            if (choicesGroup != null) choicesGroup.blocksRaycasts = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var panelSprite = SciFiGUI.RoundedPanel(72, 10f, 2f,
                new Color(0.02f, 0.06f, 0.1f, 0.85f), new Color(SciFiGUI.Cyan.r, SciFiGUI.Cyan.g, SciFiGUI.Cyan.b, 0.6f));
            for (int i = 0; i < options.Count; i++)
            {
                int idx = i;
                string label = $"{i + 1}. {options[i]}";
                var chip = SciFiGUI.Rect("Choice_" + i, choicesRoot, Vector2.zero, Vector2.zero);
                var layout = chip.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = Mathf.Min(Screen.safeArea.width * 0.6f, 500f);
                layout.preferredWidth = choicesRoot.sizeDelta.x - 16f;
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
                lbl.anchorMin = Vector2.zero;
                lbl.anchorMax = Vector2.one;
                lbl.offsetMin = new Vector2(16f, 6f);
                lbl.offsetMax = new Vector2(-16f, -6f);
                var txt = lbl.gameObject.AddComponent<Text>();
                SciFiGUI.StyleText(txt, font, 20, FontStyle.Normal, SciFiGUI.BodyWhite, TextAnchor.MiddleCenter);
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.verticalOverflow = VerticalWrapMode.Truncate;
                txt.text = label;
                float textWidth = Mathf.Max(180f, choicesRoot.sizeDelta.x - 64f);
                TextGenerationSettings generation = txt.GetGenerationSettings(new Vector2(textWidth, 0f));
                float preferredTextHeight = txt.cachedTextGeneratorForLayout.GetPreferredHeight(label, generation);
                float chipHeight = Mathf.Clamp(preferredTextHeight + 24f, 56f,
                    Mathf.Min(180f, Screen.safeArea.height * 0.28f));
                layout.minHeight = chipHeight;
                layout.preferredHeight = chipHeight;
                btn.onClick.AddListener(() =>
                {
                    Action<int> callback = choiceCb;
                    HideChoices();
                    callback?.Invoke(idx);
                });
                choiceObjs.Add(chip.gameObject);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesRoot);
        }

        public void HideChoices()
        {
            foreach (var go in choiceObjs) Destroy(go);
            choiceObjs.Clear();
            if (!choicesOpen) return;
            choicesOpen = false;
            ModalOpen = false;
            if (choicesGroup != null) choicesGroup.blocksRaycasts = false;
            Cursor.lockState = cursorLockBeforeChoices;
            Cursor.visible = cursorVisibleBeforeChoices;
        }

        void OnDestroy()
        {
            HideChoices();
            ModalOpen = false;
            if (I == this) I = null;
        }
    }
}
