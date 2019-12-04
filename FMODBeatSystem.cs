/* Reference: https://alessandrofama.com/tutorials/fmod-unity/beat-marker-system/ */

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

class FMODBeatSystem : MonoBehaviour
{
    [SerializeField] [FMODUnity.EventRef] string Event;
    FMOD.Studio.EventInstance instance;

    [StructLayout(LayoutKind.Sequential)] [System.Serializable]
    public class TimelineInfo
    {
        public float bpm = 0;
        public int currentBeat = 0;
        public int timelinePosition = 0;
        public float time { get { return timelinePosition / 1000f; } } // time in seconds;
        public string lastMarker = "";
        public float lastMarkerPosition = 0;
    }

    bool isPlaying = false;

    public TimelineInfo Timeline;
    GCHandle timelineHandle;

    FMOD.Studio.EVENT_CALLBACK beatCallback;

    public delegate void BeatAction();
    public event BeatAction OnBeat;
    public delegate void MarkerAction();
    public event MarkerAction OnMarker;

    private void Start()
    {
        instance = FMODUnity.RuntimeManager.CreateInstance(Event);
        AssignBeatEvent(instance);
    }

    private void Update()
    {
        instance.getTimelinePosition(out Timeline.timelinePosition);
    }

    public void Play()
    {
        if (!isPlaying)
        {
            instance.start();
            isPlaying = true;
        }

    }

    public void ToggleMusic(string param, float value)
    {
        instance.setParameterByName(param, value);
    }

    public void AssignBeatEvent(FMOD.Studio.EventInstance instance)
    {
        Timeline = new TimelineInfo();
        timelineHandle = GCHandle.Alloc(Timeline, GCHandleType.Pinned);
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);
        instance.setUserData(GCHandle.ToIntPtr(timelineHandle));
        instance.setCallback(beatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT | FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
    }

    public void StopAndClear(FMOD.Studio.EventInstance instance)
    {
        instance.setUserData(IntPtr.Zero);
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
        timelineHandle.Free();
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    FMOD.RESULT BeatEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, FMOD.Studio.EventInstance instance, IntPtr parameterPtr)
    {
        IntPtr timelineInfoPtr;
        FMOD.RESULT result = instance.getUserData(out timelineInfoPtr);
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError("Timeline Callback error: " + result);
        }
        else if (timelineInfoPtr != IntPtr.Zero)
        {
            GCHandle timelineHandle = GCHandle.FromIntPtr(timelineInfoPtr);
            TimelineInfo timelineInfo = (TimelineInfo)timelineHandle.Target;

            switch (type)
            {
                case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT:
                    {
                        var parameter = (FMOD.Studio.TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_BEAT_PROPERTIES));
                        timelineInfo.currentBeat = parameter.beat;
                        timelineInfo.bpm = parameter.tempo;
                        if (OnBeat != null)
                        {
                            OnBeat();
                        }
                    }
                    break;

                case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                    {
                        var parameter = (FMOD.Studio.TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_MARKER_PROPERTIES));
                        if (OnMarker != null)
                        {
                            OnMarker();
                        }
                        timelineInfo.lastMarker = parameter.name;
                        timelineInfo.lastMarkerPosition = parameter.position/1000f;
                    }
                    break;

                case FMOD.Studio.EVENT_CALLBACK_TYPE.STOPPED:
                    {
                        print("stopped");
                    }
                    break;
            }
        }
        return FMOD.RESULT.OK;
    }

    public void Stop(bool isImmediate)
    {
        if (instance.hasHandle())
        {
            StopAndClear(instance);
            if (isImmediate)
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
            else
            {
                instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
            isPlaying = false;
        }
    }

    private void OnApplicationQuit()
    {
        if (isPlaying)
        {
            Stop(true);
        }
    }

}