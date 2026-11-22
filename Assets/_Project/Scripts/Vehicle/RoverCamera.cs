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
        public Vector3 offset = new Vector3(0f, 4.2f, -8.5f); // 车后上方
        public float followSmoothing = 7f;
        public float collisionRadius = 0.3f;
        public float collisionPadding = 0.25f;

        [Header("速度感视野")]
        public float baseFov = 58f;
        public float maxFov = 68f;
        public float fovMaxSpeed = 8f;

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

            // 位置：对车后方的目标点做插值，避免相机刚性固定产生的僵硬感
            Vector3 focus = target.position + target.forward * 2.4f + Vector3.up * 1.5f;
            Vector3 desired = target.position + target.TransformDirection(offset);
            Vector3 direction = desired - focus;
            float distance = direction.magnitude;
            if (distance > 0.01f)
            {
                direction /= distance;
                var hits = Physics.SphereCastAll(focus, collisionRadius, direction, distance,
                    ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(target)) continue;
                    desired = focus + direction * Mathf.Max(0.8f, hit.distance - collisionPadding);
                    break;
                }
            }
            float followT = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, followT);

            // 看向车前方一点，过弯时镜头自然带动视线
            transform.LookAt(focus);

            // FOV 随速度拉伸，以较低成本增强速度感
            if (cam && rover)
                cam.fieldOfView = Mathf.Lerp(baseFov, maxFov,
                    Mathf.Clamp01(rover.SpeedKmh / 3.6f / fovMaxSpeed));
        }
    }
}
