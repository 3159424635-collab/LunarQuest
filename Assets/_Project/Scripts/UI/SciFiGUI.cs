using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// 赛博风 UI 公共绘制工具：圆角描边面板贴图（九宫格拉伸）、颜色常量。
    /// </summary>
    public static class SciFiGUI
    {
        public static readonly Color Cyan = new Color(0.49f, 0.96f, 1f);
        public static readonly Color BodyWhite = new Color(0.93f, 0.97f, 1f);
        public static readonly Color PanelFill = new Color(0.015f, 0.045f, 0.08f, 0.82f);

        /// <summary>生成圆角描边面板贴图（可九宫格拉伸，圆角不变形）。</summary>
        public static Sprite RoundedPanel(int size, float radius, float border, Color fill, Color edge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - c) - (c - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - c) - (c - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col = dist <= radius - border ? fill
                        : dist <= radius ? edge
                        : Color.clear;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            float b = c - radius;
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
            return r;
        }

        public static void StyleText(Text t, Font font, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
        }
    }
}
