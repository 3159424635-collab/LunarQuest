using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest
{
    /// <summary>
    /// 着陆小游戏：↑↓控制反推推力，←→水平微调。
    /// 明亮月面 + 中文标签 HUD + 导航平板。
    /// </summary>
    public class LandingMinigame : MonoBehaviour
    {
        [Header("物理")]
        public float gravity = 1.62f;
        public float maxThrust = 10f;
        public float thrustRate = 5f;
        public float rcsForce = 3f;
        public float startAltitude = 500f;
        public float fuelCapacity = 100f;
        public float fuelBurnRate = 4f;

        [Header("安全阈值")]
        public float safeVerticalSpeed = 5f;
        public float safeHorizontalSpeed = 3f;
        public float safeStress = 0.8f;

        [Header("视觉")]
        public Camera mainCam;

        float altitude, vSpeed, hSpeed, thrust, fuel, stress;
        bool playing, landed, crashed;
        float gameTimer;
        float tutorialTimer = 5f; // 前5秒显示教学

        Texture2D texWhite, texGreen, texYellow, texRed, texCyan, texDark;
        GUIStyle sLabel, sValue, sBig, sHint;
        bool init;

        void Start() { ResetGame(); }

        public void ResetGame()
        {
            altitude = startAltitude;
            vSpeed = -15f;
            hSpeed = 0f;
            thrust = 0f;
            fuel = fuelCapacity;
            playing = true;
            landed = crashed = false;
            stress = 0f;
            gameTimer = 0f;
            tutorialTimer = 5f;
        }

        void Update()
        {
            if (!playing) return;
            gameTimer += Time.deltaTime;
            if (tutorialTimer > 0) tutorialTimer -= Time.deltaTime;

            float target = Input.GetKey(KeyCode.UpArrow) ? maxThrust : 0f;
            thrust = Mathf.MoveTowards(thrust, target, thrustRate * Time.deltaTime);

            float h = Input.GetAxisRaw("Horizontal");
            hSpeed += h * rcsForce * Time.deltaTime;
            hSpeed *= 1f - 0.3f * Time.deltaTime;

            float burn = (thrust / maxThrust) * fuelBurnRate * Time.deltaTime;
            fuel = Mathf.Max(0f, fuel - burn);
            if (fuel <= 0) thrust = 0;

            vSpeed += (thrust - gravity) * Time.deltaTime;
            altitude += vSpeed * Time.deltaTime;

            float vS = Mathf.Abs(vSpeed) / (safeVerticalSpeed * 2f);
            float hS = Mathf.Abs(hSpeed) / (safeHorizontalSpeed * 2f);
            stress = Mathf.Clamp01(Mathf.Max(vS, hS));

            // 相机跟随高度（视觉反馈）
            if (mainCam != null)
            {
                float t = Mathf.Clamp01(altitude / startAltitude);
                float camY = Mathf.Lerp(-29.5f, 0f, t);
                mainCam.transform.position = new Vector3(
                    mainCam.transform.position.x + hSpeed * 0.05f * Time.deltaTime, camY, 0);
            }

            if (altitude <= 0f)
            {
                altitude = 0f;
                playing = false;
                bool safe = Mathf.Abs(vSpeed) <= safeVerticalSpeed && Mathf.Abs(hSpeed) <= safeHorizontalSpeed;
                landed = safe;
                crashed = !safe;
                if (QuestManager.I != null)
                    QuestManager.I.FireEvent(safe ? "landing_succeeded" : "landing_failed");
            }
        }

        void OnGUI()
        {
            if (!init) Init();
            if (!playing && !landed && !crashed) return;

            float sw = Screen.width, sh = Screen.height;

            // ==== 底部信息面板 ====
            float panelW = Mathf.Min(sw * 0.85f, 700f);
            float panelH = 130f;
            float panelY = sh - panelH - 20f;
            Rect panel = new Rect((sw - panelW) / 2, panelY, panelW, panelH);

            // 半透明背景
            Color saved = GUI.color;
            GUI.color = new Color(0, 0.02f, 0.05f, 0.85f);
            GUI.DrawTexture(panel, texWhite);
            GUI.color = saved;
            // 青色边框
            DrawBorder(panel, 2f, new Color(0.3f, 0.8f, 1f, 0.6f));

            // 高度
            float colW = panelW / 5;
            Rect r1 = new Rect(panel.x + 10, panel.y + 8, colW - 15, panelH - 16);
            GUI.Label(new Rect(r1.x, r1.y, r1.width, 22), "高度", sLabel);
            GUI.Label(new Rect(r1.x, r1.y + 24, r1.width, 34), altitude >= 100 ? $"{altitude:F0}m" : $"{altitude:F1}m", sValue);
            DrawVBar(new Rect(r1.x, r1.y + 62, 8, 50), Mathf.Clamp01(altitude / startAltitude), Color.cyan);

            // 垂直速度
            Rect r2 = new Rect(panel.x + colW, panel.y + 8, colW - 15, panelH - 16);
            GUI.Label(new Rect(r2.x, r2.y, r2.width, 22), "下降速度", sLabel);
            GUI.Label(new Rect(r2.x, r2.y + 24, r2.width, 34), $"{vSpeed:F1} m/s", sValue);
            Color vc = Mathf.Abs(vSpeed) <= safeVerticalSpeed ? Color.green : Mathf.Abs(vSpeed) <= safeVerticalSpeed * 1.5f ? Color.yellow : Color.red;
            DrawVBar(new Rect(r2.x, r2.y + 62, 8, 50), Mathf.Clamp01(Mathf.Abs(vSpeed) / 20f), vc);

            // 水平速度
            Rect r3 = new Rect(panel.x + colW * 2, panel.y + 8, colW - 15, panelH - 16);
            GUI.Label(new Rect(r3.x, r3.y, r3.width, 22), "水平速度", sLabel);
            GUI.Label(new Rect(r3.x, r3.y + 24, r3.width, 34), $"{hSpeed:F1} m/s", sValue);
            Color hc = Mathf.Abs(hSpeed) <= safeHorizontalSpeed ? Color.green : Color.yellow;
            DrawVBar(new Rect(r3.x, r3.y + 62, 8, 50), Mathf.Clamp01(Mathf.Abs(hSpeed) / 8f), hc);

            // 燃料
            Rect r4 = new Rect(panel.x + colW * 3, panel.y + 8, colW - 15, panelH - 16);
            GUI.Label(new Rect(r4.x, r4.y, r4.width, 22), "燃料", sLabel);
            GUI.Label(new Rect(r4.x, r4.y + 24, r4.width, 34), $"{fuel:F0}%", sValue);
            Color fc = fuel > 30f ? Color.cyan : Color.red;
            DrawVBar(new Rect(r4.x, r4.y + 62, 8, 50), fuel / fuelCapacity, fc);

            // 推力
            Rect r5 = new Rect(panel.x + colW * 4, panel.y + 8, colW - 15, panelH - 16);
            GUI.Label(new Rect(r5.x, r5.y, r5.width, 22), "反推推力", sLabel);
            GUI.Label(new Rect(r5.x, r5.y + 24, r5.width, 34), $"{thrust / maxThrust * 100:F0}%", sValue);
            DrawVBar(new Rect(r5.x, r5.y + 62, 8, 50), thrust / maxThrust, Color.yellow);

            // ==== 操作提示（始终显示） ====
            Rect hint = new Rect((sw - 500) / 2, panelY - 35, 500, 28);
            GUI.Label(hint, "↑ 加大反推（减速）   ↓ 松开（加速下降）   ←→ 水平微调", sHint);

            // ==== 教学（前5秒） ====
            if (tutorialTimer > 0 && playing)
            {
                float tw = Mathf.Min(sw * 0.8f, 600f);
                Rect tut = new Rect((sw - tw) / 2, sh * 0.3f, tw, 100);
                GUI.color = new Color(0, 0.05f, 0.1f, 0.9f);
                GUI.DrawTexture(tut, texWhite);
                GUI.color = saved;
                DrawBorder(tut, 2f, new Color(0.3f, 0.8f, 1f));
                GUI.Label(new Rect(tut.x + 20, tut.y + 12, tw - 40, 30), "着 陆 模 式", sBig);
                GUI.Label(new Rect(tut.x + 20, tut.y + 48, tw - 40, 24),
                    "目标：着陆时下降速度 < 5 m/s（绿色区）", sLabel);
                GUI.Label(new Rect(tut.x + 20, tut.y + 72, tw - 40, 24),
                    "按 ↑ 减速，松开让重力加速，脉动式控制", sLabel);
            }

            // ==== 高度告警 ====
            if (playing && altitude < 100 && altitude > 0)
            {
                float aw = 300f;
                Rect alert = new Rect((sw - aw) / 2, sh * 0.15f, aw, 36);
                Color ac = Mathf.Abs(vSpeed) > safeVerticalSpeed ? Color.red : Color.green;
                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(alert, texWhite);
                GUI.color = saved;
                string msg = Mathf.Abs(vSpeed) > safeVerticalSpeed ? "⚠ 速度过快！按 ↑ 减速" : "✓ 速度安全，保持";
                var alertStyle = new GUIStyle(sValue) { normal = { textColor = ac } };
                GUI.Label(alert, msg, alertStyle);
            }

            // ==== 结果 ====
            if (landed)
            {
                Rect wr = new Rect((sw - 400) / 2, sh / 2 - 60, 400, 110);
                GUI.color = new Color(0, 0.1f, 0, 0.9f);
                GUI.DrawTexture(wr, texWhite);
                GUI.color = saved;
                DrawBorder(wr, 2f, Color.green);
                GUI.Label(new Rect(wr.x, wr.y + 15, wr.width, 36), "着陆成功！", sBig);
                GUI.Label(new Rect(wr.x, wr.y + 55, wr.width, 22),
                    $"用时 {gameTimer:F0} 秒 | 着陆速度 {Mathf.Abs(vSpeed):F1} m/s | 剩余燃料 {fuel:F0}%", sLabel);
                if (GUI.Button(new Rect(wr.x + 120, wr.y + 80, 160, 26), "进入月面 →"))
                    SceneManager.LoadScene("S4_MoonSurface_Whitebox");
            }
            else if (crashed)
            {
                Rect lr = new Rect((sw - 400) / 2, sh / 2 - 60, 400, 110);
                GUI.color = new Color(0.1f, 0, 0, 0.9f);
                GUI.DrawTexture(lr, texWhite);
                GUI.color = saved;
                DrawBorder(lr, 2f, Color.red);
                GUI.Label(new Rect(lr.x, lr.y + 15, lr.width, 36), "着陆失败", sBig);
                GUI.Label(new Rect(lr.x, lr.y + 55, lr.width, 22),
                    $"着陆速度 {Mathf.Abs(vSpeed):F1} m/s（需 < {safeVerticalSpeed} m/s）", sLabel);
                if (GUI.Button(new Rect(lr.x + 120, lr.y + 80, 160, 26), "重新尝试"))
                    ResetGame();
            }
        }

        void DrawVBar(Rect r, float pct, Color c)
        {
            GUI.DrawTexture(r, texDark);
            var saved = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * (1 - pct), r.width, r.height * pct), texWhite);
            GUI.color = saved;
        }

        void DrawBorder(Rect r, float w, Color c)
        {
            var saved = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), texWhite);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - w, r.width, w), texWhite);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), texWhite);
            GUI.DrawTexture(new Rect(r.x + r.width - w, r.y, w, r.height), texWhite);
            GUI.color = saved;
        }

        void Init()
        {
            texWhite = T(Color.white);
            texGreen = T(Color.green);
            texYellow = T(Color.yellow);
            texRed = T(Color.red);
            texCyan = T(Color.cyan);
            texDark = T(new Color(0.1f, 0.12f, 0.15f));
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            sLabel = GS(15, FontStyle.Normal, new Color(0.6f, 0.75f, 0.9f), font);
            sValue = GS(24, FontStyle.Bold, Color.white, font);
            sBig = GS(28, FontStyle.Bold, new Color(0.3f, 0.9f, 1f), font);
            sHint = GS(14, FontStyle.Normal, new Color(0.5f, 0.7f, 0.9f), font);
            init = true;
        }

        GUIStyle GS(int size, FontStyle style, Color color, Font font)
        {
            var s = new GUIStyle
            {
                fontSize = size,
                fontStyle = style,
                alignment = TextAnchor.MiddleCenter,
            };
            s.normal.textColor = color;
            if (font != null) s.font = font;
            return s;
        }

        static Texture2D T(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
