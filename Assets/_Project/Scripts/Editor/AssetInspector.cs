using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LunarQuest.EditorTools
{
    /// <summary>
    /// 模型资产体检：网格/材质/贴图统计 + 四角度渲染截图，输出到 Logs/。
    /// 批处理入口：LunarQuest.EditorTools.AssetInspector.InspectMoonBase
    /// </summary>
    public static class AssetInspector
    {
        public static void InspectMoonBase()
        {
            Inspect("Assets/_Project/Art/Models/月球基地.fbx", "moonbase");
        }

        static void Inspect(string path, string tag)
        {
            Directory.CreateDirectory("Logs");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[AssetInspector] 找不到 " + path);
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            // 启用全部子物体（场景搭建时摘除的地面网格也要看）
            SetActiveRecursive(go.transform, true);

            var sb = new StringBuilder();
            sb.AppendLine($"# {path}");
            int totalVerts = 0, totalTris = 0, meshCount = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                meshCount++;
                totalVerts += mf.sharedMesh.vertexCount;
                totalTris += mf.sharedMesh.triangles.Length / 3;
                sb.AppendLine($"网格 {GetPath(mf.transform)}: 顶点{mf.sharedMesh.vertexCount} 三角{mf.sharedMesh.triangles.Length / 3}");
            }
            sb.AppendLine($"合计: {meshCount} 个网格, {totalVerts} 顶点, {totalTris} 三角形");
            sb.AppendLine();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    string tex = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "无";
                    sb.AppendLine($"渲染器 {GetPath(r.transform)} 材质[{mat?.name}] 主贴图[{tex}]");
                }
            }
            File.WriteAllText($"Logs/{tag}_stats.txt", sb.ToString());

            Bounds b = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            float radius = Mathf.Max(b.size.magnitude * 0.7f, 1f);
            Vector3[] views =
            {
                new Vector3(1f, 0.55f, 1f), new Vector3(-1f, 0.55f, 1f),
                new Vector3(0f, 1.4f, -1f), new Vector3(1f, 0.25f, -1f),
            };
            var camGo = new GameObject("InspectCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = radius * 6f;
            cam.fieldOfView = 45f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < views.Length; i++)
            {
                cam.transform.position = b.center + views[i].normalized * radius;
                cam.transform.LookAt(b.center);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{tag}_{i}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(go);
            Debug.Log($"[AssetInspector] 完成：Logs/{tag}_stats.txt + 4 张截图");
        }

        static void SetActiveRecursive(Transform t, bool active)
        {
            t.gameObject.SetActive(active);
            for (int i = 0; i < t.childCount; i++) SetActiveRecursive(t.GetChild(i), active);
        }

        static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
    }
}
