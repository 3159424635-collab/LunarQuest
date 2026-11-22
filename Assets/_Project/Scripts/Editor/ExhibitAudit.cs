
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class ExhibitAudit
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();

            // 渲染展台特写（穹顶大厅内三个展台）
            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("AuditCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f; cam.fieldOfView = 55f;
            cam.targetTexture = rt;
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                (new Vector3(-3f, 3.5f, 176f), new Vector3(0.6f, 2.5f, 182.6f), "exhibit_0"),
                (new Vector3(-3f, 3.5f, 178f), new Vector3(0.6f, 2.5f, 172.6f), "exhibit_1"),
                (new Vector3(0f, 3.5f, 180f), new Vector3(-8.6f, 2.5f, 182.6f), "exhibit_2"),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes($"Logs/{v.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(camGo);

            // ===== 剧本内容对照清单 =====
            sb.AppendLine("===== S4 剧本要素对照 =====");
            string[] need = { "Player", "Rover", "QuestManager", "SciFiHUD", "InteractionPromptUI",
                "NPC_周航", "NPC_许青", "NPC_陈越", "SampleRock_A", "Crater03Trigger",
                "ExhibitStand_0", "ExhibitStand_1", "ExhibitStand_2", "DomeDoor", "Airlock_S_Outer",
                "Airlock_S_Inner", "Airlock_W_Gate", "FieldTabletNavigator", "LunarTerrainVisual" };
            foreach (var n in need)
            {
                bool found = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name.Contains(n.Replace("NPC_", "NPC_"))) { found = true; break; }
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name.Contains(n)) { found = true; break; }
                    if (found) break;
                }
                sb.AppendLine($"  {(found ? "OK " : "缺失")} {n}");
            }
            // 展台材质检查
            sb.AppendLine("===== 展台模型材质检查 =====");
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.Contains("ExhibitMini")) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1;
                        bool hasTex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                        sb.AppendLine($"  {root.name}: 金属度={metal:F2} 贴图={(hasTex ? "有" : "无")} 材质={m.name}");
                    }
                }
            }
            File.WriteAllText("Logs/script_audit.txt", sb.ToString());
            Debug.Log("[ExhibitAudit] " + sb.ToString());
        }
    }
}
