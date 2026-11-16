
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class SealProbe
    {
        public static void Probe()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();

            // 玻璃罩中心约 (4, y, 190)，半径约 52
            // 从罩外 360° 环绕发射向内射线（高度=玩家眼部 y≈4.2）
            float cx = 4f, cz = 190f, probeR = 60f, probeY = 4.2f;
            int leaks = 0, total = 0;

            for (int deg = 0; deg < 360; deg += 5)
            {
                float rad = deg * Mathf.PI / 180f;
                var origin = new Vector3(cx + probeR * Mathf.Cos(rad), probeY, cz + probeR * Mathf.Sin(rad));
                var dir = (new Vector3(cx, probeY, cz) - origin).normalized;

                // 发射 50m 长的射线向罩心
                // 无过滤：任何碰撞体挡住射线就算密封（排除地面）
                RaycastHit[] hits = Physics.RaycastAll(origin, dir, 55f);
                bool blocked = false;
                string blockers = "";
                foreach (var h in hits)
                {
                    if (h.collider is TerrainCollider) continue;
                    blocked = true;
                    blockers += h.collider.gameObject.name + "@" + h.distance.ToString("F0") + "m ";
                }
                total++;
                if (!blocked)
                {
                    leaks++;
                    sb.AppendLine($"泄漏! 角度={deg}° origin=({origin.x:F0},{origin.z:F0}) → 无任何阻挡，射线直达罩心");
                }
            }

            // 再从更高处检测（y=15，穿过玻璃罩中上部）
            sb.AppendLine($"\n--- 高处检测 (y=15) ---");
            int leaks2 = 0;
            for (int deg = 0; deg < 360; deg += 10)
            {
                float rad = deg * Mathf.PI / 180f;
                var origin = new Vector3(cx + probeR * Mathf.Cos(rad), 15f, cz + probeR * Mathf.Sin(rad));
                var dir = (new Vector3(cx, 15f, cz) - origin).normalized;
                RaycastHit[] hits2 = Physics.RaycastAll(origin, dir, 55f);
                bool blocked = false;
                foreach (var h in hits2)
                {
                    if (h.collider is TerrainCollider) continue;
                    blocked = true; break;
                }
                if (!blocked)
                {
                    leaks2++;
                    sb.AppendLine($"高处泄漏! 角度={deg}°");
                }
            }

            sb.AppendLine($"\n===== 密封性报告 =====");
            sb.AppendLine($"低处(眼高): {leaks}/{total} 方向有泄漏 ({leaks*100/total}%)");
            sb.AppendLine($"高处(15m): {leaks2}/36 方向有泄漏 ({leaks2*100/36}%)");
            File.WriteAllText("Logs/seal_probe.txt", sb.ToString());
            Debug.Log("[SealProbe] " + sb.ToString());
        }
    }
}
