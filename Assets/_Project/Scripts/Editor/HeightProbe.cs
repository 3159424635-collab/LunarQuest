
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

namespace LunarQuest.EditorTools
{
    public static class HeightProbe
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            var terrain = Object.FindObjectOfType<Terrain>();
            foreach (float z in new float[] { 126f, 131f, 136f, 141f, 146f, 151f, 156f, 161f })
            {
                float h = terrain.SampleHeight(new Vector3(4f, 500f, z)) + terrain.transform.position.y;
                sb.AppendLine($"(4, {z}) 地形高度 = {h:F2}m");
            }
            File.WriteAllText("Logs/heights.txt", sb.ToString());
            Debug.Log("[HeightProbe]\n" + sb);
        }
    }
}
