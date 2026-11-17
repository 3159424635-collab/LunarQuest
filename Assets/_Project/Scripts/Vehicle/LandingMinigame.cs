using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarQuest
{
    /// <summary>
    /// 着陆小游戏（第二幕·落）：玩家在着陆器舱内，用方向键控制反推推力。
    /// ↑ 加大推力（减速） / ↓ 减小推力 / ←→ 水平微调
    /// 成功条件：接地时垂直速度<5m/s，水平速度<2m/s，应力<80%
    /// 失败：弹出重试提示，重置到初始高度。
    /// </summary>
    public class LandingMinigame : MonoBehaviour
    {
        [Header("物理参数")]
        public float gravity = 1.62f;           // 月面重力
        public float maxThrust = 10f;     // TWR≈6.2            // 最大反推推力 m/s²
        public float thrustRate = 5f;           // 推力变化速率
        public float rcsForce = 3f;              // 水平微调力
        public float startAltitude = 500f;      // 初始高度(米)，约60秒游戏时长      // 初始高度(米)
        public float fuelCapacity = 100f;        // 燃料容量
        public float fuelBurnRate = 4f;          // 燃料消耗/秒（满推力时）

        [Header("安全阈值")]
        public float safeVerticalSpeed = 5f;     // 安全垂直速度 m/s
        public float safeHorizontalSpeed = 2f;   // 安全水平速度 m/s
        public float safeStress = 0.8f;          // 安全应力（0-1）

        [Header("视觉")]
        public Transform landerModel;            // 着陆器模型（视觉）
        public Camera mainCam;                   // 主相机

        // 运行时状态
        float altitude, verticalSpeed, horizontalSpeed, thrust, fuel;
        bool playing, landed, crashed;
        float stress;

        // HUD 元素
        Rect hudRect;
        GUIStyle labelStyle, valueStyle, barBg, barFill;
        Texture2D whiteTex, greenTex, yellowTex, redTex;
        bool stylesInitialized;

        // 事件回调
        public static event System.Action<bool> OnLandingComplete;

        void Start()
        {
            ResetGame();
        }

        public void ResetGame()
        {
            altitude = startAltitude;
            verticalSpeed = -15f; // 初始下降速度
            horizontalSpeed = 2f; // 初始水平漂移
            thrust = 0f;
            fuel = fuelCapacity;
            playing = true;
            landed = false;
            crashed = false;
            stress = 0f;
            stylesInitialized = false;
        }

        void Update()
        {
            if (!playing) return;

            // 输入
            float targetThrust = 0f;
            if (Input.GetKey(KeyCode.UpArrow)) targetThrust = maxThrust;
            else if (Input.GetKey(KeyCode.DownArrow)) targetThrust = 0f;
            else targetThrust = thrust; // 松开保持当前推力

            // 推力渐变（不是瞬间跳变）
            thrust = Mathf.MoveTowards(thrust, targetThrust, thrustRate * Time.deltaTime);

            // 水平微调
            float hInput = Input.GetAxisRaw("Horizontal");
            horizontalSpeed += hInput * rcsForce * Time.deltaTime;
            // 水平阻尼
            horizontalSpeed *= 1f - 0.3f * Time.deltaTime;

            // 燃料消耗
            float burn = (thrust / maxThrust) * fuelBurnRate * Time.deltaTime;
            fuel = Mathf.Max(0f, fuel - burn);
            if (fuel <= 0f) thrust = 0f;

            // 物理模拟
            float netAccel = thrust - gravity;
            verticalSpeed += netAccel * Time.deltaTime;
            altitude += verticalSpeed * Time.deltaTime;

            // 应力计算（速度越快应力越大）
            float vStress = Mathf.Abs(verticalSpeed) / (safeVerticalSpeed * 2f);
            float hStress = Mathf.Abs(horizontalSpeed) / (safeHorizontalSpeed * 2f);
            stress = Mathf.Clamp01(Mathf.Max(vStress, hStress));

            // 着陆判定
            if (altitude <= 0f)
            {
                altitude = 0f;
                playing = false;
                bool safe = Mathf.Abs(verticalSpeed) <= safeVerticalSpeed
                         && Mathf.Abs(horizontalSpeed) <= safeHorizontalSpeed
                         && stress <= safeStress;
                if (safe)
                {
                    landed = true;
                    OnLandingComplete?.Invoke(true);
                    if (QuestManager.I != null) QuestManager.I.FireEvent("landing_succeeded");
                }
                else
                {
                    crashed = true;
                    OnLandingComplete?.Invoke(false);
                    if (QuestManager.I != null) QuestManager.I.FireEvent("landing_failed");
                }
            }

            // 视觉：相机随高度下降接近月面
            if (mainCam != null)
            {
                float t = Mathf.Clamp01(altitude / startAltitude);
                float camY = Mathf.Lerp(-19.5f, 0f, t); // 2000m时y=0，0m时y=-19.5（接近月面）
                mainCam.transform.position = new Vector3(
                    mainCam.transform.position.x + horizontalSpeed * 0.01f * Time.deltaTime,
                    camY,
                    mainCam.transform.position.z);
                // 微弱相机震动（推力越大震动越强）
                float shake = (thrust / maxThrust) * 0.02f;
                mainCam.transform.localPosition += new Vector3(
                    Random.Range(-shake, shake), 0, Random.Range(-shake, shake));
            }
        }

        void OnGUI()
        {
            if (!stylesInitialized) InitStyles();
            if (!playing && !landed && !crashed) return;

            // 全屏半透明背景（舱内氛围）
            GUI.color = new Color(0, 0, 0, 0.3f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
            GUI.color = Color.white;

            float margin = 20f;
            float gaugeW = 200f;

            // ==== 左侧：高度表 ====
            Rect leftRect = new Rect(margin, margin, gaugeW, Screen.height - margin * 2 - 60);
            GUI.Box(leftRect, "");
            GUI.Label(new Rect(leftRect.x + 10, leftRect.y + 8, gaugeW - 20, 24), "高度", labelStyle);
            string altText = altitude >= 1000 ? $"{altitude/1000:F1} km" : $"{altitude:F0} m";
            GUI.Label(new Rect(leftRect.x + 10, leftRect.y + 32, gaugeW - 20, 36), altText, valueStyle);
            // 高度条（从高到低）
            float altPct = Mathf.Clamp01(altitude / startAltitude);
            DrawBar(new Rect(leftRect.x + 10, leftRect.y + 76, gaugeW - 20, Screen.height - margin * 2 - 160), altPct, GetColorForSpeed());

            // ==== 右侧：速度/燃料/应力 ====
            Rect rightRect = new Rect(Screen.width - margin - gaugeW, margin, gaugeW, 400);
            GUI.Box(rightRect, "");
            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 8, gaugeW - 20, 24), "下降速度", labelStyle);
            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 32, gaugeW - 20, 36),
                $"{verticalSpeed:F1} m/s", valueStyle);
            DrawBar(new Rect(rightRect.x + 10, rightRect.y + 76, gaugeW - 20, 20),
                Mathf.Clamp01(Mathf.Abs(verticalSpeed) / 20f), GetColorForSpeed());

            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 106, gaugeW - 20, 24), "水平速度", labelStyle);
            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 130, gaugeW - 20, 36),
                $"{horizontalSpeed:F1} m/s", valueStyle);
            DrawBar(new Rect(rightRect.x + 10, rightRect.y + 174, gaugeW - 20, 20),
                Mathf.Clamp01(Mathf.Abs(horizontalSpeed) / 10f),
                Mathf.Abs(horizontalSpeed) <= safeHorizontalSpeed ? Color.green : Color.red);

            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 204, gaugeW - 20, 24), "燃料", labelStyle);
            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 228, gaugeW - 20, 36),
                $"{fuel:F0}%", valueStyle);
            DrawBar(new Rect(rightRect.x + 10, rightRect.y + 272, gaugeW - 20, 20),
                fuel / fuelCapacity, fuel > 30f ? Color.cyan : Color.red);

            GUI.Label(new Rect(rightRect.x + 10, rightRect.y + 302, gaugeW - 20, 24), "着陆应力", labelStyle);
            DrawStressBar(new Rect(rightRect.x + 10, rightRect.y + 330, gaugeW - 20, 24), stress);

            // ==== 底部：操作提示 ====
            Rect hintRect = new Rect(Screen.width / 2 - 250, Screen.height - 60, 500, 40);
            GUI.Box(hintRect, "");
            GUI.Label(hintRect, "↑ 加大反推  ↓ 减小  ←→ 水平微调", labelStyle);

            // ==== 推力指示器 ====
            Rect thrustRect = new Rect(Screen.width / 2 - 60, Screen.height - 110, 120, 36);
            GUI.Box(thrustRect, "");
            GUI.Label(thrustRect, $"推力 {thrust/maxThrust*100:F0}%", valueStyle);
            DrawBar(new Rect(thrustRect.x, thrustRect.y + thrustRect.height - 6, thrustRect.width, 4),
                thrust / maxThrust, Color.yellow);

            // ==== 结果提示 ====
            if (landed)
            {
                Rect winRect = CenterRect(400, 120);
                GUI.Box(winRect, "");
                GUI.Label(CenterRect(380, 30), "着陆成功", valueStyle);
                GUI.Label(CenterRect(380, 24), "欢迎来到月球", labelStyle);
                if (GUI.Button(CenterRect(160, 30), "进入月面 →"))
                {
                    SceneManager.LoadScene("S4_MoonSurface_Whitebox");
                }
            }
            else if (crashed)
            {
                Rect loseRect = CenterRect(400, 140);
                GUI.Box(loseRect, "");
                GUI.Label(CenterRect(380, 30), "着陆失败", valueStyle);
                GUI.Label(CenterRect(380, 22),
                    $"垂直速度{Mathf.Abs(verticalSpeed):F1}m/s (需<5) 水平{Mathf.Abs(horizontalSpeed):F1}m/s (需<2)", labelStyle);
                if (GUI.Button(CenterRect(160, 30), "重试"))
                {
                    ResetGame();
                }
            }
        }

        Color GetColorForSpeed()
        {
            float v = Mathf.Abs(verticalSpeed);
            if (v <= safeVerticalSpeed) return Color.green;
            if (v <= safeVerticalSpeed * 1.5f) return Color.yellow;
            return Color.red;
        }

        void DrawStressBar(Rect r, float value)
        {
            // 绿→黄→红三段
            Color c = value < 0.4f ? Color.green : value < 0.8f ? Color.yellow : Color.red;
            DrawBar(r, value, c);
        }

        void DrawBar(Rect r, float pct, Color color)
        {
            GUI.DrawTexture(r, whiteTex);
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * pct, r.height), whiteTex);
            GUI.color = saved;
        }

        Rect CenterRect(float w, float h)
        {
            return new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        }

        void InitStyles()
        {
            whiteTex = MakeTex(Color.white);
            labelStyle = new GUIStyle();
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            valueStyle = new GUIStyle();
            valueStyle.fontSize = 28;
            valueStyle.fontStyle = FontStyle.Bold;
            valueStyle.normal.textColor = Color.white;
            valueStyle.alignment = TextAnchor.MiddleCenter;
            stylesInitialized = true;
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
