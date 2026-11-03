using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 白盒测试用的样本岩石：看向它按 E，控制台输出采集日志并消失。
    /// 用途：验证交互链路是否打通。正式版会升级为"按住 E 进度条采样"并接入任务系统。
    /// </summary>
    public class SampleRock : InteractableBase
    {
        void Reset() => prompt = "按 E 采集月壤样本";

        public override void Interact(GameObject who)
        {
            Debug.Log($"[采样] {who.name} 采集了一份月壤样本：{name}");
            Destroy(gameObject);
        }
    }
}
