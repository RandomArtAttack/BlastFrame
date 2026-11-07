using UnityEngine;
using UnityEngine.UI;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.UI
{
    using BlastFrame.Gameplay; // DamageInfo

    /// <summary>
    /// Directional damage indicators: on damage that has a world source, a small arrow appears on a
    /// ring around screen center pointing at the source, re-aims every frame as the player turns, and
    /// fades out. Sourceless damage (DamageInfo.HasSource == false, e.g. submersion) shows nothing —
    /// the orange overlay covers that. Binds through the EntityRegistry the same way HealthDisplay
    /// does (retries each frame until the player registers) and reacts to events only. Uses a fixed
    /// pool of slots created once in Awake — no per-event allocation; the oldest slot is recycled
    /// when all are busy.
    /// </summary>
    public class DamageIndicatorUI : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assign the EntityRegistry.asset here (or wired by the Fix).")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("UI prefab asset for one indicator: a RectTransform + Image, arrow sprite authored " +
                 "pointing UP. Instantiated slotCount times as children in Awake.")]
        [SerializeField] private GameObject indicatorPrefab;

        [Tooltip("Ring radius in pixels from screen center where indicators sit.")]
        [SerializeField] private FloatReference ringRadius = new FloatReference(140f);

        [Tooltip("Seconds an indicator stays on screen before it is recycled.")]
        [SerializeField] private FloatReference lifetime = new FloatReference(1.6f);

        [Tooltip("Last fraction of the lifetime over which the indicator fades from full to zero (0.6 = last 60%).")]
        [SerializeField] private FloatReference fadePortion = new FloatReference(0.6f);

        [Tooltip("How many indicator slots exist. Oldest is recycled when all are busy.")]
        [SerializeField] private IntReference slotCount = new IntReference(6);

        private struct Slot
        {
            public RectTransform rect;
            public Image image;
            public Vector3 sourceWorld;
            public float dieAt;
            public bool active;
        }

        private Slot[] _slots;
        private bool _bound;
        private PlayerHealth _health;
        private Transform _playerTransform;
        private Transform _cameraTransform; // player camera, cached once; may be null (fall back to player forward)

        /// <summary>Called by HUDController to inject the registry when it is set there.</summary>
        public void SetRegistry(EntityRegistrySO registry) => entityRegistry = registry;

        private void Awake()
        {
            int count = Mathf.Max(0, slotCount.Value);
            _slots = new Slot[count];
            for (int i = 0; i < count; i++)
            {
                var go = indicatorPrefab != null
                    ? Instantiate(indicatorPrefab, transform)
                    : new GameObject("Indicator", typeof(RectTransform), typeof(Image));
                if (indicatorPrefab == null) go.transform.SetParent(transform, false);
                go.name = "Indicator" + i;

                var rect = go.GetComponent<RectTransform>();
                if (rect == null) rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;

                var image = go.GetComponent<Image>();
                if (image != null) image.raycastTarget = false;

                go.SetActive(false);

                _slots[i] = new Slot { rect = rect, image = image, active = false };
            }
        }

        private void Start() => TryBind();

        private void Update()
        {
            if (!_bound) TryBind();
            DriveSlots();
        }

        private void TryBind()
        {
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;

            _playerTransform = entityRegistry.PlayerTransform;
            _health = _playerTransform.GetComponent<PlayerHealth>();
            if (_health == null) return;

            // Angle source = the player's camera forward, flattened. Never Camera.main.
            var cam = _playerTransform.GetComponentInChildren<Camera>(true);
            _cameraTransform = cam != null ? cam.transform : null;

            _health.OnDamagedInfo += OnDamagedInfo;
            _bound = true;
        }

        private void OnDamagedInfo(DamageInfo info)
        {
            if (!info.HasSource || _slots == null || _slots.Length == 0) return;

            int chosen = -1;
            float earliestDie = float.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].active) { chosen = i; break; }
                if (_slots[i].dieAt < earliestDie) { earliestDie = _slots[i].dieAt; chosen = i; }
            }
            if (chosen < 0) return;

            _slots[chosen].sourceWorld = info.SourcePosition;
            _slots[chosen].dieAt = Time.time + lifetime.Value;
            _slots[chosen].active = true;
            if (_slots[chosen].rect != null) _slots[chosen].rect.gameObject.SetActive(true);
        }

        private void DriveSlots()
        {
            if (_slots == null || _playerTransform == null) return;

            float now = Time.time;
            float life = Mathf.Max(0.0001f, lifetime.Value);
            float fadeDuration = life * Mathf.Clamp01(fadePortion.Value);
            float radius = ringRadius.Value;
            Vector3 playerPos = _playerTransform.position;

            Vector3 camForwardFlat = _cameraTransform != null ? _cameraTransform.forward : _playerTransform.forward;
            camForwardFlat.y = 0f;
            if (camForwardFlat.sqrMagnitude < 0.0001f) camForwardFlat = Vector3.forward;
            else camForwardFlat.Normalize();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].active) continue;

                if (now >= _slots[i].dieAt)
                {
                    _slots[i].active = false;
                    if (_slots[i].rect != null) _slots[i].rect.gameObject.SetActive(false);
                    continue;
                }

                Vector3 flatToSource = _slots[i].sourceWorld - playerPos;
                flatToSource.y = 0f;
                if (flatToSource.sqrMagnitude < 0.0001f) flatToSource = -camForwardFlat; // from below reads as behind

                // bearing 0 = source dead ahead = indicator at the TOP of the ring.
                float bearing = Vector3.SignedAngle(camForwardFlat, flatToSource, Vector3.up);
                float rad = bearing * Mathf.Deg2Rad;

                var rect = _slots[i].rect;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
                    rect.localEulerAngles = new Vector3(0f, 0f, -bearing);
                }

                if (_slots[i].image != null)
                {
                    float remaining = _slots[i].dieAt - now;
                    float alpha = fadeDuration > 0.0001f ? Mathf.Clamp01(remaining / fadeDuration) : 1f;
                    Color c = _slots[i].image.color;
                    c.a = alpha;
                    _slots[i].image.color = c;
                }
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamagedInfo -= OnDamagedInfo;
        }
    }
}
