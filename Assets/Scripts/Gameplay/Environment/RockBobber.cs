using UnityEngine;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Platforms;

namespace BlastFrame.Gameplay.Environment
{
    /// <summary>
    /// Makes a rock (or any object) ride the VISUAL surface of the lava beneath it. The lava's
    /// height is displaced in the vertex shader (GPU) — it never updates the CPU mesh or the lava
    /// collider — so the only way to seat an object on the moving surface is to mirror the shader's
    /// displacement function on the CPU. At Rebind it fires one downward raycast to find the lava and
    /// captures the REAL mesh UV (RaycastHit.textureCoord) and undisplaced surface height
    /// (hit.point.y) directly under itself; each FixedUpdate it samples the SAME scrolling noise the
    /// GPU samples AT THAT UV and seats on baseHeight + displacement.
    ///
    /// The bob is driven ONLY by the V.4 lava river ("RAA Materials/RAA Lava Flow V.4") turbulence.
    /// There is NO sine fallback and NO "rest in place over a ripple": if the V.4 river params can't be
    /// resolved the rock simply HOLDS STILL and logs why — so a broken setup is obvious while testing
    /// instead of being masked by a fake bob. Assign River Material to source the churn from the river
    /// even when the rock sits over another lava piece; leave it null to read the V.4 surface beneath.
    ///
    /// It is a KINEMATIC-RIGIDBODY MOVING PLATFORM: it moves its own (interpolated) kinematic
    /// Rigidbody via MovePosition/MoveRotation so the player's motor collides with it and — because it
    /// implements IRidePlatform — the PlatformRiderModule carries a grounded player up/down with the
    /// bob, exactly like a MovingPlatform. The LAVA collider is never modified (only this rock moves).
    ///
    /// MOVING + BOBBING: if a sibling MovingPlatform is present, RockBobber becomes the SINGLE position
    /// owner — it takes the platform's path XZ (MovingPlatform.NextPosition), RE-SAMPLES the lava under
    /// that moving XZ every tick (so the bob tracks the surface it travels over, not a fixed spot), and
    /// MovePosition's the composed (pathXZ, easedBobY). The horizontal carry comes from MovingPlatform's
    /// own ride velocity; this component's is vertical-only — the rider sums both. (For this combo, the
    /// path waypoints' Y is irrelevant: the lava surface sets the height.)
    ///
    /// V.4 needs Read/Write on BOTH the noise texture AND the lava mesh (textureCoord needs a readable,
    /// non-convex MeshCollider). Run Implement Fix 046 for the lava meshes; Rebind warns if not readable.
    ///
    /// Self-contained: the only Inspector reference allowed is the river Material (a UnityEngine asset);
    /// the lava surface is found at runtime by layer via raycast. Set Lava Mask to the "Lava" layer.
    /// </summary>
    // Runs BEFORE PlayerController (default order 0): the bob must move and publish this tick's ride
    // velocity FIRST, so the PlatformRiderModule carries the player by the SAME tick's rise. If it ran
    // after, the rider would read last tick's (stale) velocity — at the bob's trough that means the rock
    // starts rising into the player's feet before they're lifted, the ground SphereCast then starts
    // inside the rock and fails, the player drops to "not grounded", and the carry collapses. That is
    // exactly the "upward motion stops it working" bug. Order-before makes the up-carry penetration-free.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody))]
    public class RockBobber : MonoBehaviour, IRidePlatform
    {
        [Header("Lava search (runtime, self-contained)")]
        [Tooltip("Layers the downward raycast tests to find the lava surface under this rock. Set this " +
                 "to the 'Lava' layer. If left at Nothing, the component auto-uses the 'Lava' layer by name.")]
        [SerializeField] private LayerMask lavaMask = 0;

        [Tooltip("How far ABOVE this object the lava-finding ray starts, in metres. Keep > the bob amplitude " +
                 "so the ray always begins above the surface.")]
        [SerializeField] private FloatReference rayStartHeight = new FloatReference(3f);

