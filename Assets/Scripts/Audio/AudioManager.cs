using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using BlastFrame.Core;
using BlastFrame.Core.Audio;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Audio
{
    /// <summary>
    /// One pooled-AudioSource manager for the whole project. Lives on a GameObject in the Core
    /// scene. Implements IAudioManager so other systems never hold a direct reference — they
    /// go through ServiceLocator.Get&lt;IAudioManager&gt;() or raise a GameEventSO that this manager
    /// subscribes to via its AudioCueBinding list.
    ///
    /// Pool is built from child AudioSources created at Awake — no runtime Instantiate during
    /// gameplay. Volume is routed through the AudioMixer (linear 0..1 → dB conversion).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IAudioManager
    {
        // -----------------------------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------------------------

        [Tooltip("The project AudioMixer asset. Must have exposed parameters matching " +
                 "AudioMixerParams constants: MasterVolume, MusicVolume, SfxVolume.")]
        [SerializeField] private AudioMixer mixer;

        [Tooltip("Mixer group routed for all SFX (including 3D world audio).")]
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Tooltip("Mixer group routed for music playback.")]
        [SerializeField] private AudioMixerGroup musicGroup;

        [Tooltip("Number of pooled AudioSource children created at startup for SFX. " +
                 "Tune upward if you hear audio dropout on busy frames (start at 12).")]
        [SerializeField] private int sfxSourceCount = 12;

        [Tooltip("GameEventSO → AudioCueSO bindings. When the event is raised the cue plays " +
                 "as a 2D sound (Play, not PlayAt). This keeps systems fully decoupled from " +
                 "AudioManager — they just raise their event.")]
        [SerializeField] private List<AudioCueBinding> eventBindings = new List<AudioCueBinding>();

        // -----------------------------------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------------------------------

        private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();
        private AudioSource _musicSource;

        // -----------------------------------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------------------------------

        private void Awake()
        {
            ServiceLocator.Register<IAudioManager>(this);
            BuildPool();
        }

        private void OnEnable()
        {
            foreach (var binding in eventBindings)
                binding.Subscribe(this);
        }

        private void OnDisable()
        {
            foreach (var binding in eventBindings)
                binding.Unsubscribe(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IAudioManager>(this);
        }

        // -----------------------------------------------------------------------------------------
        // IAudioManager
        // -----------------------------------------------------------------------------------------

        /// <summary>Play a 2D cue (spatialBlend ignored in favour of cue's own setting).</summary>
        public void Play(AudioCueSO cue)
        {
            if (!ValidateCue(cue)) return;
            var source = RentSource();
            if (source == null) return;
            ConfigureSource(source, cue, Vector3.zero, worldSpace: false);
            source.Play();
            ReturnAfterPlay(source, cue);
        }

        /// <summary>Play a cue positioned in world space (3D spatial).</summary>
        public void PlayAt(AudioCueSO cue, Vector3 position)
        {
            if (!ValidateCue(cue)) return;
            var source = RentSource();
            if (source == null) return;
            source.transform.position = position;
            ConfigureSource(source, cue, position, worldSpace: true);
            source.Play();
            ReturnAfterPlay(source, cue);
        }

        /// <summary>Play music on the dedicated music source (looping by default).</summary>
        public void PlayMusic(AudioCueSO cue)
        {
            if (!ValidateCue(cue)) return;
            _musicSource.clip = cue.PickClip();
            _musicSource.volume = cue.Volume;
            _musicSource.pitch = cue.PickPitch();
            _musicSource.loop = cue.Loop;
            _musicSource.outputAudioMixerGroup = musicGroup;
            _musicSource.Play();
        }

        /// <summary>
        /// Convert a linear 0..1 value to dB and apply to the named mixer parameter.
        /// Use the AudioMixerParams constants for paramName.
        /// </summary>
        public void SetVolume(string mixerParam, float linear01)
        {
            if (mixer == null)
            {
                Debug.LogWarning("[AudioManager] No AudioMixer assigned — cannot set volume.");
                return;
            }
            float db = Mathf.Log10(Mathf.Max(0.0001f, linear01)) * 20f;
            mixer.SetFloat(mixerParam, db);
        }

        // -----------------------------------------------------------------------------------------
        // Pool helpers
        // -----------------------------------------------------------------------------------------

        private void BuildPool()
        {
            // Music source — dedicated, not pooled.
            var musicGo = new GameObject("AudioSource_Music");
            musicGo.transform.SetParent(transform, false);
            _musicSource = musicGo.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.outputAudioMixerGroup = musicGroup;

            // SFX pool.
            for (int i = 0; i < sfxSourceCount; i++)
            {
                var sfxGo = new GameObject($"AudioSource_SFX_{i:00}");
                sfxGo.transform.SetParent(transform, false);
                var src = sfxGo.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.outputAudioMixerGroup = sfxGroup;
                _sfxPool.Enqueue(src);
            }
        }

        /// <summary>
        /// Returns a free pooled source, or null if pool is exhausted (logs a warning).
        /// </summary>
        private AudioSource RentSource()
        {
            if (_sfxPool.Count == 0)
            {
                Debug.LogWarning("[AudioManager] SFX pool exhausted — increase sfxSourceCount in the Inspector.");
                return null;
            }
            return _sfxPool.Dequeue();
        }

        private void ConfigureSource(AudioSource src, AudioCueSO cue, Vector3 position, bool worldSpace)
        {
            src.clip = cue.PickClip();
            src.volume = cue.Volume;
            src.pitch = cue.PickPitch();
            src.spatialBlend = worldSpace ? cue.SpatialBlend : 0f;
            src.loop = cue.Loop;
            src.outputAudioMixerGroup = sfxGroup;
            if (worldSpace) src.transform.position = position;
        }

        /// <summary>Coroutine-free return: poll in Update via a self-contained helper component.</summary>
        private void ReturnAfterPlay(AudioSource source, AudioCueSO cue)
        {
            if (cue.Loop) return; // looping sources are not auto-returned; caller must stop them

            var returner = source.gameObject.GetComponent<SourceReturner>();
            if (returner == null) returner = source.gameObject.AddComponent<SourceReturner>();
            returner.Init(source, _sfxPool);
        }

        private static bool ValidateCue(AudioCueSO cue)
        {
            if (cue == null) { Debug.LogWarning("[AudioManager] Null AudioCueSO passed — ignoring."); return false; }
            if (!cue.HasClips) { Debug.LogWarning($"[AudioManager] '{cue.name}' has no clips — ignoring."); return false; }
            return true;
        }
    }

    // =============================================================================================
    // AudioCueBinding — serializable event→cue pairing
    // =============================================================================================

    /// <summary>
    /// Pairs a GameEventSO with an AudioCueSO. When the event is raised AudioManager plays the
    /// cue as a 2D sound. Drag any GameEvent into the Inspector; no code wiring required.
    /// </summary>
    [System.Serializable]
    public sealed class AudioCueBinding
    {
        [Tooltip("The GameEventSO that triggers playback. Raise this event from any system — " +
                 "AudioManager handles the sound entirely.")]
        [SerializeField] private GameEventSO gameEvent;

        [Tooltip("The AudioCueSO to play when the event fires.")]
        [SerializeField] private AudioCueSO cue;

        private AudioManager _owner;
        private System.Action _handler;

        internal void Subscribe(AudioManager owner)
        {
            if (gameEvent == null || cue == null) return;
            _owner = owner;
            _handler = () => _owner.Play(cue);
            gameEvent.Register(_handler);
        }

        internal void Unsubscribe(AudioManager owner)
        {
            if (gameEvent == null || _handler == null) return;
            gameEvent.Unregister(_handler);
            _handler = null;
        }
    }

    // =============================================================================================
    // SourceReturner — lightweight return-to-pool watcher (no coroutines)
    // =============================================================================================

    /// <summary>
    /// Added dynamically to a pooled AudioSource child when it begins playing. Polls isPlaying
    /// each frame and returns the source to the pool when playback ends. Destroyed after return.
    /// One allocation per play call, but the component lives on a persistent child object so the
    /// allocation cost is a single AddComponent on a pooled object — acceptable outside FixedUpdate.
    /// </summary>
    internal sealed class SourceReturner : MonoBehaviour
    {
        private AudioSource _source;
        private Queue<AudioSource> _pool;

        internal void Init(AudioSource source, Queue<AudioSource> pool)
        {
            _source = source;
            _pool = pool;
        }

        private void Update()
        {
            if (_source == null || !_source.isPlaying)
            {
                if (_source != null)
                {
                    _source.clip = null;
                    _source.transform.localPosition = Vector3.zero;
                    _pool.Enqueue(_source);
                }
                Destroy(this);
            }
        }
    }
}
