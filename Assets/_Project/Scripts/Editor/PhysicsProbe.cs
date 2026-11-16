
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class PhysicsProbe
    {
        public static void ProbeAll()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();

            // ===== 路线1：南气闸入口（z=126 → z=160） =====
            sb.AppendLine("===== 南气闸入口 =====");
            for (int step = 0; step <= 60; step++)
            {
                float z = 126f + step * 0.5f;
                float x = 4f;
                var origin = new Vector3(x, 3.2f, z);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.forward, 10f);
                foreach (var h in hits)
                {
                    if (h.collider.gameObject.name.Contains("Path") || h.collider.gameObject.name == "L" || h.collider.gameObject.name == "R") continue;
                    sb.AppendLine($"  气闸 z={z:F1}: {h.collider.gameObject.name}@{h.distance:F1}m");
                }
            }
            // ===== 路线2：穹顶南门（z=157 → z=175，x=-4） =====
            sb.AppendLine("===== 穹顶南门 =====");
            for (int step = 0; step <= 30; step++)
            {
                float z = 157f + step * 0.5f;
                float x = -4f;
                var origin = new Vector3(x, 3.2f, z);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.forward, 10f);
                foreach (var h in hits)
                {
                    if (h.collider.gameObject.name.Contains("Path") || h.collider.gameObject.name.Contains("Panel")) continue;
                    sb.AppendLine($"  穹顶门 z={z:F1}: {h.collider.gameObject.name}@{h.distance:F1}m");
                }
            }
            // ===== 路线3：走廊A（x=-6 → x=-20，z=178） =====
            sb.AppendLine("===== 走廊A =====");
            for (int step = 0; step <= 28; step++)
            {
                float x = -6f - step * 0.5f;
                var origin = new Vector3(x, 3.2f, 178f);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.left, 8f);
                foreach (var h in hits)
                {
                    sb.AppendLine($"  走廊A x={x:F1}: {h.collider.gameObject.name}@{h.distance:F1}m");
                }
            }
            // ===== 浮空物扫描：罩内区域逐点向下+向上射线，找悬空几何体 =====
            sb.AppendLine("===== 浮空物扫描（罩内 60×40m 区域） =====");
            for (int gx = -20; gx <= 20; gx += 4)
            {
                for (int gz = 150; gz <= 200; gz += 4)
                {
                    var origin = new Vector3(gx, 30f, gz);  // 从30m高向下
                    RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f);
                    foreach (var h in hits)
                    {
                        string n = h.collider.gameObject.name;
                        // 排除地面/地板/已知正常物件
                        if (n.Contains("Terrain") || n.Contains("Floor") || n.Contains("floor") ||
                            n.Contains("Rock") || n.Contains("Path") || n.Contains("Ground") ||
                            n.Contains("Dome_") || n.Contains("Mod") || n.Contains("Cor") ||
                            n.Contains("Bunk") || n.Contains("Desk") || n.Contains("Bench") ||
                            n.Contains("Holo") || n.Contains("Exhibit") || n.Contains("Lamp") ||
                            n.Contains("Solar") || n.Contains("Crate") || n.Contains("Antenna") ||
                            n.Contains("AirS") || n.Contains("AirE") || n.Contains("AirT") ||
                            n.Contains("Airlock") || n.Contains("Gate") || n.Contains("DomeDoor") ||
                            n.Contains("Player") || n.Contains("Rover") || n.Contains("NPC")) continue;
                        float heightAboveGround = h.point.y - 2.4f;
                        if (heightAboveGround > 1.5f)
                            sb.AppendLine($"  浮空? ({gx},{gz}) y={h.point.y:F1}(+{heightAboveGround:F1}m): {n}");
                    }
                }
            }
            File.WriteAllText("Logs/physics_probe.txt", sb.ToString());
            Debug.Log("[PhysicsProbe] 完成 → Logs/physics_probe.txt\n" + sb.ToString());
        }
    }
}
