using UnityEngine;
using UnityEngine.UI;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.UI
{
    using BlastFrame.Gameplay; // DamageInfo

    /// <summary>
    /// Full-screen orange overlay: a brief translucent flash on ANY damage, and an opaque "you're
    /// under the lava" wash while the player's head is submerged. Binds through the EntityRegistry the
    /// same way HealthDisplay does — retries each frame until the player is registered, and reacts to
    /// events only (never queries managers). The submersion floor wins over the hit flash
    /// (effective alpha = max of the two). Disables the Image when fully transparent to avoid overdraw.
    /// </summary>
    public class DamageOverlayUI : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assign the EntityRegistry.asset here (or wired by the Fix).")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("The full-screen Image this widget drives (a child of this object). Alpha is animated; " +
                 "its raycastTarget is forced off so it never blocks input.")]
        [SerializeField] private Image overlayImage;

        [Tooltip("Colour of the brief any-damage flash. Alpha is the peak flash opacity.")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.35f, 0f, 0.35f);

        [Tooltip("Colour of the sustained submersion wash. Alpha is the (near-opaque) submerged opacity.")]
        [SerializeField] private Color submergedColor = new Color(1f, 0.35f, 0f, 0.95f);

        [Tooltip("Seconds for a hit flash to fade from peak to zero. ~0.4.")]
        [SerializeField] private FloatReference hitFlashDuration = new FloatReference(0.4f);

        [Tooltip("Seconds to fade IN to the full submerged wash when the head goes under. ~0.15.")]
        [SerializeField] private FloatReference submergeFadeIn = new FloatReference(0.15f);

        [Tooltip("Seconds to fade OUT the submerged wash when the head surfaces. ~0.3.")]
        [SerializeField] private FloatReference submergeFadeOut = new FloatReference(0.3f);

        private bool _bound;
        private PlayerHealth _health;
        private LavaSubmersionDetector _detector;

        private float _flashTimer;   // seconds left in the current hit flash
        private float _flashAlpha;   // current flash contribution
        private bool _submerged;     // submersion target from the detector
        private float _submergeAlpha; // current submersion contribution (eased toward the target)

        /// <summary>Called by HUDController to inject the registry when it is set there.</summary>
        public void SetRegistry(EntityRegistrySO registry) => entityRegistry = registry;

        private void Awake()
        {
            if (overlayImage != null)
            {
                overlayImage.raycastTarget = false;
                overlayImage.enabled = false;
            }
        }

        private void Start() => TryBind();

        private void Update()
        {
            if (!_bound) TryBind();
            DriveOverlay();
        }

        private void TryBind()
        {
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;

            var playerTf = entityRegistry.PlayerTransform;
            _health = playerTf.GetComponent<PlayerHealth>();
            if (_health == null) return;

            _health.OnDamagedInfo += OnDamagedInfo;

            // The submersion detector is optional — the flash still works without it.
            _detector = playerTf.GetComponent<LavaSubmersionDetector>();
            if (_detector != null)
            {
                _detector.OnSubmergedChanged += OnSubmergedChanged;
                _submerged = _detector.IsSubmerged;
            }

            _bound = true;
        }

        private void OnDamagedInfo(DamageInfo info) => _flashTimer = hitFlashDuration.Value;

        private void OnSubmergedChanged(bool submerged) => _submerged = submerged;

        private void DriveOverlay()
        {
            float dt = Time.deltaTime;

            // Hit flash: linear fade from peak (hitFlashColor.a) to zero over the duration.
            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                float dur = hitFlashDuration.Value;
                _flashAlpha = dur > 0.0001f ? hitFlashColor.a * Mathf.Clamp01(_flashTimer / dur) : 0f;
            }
            else
            {
                _flashAlpha = 0f;
            }

            // Submersion wash: ease toward full/zero over the configured fade times.
            float subTarget = _submerged ? submergedColor.a : 0f;
            float fade = _submerged ? submergeFadeIn.Value : submergeFadeOut.Value;
            float speed = fade > 0.0001f ? submergedColor.a / fade : float.MaxValue;
            _submergeAlpha = Mathf.MoveTowards(_submergeAlpha, subTarget, speed * dt);

            if (overlayImage == null) return;

            // Submersion FLOOR wins over the flash.
            float alpha = Mathf.Max(_flashAlpha, _submergeAlpha);
            if (alpha <= 0f)
            {
                if (overlayImage.enabled) overlayImage.enabled = false;
                return;
            }

            if (!overlayImage.enabled) overlayImage.enabled = true;
            Color c = _submergeAlpha >= _flashAlpha ? submergedColor : hitFlashColor;
            c.a = alpha;
            overlayImage.color = c;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamagedInfo -= OnDamagedInfo;
            if (_detector != null) _detector.OnSubmergedChanged -= OnSubmergedChanged;
        }
    }
}
