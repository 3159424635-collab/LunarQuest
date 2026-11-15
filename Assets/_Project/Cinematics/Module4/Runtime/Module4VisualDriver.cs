using UnityEngine;

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
        public float engineOn = 18;
        public float engineOff = 60;

        void Update()
        {
            if (!sequence || !sequence.director) return;
            float t = (float)sequence.director.time;
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
    }
}
