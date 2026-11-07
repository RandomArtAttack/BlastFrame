using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Weapons
{
    /// <summary>
    /// Pure-visual pooled pop: no physics, no damage. Scales down over its lifetime then despawns.
    /// Used where an AoeExplosion would be unsafe — specifically the HomingMissile "shot down / caught
    /// in a blast" pop, which is spawned RE-ENTRANTLY from inside another explosion's damage loop.
    /// AoeExplosion's OnSpawn runs Physics.OverlapSphereNonAlloc into a shared static buffer, so
    /// spawning one during another explosion's iteration corrupts that buffer; VfxPop touches no
    /// physics, so it is safe to spawn from anywhere.
    /// </summary>
    public class VfxPop : MonoBehaviour, IPoolable
    {
        [Tooltip("Seconds the pop is visible before it despawns. It shrinks to zero over this time.")]
        [SerializeField] private FloatReference lifetime = new FloatReference(0.3f);

        private IPoolManager _pool;
        private float _timer;
        private bool _alive;
        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        private void Start() => _pool = ServiceLocator.Get<IPoolManager>();

        public void OnSpawn()
        {
            _alive = true;
            _timer = lifetime.Value;
            transform.localScale = _baseScale != Vector3.zero ? _baseScale : Vector3.one;
        }

        public void OnDespawn()
        {
            _alive = false;
            transform.localScale = _baseScale != Vector3.zero ? _baseScale : Vector3.one;
        }

        private void Update()
        {
            if (!_alive) return;
            _timer -= Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.0001f, lifetime.Value));
            transform.localScale = (_baseScale != Vector3.zero ? _baseScale : Vector3.one) * t;
            if (_timer <= 0f)
            {
                _alive = false;
                if (_pool == null) ServiceLocator.TryGet(out _pool);
                _pool?.Despawn(gameObject);
            }
        }
    }
}
