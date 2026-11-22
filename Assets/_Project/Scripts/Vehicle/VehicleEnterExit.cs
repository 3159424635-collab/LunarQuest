using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 上下车切换：步行靠近月球车按 F 上车，车内再按 F 下车。
    /// 自动完成控制器与相机的启停（玩家整体隐藏/显示），下车时把玩家放到车侧安全点。
    /// 挂载要求：月球车物体（需已有 RoverController），Player 拖玩家根物体，
    /// RoverCamera 拖场景中挂 RoverCamera 的相机物体。
    /// </summary>
    public class VehicleEnterExit : MonoBehaviour
    {
        public Transform player;                    // 玩家根物体
        public RoverCamera roverCamera;             // 月球车相机
        public float enterRange = 3.5f;             // 可上车距离（米）
        public KeyCode toggleKey = KeyCode.F;
        public Vector3 exitOffset = new Vector3(-2.5f, 0f, 0f); // 下车点（车身左侧，本地坐标）

        RoverController rover;
        Rigidbody roverBody;
        bool inVehicle;

        public bool IsInVehicle => inVehicle;

        void Awake()
        {
            rover = GetComponent<RoverController>();
            roverBody = GetComponent<Rigidbody>();
            // 初始为步行状态：车辆控制与车辆相机关闭，避免键盘同时驱动两者
            rover.enabled = false;
            ParkRover();
            if (roverCamera) roverCamera.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!player) return;

            if (!inVehicle)
            {
                float dist = Vector3.Distance(player.position, transform.position);
                if (dist <= enterRange && Input.GetKeyDown(toggleKey)) Enter();
            }
            else if (Input.GetKeyDown(toggleKey))
            {
                Exit();
            }
        }

        void Enter()
        {
            inVehicle = true;
            player.gameObject.SetActive(false);     // 玩家整体隐藏（含相机、控制器、交互）
            UnparkRover();
            rover.enabled = true;
            if (roverCamera)
            {
                roverCamera.gameObject.SetActive(true);
                // 编辑器搭建时相机组件可能是禁用状态，上车时一并开启，避免黑屏
                var cam = roverCamera.GetComponent<Camera>();
                if (cam) cam.enabled = true;
            }
        }

        /// <summary>外部切换入口（交互脚本 E 键调用，与 F 键等效）。</summary>
        public void Toggle()
        {
            if (inVehicle) Exit();
            else Enter();
        }

        /// <summary>S4 进场时强制恢复步行状态，防止旧相机或车辆状态占用输入。</summary>
        public void ForceWalkingState()
        {
            inVehicle = false;
            if (rover == null) rover = GetComponent<RoverController>();
            if (roverBody == null) roverBody = GetComponent<Rigidbody>();
            if (rover != null) rover.enabled = false;
            ParkRover();
            if (roverCamera != null) roverCamera.gameObject.SetActive(false);
            if (player != null) player.gameObject.SetActive(true);
        }

        void Exit()
        {
            inVehicle = false;
            rover.enabled = false;
            ParkRover();
            if (roverCamera) roverCamera.gameObject.SetActive(false);
            // 下车点按车身朝向变换到世界坐标，抬高少许防止卡进地面或车体碰撞
            player.position = transform.TransformPoint(exitOffset) + Vector3.up * 0.35f;
            player.gameObject.SetActive(true);
        }

        void ParkRover()
        {
            if (roverBody == null) roverBody = GetComponent<Rigidbody>();
            if (roverBody == null) return;

            // 关闭控制器后若仍让刚体参与物理，SEV 的高车身会在微小坡面或残余角速度下慢慢翻倒。
            // 停车时清除动量并锁成运动学刚体；不强制旋转，避免在斜坡上下车时车身瞬间跳姿态。
            roverBody.velocity = Vector3.zero;
            roverBody.angularVelocity = Vector3.zero;
            roverBody.isKinematic = true;
            roverBody.Sleep();
        }

        void UnparkRover()
        {
            if (roverBody == null) roverBody = GetComponent<Rigidbody>();
            if (roverBody == null) return;
            roverBody.isKinematic = false;
            roverBody.velocity = Vector3.zero;
            roverBody.angularVelocity = Vector3.zero;
            roverBody.WakeUp();
        }
    }
}
