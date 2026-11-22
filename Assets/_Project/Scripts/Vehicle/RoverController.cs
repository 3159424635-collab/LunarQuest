using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 月面探测车控制器。
    /// 操控：W/S 或 ↑/↓ 前进、刹车和倒车，A/D 或 ←/→ 转向，
    /// Shift 短时加速，空格刹车，R 异常姿态复位。
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
        public float motorForce = 5.5f;       // 加速度 m/s²，不再受模型质量影响
        public float maxSpeed = 6.5f;         // 游戏化上限：约 23 km/h
        public float reverseMaxSpeed = 3f;    // 倒车速度上限
        public float boostMultiplier = 1.2f;  // Shift 短时加速档倍率
        public float brakeForce = 10f;        // 制动加速度 m/s²
        public float rollingResistance = 1.4f;// 松开油门时的滚动阻力

        [Header("转向")]
        public float steerSpeed = 72f;        // 最大转向角速度（度/秒）
        public float steerFalloffSpeed = 3.5f;// 超过此速度转向逐步变缓
        public float minSteerFactor = 0.5f;   // 高速段最低转向效率
        public float launchSteerFactor = 0.42f;// 起步时保留转向，避免“车动了才能拐”

        [Header("抓地与稳定")]
        public float normalGrip = 8f;         // 常规横向抓地（抑制侧滑）
        public float handbrakeGrip = 4f;      // 制动时仍保留部分抓地
        public float downForce = 4f;          // 轻度贴地；原 25 m/s² 会让车像被吸住
        public float groundAlignSpeed = 7f;   // 顺应地形法线，避免锁死水平姿态后被小坡卡住
        public float groundProbeDistance = 5f;
        public float stallAssistDelay = 0.75f;// 持续给油但不移动时触发低速越障辅助
        public float stallAssistHeight = 0.42f;
        public float stallAssistSpeed = 1.4f;

        Rigidbody rb;
        float yaw; // 累积航向角，由本脚本接管（X/Z 轴旋转已冻结）
        float stallTimer;
        Vector3 groundNormal = Vector3.up;
        bool grounded;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb.mass < 10f) rb.mass = 800f;
            // 不再冻结俯仰/横滚：冻结后长底盘无法顺应月面起伏，前缘会直接顶住坡面。
            // 姿态由地面法线平滑稳定，比完全交给碰撞求解更不容易翻车。
            rb.constraints = RigidbodyConstraints.None;
            rb.centerOfMass = new Vector3(0f, -0.85f, 0f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.drag = 0.12f;
            rb.angularDrag = 5f;
            rb.maxAngularVelocity = 3f;
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

            grounded = TryGetGround(transform.position, out RaycastHit groundHit);
            if (grounded)
                groundNormal = Vector3.Slerp(groundNormal, groundHit.normal, 0.35f).normalized;
            else
                groundNormal = Vector3.Slerp(groundNormal, Vector3.up, 0.08f).normalized;

            Vector3 driveForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            if (driveForward.sqrMagnitude < 0.1f) driveForward = transform.forward;

            float speed = Vector3.Dot(rb.velocity, driveForward); // 沿坡面前向速度（带符号）
            float flatSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;

            // ---- 纵向动力：前进 / 刹车 / 倒车三态 ----
            if (throttle > 0.01f)
            {
                rb.AddForce(driveForward * motorForce * (boost ? boostMultiplier : 1f) * throttle,
                    ForceMode.Acceleration);
            }
            else if (throttle < -0.01f)
            {
                if (speed > 1f)
                    rb.AddForce(-driveForward * brakeForce * -throttle, ForceMode.Acceleration);
                else
                    rb.AddForce(driveForward * motorForce * 0.65f * throttle, ForceMode.Acceleration);
            }
            else if (flatSpeed > 0.05f)
            {
                // 无输入时自然滑行减速，停车不再长时漂移。
                Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                rb.AddForce(-flatVelocity.normalized * Mathf.Min(rollingResistance, flatSpeed / Time.fixedDeltaTime),
                    ForceMode.Acceleration);
            }

            // ---- 速度上限（Shift 时上限同步放宽）----
            float cap = speed < -0.05f ? reverseMaxSpeed : maxSpeed * (boost ? boostMultiplier : 1f);
            if (flatSpeed > cap)
                rb.velocity = new Vector3(rb.velocity.x / flatSpeed * cap, rb.velocity.y, rb.velocity.z / flatSpeed * cap);

            // ---- 转向：起步时也可转向，高速逐步变缓 ----
            float steerFactor = Mathf.Lerp(1f, minSteerFactor,
                Mathf.InverseLerp(steerFalloffSpeed, maxSpeed, flatSpeed));
            float driveDir = speed < -0.5f ? -1f : 1f; // 倒车时转向反向，符合开车直觉
            float movingGate = Mathf.Clamp01(flatSpeed / 1.5f);
            float speedGate = Mathf.Abs(throttle) > 0.05f ? Mathf.Max(launchSteerFactor, movingGate) : movingGate;
            yaw += steer * steerSpeed * steerFactor * driveDir * speedGate * Time.fixedDeltaTime;
            Vector3 heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            heading = Vector3.ProjectOnPlane(heading, groundNormal).normalized;
            if (heading.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(heading, groundNormal);
                float alignT = 1f - Mathf.Exp(-groundAlignSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, alignT));
            }

            // ---- 抓地：把横向分速度按强度往回拽；按住空格(手刹)时抓地放松 → 甩尾漂移 ----
            Vector3 driveRight = Vector3.Cross(groundNormal, driveForward).normalized;
            Vector3 lateral = driveRight * Vector3.Dot(rb.velocity, driveRight);
            rb.AddForce(-lateral * (handbrake ? handbrakeGrip : normalGrip), ForceMode.Acceleration);

            // ---- 轻度贴地 + 制动 ----
            rb.AddForce(-transform.up * downForce, ForceMode.Acceleration);
            if (handbrake)
                rb.AddForce(-driveForward * speed * 5f, ForceMode.Acceleration);

            // 长底盘在岩石边缘或短坡上低速顶住时，检查前方地面高度并给予一次小幅抬升。
            // 只对可跨越的地形台阶生效，不会让车辆攀爬垂直墙面。
            if (grounded && Mathf.Abs(throttle) > 0.35f && flatSpeed < 0.32f)
                stallTimer += Time.fixedDeltaTime;
            else
                stallTimer = 0f;
            if (stallTimer >= stallAssistDelay)
            {
                Vector3 direction = throttle >= 0f ? driveForward : -driveForward;
                TryAssistOverTerrain(direction, groundHit.point);
                stallTimer = 0f;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) RecoverVehicle();

            // 上车后玩家根物体（包括 HUDController）会停用，由车辆继续刷新速度。
            if (SciFiHUD.I != null)
                SciFiHUD.I.SetSpeed(SpeedKmh.ToString("0") + " km/h");
        }

        /// <summary>当前速度 km/h，HUD 速度表直接读这个。</summary>
        public float SpeedKmh => rb.velocity.magnitude * 3.6f;

        bool TryGetGround(Vector3 position, out RaycastHit best)
        {
            best = default;
            var hits = Physics.SphereCastAll(position + Vector3.up * 2.2f, 0.38f, Vector3.down,
                groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                best = hit;
                return true;
            }
            return false;
        }

        void TryAssistOverTerrain(Vector3 direction, Vector3 currentGroundPoint)
        {
            if (!TryGetGround(transform.position + direction * 1.9f + Vector3.up * 0.2f,
                    out RaycastHit ahead)) return;
            float step = ahead.point.y - currentGroundPoint.y;
            if (step < 0.04f || step > 1.05f || ahead.normal.y < 0.3f) return;

            rb.position += Vector3.up * Mathf.Min(stallAssistHeight, step + 0.12f) + direction * 0.18f;
            Vector3 retainedVertical = Vector3.Project(rb.velocity, Vector3.up);
            rb.velocity = direction * stallAssistSpeed + retainedVertical;
        }

        void RecoverVehicle()
        {
            Vector3 position = rb.position + Vector3.up * 1.5f;
            var hits = Physics.RaycastAll(rb.position + Vector3.up * 4f, Vector3.down, 20f,
                ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                // 射线会先穿过车身自己，必须跳过，否则 R 会把车放到自己车顶上。
                if (hit.collider.transform.IsChildOf(transform)) continue;
                position = hit.point + Vector3.up * 1.2f;
                break;
            }

            rb.position = position;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }
}