        [Tooltip("Max length of the downward lava-finding ray, in metres.")]
        [SerializeField] private FloatReference rayMaxDistance = new FloatReference(20f);

        [Header("Seating")]
        [Tooltip("Vertical offset added after sampling the surface, in metres. Negative sinks the rock " +
                 "into the lava (half-submerged look); positive lifts it to ride on top.")]
        [SerializeField] private FloatReference heightOffset = new FloatReference(0f);

        [Tooltip("Multiplier on the sampled wave height. 1 = exactly the lava surface. Lower it so a heavy " +
                 "rock only partly follows the churn; raise it to exaggerate the bob.")]
        [SerializeField] private FloatReference displacementMultiplier = new FloatReference(1f);

        [Tooltip("THE feel knob: how FAST the rock chases the lava surface. Higher = snappier/lighter; lower = " +
                 "slower/heavier, so on big fast waves it visibly lags then settles. 0 = frozen (won't follow); " +
                 "~3 floaty, ~8 heavy, ~15 snappy, ~25 near-instant. Framerate-independent. The player riding it " +
                 "is carried by the rock's ACTUAL eased motion, so the carry stays in sync.")]
        [SerializeField] private FloatReference followSpeed = new FloatReference(8f);

        [Header("Surface tilt (optional)")]
        [Tooltip("If on, the rock tilts to match the local slope of the wave (finite-difference normal). " +
                 "Off = stays upright and only bobs vertically.")]
        [SerializeField] private bool alignToSurface = false;

        [Tooltip("How strongly the rock leans into the surface normal (0 = upright, 1 = full lean). Only used " +
                 "when Align To Surface is on.")]
        [SerializeField] private FloatReference alignStrength = new FloatReference(0.5f);

        [Header("V.4 river (the ONLY thing that drives the bob)")]
        [Tooltip("OPTIONAL. The RAA Lava Flow V.4 'Lava River' material. When set, the bob's noise " +
                 "(strength/scroll/texture) is read from THIS material — so the rock bobs from the river " +
                 "even if it sits over another lava piece (e.g. a ripple). Leave null to read the V.4 " +
                 "surface directly beneath the rock. It is NOT calculated from anything but the V.4 river.")]
        [SerializeField] private Material riverMaterial;

        [Tooltip("Tick if the lava Noise texture is imported as sRGB (the default). In a Linear-space " +
                 "project the GPU samples an sRGB texture as linear, so the CPU bob must linearise too to " +
                 "match the surface exactly. Untick only if you set the noise texture's import to Linear.")]
        [SerializeField] private bool noiseIsSRGB = true;

        // ----- Cached state (resolved once at Rebind) -----------------------------------------
        private Rigidbody _rb;              // kinematic body moved each FixedUpdate (carries the player)
        private Transform _lava;            // the lava renderer transform under the rock (object->world Y scale)
        private bool _v4Ready;              // true only when the V.4 river bob is fully resolved
        private float _baseX, _baseZ;       // this object's fixed world XZ (stationary mode)
        private float _prevY;               // last ACTUAL (eased) world Y, for the ride velocity
        private float _prevPathX, _prevPathZ; // last tick's XZ, to detect a path teleport (ReuseLoopTeleport)
        private Vector3 _rideVelocity;      // world velocity handed to the rider this tick
        private int _lavaMask;              // resolved lava layer mask (cached for per-tick re-sampling)
        private float _rayLen;              // resolved ray length (cached for per-tick re-sampling)
        private MovingPlatform _externalMover; // sibling that owns the XZ path (null = stationary rock)

