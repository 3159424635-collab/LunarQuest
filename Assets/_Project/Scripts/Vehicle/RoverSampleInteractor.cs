using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 驾驶状态下的车载机械臂交互。车辆靠近未采集月壤点时显示 E 键提示，
    /// 无需下车即可完成采样；其他 NPC/展品交互仍要求步行进行。
    /// </summary>
    [RequireComponent(typeof(VehicleEnterExit))]
    public sealed class RoverSampleInteractor : MonoBehaviour
    {
        public float interactRange = 6.5f;

        VehicleEnterExit vehicle;
        InteractionPromptUI promptUI;
        SampleRock current;

        public SampleRock CurrentSample => current;

        void Awake()
        {
            vehicle = GetComponent<VehicleEnterExit>();
        }

        void Start()
        {
            promptUI = FindObjectOfType<InteractionPromptUI>();
        }

        void Update()
        {
            if (vehicle == null || !vehicle.IsInVehicle)
            {
                SetCurrent(null);
                return;
            }

            if (promptUI == null) promptUI = FindObjectOfType<InteractionPromptUI>();
            SampleRock nearest = null;
            float nearestSqr = interactRange * interactRange;
            foreach (SampleRock sample in FindObjectsOfType<SampleRock>())
            {
                float distanceSqr = (sample.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr >= nearestSqr) continue;
                nearest = sample;
                nearestSqr = distanceSqr;
            }
            SetCurrent(nearest);

            if (current != null && Input.GetKeyDown(KeyCode.E))
                TryInteract();
        }

        public bool TryInteract()
        {
            if (current == null || vehicle == null || !vehicle.IsInVehicle) return false;
            SampleRock sample = current;
            sample.Interact(gameObject);
            return true;
        }

        void OnDisable()
        {
            SetCurrent(null);
        }

        void SetCurrent(SampleRock next)
        {
            if (ReferenceEquals(current, next)) return;
            current = next;
            if (promptUI == null) return;
            promptUI.Show(current != null
                ? current.prompt.Replace("按 E 采集", "按 E 启动车载机械臂采集")
                : null);
        }
    }
}
