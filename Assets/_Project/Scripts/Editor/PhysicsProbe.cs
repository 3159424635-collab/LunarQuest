
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class PhysicsProbe
    {
        public static void ProbeEntry()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();

            // 从玩家实际行进路线逐点向上/向前发射射线，检测所有碰撞体
            // 玩家高1.8m，从 y=ground+0.9 向北（+z方向）探测
            for (int step = 0; step <= 40; step++)
            {
                float z = 126f + step * 0.5f;  // 从外门z=129往前走到罩内z=146
                float x = 4f;                    // 南气闸中线
                var origin = new Vector3(x, 2.32f + 0.9f, z);  // 眼部高度
                var dir = Vector3.forward;       // 向北看

                // 向前射线检测（8m长）
                RaycastHit[] hits = Physics.RaycastAll(origin, dir, 8f);
                var hitInfo = new System.Collections.Generic.List<string>();
                foreach (var h in hits)
                {
                    if (h.collider.gameObject.name.Contains("Path")) continue;
                    hitInfo.Add($"{h.collider.gameObject.name}@{h.distance:F1}m");
                }
                if (hitInfo.Count > 0)
                    sb.AppendLine($"z={z:F1}: 前方命中 → {string.Join(", ", hitInfo)}");

                // 垂直向下射线（脚下有没有地板）
                RaycastHit down;
                if (!Physics.Raycast(origin, Vector3.down, out down, 10f))
                    sb.AppendLine($"z={z:F1}: 脚下无地板！");
            }
            File.WriteAllText("Logs/physics_probe.txt", sb.ToString());
            Debug.Log("[PhysicsProbe] 完成 → Logs/physics_probe.txt\n" + sb.ToString());
        }
    }
}
