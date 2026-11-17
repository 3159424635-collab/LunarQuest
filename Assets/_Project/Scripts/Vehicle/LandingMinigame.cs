using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest
{
    public class LandingMinigame : MonoBehaviour
    {
        public float gravity = 1.62f;
        public float maxThrust = 10f;
        public float thrustRate = 5f;
        public float rcsForce = 3f;
        public float startAltitude = 500f;
        public float fuelCapacity = 100f;
        public float fuelBurnRate = 4f;
        public float safeVerticalSpeed = 5f;
        public float safeHorizontalSpeed = 3f;
        public float safeStress = 0.8f;
        public Camera mainCam;
        public GameObject dustEffect; // 引擎扬尘粒子

        float altitude, vSpeed, hSpeed, thrust, fuel, stress;
        bool playing, landed, crashed;
        float gameTimer;
        int phase;
        float phaseTimer, transitProgress;
        float landingShake;

        static readonly Color CBg = new Color(0f, 0.05f, 0.02f, 0.92f);
        static readonly Color CDim = new Color(0f, 0.35f, 0.15f);
        static readonly Color CGrid = new Color(0f, 0.25f, 0.10f);
        static readonly Color CBright = new Color(0f, 1f, 0.4f);
        static readonly Color CWhite = new Color(0.8f, 1f, 0.85f);
        static readonly Color CWarn = new Color(1f, 0.8f, 0f);
        static readonly Color CDanger = new Color(1f, 0.2f, 0.1f);

        Texture2D tex;
        GUIStyle sS, sM, sL, sT;
        bool init;

        void Start() { ResetGame(); }

        public void ResetGame()
        {
            altitude = startAltitude; vSpeed = -15; hSpeed = 0; thrust = 0;
            fuel = fuelCapacity; playing = true; landed = crashed = false;
            stress = 0; gameTimer = 0; phase = 0; phaseTimer = 0; transitProgress = 0;
        }

        void Update()
        {
            if (!playing) return;
            gameTimer += Time.deltaTime;
            if (phase == 0)
            {
                phaseTimer += Time.deltaTime;
                transitProgress = Mathf.Clamp01(phaseTimer / 5f);
                if (phaseTimer >= 5f) { phase = 1; if (QuestManager.I) QuestManager.I.FireEvent("landing_minigame_start"); }
                return;
            }
            float target = Input.GetKey(KeyCode.UpArrow) ? maxThrust : 0;
            thrust = Mathf.MoveTowards(thrust, target, thrustRate * Time.deltaTime);
            hSpeed += Input.GetAxisRaw("Horizontal") * rcsForce * Time.deltaTime;
            hSpeed *= 1 - 0.3f * Time.deltaTime;
            fuel = Mathf.Max(0, fuel - (thrust / maxThrust) * fuelBurnRate * Time.deltaTime);
            if (fuel <= 0) thrust = 0;
            vSpeed += (thrust - gravity) * Time.deltaTime;
            altitude += vSpeed * Time.deltaTime;
            stress = Mathf.Clamp01(Mathf.Max(Mathf.Abs(vSpeed) / (safeVerticalSpeed * 2), Mathf.Abs(hSpeed) / (safeHorizontalSpeed * 2)));
            if (mainCam)
            {
                Vector3 basePos = new Vector3(hSpeed * 2f, Mathf.Lerp(-29.5f, 0, altitude / startAltitude), 0);
                // 推力震动（推力越大越抖）
                float thrustShake = (thrust / maxThrust) * 0.08f;
                // 着陆震动（衰减）
                if (landingShake > 0) landingShake = Mathf.MoveTowards(landingShake, 0, Time.deltaTime * 0.5f);
                float totalShake = thrustShake + landingShake;
                mainCam.transform.position = basePos + new Vector3(
                    Random.Range(-totalShake, totalShake),
                    Random.Range(-totalShake, totalShake),
                    Random.Range(-totalShake, totalShake));
            }
            // 扬尘：高度<50m且有推力时启用
            if (dustEffect != null)
            {
                bool shouldDust = altitude < 50f && thrust > 2f && playing;
                if (shouldDust && !dustEffect.activeSelf) dustEffect.SetActive(true);
                else if (!shouldDust && dustEffect.activeSelf) dustEffect.SetActive(false);
            }

            // 着陆震动
            if (altitude <= 0)
            {
                altitude = 0;
                if (playing) landingShake = Mathf.Abs(vSpeed) * 0.05f; // 震动强度∝着陆速度
                playing = false;
                bool ok = Mathf.Abs(vSpeed) <= safeVerticalSpeed && Mathf.Abs(hSpeed) <= safeHorizontalSpeed;
                landed = ok; crashed = !ok;
                if (QuestManager.I) QuestManager.I.FireEvent(ok ? "landing_succeeded" : "landing_failed");
            }
        }

        void OnGUI()
        {
            if (!init) Init();
            if (!playing && !landed && !crashed) return;
            // GUI.matrix 全局缩放：按 1280×720 逻辑分辨率设计，自动适配任何屏幕
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float sw = 1280f, sh = 720f; // 逻辑分辨率（所有坐标基于此）
            for (float y = 0; y < sh; y += 4) Draw(new Rect(0, y, sw, 1), new Color(0, 0, 0, 0.15f));
            if (phase == 0) { DrawTransit(sw, sh); return; }
            DrawLanding(sw, sh);
            if (landed) DrawResult(sw, sh, true);
            if (crashed) DrawResult(sw, sh, false);
        }

        void DrawTransit(float sw, float sh)
        {
            float w = Mathf.Min(sw * 0.8f, 650), h = 280;
            var r = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
            Draw(r, CBg); Border(r, CBright);
            Label(r.x, r.y + 10, w, 26, "◤ 地月转移轨迹 ◢", sL);
            Line(r.x + 20, r.y + 45, r.x + w - 20, r.y + 45, CDim);
            float ex = r.x + 70, ey = r.y + 110;
            Circle(ex, ey, 28, CDim); Circle(ex, ey, 22, new Color(0.05f, 0.3f, 0.15f)); Circle(ex, ey, 18, CDim);
            Label(ex - 25, ey + 35, 50, 16, "地球", sS);
            float mx = r.x + w - 70, my = r.y + 110;
            Circle(mx, my, 16, CDim); Circle(mx, my, 12, new Color(0.1f, 0.4f, 0.2f));
            Label(mx - 25, my + 22, 50, 16, "月球", sS);
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                float px = Mathf.Lerp(ex + 30, mx - 18, t);
                float py = ey - Mathf.Sin(t * Mathf.PI) * 35;
                Draw(new Rect(px, py, 2, 2), i <= transitProgress * 40 ? CBright : CGrid);
            }
            float sx = Mathf.Lerp(ex + 30, mx - 18, transitProgress);
            float sy = ey - Mathf.Sin(transitProgress * Mathf.PI) * 35;
            Draw(new Rect(sx - 3, sy - 3, 6, 6), CWhite);
            Draw(new Rect(sx - 5, sy + 3, 3, 6), CBright);
            Draw(new Rect(sx + 2, sy + 3, 3, 6), CBright);
            float dist = Mathf.Lerp(384400, 384, transitProgress);
            Label(r.x, r.y + h - 65, w, 24, "距月球 " + dist.ToString("F0") + " km", sM);
            var br = new Rect(r.x + 40, r.y + h - 40, w - 80, 8);
            Draw(br, CGrid);
            Draw(new Rect(br.x, br.y, br.width * transitProgress, 8), CBright);
            Label(r.x, r.y + h - 28, w, 16, transitProgress < 1 ? "轨道推进中..." : "已入轨，准备着陆", sS);
        }

        void DrawLanding(float sw, float sh)
        {
            float altW = 200, altH = Mathf.Min(sh * 0.65f, 420);
            float altX = 15, altY = (sh - altH) / 2;
            var ar = new Rect(altX, altY, altW, altH);
            Draw(ar, CBg); Border(ar, CBright);
            Label(altX, altY - 22, altW, 18, "◤ 高度剖面 ◢", sS);
            float groundH = 50;
            float usable = altH - groundH - 30;
            for (int i = 0; i <= 5; i++)
            {
                float gy = altY + 25 + usable * i / 5f;
                Line(altX + 10, gy, altX + altW - 10, gy, CGrid);
                float mark = startAltitude * (1 - i / 5f);
                Label(altX + altW - 60, gy - 8, 55, 14, mark.ToString("F0") + "m", sT);
            }
            float grY = altY + 25 + usable;
            for (int x = 0; x < altW - 20; x += 8)
                Draw(new Rect(altX + 10 + x, grY + (x % 16 < 8 ? 0 : 3), 6, 4), CDim);
            Draw(new Rect(altX + 10, grY + groundH - 15, altW - 20, 15), new Color(0f, 0.2f, 0.08f));
            Label(altX, grY + groundH - 12, altW, 14, "≡ 月面 ≡", sT);
            float altPct = Mathf.Clamp01(altitude / startAltitude);
            float ly = altY + 25 + usable * (1 - altPct);
            float lx = altX + altW / 2 - 5;
            Draw(new Rect(lx, ly, 10, 14), CWhite);
            float fl = (thrust / maxThrust) * 18;
            if (fl > 1) { Draw(new Rect(lx + 1, ly + 14, 3, fl), CBright); Draw(new Rect(lx + 6, ly + 14, 3, fl), CBright); }
            Label(lx - 50, ly - 16, 110, 14, altitude.ToString("F0") + "m", sS);
            float vLen = Mathf.Clamp(Mathf.Abs(vSpeed) * 2, 5, 40);
            Color vCol = Mathf.Abs(vSpeed) <= safeVerticalSpeed ? CBright : CDanger;
            Draw(new Rect(lx + 25, ly + 5, 2, vLen), vCol);
            Draw(new Rect(lx + 22, ly + 5 + vLen, 8, 3), vCol);
            float safeY = grY - 20;
            Line(altX + 10, safeY, altX + altW - 10, safeY, new Color(0f, 0.8f, 0.3f, 0.5f));
            Label(altX + 10, safeY - 14, altW - 20, 12, "◄ 安全区 ►", sT);

            float rad = Mathf.Min(sh * 0.2f, 140);
            float rcx = sw - rad - 20, rcy = sh / 2;
            var rr = new Rect(rcx - rad - 15, rcy - rad - 25, rad * 2 + 30, rad * 2 + 50);
            Draw(rr, CBg); Border(rr, CBright);
            Label(rcx - 60, rcy - rad - 22, 120, 16, "◤ 水平雷达 ◢", sS);
            for (int i = 1; i <= 3; i++) Circle(rcx, rcy, rad * i / 3, CGrid);
            Line(rcx - rad, rcy, rcx + rad, rcy, CGrid);
            Line(rcx, rcy - rad, rcx, rcy + rad, CGrid);
            float sweep = (Time.time * 90) % 360 * Mathf.Deg2Rad;
            for (int i = 0; i < 15; i++)
            {
                float a = sweep - i * 0.05f;
                float fade = 1f - i / 15f;
                Draw(new Rect(rcx + Mathf.Cos(a) * rad * 0.9f, rcy + Mathf.Sin(a) * rad * 0.9f, 3, 3), new Color(0, fade, 0.4f * fade));
            }
            if (Time.time % 1 < 0.5f) Draw(new Rect(rcx - 3, rcy - 3, 6, 6), CBright);
            float blx = rcx + hSpeed * 20;
            if (Time.time % 0.5f < 0.3f) Draw(new Rect(blx - 2, rcy - 2, 4, 4), CWhite);

            float pw = Mathf.Min(sw * 0.85f, 600), ph = 80;
            var pr = new Rect((sw - pw) / 2, sh - ph - 10, pw, ph);
            Draw(pr, CBg); Border(pr, CBright);
            float cw = pw / 5;
            Label(pr.x, pr.y + 5, cw, 14, "高度", sT);
            Label(pr.x, pr.y + 20, cw, 24, altitude.ToString("F0") + "m", sM);
            Bar(new Rect(pr.x + 10, pr.y + 50, cw - 20, 6), altitude / startAltitude, CBright);
            float x2 = pr.x + cw;
            Label(x2, pr.y + 5, cw, 14, "下降", sT);
            Label(x2, pr.y + 20, cw, 24, vSpeed.ToString("F1"), sM);
            Bar(new Rect(x2 + 10, pr.y + 50, cw - 20, 6), Mathf.Abs(vSpeed) / 20, Mathf.Abs(vSpeed) <= safeVerticalSpeed ? CBright : CDanger);
            float x3 = pr.x + cw * 2;
            Label(x3, pr.y + 5, cw, 14, "水平", sT);
            Label(x3, pr.y + 20, cw, 24, hSpeed.ToString("F1"), sM);
            Bar(new Rect(x3 + 10, pr.y + 50, cw - 20, 6), Mathf.Abs(hSpeed) / 8, Mathf.Abs(hSpeed) <= safeHorizontalSpeed ? CBright : CWarn);
            float x4 = pr.x + cw * 3;
            Label(x4, pr.y + 5, cw, 14, "燃料", sT);
            Label(x4, pr.y + 20, cw, 24, fuel.ToString("F0") + "%", sM);
            Bar(new Rect(x4 + 10, pr.y + 50, cw - 20, 6), fuel / fuelCapacity, fuel > 30 ? CBright : CDanger);
            float x5 = pr.x + cw * 4;
            Label(x5, pr.y + 5, cw, 14, "推力", sT);
            Label(x5, pr.y + 20, cw, 24, (thrust / maxThrust * 100).ToString("F0") + "%", sM);
            Bar(new Rect(x5 + 10, pr.y + 50, cw - 20, 6), thrust / maxThrust, CWarn);
            Label((sw - 400) / 2, pr.y - 25, 400, 16, "↑ 反推减速   ↓ 松开加速   ←→ 水平微调", sT);
            if (playing && altitude < 100 && altitude > 0)
            {
                bool fast = Mathf.Abs(vSpeed) > safeVerticalSpeed;
                Label((sw - 300) / 2, sh * 0.15f, 300, 22, fast ? "⚠ 速度过快 按↑减速" : "● 速度安全", fast ? sM : sS);
            }
        }

        void DrawResult(float sw, float sh, bool ok)
        {
            float w = 400, h = 100;
            var r = new Rect((sw - w) / 2, sh / 2 - h / 2, w, h);
            Draw(r, CBg); Border(r, ok ? CBright : CDanger);
            Label(r.x, r.y + 10, w, 30, ok ? "◤ 着陆成功 ◢" : "◤ 着陆失败 ◢", sL);
            Label(r.x, r.y + 45, w, 18, "速度 " + Mathf.Abs(vSpeed).ToString("F1") + " m/s | 燃料 " + fuel.ToString("F0") + "%", sS);
            if (GUI.Button(new Rect(r.x + 100, r.y + 68, 200, 24), ok ? "进入月面 →" : "重新尝试"))
            {
                if (ok) SceneManager.LoadScene("S4_MoonSurface_Whitebox");
                else ResetGame();
            }
        }

        void Draw(Rect r, Color c) { var s = GUI.color; GUI.color = c; GUI.DrawTexture(r, tex); GUI.color = s; }
        void Line(float x1, float y1, float x2, float y2, Color c)
        { var s = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(x1, y1, x2 - x1, Mathf.Max(1, y2 - y1)), tex); GUI.color = s; }
        void Circle(float cx, float cy, float r, Color c)
        {
            for (int a = 0; a < 360; a += 4)
            {
                float rad = a * Mathf.Deg2Rad;
                Draw(new Rect(cx + Mathf.Cos(rad) * r, cy + Mathf.Sin(rad) * r, 2, 2), c);
            }
        }
        void Border(Rect r, Color c)
        {
            Draw(new Rect(r.x, r.y, r.width, 1), c);
            Draw(new Rect(r.x, r.y + r.height - 1, r.width, 1), c);
            Draw(new Rect(r.x, r.y, 1, r.height), c);
            Draw(new Rect(r.x + r.width - 1, r.y, 1, r.height), c);
        }
        void Label(float x, float y, float w, float h, string t, GUIStyle s) { GUI.Label(new Rect(x, y, w, h), t, s); }
        void Bar(Rect r, float pct, Color c) { Draw(r, CGrid); Draw(new Rect(r.x, r.y, r.width * Mathf.Clamp01(pct), r.height), c); }

        void Init()
        {
            tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            Font f = null; try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            sS = G(13, FontStyle.Normal, new Color(0.5f, 0.9f, 0.6f), f);
            sM = G(20, FontStyle.Bold, CWhite, f);
            sL = G(24, FontStyle.Bold, CBright, f);
            sT = G(11, FontStyle.Normal, new Color(0.4f, 0.7f, 0.5f), f);
            init = true;
        }
        GUIStyle G(int sz, FontStyle st, Color c, Font f)
        {
            var s = new GUIStyle { fontSize = sz, fontStyle = st, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = c; if (f) s.font = f; return s;
        }
    }
}
