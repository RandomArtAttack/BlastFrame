using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Variables;
using BlastFrame.Input;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.Gameplay.Coop
{
    /// <summary>Service surface for local split-screen co-op.</summary>
    public interface ILocalCoopManager
    {
        bool IsCoopActive { get; }
    }

    /// <summary>
    /// Local split-screen 2P co-op. Lives in the Core scene as a service. Watches a gamepad join action
    /// (Start / South) while a player exists; on join it spawns P2 near P1, pairs the joining gamepad to
    /// P2's input, restricts P1 to keyboard+mouse, and splits both camera stacks into horizontal halves
    /// (P1 top, P2 bottom). Holding P2's Start button leaves co-op and restores full-screen P1. A dead
    /// player respawns after a delay at the living player's position; if both are dead the normal death
    /// flow runs (provisional v1 rule — see GameDesign.md).
    ///
    /// Self-contained: references only the shared EntityRegistry SO and an optional player prefab asset;
    /// the live P1 and its input provider are resolved at runtime (registry + ServiceLocator).
    /// </summary>
    public class LocalCoopManager : MonoBehaviour, ILocalCoopManager
    {
        [Tooltip("Shared EntityRegistry SO. Used to find the live P1 to spawn P2 next to.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Optional Player prefab asset to spawn for P2. If left empty, P2 is cloned from the live " +
                 "P1 at runtime (works either way — the clone carries the full camera/viewmodel stack).")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("Local offset from P1 where P2 spawns / respawns (metres, in P1's facing frame).")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(2.5f, 0f, 0f);

        [Tooltip("Seconds a dead co-op player waits before respawning at the living player. Provisional v1 = 5.")]
        [SerializeField] private FloatReference respawnDelay = new FloatReference(5f);

        [Tooltip("Seconds P2 must hold Start to leave co-op.")]
        [SerializeField] private float leaveHoldSeconds = 1f;

        private static readonly Rect RectTop = new Rect(0f, 0.5f, 1f, 0.5f);
        private static readonly Rect RectBottom = new Rect(0f, 0f, 1f, 0.5f);
        private static readonly Rect RectFull = new Rect(0f, 0f, 1f, 1f);

        public bool IsCoopActive { get; private set; }

        private IPlayerInputProvider _inputProvider;
        private InputAction _joinAction;
        private InputAction _leaveAction;

        private GameObject _p2;
        private int _p2Index = -1;
        private InputDevice _p2Device;

        private PlayerHealth _p1Health, _p2Health;
        private PlayerMotor _p1Motor, _p2Motor;
        private float _p1RespawnAt = -1f, _p2RespawnAt = -1f;

        private readonly List<InputDevice> _kbmScratch = new List<InputDevice>(2);

        private void Awake() => ServiceLocator.Register<ILocalCoopManager>(this);

        private void Start()
        {
            ServiceLocator.TryGet<IPlayerInputProvider>(out _inputProvider);

            _joinAction = new InputAction("CoopJoin", InputActionType.Button);
            _joinAction.AddBinding("<Gamepad>/start");
            _joinAction.AddBinding("<Gamepad>/buttonSouth");
            _joinAction.performed += OnJoinPerformed;
            _joinAction.Enable();

            _leaveAction = new InputAction("CoopLeave", InputActionType.Button, interactions: $"hold(duration={leaveHoldSeconds})");
            _leaveAction.AddBinding("<Gamepad>/start");
            _leaveAction.performed += OnLeavePerformed;
            // enabled only while P2 is present
        }

        private void OnDestroy()
        {
            if (_joinAction != null) { _joinAction.performed -= OnJoinPerformed; _joinAction.Dispose(); }
            if (_leaveAction != null) { _leaveAction.performed -= OnLeavePerformed; _leaveAction.Dispose(); }
            if (IsCoopActive) TearDownCoop();
            ServiceLocator.Unregister<ILocalCoopManager>(this);
        }

        private void Update()
        {
            if (!IsCoopActive) return;

            if (_p1RespawnAt >= 0f && Time.time >= _p1RespawnAt) { _p1RespawnAt = -1f; RespawnAt(_p1Motor, _p1Health, _p2Motor); }
            if (_p2RespawnAt >= 0f && Time.time >= _p2RespawnAt) { _p2RespawnAt = -1f; RespawnAt(_p2Motor, _p2Health, _p1Motor); }
        }

        // ---- Join -------------------------------------------------------------------------------

        private void OnJoinPerformed(InputAction.CallbackContext ctx)
        {
            if (IsCoopActive || _inputProvider == null) return;
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;   // "while in gameplay"
            if (ctx.control?.device is not Gamepad pad) return;
            JoinP2(pad);
        }

        private void JoinP2(Gamepad pad)
        {
            Transform p1 = entityRegistry.PlayerTransform;
            if (p1 == null) return;

            // Pair the joining gamepad to a new input slot, and take that gamepad away from P1.
            _p2Index = _inputProvider.AddPlayer(new InputDevice[] { pad });
            _p2Device = pad;
            _inputProvider.SetPlayerDevices(0, KeyboardMouseDevices());

            Vector3 pos = p1.position + p1.TransformVector(spawnOffset);
            _p2 = playerPrefab != null
                ? Instantiate(playerPrefab, pos, p1.rotation)
                : Instantiate(p1.gameObject, pos, p1.rotation);
            _p2.name = "Player_P2";

            ConfigureSecondPlayer(_p2, pos);
            // Distinct depths so the two base cameras composite deterministically on the display.
            SetPlayerViewport(p1, RectTop, 0f);
            SetPlayerViewport(_p2.transform, RectBottom, 1f);

            CacheHealthAndMotors(p1);
            HookDeaths();

            _joinAction.Disable();
            _leaveAction.Enable();
            IsCoopActive = true;
        }

        private void ConfigureSecondPlayer(GameObject p2, Vector3 pos)
        {
            // Identity BEFORE the clone's Start runs, so its input resolves to slot _p2Index.
            var id = p2.GetComponentInParent<PlayerIdentity>();
            if (id == null) id = p2.AddComponent<PlayerIdentity>();
            id.SetPlayerIndex(_p2Index);

            // Only one AudioListener allowed — silence P2's.
            var listener = p2.GetComponentInChildren<AudioListener>(true);
            if (listener != null) listener.enabled = false;

            // De-tag the clone's camera so Camera.main stays deterministic (P1).
            foreach (var cam in p2.GetComponentsInChildren<Camera>(true))
                if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";

            if (p2.TryGetComponent<PlayerMotor>(out var motor)) motor.Teleport(pos);

            BuildMinimalHealthLabel(p2);
        }

        // ---- Leave ------------------------------------------------------------------------------

        private void OnLeavePerformed(InputAction.CallbackContext ctx)
        {
            if (!IsCoopActive) return;
            if (ctx.control?.device != _p2Device) return;   // only P2's own Start leaves
            TearDownCoop();
        }

        private void TearDownCoop()
        {
            UnhookDeaths();

            Transform p1 = entityRegistry != null ? entityRegistry.PlayerTransform : null;
            if (p1 != null) SetPlayerViewport(p1, RectFull, 0f);

            if (_p2 != null) Destroy(_p2);
            _p2 = null;

            if (_inputProvider != null)
            {
                _inputProvider.SetPlayerDevices(0, null);   // P1 reads all devices again
                if (_p2Index >= 0) _inputProvider.RemovePlayer(_p2Index);
            }
            _p2Index = -1;
            _p2Device = null;
            _p1RespawnAt = _p2RespawnAt = -1f;

            _leaveAction.Disable();
            _joinAction.Enable();
            IsCoopActive = false;
        }

        // ---- Death / respawn (provisional v1) --------------------------------------------------

        private void CacheHealthAndMotors(Transform p1)
        {
            _p1Health = p1.GetComponent<PlayerHealth>();
            _p1Motor = p1.GetComponent<PlayerMotor>();
            _p2Health = _p2.GetComponent<PlayerHealth>();
            _p2Motor = _p2.GetComponent<PlayerMotor>();
        }

        private void HookDeaths()
        {
            if (_p1Health != null) _p1Health.OnDeath += OnP1Death;
            if (_p2Health != null) _p2Health.OnDeath += OnP2Death;
        }

        private void UnhookDeaths()
        {
            if (_p1Health != null) _p1Health.OnDeath -= OnP1Death;
            if (_p2Health != null) _p2Health.OnDeath -= OnP2Death;
        }

        private void OnP1Death()
        {
            // Respawn only if the other player can be reached; both dead → normal death flow (untouched).
            if (_p2Health != null && !_p2Health.IsDead) _p1RespawnAt = Time.time + respawnDelay.Value;
        }

        private void OnP2Death()
        {
            if (_p1Health != null && !_p1Health.IsDead) _p2RespawnAt = Time.time + respawnDelay.Value;
        }

        private void RespawnAt(PlayerMotor deadMotor, PlayerHealth deadHealth, PlayerMotor livingMotor)
        {
            if (deadMotor == null || deadHealth == null || livingMotor == null) return;
            deadMotor.Teleport(livingMotor.transform.position + Vector3.up * 0.5f);
            deadHealth.ResetHealth();
        }

        // ---- Helpers ----------------------------------------------------------------------------

        private InputDevice[] KeyboardMouseDevices()
        {
            _kbmScratch.Clear();
            if (Keyboard.current != null) _kbmScratch.Add(Keyboard.current);
            if (Mouse.current != null) _kbmScratch.Add(Mouse.current);
            return _kbmScratch.ToArray();
        }

        private static void SetPlayerViewport(Transform playerRoot, Rect rect, float depth)
        {
            var cams = playerRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                var data = cams[i].GetUniversalAdditionalCameraData();
                // Only the Base camera owns the viewport/depth; URP overlay cameras inherit them.
                if (data == null || data.renderType == CameraRenderType.Base)
                {
                    cams[i].rect = rect;
                    cams[i].depth = depth;
                }
            }
        }

        private static void BuildMinimalHealthLabel(GameObject p2)
        {
            var baseCam = FindBaseCamera(p2);
            if (baseCam == null) return;

            var canvasGo = new GameObject("P2_HUD", typeof(Canvas));
            canvasGo.transform.SetParent(p2.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = baseCam;
            canvas.planeDistance = 1f;

            var textGo = new GameObject("P2_Health", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(240f, 48f);

            var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 28f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            textGo.AddComponent<CoopPlayerHealthLabel>();
        }

        private static Camera FindBaseCamera(GameObject root)
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType == CameraRenderType.Base) return cam;
            }
            return null;
        }
    }
}
