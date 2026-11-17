
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Reflection;

namespace LunarQuest.EditorTools
{
    public static class SampleProbe
    {
        public static void Run()
        {
            var t = typeof(WhiteboxBuilder);
            var mi = t.GetMethod("SampleHeight", BindingFlags.NonPublic | BindingFlags.Static);
            var sb = new StringBuilder();
            foreach (var pt in new[] { (4f, 141f), (4f, 156f), (0f, 170f), (-8f, 188f) })
            {
                float h = (float)mi.Invoke(null, new object[] { pt.Item1, pt.Item2 });
                sb.AppendLine($"SampleHeight({pt.Item1}, {pt.Item2}) = {h:F2}");
            }
            var fi = t.GetField("BaseFlat", BindingFlags.NonPublic | BindingFlags.Static);
            var fv = (Vector2)fi.GetValue(null);
            sb.AppendLine($"BaseFlat 常量 = {fv}");
            sb.AppendLine($"Dist((4,141)→BaseFlat) = {Vector2.Distance(new Vector2(4,141), fv):F1}");
            File.WriteAllText("Logs/sample.txt", sb.ToString());
            Debug.Log("[SampleProbe]\n" + sb);
        }
    }
}