        // The REAL mesh sample under the rock, captured once at Rebind. The rock's XZ is fixed, so the
        // mesh UV and undisplaced surface height never change — only the noise scroll moves in time.
        private Vector2 _uvC;               // mesh UV0 under the rock centre (RaycastHit.textureCoord)
        private float _surfaceBaseY;        // undisplaced lava surface Y under the rock (hit.point.y)
        private Vector2 _uvNX, _uvPX, _uvNZ, _uvPZ;       // neighbour UVs at -x/+x/-z/+z (for tilt)
        private float _baseNX, _basePX, _baseNZ, _basePZ; // neighbour undisplaced heights
        private bool _alignSampled;         // all four tilt neighbours hit the lava

        // V.4 noise params (mirror of RAA_LavaFlow_V4 CBUFFER), read from the river material.
        private Texture2D _noiseTex;
        private Vector2 _noiseScale, _noiseOffset, _noiseMove;
        private float _turbulenceStrength;
        private bool _linearizeNoise;       // match the GPU's sRGB->linear sample in a linear project

        private const float AlignEpsilon = 0.25f; // world-space half-step for the tilt slope
        private const float TeleportJumpSqr = 4f;  // (2m)^2 XZ jump in one tick = a path warp, not travel

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Awake()
        {
            // Kinematic moving-platform body: interpolated so the bob stays smooth, no gravity so it
            // never falls, kinematic so the player can't shove it. AddComponent covers runtime builds
            // (RequireComponent only auto-adds in the editor).
            if (!TryGetComponent(out _rb)) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Moving + bobbing: if a MovingPlatform shares this object, take over the final move (it would
            // otherwise fight us for MovePosition) and let it just compute the path. We own the body.
            if (TryGetComponent(out _externalMover))
                _externalMover.SetExternallyDriven(true);
        }

        private void Start()
        {
            _baseX = transform.position.x;
            _baseZ = transform.position.z;
            _prevY = transform.position.y;
            _prevPathX = _baseX;
            _prevPathZ = _baseZ;
            Rebind();
        }

        private void FixedUpdate()
        {
            // No fallback, no static: if the V.4 river isn't resolved the rock holds still (Rebind has
            // already logged why). Only the river bobs it.
            if (!_v4Ready)
            {
                _rideVelocity = Vector3.zero;
                return;
            }

            float t = Time.timeSinceLevelLoad;
            float dt = Time.fixedDeltaTime;

            // XZ: a sibling MovingPlatform owns the horizontal path; otherwise the rock is fixed. A
            // travelling rock must RE-SAMPLE the lava at its new XZ each tick (the surface under it
            // changes); a fixed rock reuses the Rebind sample (no per-tick raycast).
            float curX, curZ;
            if (_externalMover != null && _externalMover.isActiveAndEnabled)
            {
                Vector3 p = _externalMover.NextPosition;
                curX = p.x; curZ = p.z;
                ResampleSurfaceAt(curX, curZ);                       // updates _uvC/_surfaceBaseY/_lava (holds last on miss)
                if (alignToSurface) ResampleTiltNeighbours(curX, curZ);
            }
            else
            {
                curX = _baseX; curZ = _baseZ;
            }

            // TARGET = the REAL surface: undisplaced height + the GPU's own vertex displacement sampled at
            // the actual mesh UV under the rock. Object-space disp -> world via the lava's Y scale.
            float disp = SampleNoiseDisp(_uvC, t) * displacementMultiplier.Value;
            float targetY = _surfaceBaseY + disp * _lava.lossyScale.y + heightOffset.Value;

            // A large XZ jump in one tick = a path TELEPORT (MovingPlatform ReuseLoopTeleport warp). Snap to
            // the new surface instead of easing across the warp, and report no ride velocity (the platform
            // vanished from under any rider — leave them behind, don't fling them by the warp delta).
            float dx = curX - _prevPathX, dz = curZ - _prevPathZ;
            bool teleported = (dx * dx + dz * dz) > TeleportJumpSqr;

            float newY;
            if (teleported)
            {
                newY = targetY;
                _rideVelocity = Vector3.zero;
            }
            else
            {
                // EASE the Y toward the target, so a heavy rock lags big/fast waves (heft). Framerate-
                // independent exponential smoothing (followSpeed = rate, higher = snappier, no overshoot).
                // XZ follows the path exactly — only the bob has weight. Ride velocity is VERTICAL only;
                // horizontal carry comes from MovingPlatform (PlatformRiderModule sums all IRidePlatform).
                float blend = 1f - Mathf.Exp(-Mathf.Max(0f, followSpeed.Value) * dt);
                newY = Mathf.Lerp(_prevY, targetY, blend);
                _rideVelocity = new Vector3(0f, (newY - _prevY) / dt, 0f);
            }
            _prevY = newY;
            _prevPathX = curX;
            _prevPathZ = curZ;

            _rb.MovePosition(new Vector3(curX, newY, curZ));

            if (alignToSurface) AlignToSurfaceNormal(t);
        }

        // IRidePlatform — the grounded rider matches this (rising) velocity; ground-snap follows it down.
        public Vector3 SampleRideVelocity(Vector3 riderPosition) => _rideVelocity;

        // ----- Setup ---------------------------------------------------------------------------

        /// <summary>Re-find the lava under this object and re-read the V.4 river params. Call after moving
        /// the rock in the editor or changing the river material.</summary>
        [ContextMenu("Rebind To Lava Below")]
        public void Rebind()
        {
            _v4Ready = false;
            _lava = null;
            _alignSampled = false;
            _baseX = transform.position.x; // keep self-sufficient for the editor ContextMenu (pre-Start)
            _baseZ = transform.position.z;

            _lavaMask = lavaMask.value != 0 ? lavaMask.value : LayerMask.GetMask("Lava");
            _rayLen = rayMaxDistance.Value + rayStartHeight.Value;
            Vector3 origin = transform.position + Vector3.up * rayStartHeight.Value;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    _rayLen, _lavaMask, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning($"[RockBobber] No Lava-layer collider within {_rayLen:0.#}m below this rock " +
                    $"(rock Y={transform.position.y:0.##}). {DescribeWhatsBelow(origin)} Rock will NOT bob.", this);
                return;
            }

            var renderer = hit.collider.GetComponentInParent<Renderer>()
                           ?? hit.collider.GetComponentInChildren<Renderer>();
            _lava = renderer != null ? renderer.transform : null;
            if (_lava == null)
            {
                Debug.LogWarning("[RockBobber] Lava hit has no Renderer (can't read its object->world scale). " +
                    "Rock will NOT bob.", this);
                return;
            }

            // textureCoord needs the hit mesh Read/Write Enabled AND a non-convex MeshCollider — it
            // silently returns (0,0) otherwise, which would track the wrong point. Warn loudly.
            if (hit.collider is MeshCollider mc && mc.sharedMesh != null && !mc.sharedMesh.isReadable)
                Debug.LogWarning($"[RockBobber] Lava mesh '{mc.sharedMesh.name}' is NOT Read/Write Enabled — " +
                    "hit.textureCoord returns (0,0), so the bob can't follow the surface. Run " +
                    "Tools > Blast Frame > Implement Fix > 046 to enable it.", this);

            // Capture the REAL mesh UV + undisplaced surface height directly under the rock.
            _uvC = hit.textureCoord;
            _surfaceBaseY = hit.point.y;

            // The churn is sourced ONLY from the V.4 river: the override material if set, else the surface
            // beneath (which must then be the V.4 river). No ripple/V.3/other material drives the bob.
            Material src = riverMaterial != null ? riverMaterial : (renderer != null ? renderer.sharedMaterial : null);
            if (!CacheV4(src))
            {
                string found = renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    ? $"'{renderer.sharedMaterial.shader.name}' on '{hit.collider.name}'" : "(none)";
                Debug.LogWarning($"[RockBobber] Found lava below ({found}) but no usable V.4 river params — assign " +
                    "'River Material' (the RAA Lava Flow V.4 'Lava River' material), or sit the rock on the V.4 river " +
                    "surface, and make sure its noise texture is Read/Write Enabled. Rock will NOT bob.", this);
                return;
            }

            _v4Ready = true;

            // Tilt neighbours for the stationary case: sample the four cardinal points once. A travelling
            // rock refreshes these each tick via ResampleTiltNeighbours instead.
            if (_externalMover == null) ResampleTiltNeighbours(_baseX, _baseZ);
        }

