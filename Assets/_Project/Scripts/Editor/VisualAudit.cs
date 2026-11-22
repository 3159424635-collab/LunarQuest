
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace LunarQuest.EditorTools
{
    public static class VisualAudit
    {
        public static void AuditTerrainComparison()
        {
            RenderTerrain("Assets/_Project/Scenes/S3_Landing.unity", "Logs/s3_terrain_overview.png");
            RenderTerrain("Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", "Logs/s4_terrain_overview.png");
            Debug.Log("[VisualAudit] S3/S4 月面整幅贴图对比完成");
        }

        static void RenderTerrain(string scenePath, string outputPath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Terrain terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[VisualAudit] 场景缺少 Terrain: " + scenePath);
                return;
            }

            var rt = new RenderTexture(1200, 1200, 24);
            var camGo = new GameObject("TerrainAuditCam");
            var cam = camGo.AddComponent<Camera>();
            Vector3 terrainCenter = terrain.transform.position + terrain.terrainData.size * 0.5f;
            cam.orthographic = true;
            cam.orthographicSize = terrain.terrainData.size.x * 0.51f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.018f, 0.022f);
            cam.transform.position = new Vector3(terrainCenter.x, 520f, terrainCenter.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, 1200f, 1200f), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Logs");
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }

        public static void AuditThirdPerson()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var player = Object.FindObjectOfType<FirstPersonController>();
            if (player == null || player.thirdPersonBody == null)
            {
                Debug.LogError("[VisualAudit] 第三人称角色缺失");
                return;
            }

            player.thirdPersonBody.SetActive(true);
            player.thirdPersonBody.transform.localRotation = Quaternion.identity;
            var animator = player.thirdPersonBody.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Play("walk", 0, 0.35f);
                animator.Update(0f);
            }

            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("ThirdPersonAuditCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.targetTexture = rt;
            Vector3 focus = player.transform.position + Vector3.up * player.thirdPersonFocusHeight;
            cam.transform.position = focus + player.transform.TransformDirection(
                new Vector3(player.thirdPersonShoulderOffset, player.thirdPersonHeight, -player.thirdPersonDistance));
            cam.transform.LookAt(focus);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            tex.Apply();
            File.WriteAllBytes("Logs/s4_third_person_walk.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Debug.Log("[VisualAudit] 第三人称全身构图与 walk 姿态审计完成");
        }

        public static void AuditS4Repairs()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var rt = new RenderTexture(1600, 900, 24);
            var camGo = new GameObject("RepairAuditCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 50f;
            cam.targetTexture = rt;
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                (new Vector3(3f, 4.5f, 107f), new Vector3(12f, 2.2f, 118f), "s4repair_rover"),
                (new Vector3(-8.6f, 4.2f, 177.5f), new Vector3(-8.6f, 3.6f, 182.6f), "s4repair_exhibit"),
                (new Vector3(7f, 3.2f, 173.5f), new Vector3(7f, 2.2f, 177f), "s4repair_chenyue"),
                (new Vector3(8f, 4f, 166f), new Vector3(0f, 2.2f, 178f), "s4repair_npcs"),
            };
            foreach (var view in views)
            {
                cam.transform.position = view.pos;
                cam.transform.LookAt(view.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{view.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Debug.Log("[VisualAudit] S4 修复项四视角审计完成");
        }

        public static void Audit()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S4_MoonSurface_Whitebox.unity", OpenSceneMode.Single);
            var rt = new RenderTexture(1920, 1080, 24);
            var camGo = new GameObject("AuditCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 60f;
            cam.targetTexture = rt;

            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                // 玻璃罩近距离各角度（找玻璃缺口）
                (new Vector3(4f, 5f, 130f), new Vector3(4f, 8f, 160f), "va_glass_S_close"),
                (new Vector3(-40f, 5f, 160f), new Vector3(0f, 10f, 180f), "va_glass_SW"),
                (new Vector3(-40f, 5f, 220f), new Vector3(0f, 10f, 200f), "va_glass_NW"),
                (new Vector3(30f, 5f, 220f), new Vector3(0f, 10f, 200f), "va_glass_NE"),
                (new Vector3(30f, 5f, 160f), new Vector3(0f, 10f, 180f), "va_glass_SE"),
                (new Vector3(4f, 20f, 130f), new Vector3(4f, 15f, 170f), "va_glass_S_high"),
                // 穹顶建筑各角度（找穹顶缺口）
                (new Vector3(-4f, 3f, 158f), new Vector3(-4f, 5f, 172f), "va_dome_S"),
                (new Vector3(-20f, 3f, 178f), new Vector3(-8f, 5f, 178f), "va_dome_W"),
                (new Vector3(-4f, 3f, 195f), new Vector3(-4f, 5f, 180f), "va_dome_N"),
                (new Vector3(10f, 3f, 178f), new Vector3(-2f, 5f, 178f), "va_dome_E"),
                (new Vector3(-4f, 12f, 165f), new Vector3(-4f, 5f, 178f), "va_dome_top"),
                // 罩内从里往外看（找从里面能看到外面的缺口）
                (new Vector3(-4f, 3f, 178f), new Vector3(-40f, 5f, 160f), "va_inside_SW"),
                (new Vector3(-4f, 3f, 178f), new Vector3(30f, 5f, 160f), "va_inside_SE"),
                (new Vector3(0f, 8f, 185f), new Vector3(50f, 3f, 130f), "va_inside_outS"),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos;
                cam.transform.LookAt(v.look);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{v.tag}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(camGo);
            Debug.Log($"[VisualAudit] {views.Length} 张审计截图完成");
        }
    }
}
