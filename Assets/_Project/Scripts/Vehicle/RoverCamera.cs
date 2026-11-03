using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 月球车追尾相机：平滑跟随 + 速度感视野（越快 FOV 越大）。
    /// 挂载要求：场景里新建一个 Camera，挂本脚本，把月球车拖进 target。
    /// 注意：上车期间启用此相机、禁用玩家相机（上下车切换脚本后续提供）。
    /// </summary>
    public class RoverCamera : MonoBehaviour
    {
        public Transform target;                       // 月球车
        public Vector3 offset = new Vector3(0f, 3.5f, -7f); // 车后上方
        public float followSmoothing = 6f;             // 跟随平滑度，越大越"跟得紧"

        [Header("速度感视野")]
        public float baseFov = 60f;
        public float maxFov = 78f;                     // 高速时视野
        public float fovMaxSpeed = 20f;                // 达到该速度(m/s)时 FOV 拉满

        Camera cam;
        RoverController rover;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target) rover = target.GetComponent<RoverController>();
        }

        void LateUpdate()
        {
            if (!target) return;

            // 位置：车后方的目标点做插值，避免硬邦邦的"焊死在车上"
            Vector3 desired = target.position + target.TransformDirection(offset);
            transform.position = Vector3.Lerp(transform.position, desired, followSmoothing * Time.deltaTime);

            // 看向车前方一点，过弯时镜头自然带动视线
            transform.LookAt(target.position + target.forward * 3f + Vector3.up * 1.2f);

            // FOV 随速度拉伸，速度感的低成本特效
            if (cam && rover)
                cam.fieldOfView = Mathf.Lerp(baseFov, maxFov,
                    Mathf.Clamp01(rover.SpeedKmh / 3.6f / fovMaxSpeed));
        }
    }
}