        /// <summary>Reads the V.4 turbulence params from the river material. Returns false (rock holds)
        /// if the material isn't a V.4 river, has no strength, or its noise isn't CPU-readable.</summary>
        private bool CacheV4(Material m)
        {
            if (m == null || !m.HasProperty("_NoiseMap") || !m.HasProperty("_TurbulenceStrength"))
                return false; // not a V.4 river material

            _noiseTex = m.GetTexture("_NoiseMap") as Texture2D;
            _noiseScale = m.GetTextureScale("_NoiseMap");
            _noiseOffset = m.GetTextureOffset("_NoiseMap");
            Vector4 move = m.HasProperty("_NoiseMove") ? m.GetVector("_NoiseMove") : Vector4.zero;
            _noiseMove = new Vector2(move.x, move.y);
            _turbulenceStrength = m.GetFloat("_TurbulenceStrength");

            bool readable = _noiseTex != null && _noiseTex.isReadable;
            if (_noiseTex != null && !readable)
                Debug.LogWarning($"[RockBobber] '{_noiseTex.name}' (lava noise) is not Read/Write Enabled — " +
                    "can't sample it on the CPU. Enable it in the texture import settings.", this);

            // The GPU sampler converts an sRGB-imported texture to linear in a linear-space project;
            // mirror that on the CPU (GetPixelBilinear returns the raw gamma value) so heights match.
            _linearizeNoise = noiseIsSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;

            // Need a readable noise texture and a non-zero strength to mirror the turbulence.
            return readable && Mathf.Abs(_turbulenceStrength) > Mathf.Epsilon;
        }

