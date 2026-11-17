
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace LunarQuest.EditorTools
{
    public static class S1SizeCheck
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S1_LaunchSite.unity", OpenSceneMode.Single);
            var sb = new System.Text.StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                var r = root.GetComponent<Renderer>();
                if (r == null)
                {
                    var rc = root.GetComponentInChildren<Renderer>();
                    if (rc == null) continue;
                }
                Bounds b = default; bool first = true;
                foreach (var rr in root.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = rr.bounds; first = false; } else b.Encapsulate(rr.bounds);
                }
                if (!first)
                    sb.AppendLine($"{root.name}: pos={root.transform.position} size={b.size}");
            }
            File.WriteAllText("Logs/s1_sizes.txt", sb.ToString());
            Debug.Log("[S1SizeCheck]\n" + sb);
        }
    }
}
