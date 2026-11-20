using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 交互提示（赛博朋克 2077 风格）：准星下方，[按键徽章 + 动作文本] 水平排列。
    /// 使用 ConstantPixelSize 画布（不用 ScaleWithScreenSize，避免分辨率不匹配导致偏移），
    /// 字体 60px + Pixel Perfect 保证清晰。
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        public Font font;

        CanvasGroup group;
        Image badge;
        Text badgeText;
        Text label;
        float targetAlpha;
        string shown;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            Rect safe = Screen.safeArea;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            // 不用 CanvasScaler —— 直接用像素坐标，适配任何分辨率
            // 字号直接设大（60px），不搞缩放把戏

            // 准星
            var cross = new GameObject("Crosshair", typeof(RectTransform));
            cross.transform.SetParent(canvas.transform, false);
            var crossRect = (RectTransform)cross.transform;
            crossRect.anchorMin = crossRect.anchorMax = new Vector2(0.5f, 0.5f);
            crossRect.sizeDelta = new Vector2(5f, 5f);
            var crossImg = cross.AddComponent<Image>();
            crossImg.sprite = MakeDot();
            crossImg.color = new Color(1f, 1f, 1f, 0.7f);
            crossImg.raycastTarget = false;

            // 提示行（锚点居中，向下偏移 90px）
            var row = new GameObject("PromptRow", typeof(RectTransform));
            row.transform.SetParent(canvas.transform, false);
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, -90f);
            rowRect.sizeDelta = new Vector2(Mathf.Min(800f, Screen.width * 0.85f), 70f);
            group = row.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // 按键徽章
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(rowRect, false);
            var badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.sizeDelta = new Vector2(56f, 56f);
            badge = badgeGo.AddComponent<Image>();
            badge.sprite = MakeChip();
            badge.raycastTarget = false;

            // Image 与 Text 都继承自 Graphic，Unity 不允许它们挂在同一个 GameObject 上。
            // 文字使用独立子物体，避免 Awake 阶段 AddComponent<Text>() 失败后空引用。
            var badgeTextGo = new GameObject("Key", typeof(RectTransform));
            badgeTextGo.transform.SetParent(badgeRect, false);
            badgeText = badgeTextGo.AddComponent<Text>();
            SetupText(badgeText, 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(badgeText.rectTransform);

            // 动作文本
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowRect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.sizeDelta = new Vector2(Mathf.Min(600f, Screen.width * 0.6f), 56f);
            label = labelGo.AddComponent<Text>();
            SetupText(label, 28, FontStyle.Normal, new Color(0.87f, 0.98f, 1f), TextAnchor.MiddleLeft);
            var outline = labelGo.AddComponent<Outline>();
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

        void SetupText(Text t, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            if (t == null) return;
            try
            {
            var safeFont = font != null ? font : GetFont();
            if (safeFont != null) t.font = safeFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.resizeTextForBestFit = false;
            t.raycastTarget = false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[InteractionPromptUI] SetupText 安全跳过: " + e.Message);
            }
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static Font GetFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Verdana.ttf"); } catch { }
            return f; // 可能返回 null，由调用方处理
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
}