        // ----- Displacement sampler (CPU mirror of RAA_LavaFlow_V4 vert) -----------------------

        /// <summary>Object-space vertical displacement the GPU applies at a given mesh UV: the scrolling
        /// noise sample * strength. noiseUV mirrors TRANSFORM_TEX(uv,_NoiseMap) + _NoiseMove * _Time.y
        /// (URP's _Time.y == Time.timeSinceLevelLoad). sRGB noise is linearised to match the GPU.</summary>
        private float SampleNoiseDisp(Vector2 meshUV, float t)
        {
            float u = meshUV.x * _noiseScale.x + _noiseOffset.x + _noiseMove.x * t;
            float v = meshUV.y * _noiseScale.y + _noiseOffset.y + _noiseMove.y * t;
            float n = _noiseTex.GetPixelBilinear(u, v).r; // wrap mode handled by the texture
            if (_linearizeNoise) n = Mathf.GammaToLinearSpace(n);
            return n * _turbulenceStrength;
        }

        // ----- Helpers -------------------------------------------------------------------------

        /// <summary>Diagnostic only: a maskless downward probe so the "no lava" warning can say what is
        /// actually under the rock — collider, layer, distance, shader — to pinpoint the cause (river has
        /// no collider / wrong layer / rock too high / over a gap). Skips the rock's own colliders.</summary>
        private string DescribeWhatsBelow(Vector3 origin)
        {
            const float probe = 500f;
            var hits = Physics.RaycastAll(origin, Vector3.down, probe, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue; // ignore this rock's own collider
                string layer = LayerMask.LayerToName(h.collider.gameObject.layer);
                if (string.IsNullOrEmpty(layer)) layer = "#" + h.collider.gameObject.layer;
                var rend = h.collider.GetComponentInParent<Renderer>();
                string shader = rend != null && rend.sharedMaterial != null && rend.sharedMaterial.shader != null
                    ? rend.sharedMaterial.shader.name : "(no renderer/material)";
                return $"Nearest surface below is '{h.collider.name}' on layer '{layer}' at {h.distance:0.##}m, " +
                       $"shader '{shader}' — if that IS the river, it needs a collider on the Lava layer; " +
                       "if it's a different surface, the rock isn't actually over the river.";
            }
            return $"Nothing (besides itself) is below within {probe:0}m — the rock is over a gap or far above the level.";
        }

