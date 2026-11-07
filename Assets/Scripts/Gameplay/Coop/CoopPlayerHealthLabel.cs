using UnityEngine;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.Gameplay.Coop
{
    /// <summary>
    /// Minimal split-screen health readout for a co-op player. Finds its owning PlayerHealth in a parent
    /// and mirrors current/max onto a sibling TMP text. Built at runtime by LocalCoopManager for P2 (the
    /// full dual HUD is a documented follow-up). Reacts to events only.
    /// </summary>
    [RequireComponent(typeof(TMPro.TMP_Text))]
    public class CoopPlayerHealthLabel : MonoBehaviour
    {
        private PlayerHealth _health;
        private TMPro.TMP_Text _text;

        private void Start()
        {
            _text = GetComponent<TMPro.TMP_Text>();
            _health = GetComponentInParent<PlayerHealth>();
            if (_health != null)
            {
                _health.OnHealthChanged += OnHealthChanged;
                OnHealthChanged(_health.Current, _health.Max);
            }
            else if (_text != null)
            {
                _text.text = "P2";
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            if (_text != null) _text.text = $"P2  {current}/{max}";
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= OnHealthChanged;
        }
    }
}
