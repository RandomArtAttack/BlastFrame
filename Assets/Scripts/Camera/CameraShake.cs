using UnityEngine;

namespace BlastFrame.CameraRig
{
    /// <summary>
    /// Simple camera-local additive shake for damage / explosions / heavy landings. Decays over
    /// time; applied as a local offset so it composes with FirstPersonCamera's pitch.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("How fast a shake decays back to zero (higher = snappier).")]
        [SerializeField] private float decay = 8f;

        [Tooltip("Max positional offset magnitude in metres at full trauma.")]
        [SerializeField] private float maxOffset = 0.15f;

        private float _trauma;
        private Vector3 _baseLocalPos;

        private void Awake() => _baseLocalPos = transform.localPosition;

        /// <summary>Add shake intensity 0..1.</summary>
        public void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                transform.localPosition = _baseLocalPos;
                return;
            }
            float shake = _trauma * _trauma;
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (maxOffset * shake);
            transform.localPosition = _baseLocalPos + offset;
            _trauma = Mathf.MoveTowards(_trauma, 0f, decay * Time.deltaTime);
        }
    }
}
