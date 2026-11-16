
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace LunarQuest.EditorTools
{
    public static class VisualAudit
    {
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
