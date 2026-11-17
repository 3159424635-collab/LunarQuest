
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class ExhibitProbe
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            // 模拟玩家站在展台前 2m，眼高约 2.3+1.6=3.9? 实际 ground≈2.3 → 眼≈3.9
            // 展台在 (0.6, 182.6) 附近；玩家站 (0.6, 179) 眼高 3.9，看向展台模型顶部(~3.5)和桌面(~3.3)
            Vector3 eye = new Vector3(0.6f, 3.9f, 179.5f);
            Vector3[] targets =
            {
                new Vector3(0.6f, 3.4f, 182.6f),   // 平视模型中部
                new Vector3(0.6f, 3.0f, 182.6f),   // 俯视桌面
                new Vector3(0.6f, 4.2f, 182.6f),   // 看模型顶部
            };
            string[] names = { "平视模型中部", "俯视桌面", "看模型顶部" };
            for (int i = 0; i < targets.Length; i++)
            {
                var dir = (targets[i] - eye).normalized;
                var all = Physics.RaycastAll(eye, dir, 4f);
                System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
                var ib = default(InteractableBase); var hitName = "无"; var dist = 0f;
                foreach (var h in all)
                {
                    var cand = h.collider.GetComponentInParent<InteractableBase>();
                    if (cand != null) { ib = cand; hitName = h.collider.name; dist = h.distance; break; }
                }
                sb.AppendLine($"{names[i]}: [RaycastAll] {(ib ? $"✓ {hitName} @ {dist:F1}m → {ib.prompt}" : "✗ 4m内无可交互物")} (穿透 {all.Length} 个碰撞体)");
            }
            // 三个展台都测：站位在桌子正南 1.5m
            Vector3[] standPos = { new Vector3(0.6f, 0, 181.0f), new Vector3(0.6f, 0, 174.0f), new Vector3(-7.2f, 0, 181.0f) };
            Vector3[] standLook = { new Vector3(0.6f, 0, 182.6f), new Vector3(0.6f, 0, 172.7f), new Vector3(-8.6f, 0, 182.6f) };
            for (int i = 0; i < 3; i++)
            {
                var eye2 = new Vector3(standPos[i].x, 3.6f, standPos[i].z);
                var dir2 = (new Vector3(standLook[i].x, 3.2f, standLook[i].z) - eye2).normalized;
                var all2 = Physics.RaycastAll(eye2, dir2, 4f);
                System.Array.Sort(all2, (a, b) => a.distance.CompareTo(b.distance));
                var ib2 = default(InteractableBase); var n2 = "无"; var d2 = 0f;
                foreach (var h in all2)
                {
                    var c = h.collider.GetComponentInParent<InteractableBase>();
                    if (c != null) { ib2 = c; n2 = h.collider.name; d2 = h.distance; break; }
                }
                sb.AppendLine($"展台{i} 正面: {(ib2 ? $"✓ {n2} @ {d2:F1}m → {ib2.prompt}" : "✗ 4m内无可交互物")}");
            }
            File.WriteAllText("Logs/exhibit_probe.txt", sb.ToString());
            Debug.Log("[ExhibitProbe]\n" + sb);
        }
    }
}
