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
        bool inVehicle;

        void Awake()
        {
            rover = GetComponent<RoverController>();
            // 初始为步行状态：车辆控制与车辆相机关闭，避免键盘同时驱动两者
            rover.enabled = false;
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
            rover.enabled = true;
            if (roverCamera) roverCamera.gameObject.SetActive(true);
        }

        void Exit()
        {
            inVehicle = false;
            rover.enabled = false;
            if (roverCamera) roverCamera.gameObject.SetActive(false);
            // 下车点按车身朝向变换到世界坐标，避免下到车底或坡内
            player.position = transform.TransformPoint(exitOffset);
            player.gameObject.SetActive(true);
        }
    }
}
