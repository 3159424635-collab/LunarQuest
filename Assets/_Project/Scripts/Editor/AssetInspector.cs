using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        public static void InspectInterior()
        {
            Inspect("Assets/_Project/Art/Models/Sketchfab/command_center_interior/scene.gltf", "interior");
        }

        public static void InspectCustomBase()
        {
            Inspect("Assets/_Project/Art/Models/月球操作仓.fbx", "opod");
        }

        public static void InspectNasaModels()
        {
            Inspect("Assets/_Project/Art/Models/NASA/Space Exploration Vehicle/Space Exploration Vehicle.glb",
                "nasa_sev");
            Inspect("Assets/_Project/Art/Models/NASA/Apollo Lunar Module/Apollo Lunar Module.glb",
                "nasa_apollo_lm");
        }

        // 全面排查建筑漏缝：外围四角度 + 所有关键连接点
        public static void AuditBuilding()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);

            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("Audit");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 70f;
            cam.targetTexture = rt;

            // 基地中心大约在 (0, 2, 185)，玻璃罩半径 ~52
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                // 外围四角度（看整体）
                (new Vector3(70f, 15f, 120f), new Vector3(0f, 5f, 185f), "audit_S_out"),
                (new Vector3(-80f, 15f, 120f), new Vector3(0f, 5f, 185f), "audit_SW_out"),
                (new Vector3(-80f, 15f, 260f), new Vector3(0f, 5f, 185f), "audit_NW_out"),
                (new Vector3(70f, 15f, 260f), new Vector3(0f, 5f, 185f), "audit_NE_out"),
                // 南气闸近距离（看密封）
                (new Vector3(4f, 2f, 124f), new Vector3(4f, 2.5f, 135f), "audit_airS_far"),
                (new Vector3(4f, 2.2f, 131f), new Vector3(4f, 2.5f, 138f), "audit_airS_tunnel"),
                (new Vector3(4f, 2.2f, 136f), new Vector3(4f, 2.5f, 145f), "audit_airS_inner"),
                (new Vector3(8f, 2.5f, 134f), new Vector3(0f, 2.5f, 134f), "audit_airS_wall"),
                // 西车闸近距离
                (new Vector3(-70f, 3f, 190f), new Vector3(-55f, 3f, 190f), "audit_gateW_far"),
                (new Vector3(-62f, 2.5f, 190f), new Vector3(-50f, 2.5f, 190f), "audit_gateW_tunnel"),
                // 穹顶门
                (new Vector3(-4f, 2.5f, 158f), new Vector3(-4f, 3f, 168f), "audit_domeDoor"),
                // 穹顶内部各方向（看墙有没有洞）
                (new Vector3(-4f, 2.5f, 170f), new Vector3(-4f, 4f, 185f), "audit_dome_in_N"),
                (new Vector3(-4f, 2.5f, 185f), new Vector3(-20f, 4f, 185f), "audit_dome_in_W"),
                (new Vector3(-4f, 2.5f, 185f), new Vector3(10f, 4f, 185f), "audit_dome_in_E"),
                // 走廊连接处
                (new Vector3(-10f, 2.5f, 178f), new Vector3(-16f, 2.5f, 178f), "audit_corA"),
                (new Vector3(-4f, 2.5f, 190f), new Vector3(-4f, 2.5f, 198f), "audit_corB"),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos;
                cam.transform.LookAt(v.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{v.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);

            // 数据审计：打印所有独立 Unity 侧建筑件的坐标和尺寸
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 独立建筑组件坐标 ===");
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name.StartsWith("Airlock") || go.name.StartsWith("AirTunnel") ||
                    go.name.StartsWith("DomeDoor") || go.name.StartsWith("PathLight"))
                {
                    sb.AppendLine($"{go.name}: pos={go.transform.position}");
                    foreach (Transform child in go.transform)
                    {
                        var col = child.GetComponent<Collider>();
                        if (col is BoxCollider bc)
                            sb.AppendLine($"  {child.name}: center={bc.center} size={bc.size} world={child.position}");
                    }
                }
            }
            File.WriteAllText("Logs/audit_positions.txt", sb.ToString());
            Debug.Log($"[AuditBuilding] {views.Length} 张截图 + 坐标报告完成");
        }

        // 沿南气闸进入路径逐帧取证：出生点→外门→前室→内门→罩内
        public static void RenderEntry()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);

            // 坐标取证：基地根、全部自动门、玻璃罩包围盒
            var sb = new StringBuilder();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "MoonBase_Custom")
                    sb.AppendLine($"MoonBase_Custom root: {go.transform.position} scale {go.transform.localScale}");
                if (go.name.Contains("Airlock") || go.name == "DomeDoor")
                {
                    Bounds gb = default; bool first = true;
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        if (first) { gb = r.bounds; first = false; } else gb.Encapsulate(r.bounds);
                    }
                    sb.AppendLine($"{go.name}: root {go.transform.position} | 渲染范围 {gb}");
                }
            }
            var glass = GameObject.Find("Glass_Hemisphere");
            if (glass != null)
            {
                Bounds gb = default; bool first = true;
                foreach (var r in glass.GetComponentsInChildren<Renderer>())
                {
                    if (first) { gb = r.bounds; first = false; } else gb.Encapsulate(r.bounds);
                }
                sb.AppendLine($"Glass_Hemisphere: pos {glass.transform.position} | 范围 {gb}");
            }
            var south = GameObject.Find("AirS_Roof");
            if (south != null) sb.AppendLine($"南气闸前室(AirS_Roof): {south.transform.position}");
            var east = GameObject.Find("AirE_Roof");
            if (east != null) sb.AppendLine($"东车闸(AirE_Roof): {east.transform.position}");
            File.WriteAllText("Logs/entry_positions.txt", sb.ToString());
            Debug.Log("[取证] " + sb.ToString());

            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("Walk");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 70f;
            cam.targetTexture = rt;
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                (new Vector3(-12f, 1.7f, 124f), new Vector3(-12f, 1.9f, 134f), "entry_0"),
                (new Vector3(-12f, 1.7f, 131f), new Vector3(-12f, 1.9f, 141f), "entry_1"),
                (new Vector3(-12f, 1.7f, 136f), new Vector3(-12f, 1.9f, 146f), "entry_2"),
                (new Vector3(-12f, 1.7f, 142f), new Vector3(-12f, 1.9f, 152f), "entry_3"),
                (new Vector3(-12f, 1.7f, 148f), new Vector3(-6f, 2.2f, 158f), "entry_4"),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos;
                cam.transform.LookAt(v.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{v.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Debug.Log("[AssetInspector] 进入路径取证完成 Logs/entry_*.png");
        }

        // 不重建、只打开当前磁盘上的 S4 场景并渲染三张预览（现场取证用）
        public static void RenderS4()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("Shot");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 55f;
            cam.targetTexture = rt;
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                (new Vector3(-4f, 55f, 120f), new Vector3(-4f, 2f, 172f), "s4now_0"),
                (new Vector3(0f, 8f, 140f), new Vector3(-4f, 3f, 170f), "s4now_1"),
                (new Vector3(-4f, 2.2f, 148f), new Vector3(-4f, 1.5f, 162f), "s4now_2"),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos;
                cam.transform.LookAt(v.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{v.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Debug.Log("[AssetInspector] S4 现场渲染完成 Logs/s4now_*.png");
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
