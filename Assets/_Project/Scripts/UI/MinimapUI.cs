using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 小地图图标层：玩家箭头随朝向旋转（固定在小地图中心），任务点按世界坐标
    /// 换算定位（用 MinimapController.WorldToMinimapLocal），超出范围钳制在边缘
    /// 以提示方向。坐标换算与钳制逻辑独立于美术样式，图标换成正式资源后无需改代码。
    /// 挂载要求：场景新建空物体；箭头与任务点是地图底图 RawImage 的子 Image。
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public MinimapController minimap;       // 小地图相机（挂 MinimapController）
        public RectTransform mapImage;          // 小地图底图（RawImage，轴心居中）
        public RectTransform playerArrow;       // 玩家箭头 Image（位于底图中心）
        public RectTransform questMarker;       // 任务点 Image
        public Transform questTarget;           // 任务点对应的世界物体（任务系统赋值）

        void LateUpdate()
        {
            if (!minimap || !minimap.target || !mapImage) return;

            // 箭头旋转：世界航向角（绕 Y）对应 UI 平面上的反向旋转
            if (playerArrow)
            {
                float yaw = minimap.target.eulerAngles.y;
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }

            if (questMarker && questTarget)
            {
                // 世界坐标 → 底图局部坐标（-0.5~+0.5），再乘底图尺寸换算为像素
                Vector2 local = MinimapController.WorldToMinimapLocal(
                    questTarget.position, minimap.target, minimap.orthographicSize);
                float size = mapImage.rect.width != 0f ? mapImage.rect.width : 220f;
                Vector2 p = local * size;

                // 超出地图范围时钳制在边缘（保留方向提示）
                float limit = size * 0.48f;
                questMarker.anchoredPosition = new Vector2(
                    Mathf.Clamp(p.x, -limit, limit),
                    Mathf.Clamp(p.y, -limit, limit));

                questMarker.gameObject.SetActive(true);
            }
            else if (questMarker)
            {
                questMarker.gameObject.SetActive(false);
            }
        }
    }
}
