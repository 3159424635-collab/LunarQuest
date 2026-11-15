using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 月球车交互入口：让玩家的 E 交互与提示系统能识别车辆，
    /// 按 E 与按 F 均可上下车。挂载要求：月球车物体（需已有 VehicleEnterExit）。
    /// </summary>
    public class VehicleDoor : InteractableBase
    {
        public VehicleEnterExit vehicle;

        public override void Interact(GameObject who)
        {
            if (vehicle) vehicle.Toggle();
        }
    }
}
