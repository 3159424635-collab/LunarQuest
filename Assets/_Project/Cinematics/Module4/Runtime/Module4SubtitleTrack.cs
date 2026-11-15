using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LunarQuest.Module4
{
    public sealed class Module4SubtitleMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var view = playerData as Module4HudView;
            if (!view) return;
            for (int i = 0; i < playable.GetInputCount(); ++i)
            {
                if (playable.GetInputWeight(i) < .01f) continue;
                var item = (ScriptPlayable<Module4SubtitleBehaviour>)playable.GetInput(i);
                var text = item.GetBehaviour();
                view.SetSubtitle(text.speaker, text.text);
                return;
            }
            view.SetSubtitle("", "");
        }
    }

    [TrackColor(.10f, .72f, .83f)]
    [TrackClipType(typeof(Module4SubtitleClip))]
    [TrackBindingType(typeof(Module4HudView))]
    public sealed class Module4SubtitleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<Module4SubtitleMixer>.Create(graph, inputCount);
        }
    }
}
