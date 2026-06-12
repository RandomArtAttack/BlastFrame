using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Weapons
{
    /// <summary>
    /// Pooled AoE explosion. On OnSpawn: overlaps a sphere (Physics.OverlapSphereNonAlloc),
    /// applies damage to every IDamageable found, scales visually, then despawns after lifetime.
    /// Uses a non-allocating overlap buffer — safe to call from gameplay without per-frame GC.
    /// </summary>
    public class AoeExplosion : MonoBehaviour, IPoolable
    {
        [Tooltip("Explosion blast radius in metres.")]
        [SerializeField] private FloatReference radius = new FloatReference(3f);

        [Tooltip("Damage dealt to every IDamageable inside the blast radius.")]
        [SerializeField] private IntReference damage = new IntReference(3);

        [Tooltip("Seconds the visual remains visible before the object despawns.")]
        [SerializeField] private FloatReference visualLifetime = new FloatReference(0.35f);

        [Tooltip("Layer mask restricting which layers the AoE overlap sphere tests. 0 = All Layers.")]
        [SerializeField] private LayerMask damageLayers = ~0;

        private static readonly Collider[] _overlapBuffer = new Collider[32];

        private IPoolManager _pool;
        private float _timer;
        private bool _alive;
        private Vector3 _baseScale;

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (!_alive) return;

            _timer -= Time.deltaTime;

            // Shrink visual toward zero over its lifetime.
            float t = Mathf.Clamp01(_timer / visualLifetime.Value);
            transform.localScale = _baseScale * t;

            if (_timer <= 0f) Despawn();
        }

        // ----- IPoolable -----------------------------------------------------------------------

        public void OnSpawn()
        {
            _alive = true;
            _timer = visualLifetime.Value;
            transform.localScale = _baseScale != Vector3.zero ? _baseScale : Vector3.one;

            // Radial damage — one-shot at spawn, non-allocating.
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius.Value, _overlapBuffer, damageLayers);
            for (int i = 0; i < count; i++)
            {
                var damageable = _overlapBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                    damageable.TakeDamage(damage.Value);
            }
        }

        public void OnDespawn()
        {
            _alive = false;
            transform.localScale = _baseScale != Vector3.zero ? _baseScale : Vector3.one;
        }

        private void Despawn()
        {
            if (!_alive) return;
            _alive = false;
            _pool?.Despawn(gameObject);
        }
    }
}
