using UnityEngine;
using UnityEngine.Rendering;

namespace LunarQuest.Module4
{
    // Presentation effects only; no player, quest, landing or AR logic is replaced.
    public sealed class Module4VisualDriver : MonoBehaviour
    {
        public Module4Sequence sequence;
        public ParticleSystem[] engines;
        public ParticleSystem smoke;
        public Transform parachute;
        public GameObject reentryGlow;
        public Transform earth;
        [Header("随镜头阶段切换的天空与环境光")]
        public Material phase1Sky;
        public Material phase2Sky;
        public Material phase3Sky;
        public Color phase1Ambient = new Color(.11f,.12f,.15f);
        public Color phase2Ambient = new Color(.18f,.2f,.23f);
        public Color phase3Ambient = new Color(.46f,.48f,.43f);
        public float phase2Start = 19;
        public float phase3Start = 32;
        public bool phase3Fog;
        public Color phase3FogColor = new Color(.66f,.73f,.76f);
        public float phase3FogStart = 160;
        public float phase3FogEnd = 760;
        public float engineOn = 18;
        public float engineOff = 60;
        int environmentPhase = -1;

        void OnEnable()
        {
            environmentPhase = -1;
        }

        void Update()
        {
            if (!sequence || !sequence.director) return;
            float t = (float)sequence.director.time;
            UpdateEnvironment(t);
            bool firing = t >= engineOn && t < engineOff;
            foreach (var engine in engines)
            {
                if (!engine) continue;
                var emission = engine.emission;
                emission.enabled = firing;
            }
            if (smoke)
            {
                var emission = smoke.emission;
                emission.enabled = firing && t < 36;
            }
            if (parachute) parachute.localScale = Vector3.one * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(32, 35, t));
            if (reentryGlow) reentryGlow.SetActive(t >= 21 && t < 32);
            if (earth) earth.localRotation = Quaternion.Euler(12, 90 + t * .25f, -8);
        }

        void UpdateEnvironment(float t)
        {
            if (!phase1Sky && !phase2Sky && !phase3Sky) return;
            int phase = t < phase2Start ? 0 : t < phase3Start ? 1 : 2;
            if (phase == environmentPhase) return;
            environmentPhase = phase;
            RenderSettings.skybox = phase == 0 ? phase1Sky : phase == 1 ? phase2Sky : phase3Sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = phase == 0 ? phase1Ambient : phase == 1 ? phase2Ambient : phase3Ambient;
            bool fog = phase == 2 && phase3Fog;
            RenderSettings.fog = fog;
            if (fog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = phase3FogColor;
                RenderSettings.fogStartDistance = phase3FogStart;
                RenderSettings.fogEndDistance = phase3FogEnd;
            }
        }
    }
}
