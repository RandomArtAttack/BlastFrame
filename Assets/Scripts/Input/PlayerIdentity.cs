using UnityEngine;

namespace BlastFrame.Input
{
    /// <summary>
    /// Tags a player object with its local player index so per-player systems (input, HUD) can bind to
    /// the right source. Index 0 = P1 (the scene player / keyboard+mouse). LocalCoopManager sets index 1
    /// on the spawned P2. Absence of this component resolves to index 0 (single-player back-compat).
    /// </summary>
    public class PlayerIdentity : MonoBehaviour
    {
        [Tooltip("0 = P1 (keyboard/mouse, the scene player). 1 = P2 (first gamepad, spawned by co-op). " +
                 "Set at runtime by LocalCoopManager for spawned players; leave 0 for the scene P1.")]
        [SerializeField] private int playerIndex;

        public int PlayerIndex => playerIndex;

        public void SetPlayerIndex(int index) => playerIndex = index;
    }
}
