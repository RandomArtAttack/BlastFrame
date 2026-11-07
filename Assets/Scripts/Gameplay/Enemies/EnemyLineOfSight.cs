using UnityEngine;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Multi-point line-of-sight test against a target's collider bounds (center, top, bottom, both
    /// lateral edges) so a partially covered target still registers. Non-allocating (shared static
    /// RaycastHit buffer). A faithful, generalized port of the turret's HasLineOfSight/IsPointVisible:
    /// hits that are children of <paramref name="self"/> or of <paramref name="target"/> are not
    /// treated as blockers.
    /// </summary>
    public static class EnemyLineOfSight
    {
        private static readonly RaycastHit[] s_buffer = new RaycastHit[32];

        /// <summary>
        /// True if an unobstructed ray exists from <paramref name="origin"/> to ANY sampled point on
        /// the target's collider bounds (center, head, feet, left edge, right edge). Geometry parented
        /// to the shooter or to the target is ignored.
        /// </summary>
        public static bool HasLineOfSight(Transform self, Vector3 origin, Transform target, Collider targetCollider)
        {
            if (target == null) return false;

            var b = targetCollider != null
                ? targetCollider.bounds
                : new Bounds(target.position, new Vector3(1f, 2f, 1f));

            var toCenter = b.center - origin;
            var side = Vector3.Cross(toCenter, Vector3.up);
            side = side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;
            var lateral = Mathf.Max(b.extents.x, b.extents.z) * 0.9f;
            var vertical = b.extents.y * 0.9f;

            return IsPointVisible(self, origin, b.center, target)
                || IsPointVisible(self, origin, b.center + Vector3.up * vertical, target)
                || IsPointVisible(self, origin, b.center - Vector3.up * vertical, target)
                || IsPointVisible(self, origin, b.center + side * lateral, target)
                || IsPointVisible(self, origin, b.center - side * lateral, target);
        }

        private static bool IsPointVisible(Transform self, Vector3 origin, Vector3 point, Transform target)
        {
            var delta = point - origin;
            var dist = delta.magnitude;
            if (dist < 0.001f) return true;
            var dir = delta / dist;

            int n = Physics.RaycastNonAlloc(origin, dir, s_buffer, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = s_buffer[i].transform;
                if (self != null && t.IsChildOf(self)) continue; // self — not a blocker
                if (t.IsChildOf(target)) continue;               // target — not a blocker
                if (s_buffer[i].distance < dist - 0.05f) return false; // solid geometry in the way
            }
            return true;
        }
    }
}
