using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LunarQuest
{
    /// <summary>
    /// 交互提示（赛博朋克 2077 风格）：准星下方，[按键徽章 + 动作文本] 水平排列。
    /// 使用 TextMeshPro（SDF 渲染，任何缩放都清晰锐利）。
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        public TMP_FontAsset tmpFont;

        CanvasGroup group;
        Image badge;
        TextMeshProUGUI badgeText;
        TextMeshProUGUI label;
        float targetAlpha;
        string shown;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (tmpFont == null) tmpFont = TMP_Settings.defaultFontAsset;

            // 准星
            var cross = Rect("Crosshair", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero);
            cross.sizeDelta = new Vector2(6f, 6f);
            var crossImg = cross.gameObject.AddComponent<Image>();
            crossImg.sprite = MakeDot();
            crossImg.color = new Color(1f, 1f, 1f, 0.7f);
            crossImg.raycastTarget = false;

            // 提示行（水平布局组：徽章和文本自动排列、垂直居中）
            var row = Rect("PromptRow", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f));
            row.sizeDelta = new Vector2(800f, 72f);
            group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // 按键徽章
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(row, false);
            var badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.sizeDelta = new Vector2(64f, 64f);
            badge = badgeGo.AddComponent<Image>();
            badge.sprite = MakeChip();
            badge.raycastTarget = false;
            badgeText = badgeGo.AddComponent<TextMeshProUGUI>();
            SetupTMP(badgeText, 34, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            badgeText.rectStretch();

            // 动作文本
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(row, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.sizeDelta = new Vector2(650f, 64f);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            SetupTMP(label, 30, FontStyles.Normal, new Color(0.87f, 0.98f, 1f), TextAlignmentOptions.Left);
        }

        void Start()
        {
            var pi = FindObjectOfType<PlayerInteractor>();
            if (pi != null) pi.OnPromptChanged += Show;
        }

        void OnDestroy()
        {
            var pi = FindObjectOfType<PlayerInteractor>();
            if (pi != null) pi.OnPromptChanged -= Show;
        }

        public void Show(string text)
        {
            if (text == shown) return;
            shown = text;
            if (string.IsNullOrEmpty(text)) { targetAlpha = 0f; return; }

            var m = Regex.Match(text, "按\\s*([A-Za-z])");
            string key = m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
            string action = m.Success
                ? Regex.Replace(text, "^按\\s*[A-Za-z](\\s*或\\s*[A-Za-z])?\\s*", "").Trim()
                : text;
            badgeText.text = key ?? "";
            badge.enabled = key != null;
            label.text = action;
            targetAlpha = 1f;
        }

        void Update()
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * 7f);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            return r;
        }

        void SetupTMP(TextMeshProUGUI t, int size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            if (tmpFont != null) t.font = tmpFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.enableWordWrapping = false;
            t.extraPadding = true;
            t.raycastTarget = false;
        }

        static Sprite MakeDot()
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool inside = (x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f) <= 48f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);
        }

        static Sprite MakeChip()
        {
            const int S = 128, R = 28, B = 4;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var fill = new Color(0.02f, 0.06f, 0.1f, 0.78f);
            var edge = new Color(0.62f, 0.95f, 1f, 0.95f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - S / 2f) - (S / 2f - R), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - S / 2f) - (S / 2f - R), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, dist <= R - B ? fill : (dist <= R ? edge : Color.clear));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), Vector2.one * 0.5f);
        }
    }

    static class TMPExtensions
    {
        public static void rectStretch(this TextMeshProUGUI tmp)
        {
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
