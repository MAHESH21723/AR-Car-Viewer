using UnityEngine;

namespace CarViewer
{
    /// <summary>Play / pause wrapper around the narration audio clip.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceTour : MonoBehaviour
    {
        public AudioClip narration;
        AudioSource source;
        bool paused;

        public bool IsPlaying => source != null && source.isPlaying;
        public float Length => narration != null ? narration.length : 0f;
        public float Time01 => Length > 0f ? Mathf.Clamp01(source.time / Length) : 0f;
        public float TimeSeconds => source != null ? source.time : 0f;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = narration;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        public void Toggle()
        {
            if (narration == null) return;
            if (source.isPlaying) { source.Pause(); paused = true; }
            else if (paused) { source.UnPause(); paused = false; }
            else source.Play();
        }

        public void Stop()
        {
            if (source == null) return;
            source.Stop();
            paused = false;
        }

        /// <summary>True while the tour is playing or paused part-way (captions stay visible).</summary>
        public bool IsActive => IsPlaying || paused;
    }
}
