
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace LunarQuest.EditorTools
{
    public static class S3Render
    {
        public static void Render()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/S3_Landing.unity", OpenSceneMode.Single);
            var rt = new RenderTexture(1280, 720, 24);
            var camGo = new GameObject("Shot");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 800f; cam.fieldOfView = 60f;
            cam.targetTexture = rt;
            (Vector3 pos, Vector3 look, string tag)[] views =
            {
                (new Vector3(0f, 40f, 60f), new Vector3(0f, 3f, 0f), "s3_terrain_far"),
                (new Vector3(0f, 6f, 12f), new Vector3(0f, 2.5f, -8f), "s3_terrain_close"),
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
            Debug.Log("[S3Render] done");
        }
    }
}
