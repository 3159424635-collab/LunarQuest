using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 交互提示（赛博朋克 2077 风格）：准星下方，[按键徽章 + 动作文本] 水平排列。
    /// 解决字模糊：字体渲染 200px，整体 UI 缩小 0.15x → 等效 30px 但内部高分辨率。
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

        // 缩放系数：字体渲染 SIZE_MULTIPLIER 倍大小，UI 整体缩小回去
        const float SIZE_MULTIPLIER = 6f;
        const float UI_SCALE = 1f / SIZE_MULTIPLIER;

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

            // 准星（正常大小）
            var cross = Rect("Crosshair", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero);
            cross.sizeDelta = new Vector2(6f, 6f);
            cross.gameObject.AddComponent<Image>().sprite = MakeDot();
            cross.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.7f);
            cross.GetComponent<Image>().raycastTarget = false;

            // 提示容器：先建一个大尺寸容器，再整体缩小
            var row = Rect("PromptRow", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f * SIZE_MULTIPLIER));
            row.sizeDelta = new Vector2(800f * SIZE_MULTIPLIER, 72f * SIZE_MULTIPLIER);
            row.localScale = Vector3.one * UI_SCALE;
            group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f * SIZE_MULTIPLIER;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // 按键徽章（大尺寸渲染，随容器缩小）
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(row, false);
            var badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.sizeDelta = new Vector2(64f * SIZE_MULTIPLIER, 64f * SIZE_MULTIPLIER);
            badge = badgeGo.AddComponent<Image>();
            badge.sprite = MakeChip();
            badge.raycastTarget = false;
            badgeText = badgeGo.AddComponent<Text>();
            SetupText(badgeText, (int)(34 * SIZE_MULTIPLIER), FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchRect(badgeText.rectTransform);

            // 动作文本
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(row, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.sizeDelta = new Vector2(650f * SIZE_MULTIPLIER, 64f * SIZE_MULTIPLIER);
            label = labelGo.AddComponent<Text>();
            SetupText(label, (int)(30 * SIZE_MULTIPLIER), FontStyle.Normal, new Color(0.87f, 0.98f, 1f), TextAnchor.MiddleLeft);
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

        void SetupText(Text t, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            if (font != null) t.font = font;
            else t.font = GetFont();
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

        static void StretchRect(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static Font GetFont()
        {
            try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
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
