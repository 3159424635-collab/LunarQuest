using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 月球车控制器 —— 课程设计的核心硬指标。
    /// 操控：←→ 转向（视线方向），↑↓ 油门/刹车，Shift 加速，空格 手刹漂移。
    ///
    /// 技术方案：刚体 + 简化力模型（推力/抓地/下压三个力 + 速度衰减转向），
    /// 不使用 WheelCollider（调参成本高且稳定性差）；本方案以两百行以内的代码
    /// 实现稳定的越野驾驶手感，参数全部暴露在面板上便于调校。
    ///
    /// 挂载要求：给车体（BoxCollider）挂本脚本，Rigidbody 会自动加上。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RoverController : MonoBehaviour
    {
        [Header("动力")]
        public float motorForce = 6000f;      // 油门推力（牛）。起步加速不足时增大
        public float maxSpeed = 18f;          // 最高速度 m/s（约 65 km/h）
        public float boostMultiplier = 1.5f;  // Shift 加速档倍率
        public float brakeForce = 9000f;      // 行进中按 ↓ 的刹车力

        [Header("转向")]
        public float steerSpeed = 90f;        // 最大转向角速度（度/秒）
        public float steerFalloffSpeed = 6f;  // 超过此速度转向开始变钝（越野感来源）
        public float minSteerFactor = 0.35f;  // 高速段最低转向效率

        [Header("抓地与稳定")]
        public float normalGrip = 10f;        // 常规横向抓地（抑制侧滑）
        public float handbrakeGrip = 3f;      // 手刹时放松抓地 → 漂移
        public float downForce = 25f;         // 下压力，防飞跃、防侧翻

        Rigidbody rb;
        float yaw; // 累积航向角，由本脚本接管（X/Z 轴旋转已冻结）

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb.mass < 10f) rb.mass = 220f;          // 未手动设置质量时使用合理默认值
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.centerOfMass = new Vector3(0f, -0.4f, 0f); // 质心压低，抗翻的核心
            rb.interpolation = RigidbodyInterpolation.Interpolate; // 物理帧与渲染帧之间插值，追尾相机不抖
            yaw = transform.eulerAngles.y;
        }

        void FixedUpdate()
        {
            // 默认 Input Manager 的 Horizontal/Vertical 轴同时绑定 WASD 和方向键，
            // 课程的"方向键控制"要求因此直接满足，无需额外配置
            float steer = Input.GetAxis("Horizontal");
            float throttle = Input.GetAxis("Vertical");
            bool boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool handbrake = Input.GetKey(KeyCode.Space);

            float speed = Vector3.Dot(rb.velocity, transform.forward); // 前向速度（带符号）
            float flatSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

            // ---- 纵向动力：前进 / 刹车 / 倒车三态 ----
            if (throttle > 0.01f)
            {
                rb.AddForce(transform.forward * motorForce * (boost ? boostMultiplier : 1f) * throttle);
            }
            else if (throttle < -0.01f)
            {
                if (speed > 1f)
                    rb.AddForce(-transform.forward * brakeForce * -throttle); // 行进中 ↓ = 刹车
                else
                    rb.AddForce(transform.forward * motorForce * 0.5f * throttle); // 停稳后 ↓ = 半速倒车
            }

            // ---- 速度上限（Shift 时上限同步放宽）----
            float cap = maxSpeed * (boost ? boostMultiplier : 1f);
            if (flatSpeed > cap)
                rb.velocity = new Vector3(rb.velocity.x / flatSpeed * cap, rb.velocity.y, rb.velocity.z / flatSpeed * cap);

            // ---- 转向：低速灵活、高速迟钝；起步速度过低时不转向（原地不画圈）----
            float steerFactor = Mathf.Lerp(1f, minSteerFactor,
                Mathf.InverseLerp(steerFalloffSpeed, maxSpeed, flatSpeed));
            float driveDir = speed < -0.5f ? -1f : 1f; // 倒车时转向反向，符合开车直觉
            float speedGate = Mathf.Clamp01(flatSpeed / 1.5f);
            yaw += steer * steerSpeed * steerFactor * driveDir * speedGate * Time.deltaTime;
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            // ---- 抓地：把横向分速度按强度往回拽；按住空格(手刹)时抓地放松 → 甩尾漂移 ----
            Vector3 lateral = transform.right * Vector3.Dot(rb.velocity, transform.right);
            rb.AddForce(-lateral * (handbrake ? handbrakeGrip : normalGrip), ForceMode.Acceleration);

            // ---- 下压防飞 + 手刹额外减速 ----
            rb.AddForce(-transform.up * downForce, ForceMode.Acceleration);
            if (handbrake)
                rb.AddForce(-transform.forward * speed * 3f, ForceMode.Acceleration);
        }

        /// <summary>当前速度 km/h，HUD 速度表直接读这个。</summary>
        public float SpeedKmh => rb.velocity.magnitude * 3.6f;
    }
}
