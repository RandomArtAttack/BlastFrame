using UnityEngine;

namespace BlastFrame.Gameplay.Rooms
{
    public enum RoomMarkerKind { Entry, Exit, Secret, Checkpoint }

    /// <summary>
    /// Inert designer marker placed inside a room variant prefab (or level scene). No runtime behaviour: the
    /// level-design reachability lint (Tools/Blast Frame/Level Design) reads these to check that every Exit and
    /// Secret can be reached from the Entry with the player's measured jump/dash budget.
    /// </summary>
    public sealed class RoomMarker : MonoBehaviour
    {
        [Tooltip("What this point is. Entry = where the player arrives (reachability starts here); Exit / Secret / " +
                 "Checkpoint = places the lint checks the player can reach.")]
        [SerializeField] private RoomMarkerKind kind = RoomMarkerKind.Secret;

        [Tooltip("Optional label shown in lint results, e.g. 'High ledge secret'. Leave blank to use the GameObject name.")]
        [SerializeField] private string label = "";

        public RoomMarkerKind Kind => kind;
        public string Label => string.IsNullOrEmpty(label) ? name : label;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                RoomMarkerKind.Entry => Color.green,
                RoomMarkerKind.Exit => Color.cyan,
                RoomMarkerKind.Secret => Color.magenta,
                _ => Color.yellow,
            };
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
#endif
    }
}
