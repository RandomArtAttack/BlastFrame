using UnityEngine;

namespace BlastFrame.Core.Audio
{
    /// <summary>
    /// All game audio is driven by these cues — never raw AudioClips in gameplay code. Multiple
    /// clips = random selection on play (shots, hits, footsteps). Routed through the mixer by
    /// the AudioManager pool; no AudioSource components live on player/enemy prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Blast Frame/Audio/Audio Cue")]
    public class AudioCueSO : ScriptableObject
    {
        [Tooltip("Clips to choose from. More than one = a random clip is picked each play (variation).")]
        [SerializeField] private AudioClip[] clips;

        [Tooltip("Linear volume 0..1 applied to the pooled AudioSource for this cue.")]
        [Range(0f, 1f)][SerializeField] private float volume = 1f;

        [Tooltip("Minimum random pitch. 1 = unchanged.")]
        [Range(0.1f, 3f)][SerializeField] private float pitchMin = 1f;

        [Tooltip("Maximum random pitch. 1 = unchanged.")]
        [Range(0.1f, 3f)][SerializeField] private float pitchMax = 1f;

        [Tooltip("0 = 2D, 1 = fully 3D positional. Use 1 for world SFX, 0 for UI.")]
        [Range(0f, 1f)][SerializeField] private float spatialBlend = 1f;

        [Tooltip("Loop the clip (music / ambient).")]
        [SerializeField] private bool loop;

        public bool HasClips => clips != null && clips.Length > 0;
        public AudioClip PickClip() => HasClips ? clips[Random.Range(0, clips.Length)] : null;
        public float Volume => volume;
        public float PickPitch() => Random.Range(pitchMin, pitchMax);
        public float SpatialBlend => spatialBlend;
        public bool Loop => loop;
    }
}
