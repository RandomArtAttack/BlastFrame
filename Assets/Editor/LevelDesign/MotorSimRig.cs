using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using BlastFrame.Core;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;
using BlastFrame.Input;

namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>Scripted input source for the sim rig. Tests flip Move and fire the button events.</summary>
    public sealed class SimInput : IPlayerInput
    {
        public Vector2 Move { get; set; }
        public Vector2 Look => Vector2.zero;
        public bool LookFromGamepad => false;
        public bool JumpHeld { get; private set; }
        public bool FireHeld => false;
        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnDashPressed;
        public event Action OnDashReleased;
        public event Action OnFirePressed { add { } remove { } }
        public event Action OnFireReleased { add { } remove { } }
        public event Action OnInteract { add { } remove { } }
        public void SetInputEnabled(bool enabled) { }

        public void PressJump() { JumpHeld = true; OnJumpPressed?.Invoke(); }
        public void ReleaseJump() { JumpHeld = false; OnJumpReleased?.Invoke(); }
        public void PressDash() => OnDashPressed?.Invoke();
        public void ReleaseDash() => OnDashReleased?.Invoke();
        public void Reset() { Move = Vector2.zero; JumpHeld = false; }
    }

    internal sealed class SimInputProvider : IPlayerInputProvider
    {
        public SimInput Current;
        public int PlayerCount => 1;
        public IPlayerInput GetInput(int playerIndex) => Current;
        public int AddPlayer(InputDevice[] devices) => 0;
        public void RemovePlayer(int playerIndex) { }
        public void SetPlayerDevices(int playerIndex, InputDevice[] devices) { }
    }

    /// <summary>
    /// Serialized copy of the real Player's movement components (taken from Core.unity, or from whatever
    /// Player is loaded). The rig rebuilds a fresh player from it for every simulation, so tuning changes
    /// in the scene flow straight into measurements — no duplicated numbers.
    /// </summary>
    public sealed class PlayerTemplate
    {
        public const string CoreScenePath = "Assets/Scenes/Core.unity";

        /// <summary>Movement-relevant component types, in Awake order. PlayerTestMode is deliberately absent
        /// (multi-jump would make every gap "reachable").</summary>
        public static readonly Type[] MovementTypes =
        {
            typeof(PlayerStats), typeof(PlayerMotor), typeof(PlayerController),
            typeof(DashModule), typeof(JumpModule), typeof(WallSlideModule),
            typeof(LedgeVaultModule), typeof(PlatformRiderModule)
        };

        public readonly Dictionary<Type, string> Json = new Dictionary<Type, string>();
        public float Radius = 0.4f, Height = 2f;
        public Vector3 Center = new Vector3(0f, 1f, 0f);
        public int Layer = 6;
        public string Source;
        public int CollisionMask = ~0;

        /// <summary>Fingerprint of every captured value — lets cached reach profiles detect stale tuning.</summary>
        public string Hash
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in MovementTypes)
                    if (Json.TryGetValue(t, out var j)) sb.Append(StripRefs(j));
                sb.Append(Radius).Append(Height).Append(Center);
                return Hash128.Compute(sb.ToString()).ToString();
            }
        }

        private static string StripRefs(string json) =>
            System.Text.RegularExpressions.Regex.Replace(json, "\"instanceID\":-?\\d+", "");

        /// <summary>Capture from a loaded Player if one exists, else open Core.unity additively, read, close.</summary>
        /// <summary>Capture only from an already-loaded Player; null if none is loaded (never opens scenes).</summary>
        public static PlayerTemplate TryCaptureLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                var pc = FindIn(s);
                if (pc != null) return From(pc.gameObject, s.path);
            }
            return null;
        }

        public static PlayerTemplate Capture()
        {
            var loaded = TryCaptureLoaded();
            if (loaded != null) return loaded;

            if (Application.isPlaying)
                throw new InvalidOperationException("[PlayerTemplate] Exit play mode first — Core must be opened in edit mode.");

            var core = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);
            try
            {
                var pc = FindIn(core);
                if (pc == null) throw new InvalidOperationException("[PlayerTemplate] No PlayerController in " + CoreScenePath);
                return From(pc.gameObject, CoreScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(core, true);
            }
        }

        private static PlayerController FindIn(Scene s)
        {
            foreach (var root in s.GetRootGameObjects())
            {
                var pc = root.GetComponentInChildren<PlayerController>(true);
                if (pc != null) return pc;
            }
            return null;
        }

        private static PlayerTemplate From(GameObject player, string source)
        {
            var t = new PlayerTemplate { Source = source, Layer = player.layer };
            foreach (var type in MovementTypes)
            {
                var c = player.GetComponent(type);
                if (c != null) t.Json[type] = EditorJsonUtility.ToJson(c);
            }
            if (player.TryGetComponent<PlayerMotor>(out var motor))
            {
                var so = new SerializedObject(motor);
                var mask = so.FindProperty("collisionMask");
                if (mask != null) t.CollisionMask = mask.intValue;
            }
            if (player.TryGetComponent<CapsuleCollider>(out var cap))
            {
                t.Radius = cap.radius;
                t.Height = cap.height;
                t.Center = cap.center;
            }
            return t;
        }
    }

    /// <summary>
    /// Headless, deterministic harness that drives the REAL PlayerController/PlayerMotor/modules in edit mode:
    /// it calls their lifecycle methods by reflection and steps physics with Physics.Simulate. Build an arena
    /// with <see cref="Box"/>, <see cref="Spawn"/> a player, then <see cref="Step"/>. Dispose restores physics
    /// mode and the ServiceLocator. Physics.Simulate steps the DEFAULT physics scene, so run it in an empty
    /// scene (the test fixtures and menu tools do) — otherwise dynamic rigidbodies in an open level would move.
    /// </summary>
    public sealed class MotorSimRig : IDisposable
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public readonly PlayerTemplate Template;
        public readonly float Dt;

        public GameObject Player { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public SimInput Input { get; } = new SimInput();
        public int Tick { get; private set; }

        private readonly List<GameObject> _arena = new List<GameObject>();
        private readonly SimInputProvider _provider = new SimInputProvider();
        private readonly SimulationMode _prevMode;
        private readonly IPlayerInput _prevInput;
        private readonly IPlayerInputProvider _prevProvider;
        private PlayerController _controller;
        private Transform _root;

        public MotorSimRig(PlayerTemplate template)
        {
            Template = template;
            Dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            _prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            ServiceLocator.TryGet(out _prevInput);
            ServiceLocator.TryGet(out _prevProvider);
            _root = new GameObject("[MotorSimRig]") { hideFlags = HideFlags.DontSave }.transform;
            _provider.Current = Input;
            ServiceLocator.Register<IPlayerInputProvider>(_provider);
            ServiceLocator.Register<IPlayerInput>(Input);
        }

        /// <summary>Static, axis-aligned box collider (no renderer). min/max are world-space corners.</summary>
        public GameObject Box(Vector3 min, Vector3 max, string name = "Box")
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root, false);
            go.transform.position = (min + max) * 0.5f;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = max - min;
            _arena.Add(go);
            return go;
        }

        public void ClearArena()
        {
            foreach (var go in _arena) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _arena.Clear();
        }

        /// <summary>Builds a fresh player (new module state) at feet position, facing yaw degrees.</summary>
        public void Spawn(Vector3 feet, float yaw)
        {
            DespawnPlayer();
            Physics.SyncTransforms();

            var go = new GameObject("SimPlayer") { hideFlags = HideFlags.DontSave, layer = Template.Layer };
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = Template.Radius;
            cap.height = Template.Height;
            cap.center = Template.Center;

            var comps = new List<Component>();
            foreach (var type in PlayerTemplate.MovementTypes)
            {
                if (!Template.Json.TryGetValue(type, out var json)) continue;
                var c = go.AddComponent(type);
                EditorJsonUtility.FromJsonOverwrite(json, c);
                comps.Add(c);
            }

            _controller = go.GetComponent<PlayerController>();
            if (_controller != null)
            {
                // Never steer from the Core scene's yaw pivot or touch the shared registry asset.
                var so = new SerializedObject(_controller);
                so.FindProperty("orientation").objectReferenceValue = null;
                so.FindProperty("entityRegistry").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            go.SetActive(true);

            Input.Reset();

            foreach (var c in comps) Call(c, "Awake");
            foreach (var c in comps) Call(c, "Start");

            Player = go;
            Motor = go.GetComponent<PlayerMotor>();
            Tick = 0;
            Physics.SyncTransforms();
        }

        /// <summary>One FixedUpdate of the real controller followed by one physics step.</summary>
        public void Step()
        {
            Call(_controller, "FixedUpdate");
            Physics.Simulate(Dt);
            Tick++;
        }

        public Vector3 Position => Motor != null ? Motor.GetComponent<Rigidbody>().position : Vector3.zero;

        private void DespawnPlayer()
        {
            if (Player != null)
            {
                foreach (var c in Player.GetComponents<MonoBehaviour>()) Call(c, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(Player);
            }
            Player = null;
            Motor = null;
            _controller = null;
        }

        private static void Call(object target, string method)
        {
            if (target == null) return;
            for (var t = target.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                var m = t.GetMethod(method, Flags | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m == null) continue;
                m.Invoke(target, null);
                return;
            }
        }

        public void Dispose()
        {
            DespawnPlayer();
            ClearArena();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);

            ServiceLocator.Unregister<IPlayerInput>(Input);
            ServiceLocator.Unregister<IPlayerInputProvider>(_provider);
            if (_prevInput != null) ServiceLocator.Register(_prevInput);
            if (_prevProvider != null) ServiceLocator.Register(_prevProvider);

            Physics.simulationMode = _prevMode;
        }
    }
}
