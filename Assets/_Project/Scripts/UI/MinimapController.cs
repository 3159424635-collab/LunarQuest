using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 小地图相机：正交俯拍 + 跟随玩家，输出到 RenderTexture 显示在 HUD 上。
    /// 设计：北向固定（地图不随玩家旋转，读者永远知道哪边是北），玩家箭头自己转——
    /// 这是主流开放世界游戏的默认做法，也最不容易看晕。
    ///
    /// 挂载要求：场景新建 Camera → 挂本脚本 → target 拖玩家/月球车。
    /// RenderTexture 与 HUD 圆框的接法见《模块一-脚本挂载说明》。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapController : MonoBehaviour
    {
        public Transform target;        // 跟随对象
        public float height = 150f;     // 相机高度（只要盖过地形起伏即可）
        public float viewSize = 60f;    // 常规正交视野半径（米）

        [Header("M 键切换远近两档")]
        public float zoomedViewSize = 25f;
        public KeyCode toggleKey = KeyCode.M;

        Camera cam;
        bool zoomed;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = viewSize;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 正俯视
        }

        void LateUpdate()
        {
            if (!target) return;
            // 只跟随水平位置：X/Z 跟人走，高度固定
            transform.position = new Vector3(target.position.x, height, target.position.z);

            if (Input.GetKeyDown(toggleKey)) zoomed = !zoomed;
            cam.orthographicSize = zoomed ? zoomedViewSize : viewSize;
        }

        /// <summary>
        /// 世界坐标 → 小地图局部坐标（-0.5 ~ +0.5），UI 层再乘以 RawImage 尺寸即可定位。
        /// 任务点图标、基地标记都用这个换算。
        /// </summary>
        public static Vector2 WorldToMinimapLocal(Vector3 worldPos, Transform player, float orthoSize)
        {
            Vector3 d = worldPos - player.position;
            return new Vector2(d.x, d.z) / (orthoSize * 2f);
        }
    }
}
