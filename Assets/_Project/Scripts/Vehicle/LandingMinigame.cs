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
        int phase = 0; // 0=地月转移(自动) 1=着陆(玩家控制)
        float phaseTimer = 0f;
        float transitProgress = 0f; // 地月转移进度 0-1

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
            phase = 0;
            phaseTimer = 0f;
            transitProgress = 0f;
        }

        void Update()
        {
            if (!playing) return;
            gameTimer += Time.deltaTime;

            // 阶段0：地月转移（自动播放，5秒）
            if (phase == 0)
            {
                phaseTimer += Time.deltaTime;
                transitProgress = Mathf.Clamp01(phaseTimer / 5f);
                if (phaseTimer >= 5f)
                {
                    phase = 1;
                    if (QuestManager.I != null) QuestManager.I.FireEvent("landing_minigame_start");
                }
                return; // 阶段0不响应输入
            }

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
            Color saved = GUI.color;

            float sw = Screen.width, sh = Screen.height;

            // ==== 阶段0：地月转移导航平板 ====
            if (phase == 0)
            {
                // 中央大面板
                float tw = Mathf.Min(sw * 0.7f, 700f);
                float th = 300f;
                Rect transitRect = new Rect((sw - tw) / 2, (sh - th) / 2, tw, th);
                GUI.color = new Color(0, 0.03f, 0.08f, 0.92f);
                GUI.DrawTexture(transitRect, texWhite);
                GUI.color = saved;
                DrawBorder(transitRect, 2f, new Color(0.3f, 0.8f, 1f));

                // 标题
                GUI.Label(new Rect(transitRect.x, transitRect.y + 15, tw, 30), "地月转移轨道", sBig);

                // 地球（蓝色圆，左侧）
                float earthR = 35f;
                float earthX = transitRect.x + 80f;
                float earthY = transitRect.y + th / 2;
                GUI.color = new Color(0.1f, 0.3f, 0.7f);
                GUI.DrawTexture(new Rect(earthX - earthR, earthY - earthR, earthR * 2, earthR * 2), texWhite);
                GUI.color = saved;
                GUI.Label(new Rect(earthX - 30, earthY + earthR + 5, 60, 18), "地球", sLabel);

                // 月球（灰色圆，右侧）
                float moonR = 18f;
                float moonX = transitRect.x + tw - 80f;
                float moonY = transitRect.y + th / 2;
                GUI.color = new Color(0.6f, 0.58f, 0.55f);
                GUI.DrawTexture(new Rect(moonX - moonR, moonY - moonR, moonR * 2, moonR * 2), texWhite);
                GUI.color = saved;
                GUI.Label(new Rect(moonX - 30, moonY + moonR + 5, 60, 18), "月球", sLabel);

                // 轨迹线（地球到月球）
                float orbitLineY = earthY;
                GUI.color = new Color(0.3f, 0.5f, 0.8f, 0.3f);
                GUI.DrawTexture(new Rect(earthX + earthR, orbitLineY - 1, moonX - earthX - earthR - moonR, 2), texWhite);
                GUI.color = saved;

                // 飞船图标（沿轨迹移动）
                float shipX = Mathf.Lerp(earthX + earthR + 10, moonX - moonR - 10, transitProgress);
                float shipY = orbitLineY - 15 + Mathf.Sin(transitProgress * Mathf.PI) * 20; // 弧线
                // 飞船三角形
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(shipX - 5, shipY - 5, 10, 10), texWhite);
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(shipX - 8, shipY + 5, 3, 8), texWhite); // 左尾焰
                GUI.DrawTexture(new Rect(shipX + 5, shipY + 5, 3, 8), texWhite); // 右尾焰
                GUI.color = saved;

                // 距离显示
                float distKm = Mathf.Lerp(384400f, 384f, transitProgress);
                string distText = distKm > 1000 ? $"{distKm:F0} km" : $"{distKm:F0} km";
                GUI.Label(new Rect(transitRect.x, transitRect.y + th - 60, tw, 24),
                    $"距月球：{distText}", sValue);
                GUI.Label(new Rect(transitRect.x, transitRect.y + th - 32, tw, 20),
                    transitProgress < 1f ? "正在接近月球轨道..." : "已进入环月轨道，准备着陆", sLabel);

                // 进度条
                float barW = tw - 80;
                Rect barRect = new Rect(transitRect.x + 40, transitRect.y + th - 90, barW, 6);
                GUI.DrawTexture(barRect, texDark);
                GUI.color = Color.cyan;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barW * transitProgress, 6), texWhite);
                GUI.color = saved;

                return; // 阶段0只显示转移视图
            }

            // ==== 阶段1：侧视图导航平板（着陆） ====
            float navW = 180f;
            float navH = Mathf.Min(sh * 0.6f, 420f);
            float navX = 30f;
            float navY = (sh - navH) / 2;

            // 平板背景
            GUI.color = new Color(0, 0.03f, 0.08f, 0.88f);
            GUI.DrawTexture(new Rect(navX - 10, navY - 10, navW + 20, navH + 20), texWhite);
            GUI.color = saved;
            DrawBorder(new Rect(navX - 10, navY - 10, navW + 20, navH + 20), 2f, new Color(0.3f, 0.8f, 1f, 0.5f));

            // 标题
            GUI.Label(new Rect(navX, navY - 8, navW, 20), "降落视图", sLabel);

            // 月面（底部灰色带）
            float groundH = 40f;
            Rect groundRect = new Rect(navX, navY + navH - groundH, navW - 40, groundH);
            GUI.color = new Color(0.55f, 0.53f, 0.50f);
            GUI.DrawTexture(groundRect, texWhite);
            GUI.color = saved;
            // 月面纹理线
            GUI.color = new Color(0.4f, 0.38f, 0.36f);
            for (int i = 0; i < 4; i++)
            {
                float gx = navX + 10 + i * (navW - 60) / 4;
                GUI.DrawTexture(new Rect(gx, groundRect.y + 5 + i * 8, 15, 4), texWhite);
            }
            GUI.color = saved;

            // 火箭图标位置（根据高度从上到下移动）
            float altPct = Mathf.Clamp01(altitude / startAltitude);
            float landerY = navY + 20 + (1 - altPct) * (navH - groundH - 60);
            float landerX = navX + (navW - 40) / 2 + hSpeed * 8; // 水平偏移可视化

            // 距离线（从火箭到月面的虚线）
            GUI.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            float lineY = landerY + 15;
            float groundY = groundRect.y;
            for (float ly = lineY; ly < groundY; ly += 8)
            {
                GUI.DrawTexture(new Rect(landerX + 5, ly, 2, 4), texWhite);
            }
            GUI.color = saved;

            // 火箭图标（三角+尾焰）
            // 舱体
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(landerX - 6, landerY, 12, 16), texWhite);
            GUI.color = saved;
            // 尾焰（推力越大越长）
            float flameLen = (thrust / maxThrust) * 20f;
            if (flameLen > 1)
            {
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(landerX - 3, landerY + 16, 6, flameLen), texWhite);
                GUI.color = new Color(1f, 0.5f, 0f);
                GUI.DrawTexture(new Rect(landerX - 2, landerY + 16, 4, flameLen * 0.6f), texWhite);
                GUI.color = saved;
            }

            // 速度箭头（在火箭右侧，长度∝速度）
            float arrowLen = Mathf.Clamp(Mathf.Abs(vSpeed) * 1.5f, 5, 50);
            Color arrowCol = Mathf.Abs(vSpeed) <= safeVerticalSpeed ? Color.green : Color.red;
            // 向下箭头（下降）
            float arrowX = landerX + 25;
            float arrowY = landerY + 8;
            GUI.color = arrowCol;
            GUI.DrawTexture(new Rect(arrowX, arrowY, 3, arrowLen), texWhite);
            // 箭头尖
            GUI.DrawTexture(new Rect(arrowX - 3, arrowY + arrowLen, 9, 5), texWhite);
            GUI.color = saved;

            // 高度标注（在火箭旁边）
            GUI.Label(new Rect(landerX - 50, landerY - 20, 100, 18), $"{altitude:F0}m", sLabel);

            // 安全区标记（月面上方5m/s速度线）
            float safeLineY = groundRect.y - 20;
            GUI.color = new Color(0f, 1f, 0f, 0.4f);
            GUI.DrawTexture(new Rect(navX + 5, safeLineY, navW - 50, 1), texWhite);
            GUI.color = saved;
            GUI.Label(new Rect(navX + 5, safeLineY - 16, navW - 50, 14), "安全着陆区", sLabel);

            // ==== 底部信息面板 ====
            float panelW = Mathf.Min(sw * 0.85f, 700f);
            float panelH = 130f;
            float panelY = sh - panelH - 20f;
            Rect panel = new Rect((sw - panelW) / 2, panelY, panelW, panelH);

            // 半透明背景
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

            // ==== 教学（阶段1前3秒） ====
            if (phase == 1 && gameTimer < 8f && playing)
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
