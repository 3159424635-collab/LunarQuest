using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LunarQuest.EditorTools
{
    public static class LandingSceneBuilder
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.98f, 0.95f);
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.40f);

            var matMoon = LM("M_MoonBright", new Color(0.72f, 0.70f, 0.66f));
            var matCrater = LM("M_CraterDark", new Color(0.45f, 0.44f, 0.42f));
            var matRock = LM("M_MoonRock", new Color(0.55f, 0.53f, 0.50f));

            var moon = GameObject.CreatePrimitive(PrimitiveType.Plane);
            moon.name = "MoonSurface";
            moon.transform.position = new Vector3(0, -30f, 0);
            moon.transform.localScale = new Vector3(200f, 1f, 200f);
            moon.GetComponent<Renderer>().sharedMaterial = matMoon;

            for (int i = 0; i < 12; i++)
            {
                var cr = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cr.name = "Crater_" + i;
                cr.transform.position = new Vector3((i % 4 - 1.5f) * 30f, -30.5f, (i / 4 - 1f) * 30f);
                cr.transform.localScale = new Vector3(5f + Random.Range(0f, 8f), 1.5f, 5f + Random.Range(0f, 8f));
                cr.GetComponent<Renderer>().sharedMaterial = matCrater;
                Object.DestroyImmediate(cr.GetComponent<Collider>());
            }
            for (int i = 0; i < 20; i++)
            {
                var rk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rk.name = "Rock_" + i;
                rk.transform.position = new Vector3(Random.Range(-80f, 80f), -30f, Random.Range(-80f, 80f));
                rk.transform.localScale = new Vector3(Random.Range(1f, 4f), Random.Range(0.5f, 2f), Random.Range(1f, 4f));
                rk.GetComponent<Renderer>().sharedMaterial = matRock;
                Object.DestroyImmediate(rk.GetComponent<Collider>());
            }

            var tgt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tgt.name = "LandingTarget";
            tgt.transform.position = new Vector3(0, -29.8f, 0);
            tgt.transform.localScale = new Vector3(8f, 0.05f, 8f);
            var tm = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.2f, 0.9f, 0.3f, 0.6f) };
            tgt.GetComponent<Renderer>().sharedMaterial = tm;
            Object.DestroyImmediate(tgt.GetComponent<Collider>());

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cam.fieldOfView = 60f;
            cam.transform.rotation = Quaternion.Euler(70, 0, 0);
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();

            var mg = new GameObject("LandingMinigame").AddComponent<LandingMinigame>();
            mg.mainCam = cam;
            mg.startAltitude = 500f;

            var qm = new GameObject("QuestManager").AddComponent<QuestManager>();
            qm.database = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Content/Characters/dialogue.json");
            qm.startEvent = "orbit_complete";

            var sf = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/UI/Fonts/NotoSansCJKsc-Regular.otf");
            var sci = new GameObject("SciFiHUD").AddComponent<SciFiHUD>();
            sci.font = sf;

            const string sp = "Assets/_Project/Scenes/S3_Landing.unity";
            EditorSceneManager.SaveScene(scene, sp);
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == sp))
            {
                int i4 = list.FindIndex(s => s.path.Contains("S4"));
                if (i4 >= 0) list.Insert(i4, new EditorBuildSettingsScene(sp, true));
                else list.Add(new EditorBuildSettingsScene(sp, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        static Material LM(string n, Color c)
        {
            string p = "Assets/_Project/Art/Materials/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
            AssetDatabase.CreateAsset(m, p);
            return m;
        }
    }
}
