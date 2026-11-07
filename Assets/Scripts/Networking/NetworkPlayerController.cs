// ---------------------------------------------------------------------------------------------
// ONLINE CO-OP FOUNDATION — guarded by BLASTFRAME_NGO. See ClientNetworkTransform.cs header.
// Written against NGO 1.x; NOT compile-verified (package install blocked by environment TLS error).
// ---------------------------------------------------------------------------------------------
#if BLASTFRAME_NGO
using Unity.Netcode;
using UnityEngine;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.Networking
{
    /// <summary>
    /// Sits on the networked player prefab (alongside a NetworkObject + ClientNetworkTransform).
    /// Owner-authoritative: the OWNER keeps its full local stack (input, camera, motor, movement
    /// modules) and streams its transform; NON-owners disable all of that and are driven purely by the
    /// ClientNetworkTransform. Shooting stays on the firing client's own simulation (damage is local —
    /// server-authoritative projectiles are the documented follow-up); an RPC lets remotes render a
    /// purely cosmetic tracer so they can see the shot.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Tooltip("Optional cosmetic tracer prefab remotes spawn when this player fires. No collider/damage.")]
        [SerializeField] private GameObject cosmeticTracerPrefab;

        [Tooltip("Seconds a cosmetic tracer lives on remotes before it is destroyed.")]
        [SerializeField] private float cosmeticTracerLifetime = 1.5f;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) return; // owner runs everything locally — nothing to strip.

            // Non-owner: this player is a remote avatar. Kill all local simulation so it only shows the
            // networked transform. (Enemies still target it via EntityRegistry through PlayerController's
            // registration, which the owner side performs on the owning client.)
            if (TryGetComponent<PlayerController>(out var controller)) controller.enabled = false;
            if (TryGetComponent<PlayerMotor>(out var motor)) motor.enabled = false;

            foreach (var comp in GetComponents<MonoBehaviour>())
                if (comp is IMovementModule) comp.enabled = false;

            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false); // also kills its AudioListener + viewmodel stack

            // Kinematic Rigidbody on a remote must not fight the NetworkTransform.
            if (TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
        }

        /// <summary>Owner calls this the moment it fires locally (wire to ChargeShot.OnReleased on the
        /// owner). Broadcasts a cosmetic-only tracer to every other client.</summary>
        public void ReportShot(Vector3 origin, Vector3 direction)
        {
            if (!IsOwner) return;
            FireCosmeticServerRpc(origin, direction);
        }

        [ServerRpc]
        private void FireCosmeticServerRpc(Vector3 origin, Vector3 direction, ServerRpcParams _ = default)
            => FireCosmeticClientRpc(origin, direction);

        [ClientRpc]
        private void FireCosmeticClientRpc(Vector3 origin, Vector3 direction)
        {
            if (IsOwner) return; // owner already spawned its real projectile locally.
            if (cosmeticTracerPrefab == null) return;
            var go = Instantiate(cosmeticTracerPrefab, origin, Quaternion.LookRotation(direction));
            Destroy(go, cosmeticTracerLifetime);
        }
    }
}
#endif
