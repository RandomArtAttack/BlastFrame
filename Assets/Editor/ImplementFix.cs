using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Input;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;
using BlastFrame.CameraRig;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Numbered one-click editor actions. Each fix is single-purpose and ADDITIVE — never edit,
    /// delete, or renumber an existing fix. Find the next number by scanning the [MenuItem]
    /// attributes below. Helpers are inlined (local functions) per fix so changing one fix can
    /// never alter another's behavior.
    ///
    /// Canonical conventions every fix relies on:
    ///   - Core scene path:        Assets/Scenes/Core.unity
    ///   - TestLevel scene path:   Assets/Scenes/TestLevel.unity   (THE test bed for all features)
    ///   - Player root name:       "Player"  (lives in the Core scene, persists across levels)
    ///   - EntityRegistry asset:   Assets/ScriptableObjects/Entities/EntityRegistry.asset
    /// </summary>
    public static class ImplementFix
    {
        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/001 - Build Core Scene And Services")]
        private static void Fix001()
        {
            void EnsureFolder(string abs) { if (!Directory.Exists(abs)) Directory.CreateDirectory(abs); }

            EnsureFolder(Path.Combine(Application.dataPath, "Scenes"));
            EnsureFolder(Path.Combine(Application.dataPath, "ScriptableObjects/Entities"));

            // EntityRegistry asset (created once; never overwritten).
            const string regPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";
            if (AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(regPath) == null)
            {
                var reg = ScriptableObject.CreateInstance<EntityRegistrySO>();
                AssetDatabase.CreateAsset(reg, regPath);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();

            var boot = new GameObject("Bootstrap");
            boot.AddComponent<CoreBootstrap>();
            boot.AddComponent<BootLoader>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Core.unity");

            // Register Core as build index 0.
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != "Assets/Scenes/Core.unity"))
                scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/Core.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Selection.activeObject = boot;
            EditorGUIUtility.PingObject(boot);
            Debug.Log("[Fix001] Core scene built (GameManager, Bootstrap) and added to Build Settings.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/002 - Build TestLevel Scene")]
        private static void Fix002()
        {
            void EnsureFolder(string abs) { if (!Directory.Exists(abs)) Directory.CreateDirectory(abs); }
            EnsureFolder(Path.Combine(Application.dataPath, "Art/Materials"));

            Material proto = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Prototype.mat");
            if (proto == null)
            {
                proto = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.6f, 0.6f, 0.62f) };
                AssetDatabase.CreateAsset(proto, "Assets/Art/Materials/Prototype.mat");
            }

            GameObject MakeBox(string name, Vector3 pos, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.position = pos;
                go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = proto;
                return go;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            MakeBox("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            MakeBox("Wall_2m", new Vector3(6f, 1f, 4f), new Vector3(1f, 2f, 4f));
            MakeBox("Ledge_High", new Vector3(-6f, 2f, 4f), new Vector3(4f, 4f, 2f));
            MakeBox("WallJump_A", new Vector3(2f, 3f, 10f), new Vector3(0.5f, 6f, 4f));
            MakeBox("WallJump_B", new Vector3(6f, 3f, 10f), new Vector3(0.5f, 6f, 4f));

            var content = new GameObject("Content"); // empty parent for placed test content
            content.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/TestLevel.unity");

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != "Assets/Scenes/TestLevel.unity"))
                scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/TestLevel.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[Fix002] TestLevel scene built (floor, obstacles, wall-jump gap) and added to Build Settings.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/003 - Add Player Input Handler To Core")]
        private static void Fix003()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<PlayerInputHandler>() == null)
            {
                var go = new GameObject("PlayerInputHandler");
                go.AddComponent<PlayerInputHandler>();
                Selection.activeObject = go;
                EditorGUIUtility.PingObject(go);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Fix003] PlayerInputHandler added to Core.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/004 - Build Player In Core")]
        private static void Fix004()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            if (GameObject.Find("Player") != null)
            {
                Debug.LogWarning("[Fix004] A 'Player' already exists in Core — aborting to avoid duplicates.");
                return;
            }

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            capsule.center = new Vector3(0f, 1f, 0f);

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerHealth>();
            var controller = player.AddComponent<PlayerController>();

            // Camera child at eye height.
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var fpc = camGo.AddComponent<FirstPersonCamera>();
            camGo.AddComponent<CameraShake>();

            // Wire EntityRegistry into the controller (SO asset reference).
            var reg = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>("Assets/ScriptableObjects/Entities/EntityRegistry.asset");
            if (reg != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("entityRegistry").objectReferenceValue = reg;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Wire the camera's body field to the player root.
            var fso = new SerializedObject(fpc);
            fso.FindProperty("body").objectReferenceValue = player.transform;
            fso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player;
            EditorGUIUtility.PingObject(player);
            Debug.Log("[Fix004] Player built in Core (kinematic motor, stats, health, FP camera).");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/005 - Add Jump Module To Player")]
        private static void Fix005()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[Fix005] No 'Player' in Core — run Fix 004 first."); return; }
            if (player.GetComponent<JumpModule>() == null) player.AddComponent<JumpModule>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player; EditorGUIUtility.PingObject(player);
            Debug.Log("[Fix005] JumpModule added to Player.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/006 - Add Dash Module To Player")]
        private static void Fix006()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[Fix006] No 'Player' in Core — run Fix 004 first."); return; }
            if (player.GetComponent<DashModule>() == null) player.AddComponent<DashModule>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player; EditorGUIUtility.PingObject(player);
            Debug.Log("[Fix006] DashModule added to Player.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/007 - Add Wall Slide Module To Player")]
        private static void Fix007()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[Fix007] No 'Player' in Core — run Fix 004 first."); return; }
            if (player.GetComponent<WallSlideModule>() == null) player.AddComponent<WallSlideModule>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player; EditorGUIUtility.PingObject(player);
            Debug.Log("[Fix007] WallSlideModule added to Player.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/008 - Add Platform Rider Module To Player")]
        private static void Fix008()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[Fix008] No 'Player' in Core — run Fix 004 first."); return; }
            if (player.GetComponent<PlatformRiderModule>() == null) player.AddComponent<PlatformRiderModule>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player; EditorGUIUtility.PingObject(player);
            Debug.Log("[Fix008] PlatformRiderModule added to Player.");
        }
    }
}
