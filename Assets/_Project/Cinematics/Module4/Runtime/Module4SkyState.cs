using UnityEngine;
using UnityEngine.Rendering;

namespace LunarQuest.Module4
{
    public sealed class Module4SkyState : MonoBehaviour
    {
        public Material sky;
        public Color ambient = new Color(.15f,.18f,.24f);
        void OnEnable()
        {
            if(sky)RenderSettings.skybox=sky;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=ambient;
            RenderSettings.fog=false;
        }
    }
}
