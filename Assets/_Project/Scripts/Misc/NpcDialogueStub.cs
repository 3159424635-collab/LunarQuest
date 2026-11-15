using System.Collections;
using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// NPC 占位对话：按 E 播放说话动作，数秒后回到待机。
    /// 正式台词接入 dialogue.json 后由任务系统替换本组件。
    /// 挂载要求：NPC 物体（需已有 Animator，控制器含 idle/talk 状态）。
    /// </summary>
    public class NpcDialogueStub : InteractableBase
    {
        public string line = "……";       // 占位台词（调试 HUD 用）
        public Animator animator;
        public float talkSeconds = 3f;

        Coroutine playing;

        public override void Interact(GameObject who)
        {
            if (animator) animator.CrossFade("talk", 0.15f);
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(BackToIdle());
        }

        IEnumerator BackToIdle()
        {
            yield return new WaitForSeconds(talkSeconds);
            if (animator) animator.CrossFade("idle", 0.3f);
        }
    }
}
