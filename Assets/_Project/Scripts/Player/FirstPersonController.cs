using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 第一人称控制器：WASD 移动 + 鼠标视角 + 空格跳跃。
    /// 核心特性：重力倍率可调——月面段设 1/6（1.62 m/s²），同样的跳跃力度能跳 6 倍高度，
    /// 直接做出"月球漫步"的手感差异。
    /// 挂载要求：本物体挂 CharacterController；主相机作为子物体放在眼部高度。
    /// </summary>
    public class FirstPersonController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 5f;            // 行走速度 m/s
        public float jumpHeight = 1.2f;         // 跳跃目标高度（米）。月面重力下实际弧线更飘、滞空更长

        [Header("重力（按场景调整）")]
        [Tooltip("1 = 地球 9.81，1/6 = 月面 1.62。切场景时改这一个参数即可")]
        public float gravityScale = 1f / 6f;

        [Header("视角")]
        public float mouseSensitivity = 150f;   // 视角转速不合适时优先调整此参数
        public float pitchLimit = 85f;          // 俯仰角度限制，避免视角过度旋转

        CharacterController cc;
        Transform cam;
        float pitch;            // 相机俯仰角
        float verticalVelocity; // 垂直速度（跳跃/下落）

        // 实际重力加速度 = -9.81 × 倍率
        float Gravity => -9.81f * gravityScale;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cam = GetComponentInChildren<Camera>().transform;
            Cursor.lockState = CursorLockMode.Locked; // 锁鼠标到屏幕中心，Esc 可解锁
            Cursor.visible = false;
        }

        void Update()
        {
            Look();
            Move();
        }

        void Look()
        {
            // 水平转身体（Y 轴），垂直转相机（俯仰），分开处理是第一人称的标准做法
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -pitchLimit, pitchLimit);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Move()
        {
            bool grounded = cc.isGrounded;
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // 贴地处理：在地面时保持下压力，避免下坡时悬空抖动

            // WASD 输入，归一化防斜向加速；转为身体朝向的世界方向
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            Vector3 worldMove = transform.TransformDirection(input) * walkSpeed;

            // 跳跃初速度按当前重力反推，保证 gravityScale 怎么调，跳跃高度都符合 jumpHeight
            if (grounded && Input.GetKeyDown(KeyCode.Space))
                verticalVelocity = Mathf.Sqrt(2f * -Gravity * jumpHeight);

            verticalVelocity += Gravity * Time.deltaTime;
            cc.Move((worldMove + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
