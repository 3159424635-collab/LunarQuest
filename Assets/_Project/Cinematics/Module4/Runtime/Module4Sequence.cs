using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LunarQuest.Module4
{
    public sealed class Module4Sequence : MonoBehaviour
    {
        public string sequenceId;
        public string title;
        public float duration;
        public PlayableDirector director;
        public SignalReceiver signalReceiver;
        public Module4HudView hud;
        public UnityEvent onCompleted = new UnityEvent();
        public int completionCount { get; private set; }
        bool sent;

        public void Bind(Camera camera, Module4HudView view)
        {
            hud = view;
            var brain = camera.GetComponent<Cinemachine.CinemachineBrain>();
            var timeline = director.playableAsset as TimelineAsset;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is CinemachineTrack) director.SetGenericBinding(track, brain);
                if (track is Module4SubtitleTrack) director.SetGenericBinding(track, hud);
            }
        }

        public void Play()
        {
            sent = false;
            director.time = 0;
            director.Play();
        }

        public void Seek(double time)
        {
            director.time = Math.Max(0, Math.Min(time, duration - .001));
            director.Evaluate();
        }

        // Bind this method to the native Timeline SignalReceiver.
        public void ReceiveEndSignal()
        {
            if (sent) return;
            sent = true;
            completionCount++;
            Debug.Log("M4_SIGNAL_COMPLETED:" + sequenceId);
            onCompleted.Invoke();
        }
    }
}