        /// <summary>One downward ray to grab a point's mesh UV + undisplaced height; defaults to the
        /// centre sample (_uvC/_surfaceBaseY) if it misses. Used to set up the tilt neighbours.</summary>
        private bool SampleAt(float worldX, float worldZ, out Vector2 uv, out float baseY)
        {
            uv = _uvC;
            baseY = _surfaceBaseY;
            Vector3 o = new Vector3(worldX, _prevY + rayStartHeight.Value, worldZ);
            if (Physics.Raycast(o, Vector3.down, out RaycastHit h, _rayLen, _lavaMask, QueryTriggerInteraction.Ignore))
            {
                uv = h.textureCoord;
                baseY = h.point.y;
                return true;
            }
            return false;
        }

        /// <summary>Per-tick re-sample for a TRAVELLING rock: raycast the lava at the current path XZ and
        /// refresh the centre mesh UV, surface height, and lava transform. Holds the last good values if
        /// the ray misses (a brief gap in the river) so the rock doesn't pop.</summary>
        private void ResampleSurfaceAt(float worldX, float worldZ)
        {
            Vector3 o = new Vector3(worldX, _prevY + rayStartHeight.Value, worldZ);
            if (Physics.Raycast(o, Vector3.down, out RaycastHit h, _rayLen, _lavaMask, QueryTriggerInteraction.Ignore))
            {
                _uvC = h.textureCoord;
                _surfaceBaseY = h.point.y;
                var rend = h.collider.GetComponentInParent<Renderer>();
                if (rend != null) _lava = rend.transform; // keep the Y-scale source current across pieces
            }
        }

        /// <summary>Refresh the four tilt-neighbour samples around a centre XZ. Each holds the centre
        /// sample if its ray misses, reading flat in that axis.</summary>
        private void ResampleTiltNeighbours(float cx, float cz)
        {
            _alignSampled =
                SampleAt(cx - AlignEpsilon, cz, out _uvNX, out _baseNX) &
                SampleAt(cx + AlignEpsilon, cz, out _uvPX, out _basePX) &
                SampleAt(cx, cz - AlignEpsilon, out _uvNZ, out _baseNZ) &
                SampleAt(cx, cz + AlignEpsilon, out _uvPZ, out _basePZ);
        }

        /// <summary>Tilt to the real surface normal, built from the four cached neighbour samples
        /// (world height = undisplaced height + current noise displacement). Exact gradient, no UV guess.</summary>
        private void AlignToSurfaceNormal(float t)
        {
            if (!_alignSampled) return;
            float sy = _lava.lossyScale.y;
            float m = displacementMultiplier.Value;
            float hNX = _baseNX + SampleNoiseDisp(_uvNX, t) * m * sy;
            float hPX = _basePX + SampleNoiseDisp(_uvPX, t) * m * sy;
            float hNZ = _baseNZ + SampleNoiseDisp(_uvNZ, t) * m * sy;
            float hPZ = _basePZ + SampleNoiseDisp(_uvPZ, t) * m * sy;
            Vector3 n = new Vector3(hNX - hPX, 2f * AlignEpsilon, hNZ - hPZ).normalized;
            Quaternion target = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _rb.MoveRotation(Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(alignStrength.Value)));
        }
    }
}
