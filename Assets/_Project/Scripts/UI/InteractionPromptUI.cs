using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 交互提示（赛博朋克 2077 风格）：准星下方居中显示 [按键徽章 + 动作文本]，
    /// 淡入淡出。自建 Canvas 与图形，无需场景接线；模块四交付正式界面时可禁用本组件替换。
    /// 数据源：PlayerInteractor.OnPromptChanged。
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        public Font font;   // 搭建脚本注入思源黑体
        CanvasGroup group;
        Image badge;
        Text badgeText;
        Text label;
        float targetAlpha;
        string shown;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 准星：中心小点
            var cross = NewRect("Crosshair", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero);
            cross.sizeDelta = new Vector2(5f, 5f);
            var crossImg = cross.gameObject.AddComponent<Image>();
            crossImg.sprite = MakeDot();
            crossImg.color = new Color(1f, 1f, 1f, 0.7f);

            // 提示根：准星下方 70px
            var root = NewRect("Prompt", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // 按键徽章（圆角描边小方块）
            var badgeRect = NewRect("KeyBadge", root, new Vector2(1f, 0.5f), new Vector2(-10f, 0f));
            badgeRect.sizeDelta = new Vector2(44f, 44f);
            badge = badgeRect.gameObject.AddComponent<Image>();
            badge.sprite = MakeChip();
            badge.rectTransform.pivot = new Vector2(1f, 0.5f);
            var bt = NewRect("KeyText", badgeRect, new Vector2(0.5f, 0.5f), Vector2.zero);
            badgeText = bt.gameObject.AddComponent<Text>();
            SetupText(badgeText, 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            bt.sizeDelta = new Vector2(44f, 44f);

            // 动作文本（徽章右侧）
            var labelRect = NewRect("Label", root, new Vector2(0f, 0.5f), new Vector2(10f, 0f));
            labelRect.sizeDelta = new Vector2(560f, 44f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            label = labelRect.gameObject.AddComponent<Text>();
            SetupText(label, 21, FontStyle.Normal, new Color(0.87f, 0.98f, 1f), TextAnchor.MiddleLeft);
            var outline = labelRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.05f, 0.1f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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

            // 解析"按 X / 按 X 或 Y"按键与动作文本
            var m = Regex.Match(text, "按\\s*([A-Za-z])");
            string key = m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
            string action = text;
            if (m.Success)
                action = Regex.Replace(text, "^按\\s*[A-Za-z](\\s*或\\s*[A-Za-z])?\\s*", "").Trim();
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

        static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            return r;
        }

        void SetupText(Text t, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            t.font = font != null ? font : GetFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
        }

        static Font GetFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        }

        static Sprite MakeDot()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    bool inside = (x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f) <= 12f;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
        }

        static Sprite MakeChip()
        {
            const int S = 64, R = 14, B = 3;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var fill = new Color(0.02f, 0.06f, 0.1f, 0.72f);
            var edge = new Color(0.62f, 0.95f, 1f, 0.95f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - S / 2f) - (S / 2f - R), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - S / 2f) - (S / 2f - R), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = dist <= R - B ? fill : (dist <= R ? edge : Color.clear);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), Vector2.one * 0.5f);
        }
    }
}
