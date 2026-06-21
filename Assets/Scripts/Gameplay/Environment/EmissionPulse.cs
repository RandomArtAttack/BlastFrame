using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Environment
{
    /// <summary>
    /// Pulses a renderer's EMISSION colour back and forth between a low and high intensity. Drives the
    /// emission through a MaterialPropertyBlock (per-object — never edits the shared material asset), so
    /// many objects sharing one material can each pulse independently and it composes with other MPB users
    /// (e.g. LavaFlowRandomizer) on the same renderer. Works on any shader with an HDR emission colour
    /// property (URP Lit's _EmissionColor, the RAA Lava Flow shaders, etc.).
    ///
    /// Self-contained: only touches its own (and optionally child) Renderers; no scene references.
    /// </summary>
    public class EmissionPulse : MonoBehaviour
    {
        [Tooltip("Shader emission-colour property to drive. URP Lit and the RAA Lava Flow shaders use '_EmissionColor'.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("The base emission value (HDR). The pulse scales THIS between Min and Max Intensity. " +
                 "Right-click the component header > 'Copy Emission From Material' to grab the material's current value.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color emissionColor = Color.white;

        [Tooltip("Emission multiplier at the LOW point of the bounce (e.g. 0.3 = dim to 30%).")]
        [SerializeField] private float minIntensity = 0.3f;

        [Tooltip("Emission multiplier at the HIGH point of the bounce (e.g. 1.5 = brighten to 150%).")]
        [SerializeField] private float maxIntensity = 1.5f;

        [Tooltip("Bounce speed (radians/sec of the underlying wave; ~6.3 ≈ one full bounce per second). Higher = faster.")]
        [SerializeField] private float pulseSpeed = 3f;

        [Tooltip("On = smooth sine bounce. Off = hard linear ping-pong (sharper turnarounds).")]
        [SerializeField] private bool smooth = true;

        [Tooltip("Also scan child Renderers (an object built from several meshes). Off = this object's Renderer only.")]
        [SerializeField] private bool includeChildren = false;

        [Tooltip("Stagger so several pulsers don't beat in unison: a 0..1 phase shift. Overridden per object " +
                 "when Randomize Phase is on.")]
        [Range(0f, 1f)]
        [SerializeField] private float phaseOffset = 0f;

        [Tooltip("Randomise the phase per object at Start (a row of pulsers looks staggered instead of synced).")]
        [SerializeField] private bool randomizePhase = false;

        private readonly struct Target
        {
            public readonly Renderer Renderer;
            public readonly int Index;
            public Target(Renderer renderer, int index) { Renderer = renderer; Index = index; }
        }

        private Target[] _targets;
        private MaterialPropertyBlock _mpb;
        private int _emissionId;
        private float _phaseRad;

        private void Awake()
        {
            _emissionId = Shader.PropertyToID(emissionProperty);
            _mpb = new MaterialPropertyBlock();
            _phaseRad = (randomizePhase ? Random.value : phaseOffset) * Mathf.PI * 2f;

            // Cache the renderer/sub-material targets once (sharedMaterials allocates — never in Update).
            var renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
            var list = new List<Target>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].HasProperty(_emissionId))
                        list.Add(new Target(r, i));
            }
            _targets = list.ToArray();

            if (_targets.Length == 0)
            {
                Debug.LogWarning($"[EmissionPulse] No renderer material with a '{emissionProperty}' property — disabling.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            float p = Time.time * pulseSpeed;
            // 0..1 bounce: smooth sine, or a hard linear ping-pong at the same rate.
            float k = smooth
                ? 0.5f + 0.5f * Mathf.Sin(p + _phaseRad)
                : Mathf.PingPong((p + _phaseRad) / Mathf.PI, 1f);

            Color pulsed = emissionColor * Mathf.Lerp(minIntensity, maxIntensity, k);

            for (int i = 0; i < _targets.Length; i++)
            {
                Target t = _targets[i];
                t.Renderer.GetPropertyBlock(_mpb, t.Index); // keep any other MPB props (e.g. LavaFlowRandomizer)
                _mpb.SetColor(_emissionId, pulsed);
                t.Renderer.SetPropertyBlock(_mpb, t.Index);
            }
        }

#if UNITY_EDITOR
        // Authoring convenience: pull the material's current emission into the serialized base colour.
        [ContextMenu("Copy Emission From Material")]
        private void CopyEmissionFromMaterial()
        {
            Renderer r = includeChildren ? GetComponentInChildren<Renderer>() : GetComponent<Renderer>();
            int id = Shader.PropertyToID(emissionProperty);
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(id))
            {
                Debug.LogWarning($"[EmissionPulse] No material with a '{emissionProperty}' property to copy from.", this);
                return;
            }
            emissionColor = r.sharedMaterial.GetColor(id);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
