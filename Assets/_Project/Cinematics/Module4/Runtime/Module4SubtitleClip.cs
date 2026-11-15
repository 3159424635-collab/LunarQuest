using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LunarQuest.Module4
{
    [Serializable]
    public sealed class Module4SubtitleBehaviour : PlayableBehaviour
    {
        public string speaker = "地面飞控";
        public string text;
    }

    [Serializable]
    public sealed class Module4SubtitleClip : PlayableAsset, ITimelineClipAsset
    {
        public Module4SubtitleBehaviour content = new Module4SubtitleBehaviour();
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        { return ScriptPlayable<Module4SubtitleBehaviour>.Create(graph, content); }
    }
}
