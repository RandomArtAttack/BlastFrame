using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Pooling;
using BlastFrame.Input;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;
using BlastFrame.Gameplay.Weapons;
using BlastFrame.Gameplay.Projectiles;
using BlastFrame.Gameplay.Powerups;
using BlastFrame.Gameplay.Rooms;
using BlastFrame.Gameplay.Economy;
using BlastFrame.Gameplay.HQ;
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

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/011 - Build Combat Prefabs And Pools")]
        private static void Fix011()
        {
            // ---- inline helpers ----------------------------------------------------------------
            void EnsureAssetFolder(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            GameObject BuildProjectilePrefab(string path)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "PlayerProjectile";
                go.transform.localScale = Vector3.one * 0.25f;

                if (!go.TryGetComponent(out Rigidbody rb)) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                if (!go.TryGetComponent(out SphereCollider col)) col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;

                go.AddComponent<PlayerProjectile>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                return prefab;
            }

            GameObject BuildExplosionPrefab(string path)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Explosion";
                go.transform.localScale = Vector3.one;

                var col = go.GetComponent<SphereCollider>();
                if (col != null) Object.DestroyImmediate(col);

                go.AddComponent<AoeExplosion>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                return prefab;
            }

            BlastFrame.Core.Entities.EntityDefinitionSO EnsureEntityDef(string assetPath, string id, GameObject prefab)
            {
                var existing = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityDefinitionSO>(assetPath);
                if (existing != null) return existing;

                var so = ScriptableObject.CreateInstance<BlastFrame.Core.Entities.EntityDefinitionSO>();
                AssetDatabase.CreateAsset(so, assetPath);
                var serialized = new SerializedObject(so);
                serialized.FindProperty("id").stringValue = id;
                serialized.FindProperty("prefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return so;
            }

            // ---- step 1: folders ---------------------------------------------------------------
            EnsureAssetFolder("Assets/Prefabs/Projectiles");
            EnsureAssetFolder("Assets/ScriptableObjects/Entities");
            EnsureAssetFolder("Assets/ScriptableObjects/Pooling");

            // ---- step 2: prefabs (create only if missing) -------------------------------------
            const string projPrefabPath = "Assets/Prefabs/Projectiles/PlayerProjectile.prefab";
            const string explPrefabPath = "Assets/Prefabs/Projectiles/Explosion.prefab";

            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projPrefabPath)
                             ?? BuildProjectilePrefab(projPrefabPath);
            var explPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(explPrefabPath)
                             ?? BuildExplosionPrefab(explPrefabPath);

            // ---- step 3: EntityDefinitionSO assets --------------------------------------------
            const string projDefPath = "Assets/ScriptableObjects/Entities/PlayerProjectile.asset";
            const string explDefPath = "Assets/ScriptableObjects/Entities/Explosion.asset";

            var projDef = EnsureEntityDef(projDefPath, BlastFrame.Core.PoolIds.PlayerProjectile, projPrefab);
            var explDef = EnsureEntityDef(explDefPath, BlastFrame.Core.PoolIds.Explosion, explPrefab);

            // ---- step 4: PoolConfigSO (create if missing, then add entries idempotently) ------
            const string poolConfigPath = "Assets/ScriptableObjects/Pooling/PoolConfig.asset";
            var poolConfig = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Pooling.PoolConfigSO>(poolConfigPath);
            if (poolConfig == null)
            {
                poolConfig = ScriptableObject.CreateInstance<BlastFrame.Core.Pooling.PoolConfigSO>();
                AssetDatabase.CreateAsset(poolConfig, poolConfigPath);
                AssetDatabase.SaveAssets();
            }

            var pcSo = new SerializedObject(poolConfig);
            var entriesProp = pcSo.FindProperty("entries");

            // Check which defs are already represented in the entries array.
            bool HasEntry(string defId)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    var defProp = elem.FindPropertyRelative("definition");
                    if (defProp.objectReferenceValue is BlastFrame.Core.Entities.EntityDefinitionSO d && d.Id == defId)
                        return true;
                }
                return false;
            }

            void AddEntry(BlastFrame.Core.Entities.EntityDefinitionSO def, int prewarm, int expand)
            {
                int idx = entriesProp.arraySize;
                entriesProp.InsertArrayElementAtIndex(idx);
                var elem = entriesProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("definition").objectReferenceValue = def;
                elem.FindPropertyRelative("prewarmCount").intValue = prewarm;
                elem.FindPropertyRelative("expandIncrement").intValue = expand;
            }

            if (!HasEntry(BlastFrame.Core.PoolIds.PlayerProjectile)) AddEntry(projDef, 20, 10);
            if (!HasEntry(BlastFrame.Core.PoolIds.Explosion))         AddEntry(explDef, 8, 4);

            pcSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            // ---- step 5: wire PoolConfig into PoolManager in Core ----------------------------
            var coreScene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Additive);

            BlastFrame.Core.Pooling.PoolManager poolMgr = null;
            foreach (var root in coreScene.GetRootGameObjects())
            {
                poolMgr = root.GetComponentInChildren<BlastFrame.Core.Pooling.PoolManager>(true);
                if (poolMgr != null) break;
            }

            if (poolMgr == null)
            {
                // Create a PoolManager GameObject in Core.
                var pmGo = new GameObject("PoolManager");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pmGo, coreScene);
                poolMgr = pmGo.AddComponent<BlastFrame.Core.Pooling.PoolManager>();
            }

            var pmSo = new SerializedObject(poolMgr);
            pmSo.FindProperty("config").objectReferenceValue = poolConfig;
            pmSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(coreScene);
            EditorSceneManager.SaveScene(coreScene);
            EditorSceneManager.CloseScene(coreScene, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = poolConfig;
            EditorGUIUtility.PingObject(poolConfig);
            Debug.Log("[Fix011] Combat prefabs + EntityDefs + PoolConfig entries + PoolManager wired. All idempotent.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/012 - Add Player Shooter To Camera")]
        private static void Fix012()
        {
            var coreScene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Fix012] No 'Player' found in Core — run Fix 004 first.");
                return;
            }

            Transform camTransform = player.transform.Find("Camera");
            if (camTransform == null)
            {
                Debug.LogError("[Fix012] No 'Camera' child found on Player — run Fix 004 first.");
                return;
            }

            var camGo = camTransform.gameObject;
            if (camGo.GetComponent<BlastFrame.Gameplay.Weapons.ChargeShot>() == null)
                camGo.AddComponent<BlastFrame.Gameplay.Weapons.ChargeShot>();

            if (camGo.GetComponent<BlastFrame.Gameplay.Player.PlayerShooter>() == null)
                camGo.AddComponent<BlastFrame.Gameplay.Player.PlayerShooter>();

            EditorSceneManager.MarkSceneDirty(coreScene);
            EditorSceneManager.SaveScene(coreScene);

            Selection.activeObject = camGo;
            EditorGUIUtility.PingObject(camGo);
            Debug.Log("[Fix012] ChargeShot + PlayerShooter added to Player/Camera in Core.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/015 - Place Test Powerup In TestLevel")]
        private static void Fix015()
        {
            // ---- inline folder helper ----------------------------------------------------------
            static void EnsureFolder015(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            // ---- step 1: create Heal PowerupSO only if it doesn't already exist ---------------
            const string powerupFolder = "Assets/ScriptableObjects/Powerups";
            const string healAssetPath = "Assets/ScriptableObjects/Powerups/Heal.asset";

            EnsureFolder015(powerupFolder);

            var healSO = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Powerups.PowerupSO>(healAssetPath);
            if (healSO == null)
            {
                healSO = ScriptableObject.CreateInstance<BlastFrame.Gameplay.Powerups.PowerupSO>();
                AssetDatabase.CreateAsset(healSO, healAssetPath);
                AssetDatabase.SaveAssets();

                // Set designer fields — only runs on first creation, never overwrites.
                var pso = new SerializedObject(healSO);
                pso.FindProperty("_id").stringValue          = "heal_basic";
                pso.FindProperty("_displayName").stringValue = "Heal Pack";
                // PowerupEffect.Heal is index 0 in the enum declaration.
                pso.FindProperty("_effect").enumValueIndex   = 0;
                // _magnitude: UseConstant = true, ConstantValue = 2 (restore 2 HP).
                var mag = pso.FindProperty("_magnitude");
                mag.FindPropertyRelative("UseConstant").boolValue    = true;
                mag.FindPropertyRelative("ConstantValue").floatValue = 2f;
                // _duration: UseConstant = true, ConstantValue = 0 (instant).
                var dur = pso.FindProperty("_duration");
                dur.FindPropertyRelative("UseConstant").boolValue    = true;
                dur.FindPropertyRelative("ConstantValue").floatValue = 0f;
                pso.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[Fix015] Created Heal.asset at " + healAssetPath + " (id=heal_basic, magnitude=2, instant).");
            }
            else
            {
                Debug.Log("[Fix015] Heal.asset already exists — skipping creation to preserve any tuned values.");
            }

            // ---- step 2: EntityRegistry asset --------------------------------------------------
            const string regPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityRegistrySO>(regPath);
            if (registry == null)
            {
                Debug.LogWarning("[Fix015] EntityRegistry.asset not found at " + regPath + ". Run Fix 001 first, then re-run Fix 015.");
                return;
            }

            // ---- step 3: open TestLevel scene --------------------------------------------------
            const string scenePath = "Assets/Scenes/TestLevel.unity";
            string fullScenePath = System.IO.Path.Combine(
                Application.dataPath.Replace("/Assets", string.Empty), scenePath);
            if (!System.IO.File.Exists(fullScenePath))
            {
                Debug.LogWarning("[Fix015] TestLevel.unity not found. Run Fix 002 to build the scene first, then re-run Fix 015.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ---- step 4: idempotency — skip if pickup already exists by name -------------------
            const string pickupGoName = "TestHealPickup";
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == pickupGoName)
                {
                    Debug.LogWarning("[Fix015] '" + pickupGoName + "' already present in TestLevel — aborting to avoid duplicates.");
                    return;
                }
                if (rootGo.transform.Find(pickupGoName) != null)
                {
                    Debug.LogWarning("[Fix015] '" + pickupGoName + "' already present (as child) in TestLevel — aborting to avoid duplicates.");
                    return;
                }
            }

            // ---- step 5: build the pickup GameObject -------------------------------------------
            var pickup = new GameObject(pickupGoName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pickup, scene);
            pickup.transform.position = new Vector3(0f, 0.5f, 4f);

            // Visual: small glowing capsule (prototype stand-in).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(pickup.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale    = new Vector3(0.4f, 0.4f, 0.4f);

            // Remove the primitive's own collider — trigger lives on the root.
            var visualCol = visual.GetComponent<CapsuleCollider>();
            if (visualCol != null) Object.DestroyImmediate(visualCol);

            // Sphere trigger on the root so the player body sweep contacts it.
            var triggerCol = pickup.AddComponent<SphereCollider>();
            triggerCol.isTrigger = true;
            triggerCol.radius    = 0.8f;
            triggerCol.center    = new Vector3(0f, 0.5f, 0f);

            // PowerupPickup component.
            var pickupComp = pickup.AddComponent<BlastFrame.Gameplay.Powerups.PowerupPickup>();

            // Wire SO references via SerializedObject (no cross-object Inspector drag-drop).
            var cso = new SerializedObject(pickupComp);
            cso.FindProperty("_powerup").objectReferenceValue        = healSO;
            cso.FindProperty("_entityRegistry").objectReferenceValue = registry;
            // _onPickedEvent intentionally left null — no event SO wired in this test fixture.
            cso.ApplyModifiedPropertiesWithoutUndo();

            // ---- step 6: parent under "Content" if present ------------------------------------
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == "Content")
                {
                    pickup.transform.SetParent(rootGo.transform, true);
                    break;
                }
            }

            // ---- step 7: save scene -----------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = pickup;
            EditorGUIUtility.PingObject(pickup);
            Debug.Log("[Fix015] TestHealPickup placed in TestLevel at (0, 0.5, 4). Heal.asset + EntityRegistry wired. Idempotent — safe to re-run.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/016 - Build Room Variant Demo In TestLevel")]
        private static void Fix016()
        {
            // ---- inline helpers ----------------------------------------------------------------
            static void EnsureAssetFolder(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                        UnityEditor.AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            // Create a trivial self-contained variant prefab (geometry only, no external references).
            // Returns the asset path actually used.
            static string EnsureVariantPrefab(string prefabPath, string label, Color color)
            {
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    return prefabPath;

                var root = new GameObject(label);

                // Floor slab.
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(10f, 1f, 10f);
                var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

                // One or two distinguishing props per variant so they look different at a glance.
                if (label == "VariantA")
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "BoxA";
                    box.transform.SetParent(root.transform, false);
                    box.transform.localPosition = new Vector3(0f, 1f, 0f);
                    box.transform.localScale = Vector3.one * 2f;
                    box.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                }
                else if (label == "VariantB")
                {
                    // Ramp (rotated box).
                    var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ramp.name = "Ramp";
                    ramp.transform.SetParent(root.transform, false);
                    ramp.transform.localPosition = new Vector3(0f, 0.25f, -2f);
                    ramp.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                    ramp.transform.localScale = new Vector3(4f, 0.4f, 5f);
                    ramp.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                }
                else // VariantC
                {
                    // Two floating platform cylinders.
                    for (int p = 0; p < 2; p++)
                    {
                        var plat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        plat.name = $"Platform{p}";
                        plat.transform.SetParent(root.transform, false);
                        plat.transform.localPosition = new Vector3(p == 0 ? -2f : 2f, 1.5f, 0f);
                        plat.transform.localScale = new Vector3(2f, 0.2f, 2f);
                        plat.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                    }
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                Debug.Log($"[Fix016] Created variant prefab at {prefabPath}.");
                return prefabPath;
            }

            // Create a RoomVariantSO wired to a prefab asset. Returns the SO.
            static BlastFrame.Gameplay.Rooms.RoomVariantSO EnsureVariantSO(
                string soPath, string id, string prefabPath)
            {
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>(soPath);
                if (existing != null) return existing;

                var so = ScriptableObject.CreateInstance<BlastFrame.Gameplay.Rooms.RoomVariantSO>();
                UnityEditor.AssetDatabase.CreateAsset(so, soPath);

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var serialized = new UnityEditor.SerializedObject(so);
                serialized.FindProperty("_id").stringValue = id;
                serialized.FindProperty("_variantPrefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[Fix016] Created RoomVariantSO '{id}' at {soPath}.");
                return so;
            }

            // ---- step 1: ensure folders --------------------------------------------------------
            EnsureAssetFolder("Assets/Prefabs/RoomVariants");
            EnsureAssetFolder("Assets/ScriptableObjects/RoomVariants");

            // ---- step 2: variant prefabs (create if missing) -----------------------------------
            EnsureVariantPrefab("Assets/Prefabs/RoomVariants/Demo_VariantA.prefab", "VariantA",
                new Color(0.2f, 0.55f, 0.9f));   // blue
            EnsureVariantPrefab("Assets/Prefabs/RoomVariants/Demo_VariantB.prefab", "VariantB",
                new Color(0.85f, 0.45f, 0.1f));   // orange
            EnsureVariantPrefab("Assets/Prefabs/RoomVariants/Demo_VariantC.prefab", "VariantC",
                new Color(0.2f, 0.75f, 0.35f));   // green

            UnityEditor.AssetDatabase.Refresh();

            // ---- step 3: RoomVariantSO assets (create if missing) ------------------------------
            var soA = EnsureVariantSO(
                "Assets/ScriptableObjects/RoomVariants/Demo_VariantA.asset",
                "Demo_Room01_VariantA",
                "Assets/Prefabs/RoomVariants/Demo_VariantA.prefab");

            var soB = EnsureVariantSO(
                "Assets/ScriptableObjects/RoomVariants/Demo_VariantB.asset",
                "Demo_Room01_VariantB",
                "Assets/Prefabs/RoomVariants/Demo_VariantB.prefab");

            var soC = EnsureVariantSO(
                "Assets/ScriptableObjects/RoomVariants/Demo_VariantC.asset",
                "Demo_Room01_VariantC",
                "Assets/Prefabs/RoomVariants/Demo_VariantC.prefab");

            // ---- step 4: open TestLevel, wire RoomController -----------------------------------
            const string testLevelPath = "Assets/Scenes/TestLevel.unity";
            if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), testLevelPath)))
            {
                Debug.LogError("[Fix016] TestLevel.unity not found — run Fix 002 first.");
                return;
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                testLevelPath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            // Find or create the "Content" parent.
            GameObject contentParent = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Content") { contentParent = root; break; }
            }
            if (contentParent == null)
            {
                contentParent = new GameObject("Content");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(contentParent, scene);
            }

            // Idempotency: skip if a RoomController named "RoomSlot_Demo" already exists.
            bool alreadyPresent = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<BlastFrame.Gameplay.Rooms.RoomController>(true) != null)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent)
            {
                Debug.LogWarning("[Fix016] A RoomController already exists in TestLevel — skipping creation to avoid duplicates.");
            }
            else
            {
                // Build RoomController slot.
                var slotGo = new GameObject("RoomSlot_Demo");
                slotGo.transform.SetParent(contentParent.transform, false);
                slotGo.transform.localPosition = Vector3.zero;

                var anchor = new GameObject("SpawnAnchor");
                anchor.transform.SetParent(slotGo.transform, false);
                anchor.transform.localPosition = new Vector3(0f, 0f, 20f);

                var controller = slotGo.AddComponent<BlastFrame.Gameplay.Rooms.RoomController>();

                // Wire via SerializedObject.
                var so = new UnityEditor.SerializedObject(controller);

                var variantsProp = so.FindProperty("_variants");
                variantsProp.ClearArray();
                variantsProp.arraySize = 3;
                variantsProp.GetArrayElementAtIndex(0).objectReferenceValue = soA;
                variantsProp.GetArrayElementAtIndex(1).objectReferenceValue = soB;
                variantsProp.GetArrayElementAtIndex(2).objectReferenceValue = soC;

                so.FindProperty("_spawnAnchor").objectReferenceValue = anchor.transform;
                so.FindProperty("_fallbackSeed").intValue = 42;
                so.FindProperty("_roomIndex").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                UnityEditor.Selection.activeObject = slotGo;
                UnityEditor.EditorGUIUtility.PingObject(slotGo);
                Debug.Log("[Fix016] RoomSlot_Demo created in TestLevel with 3 variant SOs, SpawnAnchor at (0,0,20), seed=42.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log("[Fix016] Done. Run Play mode in TestLevel; RoomController will spawn one of 3 Demo variants at (0,0,20). Change _fallbackSeed in Inspector to pick a different variant.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/020 - Place Test Boss In TestLevel")]
        private static void Fix020()
        {
            // ---- guard: TestLevel.unity must exist ---------------------------------------------
            const string scenePath = "Assets/Scenes/TestLevel.unity";
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath)))
            {
                Debug.LogError("[Fix020] TestLevel.unity not found — run Fix 002 first.");
                return;
            }

            // ---- guard: EntityRegistry asset must exist ----------------------------------------
            const string regPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityRegistrySO>(regPath);
            if (registry == null)
            {
                Debug.LogError("[Fix020] EntityRegistry.asset not found at " + regPath + " — run Fix 001 first.");
                return;
            }

            // ---- open TestLevel scene ----------------------------------------------------------
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ---- idempotency: skip if TestBoss already exists anywhere in the scene ------------
            const string bossGoName = "TestBoss";
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == bossGoName)
                {
                    Debug.LogWarning("[Fix020] '" + bossGoName + "' already present in TestLevel — aborting to avoid duplicates.");
                    return;
                }
                // Also check one level of children (e.g. under Content).
                var child = rootGo.transform.Find(bossGoName);
                if (child != null)
                {
                    Debug.LogWarning("[Fix020] '" + bossGoName + "' already present as child in TestLevel — aborting to avoid duplicates.");
                    return;
                }
            }

            // ---- build the boss GameObject -----------------------------------------------------
            var bossGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossGo.name = bossGoName;
            bossGo.transform.position = new Vector3(0f, 1.5f, 15f);
            bossGo.transform.localScale = new Vector3(3f, 3f, 3f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bossGo, scene);

            // Tint the cube red so it's instantly recognisable.
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.8f, 0.15f, 0.1f) };
            bossGo.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Remove the Cube's default non-trigger collider and add a trigger box instead
            // (the boss receives damage via IDamageable, not physics — keep trigger for projectile hits).
            var existingCol = bossGo.GetComponent<BoxCollider>();
            if (existingCol != null) Object.DestroyImmediate(existingCol);
            var trigger = bossGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;

            // Required components: EnemyStats first (EnemyCore has [RequireComponent]).
            bossGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>();
            var bossCore = bossGo.AddComponent<BlastFrame.Gameplay.Enemies.Bosses.BossCore>();

            // Two placeholder behavior components for a two-phase boss (phase 0 = MissileTurret,
            // phase 1 = ArcPredict). Start both disabled; BossCore will enable the right one on first
            // EvaluatePhases call when the scene plays.
            var behaviorA = bossGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorMissileTurret>();
            var behaviorB = bossGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>();
            behaviorA.enabled = true;
            behaviorB.enabled = false;

            // ---- wire EntityRegistry into EnemyCore (serialized private field) -----------------
            {
                var coreSo = new SerializedObject(bossCore);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();
            }
            {
                var soA = new SerializedObject(behaviorA);
                soA.FindProperty("entityRegistry").objectReferenceValue = registry;
                soA.ApplyModifiedPropertiesWithoutUndo();
            }
            {
                var soB = new SerializedObject(behaviorB);
                soB.FindProperty("entityRegistry").objectReferenceValue = registry;
                soB.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- parent under "Content" if it exists -------------------------------------------
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == "Content")
                {
                    bossGo.transform.SetParent(rootGo.transform, true);
                    break;
                }
            }

            // ---- save scene --------------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
            Debug.Log("[Fix020] TestBoss (BossCore + EnemyStats + MissileTurret/ArcPredict behaviors) placed in " +
                      "TestLevel at (0, 1.5, 15). EntityRegistry wired. Wire phase EnemyBehaviorBase " +
                      "references in BossCore.phases via the Inspector to configure health thresholds.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/021 - Add Save Manager To Core")]
        private static void Fix021()
        {
            const string scenePath = "Assets/Scenes/Core.unity";
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath)))
            {
                Debug.LogError("[Fix021] Core.unity not found — run Fix 001 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Idempotency: abort if a SaveManager already exists anywhere in the scene.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<BlastFrame.Core.Save.SaveManager>(true) != null)
                {
                    Debug.LogWarning("[Fix021] SaveManager already present in Core — nothing to do.");
                    EditorSceneManager.CloseScene(scene, true);
                    return;
                }
            }

            var go = new GameObject("SaveManager");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BlastFrame.Core.Save.SaveManager>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Fix021] SaveManager GameObject added to Core.unity and scene saved.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/017 - Add Run Manager To Core")]
        private static void Fix017()
        {
            // ---- inline helpers ----------------------------------------------------------------
            static void EnsureAssetFolder(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            // ---- guard: Core.unity must exist --------------------------------------------------
            const string scenePath = "Assets/Scenes/Core.unity";
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath)))
            {
                Debug.LogError("[Fix017] Core.unity not found — run Fix 001 first.");
                return;
            }

            // ---- open Core scene ---------------------------------------------------------------
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ---- idempotency: abort if RunManager already present ------------------------------
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<BlastFrame.Gameplay.Levels.RunManager>(true) != null)
                {
                    Debug.LogWarning("[Fix017] RunManager already present in Core — nothing to do.");
                    EditorSceneManager.CloseScene(scene, true);
                    return;
                }
            }

            // ---- add RunManager GameObject to Core scene ---------------------------------------
            var go = new GameObject("RunManager");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BlastFrame.Gameplay.Levels.RunManager>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Fix017] RunManager GameObject added to Core.unity.");

            // ---- create Level01.asset ONLY IF MISSING ------------------------------------------
            EnsureAssetFolder("Assets/ScriptableObjects/Levels");

            const string levelAssetPath = "Assets/ScriptableObjects/Levels/Level01.asset";
            if (AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Levels.LevelDefinitionSO>(levelAssetPath) != null)
            {
                Debug.Log("[Fix017] Level01.asset already exists — skipping to preserve hand-tuned values.");
            }
            else
            {
                var levelSO = ScriptableObject.CreateInstance<BlastFrame.Gameplay.Levels.LevelDefinitionSO>();
                AssetDatabase.CreateAsset(levelSO, levelAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Write initial values via SerializedObject so Unity records proper undo/dirty.
                var serialized = new SerializedObject(levelSO);
                var levelIndexProp  = serialized.FindProperty("_levelIndex");
                var displayNameProp = serialized.FindProperty("_displayName");
                if (levelIndexProp  != null) levelIndexProp.intValue   = 0;
                if (displayNameProp != null) displayNameProp.stringValue = "Sector 1 — Assembly Bay";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.SaveAssets();
                Debug.Log("[Fix017] Level01.asset created at Assets/ScriptableObjects/Levels/ with levelIndex=0.");
                EditorGUIUtility.PingObject(levelSO);
            }
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/013 - Build HUD In Core")]
        private static void Fix013()
        {
            // ---- inline helpers ----------------------------------------------------------------
            static void EnsureAssetFolder(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            // ---- guard: EntityRegistry ---------------------------------------------------------
            EnsureAssetFolder("Assets/ScriptableObjects/Entities");
            const string regPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(regPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<EntityRegistrySO>();
                AssetDatabase.CreateAsset(registry, regPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Fix013] Created EntityRegistry.asset.");
            }

            // ---- open Core scene ---------------------------------------------------------------
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            // ---- idempotency: skip if HUD Canvas already present ------------------------------
            foreach (var root in scene.GetRootGameObjects())
            {
                var existingCanvas = root.GetComponent<Canvas>();
                if (existingCanvas != null && root.name == "HUD")
                {
                    Debug.LogWarning("[Fix013] HUD Canvas already present in Core — aborting to avoid duplicates.");
                    return;
                }
            }

            // ---- EventSystem (InputSystem) — create only if none exists ----------------------
            bool hasEventSystem = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
                { hasEventSystem = true; break; }
            }
            if (!hasEventSystem)
            {
                var esGo = new GameObject("EventSystem");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(esGo, scene);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // ---- HUD Canvas --------------------------------------------------------------------
            var canvasGo = new GameObject("HUD");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasGo, scene);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            canvasGo.AddComponent<BlastFrame.UI.UIManager>();
            var hudController = canvasGo.AddComponent<BlastFrame.UI.HUDController>();

            // ---- HealthText widget (top-left) --------------------------------------------------
            var healthGo  = new GameObject("HealthText");
            healthGo.transform.SetParent(canvasGo.transform, false);
            var healthDisplay = healthGo.AddComponent<BlastFrame.UI.HealthDisplay>();

            var healthRT          = healthGo.AddComponent<UnityEngine.RectTransform>();
            healthRT.anchorMin    = new Vector2(0f, 1f);
            healthRT.anchorMax    = new Vector2(0f, 1f);
            healthRT.pivot        = new Vector2(0f, 1f);
            healthRT.anchoredPosition = new Vector2(30f, -30f);
            healthRT.sizeDelta    = new Vector2(120f, 60f);

            var healthTMP         = healthGo.AddComponent<TMPro.TextMeshProUGUI>();
            healthTMP.text        = "5";
            healthTMP.fontSize    = 42f;
            healthTMP.alignment   = TMPro.TextAlignmentOptions.Left;
            healthTMP.color       = Color.white;

            // ---- DashRing widget (bottom-left) -------------------------------------------------
            var dashGo  = new GameObject("DashRing");
            dashGo.transform.SetParent(canvasGo.transform, false);
            var dashCooldownUI = dashGo.AddComponent<BlastFrame.UI.DashCooldownUI>();

            var dashRT            = dashGo.AddComponent<UnityEngine.RectTransform>();
            dashRT.anchorMin      = new Vector2(0f, 0f);
            dashRT.anchorMax      = new Vector2(0f, 0f);
            dashRT.pivot          = new Vector2(0f, 0f);
            dashRT.anchoredPosition = new Vector2(30f, 30f);
            dashRT.sizeDelta      = new Vector2(64f, 64f);

            var dashImg           = dashGo.AddComponent<UnityEngine.UI.Image>();
            dashImg.type          = UnityEngine.UI.Image.Type.Filled;
            dashImg.fillMethod    = UnityEngine.UI.Image.FillMethod.Radial360;
            dashImg.fillClockwise = true;
            dashImg.fillAmount    = 1f;
            dashImg.color         = new Color(0.3f, 0.8f, 1f, 1f);

            // ---- ChargeBar widget (bottom-center) -----------------------------------------------
            var chargeGo  = new GameObject("ChargeBar");
            chargeGo.transform.SetParent(canvasGo.transform, false);
            var chargeBarUI = chargeGo.AddComponent<BlastFrame.UI.ChargeBarUI>();

            var chargeRT          = chargeGo.AddComponent<UnityEngine.RectTransform>();
            chargeRT.anchorMin    = new Vector2(0.5f, 0f);
            chargeRT.anchorMax    = new Vector2(0.5f, 0f);
            chargeRT.pivot        = new Vector2(0.5f, 0f);
            chargeRT.anchoredPosition = new Vector2(0f, 30f);
            chargeRT.sizeDelta    = new Vector2(300f, 24f);

            var chargeImg         = chargeGo.AddComponent<UnityEngine.UI.Image>();
            chargeImg.type        = UnityEngine.UI.Image.Type.Filled;
            chargeImg.fillMethod  = UnityEngine.UI.Image.FillMethod.Horizontal;
            chargeImg.fillAmount  = 0f;
            chargeImg.color       = new Color(1f, 0.7f, 0.1f, 1f);
            // ChargeBarUI.SetVisible(false) hides it at runtime when charge == 0; start inactive in editor too.
            chargeGo.SetActive(false);

            // ---- wire fields via SerializedObject ----------------------------------------------
            // HUDController
            {
                var so = new SerializedObject(hudController);
                so.FindProperty("entityRegistry").objectReferenceValue  = registry;
                so.FindProperty("healthDisplay").objectReferenceValue   = healthDisplay;
                so.FindProperty("dashCooldownUI").objectReferenceValue  = dashCooldownUI;
                so.FindProperty("chargeBarUI").objectReferenceValue     = chargeBarUI;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // HealthDisplay
            {
                var so = new SerializedObject(healthDisplay);
                so.FindProperty("entityRegistry").objectReferenceValue = registry;
                so.FindProperty("healthText").objectReferenceValue     = healthTMP;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // DashCooldownUI
            {
                var so = new SerializedObject(dashCooldownUI);
                so.FindProperty("entityRegistry").objectReferenceValue = registry;
                so.FindProperty("dashRingImage").objectReferenceValue  = dashImg;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // ChargeBarUI
            {
                var so = new SerializedObject(chargeBarUI);
                so.FindProperty("entityRegistry").objectReferenceValue = registry;
                so.FindProperty("chargeBarImage").objectReferenceValue = chargeImg;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- save --------------------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            Debug.Log("[Fix013] HUD Canvas built in Core: HealthText (top-left), DashRing (bottom-left), ChargeBar (bottom-center). EntityRegistry wired to all widgets.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/018 - Add Currency Manager To Core")]
        private static void Fix018()
        {
            const string scenePath = "Assets/Scenes/Core.unity";
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath)))
            {
                Debug.LogError("[Fix018] Core.unity not found — run Fix 001 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Idempotency: abort if a CurrencyManager already exists anywhere in the scene.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<BlastFrame.Gameplay.Economy.CurrencyManager>(true) != null)
                {
                    Debug.LogWarning("[Fix018] CurrencyManager already present in Core — nothing to do.");
                    EditorSceneManager.CloseScene(scene, false);
                    return;
                }
            }

            var go = new GameObject("CurrencyManager");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BlastFrame.Gameplay.Economy.CurrencyManager>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Fix018] CurrencyManager GameObject added to Core.unity and scene saved.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/019 - Build HQ Shop Demo")]
        private static void Fix019()
        {
            // ---- inline helpers ----------------------------------------------------------------
            static void EnsureFolder019(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            // Creates a PermanentUpgradeSO only if it does not already exist.
            // NEVER overwrites an existing asset — preserves all tuned cost/magnitude values.
            static BlastFrame.Gameplay.HQ.PermanentUpgradeSO EnsureUpgrade(
                string assetPath,
                string id, string displayName, string description,
                int cost, float magnitude,
                BlastFrame.Gameplay.HQ.PermanentUpgradeEffect effect)
            {
                var existing = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.HQ.PermanentUpgradeSO>(assetPath);
                if (existing != null)
                {
                    Debug.Log($"[Fix019] '{assetPath}' already exists — skipping to preserve tuned values.");
                    return existing;
                }

                var so = ScriptableObject.CreateInstance<BlastFrame.Gameplay.HQ.PermanentUpgradeSO>();
                AssetDatabase.CreateAsset(so, assetPath);

                var sobj = new SerializedObject(so);
                sobj.FindProperty("id").stringValue          = id;
                sobj.FindProperty("displayName").stringValue = displayName;
                sobj.FindProperty("description").stringValue = description;
                sobj.FindProperty("effect").enumValueIndex   = (int)effect;

                // cost: IntReference — UseConstant true, ConstantValue = cost
                var costProp = sobj.FindProperty("cost");
                costProp.FindPropertyRelative("UseConstant").boolValue    = true;
                costProp.FindPropertyRelative("ConstantValue").intValue   = cost;

                // magnitude: FloatReference — UseConstant true, ConstantValue = magnitude
                var magProp = sobj.FindProperty("magnitude");
                magProp.FindPropertyRelative("UseConstant").boolValue    = true;
                magProp.FindPropertyRelative("ConstantValue").floatValue = magnitude;

                sobj.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Fix019] Created '{assetPath}' (id={id}, cost={cost}, magnitude={magnitude}).");
                return so;
            }

            // ---- step 1: ensure folder ---------------------------------------------------------
            EnsureFolder019("Assets/ScriptableObjects/Permanents");

            // ---- step 2: sample upgrade assets (create-if-missing only) -----------------------
            var extraHealth = EnsureUpgrade(
                "Assets/ScriptableObjects/Permanents/ExtraHealth.asset",
                id:          "ExtraHealth",
                displayName: "Extra Health",
                description: "Start each run with +1 maximum health.",
                cost:        50,
                magnitude:   1f,
                effect:      BlastFrame.Gameplay.HQ.PermanentUpgradeEffect.MaxHealthBonus);

            var fasterDash = EnsureUpgrade(
                "Assets/ScriptableObjects/Permanents/FasterDash.asset",
                id:          "FasterDash",
                displayName: "Faster Dash",
                description: "Reduces dash cooldown by 1 second.",
                cost:        75,
                magnitude:   1f,
                effect:      BlastFrame.Gameplay.HQ.PermanentUpgradeEffect.DashCooldownReduce);

            // ---- step 3: PermanentUpgradeRegistrySO (create-if-missing; populate if empty) ----
            const string regPath = "Assets/ScriptableObjects/Permanents/PermanentRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.HQ.PermanentUpgradeRegistrySO>(regPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<BlastFrame.Gameplay.HQ.PermanentUpgradeRegistrySO>();
                AssetDatabase.CreateAsset(registry, regPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Fix019] Created PermanentRegistry.asset.");
            }

            // Populate the list only if it is currently empty — never overwrite a hand-curated list.
            var regSo = new SerializedObject(registry);
            var upgradesProp = regSo.FindProperty("upgrades");
            if (upgradesProp.arraySize == 0)
            {
                upgradesProp.arraySize = 2;
                upgradesProp.GetArrayElementAtIndex(0).objectReferenceValue = extraHealth;
                upgradesProp.GetArrayElementAtIndex(1).objectReferenceValue = fasterDash;
                regSo.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[Fix019] Populated PermanentRegistry.asset with ExtraHealth + FasterDash.");
            }
            else
            {
                Debug.Log("[Fix019] PermanentRegistry.asset already has entries — skipping list population.");
            }

            // ---- step 4: wire ShopManager in Core scene ----------------------------------------
            const string scenePath = "Assets/Scenes/Core.unity";
            if (!File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath)))
            {
                Debug.LogError("[Fix019] Core.unity not found — run Fix 001 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Find or create a ShopManager GameObject in Core.
            BlastFrame.Gameplay.HQ.ShopManager shopMgr = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                shopMgr = root.GetComponentInChildren<BlastFrame.Gameplay.HQ.ShopManager>(true);
                if (shopMgr != null) break;
            }

            if (shopMgr == null)
            {
                var smGo = new GameObject("ShopManager");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(smGo, scene);
                shopMgr = smGo.AddComponent<BlastFrame.Gameplay.HQ.ShopManager>();
                Debug.Log("[Fix019] Created ShopManager GameObject in Core.");
            }

            // Wire registry only if the field is currently null (never overwrite a hand-set value).
            var smSo = new SerializedObject(shopMgr);
            var regProp = smSo.FindProperty("registry");
            if (regProp.objectReferenceValue == null)
            {
                regProp.objectReferenceValue = registry;
                smSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Fix019] Wired PermanentRegistry.asset into ShopManager.registry.");
            }
            else
            {
                Debug.Log("[Fix019] ShopManager.registry already assigned — leaving as-is.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);
            Debug.Log("[Fix019] HQ Shop Demo complete: ExtraHealth.asset + FasterDash.asset + PermanentRegistry.asset + ShopManager in Core.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/009 - Place Test Platforms In TestLevel")]
        private static void Fix009()
        {
            bool SceneExists(string path) => System.IO.File.Exists(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), path));

            Material GetOrCreateMat(Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                return new Material(shader) { color = color };
            }

            Transform FindOrNullContent(Scene s)
            {
                foreach (var root in s.GetRootGameObjects())
                    if (root.name == "Content") return root.transform;
                return null;
            }

            bool AlreadyExists(Scene s, string goName)
            {
                foreach (var root in s.GetRootGameObjects())
                {
                    if (root.name == goName) return true;
                    foreach (Transform child in root.transform)
                        if (child.name == goName) return true;
                }
                return false;
            }

            GameObject MakeCubePlatform(string goName, Vector3 pos, Vector3 scale, Material mat, Transform parent)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = goName;
                go.transform.position = pos;
                go.transform.localScale = scale;
                if (go.TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterial = mat;
                if (parent != null) go.transform.SetParent(parent, true);
                return go;
            }

            void SetupKinematicRb(GameObject go)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            Transform MakeWaypoint(string wpName, Transform parent, Vector3 localPos)
            {
                var wp = new GameObject(wpName);
                wp.transform.SetParent(parent, false);
                wp.transform.localPosition = localPos;
                return wp.transform;
            }

            const string scenePath = "Assets/Scenes/TestLevel.unity";
            if (!SceneExists(scenePath))
            {
                Debug.LogError("[Fix009] TestLevel.unity not found. Run Fix 002 first to create it.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Transform content = FindOrNullContent(scene);

            const string movingName = "TestMovingPlatform";
            if (!AlreadyExists(scene, movingName))
            {
                var mat = GetOrCreateMat(new Color(0.3f, 0.55f, 0.85f));
                var go = MakeCubePlatform(movingName, new Vector3(0f, 0.5f, 3f), new Vector3(3f, 0.3f, 3f), mat, content);
                SetupKinematicRb(go);
                var platform = go.AddComponent<BlastFrame.Gameplay.Platforms.MovingPlatform>();

                var wp0 = MakeWaypoint("Waypoint_0", go.transform, Vector3.zero);
                var wp1 = MakeWaypoint("Waypoint_1", go.transform, new Vector3(0f, 0f, 8f));

                var so = new SerializedObject(platform);
                var wps = so.FindProperty("waypoints");
                wps.arraySize = 2;
                wps.GetArrayElementAtIndex(0).objectReferenceValue = wp0;
                wps.GetArrayElementAtIndex(1).objectReferenceValue = wp1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Fix009] Created TestMovingPlatform.");
            }
            else Debug.Log("[Fix009] TestMovingPlatform already exists — skipped.");

            const string rotatingName = "TestRotatingPlatform";
            if (!AlreadyExists(scene, rotatingName))
            {
                var mat = GetOrCreateMat(new Color(0.85f, 0.55f, 0.2f));
                var go = MakeCubePlatform(rotatingName, new Vector3(6f, 0.5f, 6f), new Vector3(4f, 0.3f, 4f), mat, content);
                SetupKinematicRb(go);
                go.AddComponent<BlastFrame.Gameplay.Platforms.RotatingPlatform>();
                Debug.Log("[Fix009] Created TestRotatingPlatform.");
            }
            else Debug.Log("[Fix009] TestRotatingPlatform already exists — skipped.");

            const string treadmillName = "TestTreadmillPlatform";
            if (!AlreadyExists(scene, treadmillName))
            {
                var mat = GetOrCreateMat(new Color(0.2f, 0.75f, 0.35f));
                var go = MakeCubePlatform(treadmillName, new Vector3(-6f, 0.5f, 9f), new Vector3(3f, 0.3f, 6f), mat, content);
                go.AddComponent<BlastFrame.Gameplay.Platforms.TreadmillPlatform>();
                Debug.Log("[Fix009] Created TestTreadmillPlatform.");
            }
            else Debug.Log("[Fix009] TestTreadmillPlatform already exists — skipped.");

            const string springName = "TestSpringBoard";
            if (!AlreadyExists(scene, springName))
            {
                var mat = GetOrCreateMat(new Color(0.9f, 0.2f, 0.25f));
                var go = MakeCubePlatform(springName, new Vector3(0f, 0.5f, 13f), new Vector3(2f, 0.3f, 2f), mat, content);
                go.AddComponent<BlastFrame.Gameplay.Platforms.SpringBoard>();
                Debug.Log("[Fix009] Created TestSpringBoard.");
            }
            else Debug.Log("[Fix009] TestSpringBoard already exists — skipped.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Fix009] TestLevel saved with all four test platforms.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/010 - Add Audio Manager To Core")]
        private static void Fix010()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<BlastFrame.Audio.AudioManager>() != null)
            {
                Debug.LogWarning("[Fix010] AudioManager already exists in Core — nothing to do.");
                return;
            }

            var go = new GameObject("AudioManager");
            go.AddComponent<BlastFrame.Audio.AudioManager>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Fix010] AudioManager added to Core. MANUAL: assign an AudioMixer asset + SFX/Music groups " +
                      "in the Inspector (mixer creation is not scriptable). Expose params MasterVolume/MusicVolume/SfxVolume.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/014 - Place Test Turrets In TestLevel")]
        private static void Fix014()
        {
            static void EnsureAssetFolder(string assetPath)
            {
                var parts = assetPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            static GameObject BuildProjectilePrefab014(string path, string compTypeName)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = System.IO.Path.GetFileNameWithoutExtension(path);
                go.transform.localScale = Vector3.one * 0.3f;

                if (!go.TryGetComponent(out Rigidbody rb)) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                if (!go.TryGetComponent(out SphereCollider col)) col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;

                var type = System.Type.GetType(compTypeName + ", Assembly-CSharp");
                if (type != null) go.AddComponent(type);

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                return prefab;
            }

            static BlastFrame.Core.Entities.EntityDefinitionSO EnsureEntityDef014(
                string assetPath, string id, GameObject prefab)
            {
                var existing = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityDefinitionSO>(assetPath);
                if (existing != null) return existing;

                var so = ScriptableObject.CreateInstance<BlastFrame.Core.Entities.EntityDefinitionSO>();
                AssetDatabase.CreateAsset(so, assetPath);
                var ser = new SerializedObject(so);
                ser.FindProperty("id").stringValue = id;
                ser.FindProperty("prefab").objectReferenceValue = prefab;
                ser.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return so;
            }

            static bool HasPoolEntry014(SerializedProperty entriesProp, string defId)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    var defProp = elem.FindPropertyRelative("definition");
                    if (defProp.objectReferenceValue is BlastFrame.Core.Entities.EntityDefinitionSO d && d.Id == defId)
                        return true;
                }
                return false;
            }

            static void AddPoolEntry014(SerializedProperty entriesProp,
                BlastFrame.Core.Entities.EntityDefinitionSO def, int prewarm, int expand)
            {
                int idx = entriesProp.arraySize;
                entriesProp.InsertArrayElementAtIndex(idx);
                var elem = entriesProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("definition").objectReferenceValue = def;
                elem.FindPropertyRelative("prewarmCount").intValue = prewarm;
                elem.FindPropertyRelative("expandIncrement").intValue = expand;
            }

            EnsureAssetFolder("Assets/Prefabs/Projectiles");
            EnsureAssetFolder("Assets/ScriptableObjects/Entities");
            EnsureAssetFolder("Assets/ScriptableObjects/Pooling");

            const string missilePrefabPath = "Assets/Prefabs/Projectiles/EnemyMissile.prefab";
            const string arcPrefabPath     = "Assets/Prefabs/Projectiles/ArcProjectile.prefab";

            var missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(missilePrefabPath)
                                ?? BuildProjectilePrefab014(missilePrefabPath, "BlastFrame.Gameplay.Projectiles.EnemyMissile");
            var arcPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(arcPrefabPath)
                                ?? BuildProjectilePrefab014(arcPrefabPath, "BlastFrame.Gameplay.Projectiles.ArcProjectile");

            var missileDef = EnsureEntityDef014("Assets/ScriptableObjects/Entities/EnemyMissile.asset", BlastFrame.Core.PoolIds.EnemyMissile, missilePrefab);
            var arcDef     = EnsureEntityDef014("Assets/ScriptableObjects/Entities/ArcProjectile.asset", BlastFrame.Core.PoolIds.ArcProjectile, arcPrefab);

            const string poolConfigPath = "Assets/ScriptableObjects/Pooling/PoolConfig.asset";
            var poolConfig = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Pooling.PoolConfigSO>(poolConfigPath);
            if (poolConfig == null)
            {
                poolConfig = ScriptableObject.CreateInstance<BlastFrame.Core.Pooling.PoolConfigSO>();
                AssetDatabase.CreateAsset(poolConfig, poolConfigPath);
                AssetDatabase.SaveAssets();
            }

            var pcSo = new SerializedObject(poolConfig);
            var entriesProp = pcSo.FindProperty("entries");
            if (!HasPoolEntry014(entriesProp, BlastFrame.Core.PoolIds.EnemyMissile)) AddPoolEntry014(entriesProp, missileDef, 12, 6);
            if (!HasPoolEntry014(entriesProp, BlastFrame.Core.PoolIds.ArcProjectile)) AddPoolEntry014(entriesProp, arcDef, 8, 4);
            pcSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var coreScene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Additive);
            BlastFrame.Core.Pooling.PoolManager poolMgr = null;
            foreach (var root in coreScene.GetRootGameObjects())
            {
                poolMgr = root.GetComponentInChildren<BlastFrame.Core.Pooling.PoolManager>(true);
                if (poolMgr != null) break;
            }
            if (poolMgr == null)
            {
                var pmGo = new GameObject("PoolManager");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pmGo, coreScene);
                poolMgr = pmGo.AddComponent<BlastFrame.Core.Pooling.PoolManager>();
            }
            var pmSo = new SerializedObject(poolMgr);
            var configProp = pmSo.FindProperty("config");
            if (configProp.objectReferenceValue == null)
            {
                configProp.objectReferenceValue = poolConfig;
                pmSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(coreScene);
            EditorSceneManager.SaveScene(coreScene);
            EditorSceneManager.CloseScene(coreScene, true);

            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityRegistrySO>(
                "Assets/ScriptableObjects/Entities/EntityRegistry.asset");

            var testScene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);
            Transform contentParent = null;
            foreach (var root in testScene.GetRootGameObjects())
                if (root.name == "Content") { contentParent = root.transform; break; }

            GameObject PlaceTurret014(string goName, Vector3 position, System.Type behaviorType)
            {
                foreach (var root in testScene.GetRootGameObjects())
                {
                    if (root.name == goName) return null;
                    if (contentParent != null && contentParent.Find(goName) != null) return null;
                }

                var turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                turret.name = goName;
                turret.transform.position = position;
                turret.transform.localScale = new Vector3(1f, 0.5f, 1f);

                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrel.name = "Barrel";
                barrel.transform.SetParent(turret.transform, false);
                barrel.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
                barrel.transform.localScale = new Vector3(0.2f, 0.2f, 0.8f);

                var muzzleGo = new GameObject("Muzzle");
                muzzleGo.transform.SetParent(barrel.transform, false);
                muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);

                turret.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>();
                var core = turret.AddComponent<BlastFrame.Gameplay.Enemies.EnemyCore>();
                var behavior = turret.AddComponent(behaviorType) as MonoBehaviour;

                var coreSo = new SerializedObject(core);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();

                var behSo = new SerializedObject(behavior);
                behSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                behSo.FindProperty("muzzle").objectReferenceValue = muzzleGo.transform;
                behSo.ApplyModifiedPropertiesWithoutUndo();

                if (contentParent != null) turret.transform.SetParent(contentParent, true);
                return turret;
            }

            PlaceTurret014("MissileTurret_Test", new Vector3(-8f, 1f, 8f), typeof(BlastFrame.Gameplay.Enemies.EnemyBehaviorMissileTurret));
            PlaceTurret014("ArcPredictTurret_Test", new Vector3(8f, 1f, 8f), typeof(BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict));

            EditorSceneManager.MarkSceneDirty(testScene);
            EditorSceneManager.SaveScene(testScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = poolConfig;
            EditorGUIUtility.PingObject(poolConfig);
            Debug.Log("[Fix014] Enemy projectiles, pools, PoolManager, and two test turrets placed. Idempotent.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/022 - Create Audio Mixer And Wire AudioManager")]
        private static void Fix022()
        {
            // AudioMixer assets cannot be created via public API; this uses the editor's internal
            // AudioMixerController (the same code path as Assets > Create > Audio Mixer). If Unity
            // renames the internals, this fix bails with an error instead of half-creating assets.
            const string mixerPath = "Assets/Audio/GameAudioMixer.mixer";

            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
                AssetDatabase.CreateFolder("Assets", "Audio");

            var editorAsm = typeof(UnityEditor.Editor).Assembly;
            var controllerType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var exposedParamType = editorAsm.GetType("UnityEditor.Audio.ExposedAudioParameter");
            if (controllerType == null || groupType == null || exposedParamType == null)
            {
                Debug.LogError("[Fix022] Internal AudioMixerController API not found in this Unity version — create the mixer by hand (see Fix010 log).");
                return;
            }

            // ---- step 1: mixer asset with Master group (factory creates master + snapshot) ------
            var controller = AssetDatabase.LoadMainAssetAtPath(mixerPath);
            if (controller == null)
            {
                var factory = controllerType.GetMethod("CreateMixerControllerAtPath",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (factory == null)
                {
                    Debug.LogError("[Fix022] AudioMixerController.CreateMixerControllerAtPath not found — create the mixer by hand.");
                    return;
                }
                controller = factory.Invoke(null, new object[] { mixerPath }) as Object;
                AssetDatabase.ImportAsset(mixerPath);
                controller = AssetDatabase.LoadMainAssetAtPath(mixerPath);
                Debug.Log($"[Fix022] Created {mixerPath}.");
            }
            if (controller == null)
            {
                Debug.LogError("[Fix022] Mixer asset could not be created/loaded — aborting.");
                return;
            }

            var masterGroup = controllerType.GetProperty("masterGroup")?.GetValue(controller) as Object;
            if (masterGroup == null)
            {
                Debug.LogError("[Fix022] Mixer has no master group — aborting.");
                return;
            }

            // ---- step 2: Music + SFX child groups (idempotent by name) -------------------------
            Object EnsureGroup022(string name)
            {
                var mixerAsset = (UnityEngine.Audio.AudioMixer)controller;
                foreach (var g in mixerAsset.FindMatchingGroups(name))
                    if (g.name == name) return g;

                var createGroup = controllerType.GetMethod("CreateNewGroup");
                var addChild = controllerType.GetMethod("AddChildToParent");
                var group = createGroup.Invoke(controller, new object[] { name, false }) as Object;
                addChild.Invoke(controller, new object[] { group, masterGroup });

                // Persist group + its effect sub-assets into the .mixer asset if the internal
                // call didn't already do it.
                if (!AssetDatabase.Contains(group))
                    AssetDatabase.AddObjectToAsset(group, controller);
                if (groupType.GetProperty("effects")?.GetValue(group) is Object[] effects)
                    foreach (var fx in effects)
                        if (fx != null && !AssetDatabase.Contains(fx))
                            AssetDatabase.AddObjectToAsset(fx, controller);

                Debug.Log($"[Fix022] Added '{name}' group under Master.");
                return group;
            }

            var musicGroup = EnsureGroup022("Music");
            var sfxGroup = EnsureGroup022("SFX");

            // ---- step 3: exposed volume params (MasterVolume / MusicVolume / SfxVolume) --------
            var getGuidForVolume = groupType.GetMethod("GetGUIDForVolume");
            var exposedProp = controllerType.GetProperty("exposedParameters");
            var guidField = exposedParamType.GetField("guid");
            var nameField = exposedParamType.GetField("name");
            if (getGuidForVolume == null || exposedProp == null || guidField == null || nameField == null)
            {
                Debug.LogError("[Fix022] Exposed-parameter internals not found — expose MasterVolume/MusicVolume/SfxVolume by hand (right-click each group's Volume).");
                return;
            }

            var current = (System.Collections.IEnumerable)exposedProp.GetValue(controller);
            var list = new System.Collections.Generic.List<object>();
            var existingNames = new System.Collections.Generic.HashSet<string>();
            foreach (var p in current) { list.Add(p); existingNames.Add((string)nameField.GetValue(p)); }

            void Expose022(Object group, string paramName)
            {
                if (existingNames.Contains(paramName)) return;
                var guid = getGuidForVolume.Invoke(group, null);
                var p = System.Activator.CreateInstance(exposedParamType);
                guidField.SetValue(p, guid);
                nameField.SetValue(p, paramName);
                list.Add(p);
                Debug.Log($"[Fix022] Exposed '{paramName}'.");
            }

            Expose022(masterGroup, BlastFrame.Core.AudioMixerParams.MasterVolume);
            Expose022(musicGroup, BlastFrame.Core.AudioMixerParams.MusicVolume);
            Expose022(sfxGroup, BlastFrame.Core.AudioMixerParams.SfxVolume);

            var arr = System.Array.CreateInstance(exposedParamType, list.Count);
            for (int i = 0; i < list.Count; i++) arr.SetValue(list[i], i);
            exposedProp.SetValue(controller, arr);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // ---- step 4: wire AudioManager in Core (only fills null fields) --------------------
            var coreScene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
            BlastFrame.Audio.AudioManager audioMgr = null;
            foreach (var root in coreScene.GetRootGameObjects())
            {
                audioMgr = root.GetComponentInChildren<BlastFrame.Audio.AudioManager>(true);
                if (audioMgr != null) break;
            }
            if (audioMgr == null)
            {
                Debug.LogError("[Fix022] No AudioManager in Core — run Fix 010 first.");
                return;
            }

            var amSo = new SerializedObject(audioMgr);
            var mixerProp = amSo.FindProperty("mixer");
            var sfxProp = amSo.FindProperty("sfxGroup");
            var musicProp = amSo.FindProperty("musicGroup");
            if (mixerProp.objectReferenceValue == null) mixerProp.objectReferenceValue = controller;
            if (sfxProp.objectReferenceValue == null) sfxProp.objectReferenceValue = sfxGroup;
            if (musicProp.objectReferenceValue == null) musicProp.objectReferenceValue = musicGroup;
            amSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(coreScene);
            EditorSceneManager.SaveScene(coreScene);

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
            Debug.Log("[Fix022] GameAudioMixer (Master/Music/SFX, MasterVolume/MusicVolume/SfxVolume exposed) wired into Core's AudioManager.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/023 - Default Boss Phases On TestBoss")]
        private static void Fix023()
        {
            // Fills BossCore.phases on TestBoss ONLY if the list is empty — never overwrites
            // hand-tuned phase setups. Phase 0 (full health): missile turret behavior.
            // Phase 1 (≤50% health): missile + arc-predict together (escalation).
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);

            GameObject bossGo = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "TestBoss") { bossGo = root; break; }
                var t = root.transform.Find("TestBoss");
                if (t != null) { bossGo = t.gameObject; break; }
            }
            if (bossGo == null || !bossGo.TryGetComponent(out BlastFrame.Gameplay.Enemies.Bosses.BossCore bossCore))
            {
                Debug.LogError("[Fix023] TestBoss with BossCore not found in TestLevel — run Fix 020 first.");
                return;
            }

            var missile = bossGo.GetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorMissileTurret>();
            var arc = bossGo.GetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>();
            if (missile == null || arc == null)
            {
                Debug.LogError("[Fix023] TestBoss is missing its turret behaviors — run Fix 020 first.");
                return;
            }

            var so = new SerializedObject(bossCore);
            var phasesProp = so.FindProperty("phases");
            if (phasesProp.arraySize > 0)
            {
                Debug.LogWarning("[Fix023] TestBoss already has phases configured — leaving them untouched.");
                return;
            }

            void AddPhase023(float fraction, Object[] behaviors)
            {
                int idx = phasesProp.arraySize;
                phasesProp.InsertArrayElementAtIndex(idx);
                var elem = phasesProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("healthFraction").floatValue = fraction;
                var listProp = elem.FindPropertyRelative("behaviorsToEnable");
                listProp.ClearArray();
                for (int i = 0; i < behaviors.Length; i++)
                {
                    listProp.InsertArrayElementAtIndex(i);
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = behaviors[i];
                }
            }

            AddPhase023(1f, new Object[] { missile });
            AddPhase023(0.5f, new Object[] { missile, arc });
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
            Debug.Log("[Fix023] TestBoss phases set: 100% = missile turret, ≤50% = missile + arc-predict.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/025 - Force-Wire RoomSlot Demo Variants (reflection)")]
        private static void Fix025()
        {
            // Fix024 set _variants via SerializedObject but the saves didn't persist to YAML.
            // This fix bypasses SerializedObject entirely: uses reflection to write directly into
            // the managed List<T>, then calls SetDirty + SaveScene so Unity re-serializes from the
            // managed state — guaranteed to round-trip correctly.
            var soA = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantA.asset");
            var soB = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantB.asset");
            var soC = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantC.asset");
            if (soA == null || soB == null || soC == null)
            {
                Debug.LogError("[Fix025] Demo variant SOs not found — run Fix016 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);
            BlastFrame.Gameplay.Rooms.RoomController controller = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                controller = root.GetComponentInChildren<BlastFrame.Gameplay.Rooms.RoomController>(true);
                if (controller != null) break;
            }
            if (controller == null)
            {
                Debug.LogError("[Fix025] No RoomController in TestLevel — run Fix016 first.");
                return;
            }

            // Direct reflection write — bypasses SerializedObject round-trip issues.
            var field = typeof(BlastFrame.Gameplay.Rooms.RoomController)
                .GetField("_variants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError("[Fix025] Could not reflect _variants field on RoomController — field may have been renamed.");
                return;
            }

            var list = (System.Collections.Generic.List<BlastFrame.Gameplay.Rooms.RoomVariantSO>)field.GetValue(controller);
            list.Clear();
            list.Add(soA);
            list.Add(soB);
            list.Add(soC);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = controller.gameObject;
            EditorGUIUtility.PingObject(controller.gameObject);
            Debug.Log("[Fix025] _variants force-wired via reflection: [0]=Demo_VariantA, [1]=Demo_VariantB, [2]=Demo_VariantC. TestLevel saved.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/024 - Rewire RoomSlot Demo Variants")]
        private static void Fix024()
        {
            // Fix016's _variants wiring did not persist (scene saved with 3 null entries) and its
            // idempotency guard skips rewiring on re-run. This fix fills ONLY null slots with the
            // three Demo variant SOs — designer-replaced variants are left untouched.
            var soA = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantA.asset");
            var soB = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantB.asset");
            var soC = AssetDatabase.LoadAssetAtPath<BlastFrame.Gameplay.Rooms.RoomVariantSO>("Assets/ScriptableObjects/RoomVariants/Demo_VariantC.asset");
            if (soA == null || soB == null || soC == null)
            {
                Debug.LogError("[Fix024] Demo variant SOs not found — run Fix 016 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);
            BlastFrame.Gameplay.Rooms.RoomController controller = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                controller = root.GetComponentInChildren<BlastFrame.Gameplay.Rooms.RoomController>(true);
                if (controller != null) break;
            }
            if (controller == null)
            {
                Debug.LogError("[Fix024] No RoomController in TestLevel — run Fix 016 first.");
                return;
            }

            var so = new SerializedObject(controller);
            var variantsProp = so.FindProperty("_variants");
            if (variantsProp.arraySize != 3) variantsProp.arraySize = 3;

            var sos = new Object[] { soA, soB, soC };
            int wired = 0;
            for (int i = 0; i < 3; i++)
            {
                var elem = variantsProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue == null)
                {
                    elem.objectReferenceValue = sos[i];
                    wired++;
                }
            }

            if (wired == 0)
            {
                Debug.Log("[Fix024] All 3 variant slots already wired — nothing to do.");
                return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = controller.gameObject;
            EditorGUIUtility.PingObject(controller.gameObject);
            Debug.Log($"[Fix024] Rewired {wired} null variant slot(s) on RoomSlot_Demo and saved TestLevel.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/026 - Disable VSync on Core GameManager (FPS cap instead)")]
        private static void Fix026()
        {
            // VSync's frame pacing was adding mouse-look latency. The code default is now 0, but the
            // GameManager instance in Core.unity still has vSyncCount=1 serialized from when the field
            // was added. This sets the live scene value to 0 so the frame-rate cap is used instead.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            BlastFrame.Core.GameManager gm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                gm = root.GetComponentInChildren<BlastFrame.Core.GameManager>(true);
                if (gm != null) break;
            }
            if (gm == null)
            {
                Debug.LogError("[Fix026] No GameManager in Core.unity — run Fix 001 first.");
                return;
            }

            var so = new SerializedObject(gm);
            var vsyncProp = so.FindProperty("vSyncCount");
            if (vsyncProp == null)
            {
                Debug.LogError("[Fix026] 'vSyncCount' field not found on GameManager — field may have been renamed.");
                return;
            }

            if (vsyncProp.intValue == 0)
            {
                Debug.Log("[Fix026] GameManager vSyncCount already 0 — nothing to do.");
                return;
            }

            vsyncProp.intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = gm.gameObject;
            EditorGUIUtility.PingObject(gm.gameObject);
            Debug.Log("[Fix026] GameManager vSyncCount set to 0 in Core.unity (frame-rate cap now governs).");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/027 - Set Core GameManager FPS cap to 60")]
        private static void Fix027()
        {
            // The GameManager instance in Core.unity has targetFrameRate=144 serialized; the code
            // default is now 60 but that does not touch the existing scene value. This sets the live
            // scene value to 60 so the frame-rate cap is 60 FPS.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            BlastFrame.Core.GameManager gm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                gm = root.GetComponentInChildren<BlastFrame.Core.GameManager>(true);
                if (gm != null) break;
            }
            if (gm == null)
            {
                Debug.LogError("[Fix027] No GameManager in Core.unity — run Fix 001 first.");
                return;
            }

            var so = new SerializedObject(gm);
            var fpsProp = so.FindProperty("targetFrameRate");
            if (fpsProp == null)
            {
                Debug.LogError("[Fix027] 'targetFrameRate' field not found on GameManager — field may have been renamed.");
                return;
            }

            if (fpsProp.intValue == 60)
            {
                Debug.Log("[Fix027] GameManager targetFrameRate already 60 — nothing to do.");
                return;
            }

            fpsProp.intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = gm.gameObject;
            EditorGUIUtility.PingObject(gm.gameObject);
            Debug.Log("[Fix027] GameManager targetFrameRate set to 60 in Core.unity.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/028 - Re-enable VSync on Core GameManager (tearing fix)")]
        private static void Fix028()
        {
            // FPS-cap-only (Fix026) still tears — a cap paces frames but does not align them to the
            // display refresh; only VSync does. The perceived VSync mouse lag was largely TV image
            // processing (fixed by the TV's Game Mode). This sets the Core.unity GameManager's
            // serialized vSyncCount back to 1.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            BlastFrame.Core.GameManager gm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                gm = root.GetComponentInChildren<BlastFrame.Core.GameManager>(true);
                if (gm != null) break;
            }
            if (gm == null)
            {
                Debug.LogError("[Fix028] No GameManager in Core.unity — run Fix 001 first.");
                return;
            }

            var so = new SerializedObject(gm);
            var vsyncProp = so.FindProperty("vSyncCount");
            if (vsyncProp == null)
            {
                Debug.LogError("[Fix028] 'vSyncCount' field not found on GameManager — field may have been renamed.");
                return;
            }

            if (vsyncProp.intValue == 1)
            {
                Debug.Log("[Fix028] GameManager vSyncCount already 1 — nothing to do.");
                return;
            }

            vsyncProp.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = gm.gameObject;
            EditorGUIUtility.PingObject(gm.gameObject);
            Debug.Log("[Fix028] GameManager vSyncCount set to 1 in Core.unity (VSync on — tearing eliminated).");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/029 - Lower Player airDrag to 0.2 (momentum carry fix)")]
        private static void Fix029()
        {
            // airDrag=2/s eats ~80% of inherited (platform/wall-jump/dash) horizontal velocity over a
            // jump arc. Code default is now 0.2 but the Player instance in Core.unity has 2 serialized.
            // Only overwrites if the value is still the old default 2 — a hand-tuned value is left alone.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            BlastFrame.Gameplay.Player.PlayerStats stats = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                stats = root.GetComponentInChildren<BlastFrame.Gameplay.Player.PlayerStats>(true);
                if (stats != null) break;
            }
            if (stats == null)
            {
                Debug.LogError("[Fix029] No PlayerStats in Core.unity — run Fix 011 first.");
                return;
            }

            var so = new SerializedObject(stats);
            var constProp = so.FindProperty("airDrag.ConstantValue");
            var useConstProp = so.FindProperty("airDrag.UseConstant");
            if (constProp == null || useConstProp == null)
            {
                Debug.LogError("[Fix029] 'airDrag' FloatReference not found on PlayerStats — field may have been renamed.");
                return;
            }

            if (!useConstProp.boolValue)
            {
                Debug.LogWarning("[Fix029] airDrag uses a FloatVariable asset, not a constant — adjust the asset by hand. Aborting.");
                return;
            }

            if (!Mathf.Approximately(constProp.floatValue, 2f))
            {
                Debug.LogWarning($"[Fix029] airDrag is {constProp.floatValue} (not the old default 2) — looks hand-tuned, leaving it alone.");
                return;
            }

            constProp.floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stats);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = stats.gameObject;
            EditorGUIUtility.PingObject(stats.gameObject);
            Debug.Log("[Fix029] Player airDrag 2 → 0.2 in Core.unity (inherited momentum now survives the jump arc).");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/031 - Convert DevTex materials to Triplanar shader")]
        private static void Fix031()
        {
            const string shaderPath = "Assets/DevTex/DevTexTriplanar.shader";
            AssetDatabase.Refresh();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Debug.LogError("[Fix031] DevTexTriplanar.shader not found at " + shaderPath + " — make sure the file exists and Unity has imported it.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/DevTex/Materials" });
            int converted = 0, skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { skipped++; continue; }

                // Read existing properties before changing shader (supports both Standard and URP Lit naming)
                Texture albedoTex = mat.HasProperty("_BaseMap")  ? mat.GetTexture("_BaseMap")
                                  : mat.HasProperty("_MainTex")  ? mat.GetTexture("_MainTex")
                                  : null;

                Color albedoColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                                  : mat.HasProperty("_Color")     ? mat.GetColor("_Color")
                                  : Color.white;

                float metallic   = mat.HasProperty("_Metallic")   ? mat.GetFloat("_Metallic")   : 0f;
                // Standard uses _Glossiness; URP Lit uses _Smoothness
                float smoothness = mat.HasProperty("_Smoothness")  ? mat.GetFloat("_Smoothness")
                                 : mat.HasProperty("_Glossiness")  ? mat.GetFloat("_Glossiness")
                                 : 0.5f;

                Texture emissionTex   = mat.HasProperty("_EmissionMap")   ? mat.GetTexture("_EmissionMap") : null;
                Color   emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

                // Detect transparency: trigger/zone materials have renderQueue >= 3000 or _Mode == 2
                bool isTransparent = mat.renderQueue >= 3000
                    || (mat.HasProperty("_Mode")    && Mathf.Approximately(mat.GetFloat("_Mode"),    2f))
                    || (mat.HasProperty("_Surface") && Mathf.Approximately(mat.GetFloat("_Surface"), 1f));

                // Swap to triplanar shader and write properties
                mat.shader = shader;

                mat.SetTexture("_MainTex",      albedoTex);
                mat.SetColor  ("_Color",        albedoColor);
                mat.SetFloat  ("_Tiling",       2.0f);
                mat.SetFloat  ("_Metallic",     metallic);
                mat.SetFloat  ("_Smoothness",   smoothness);
                mat.SetTexture("_EmissionMap",  emissionTex);
                mat.SetColor  ("_EmissionColor", emissionColor);

                if (isTransparent)
                {
                    mat.SetFloat("_SrcBlend", 5f);  // SrcAlpha
                    mat.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
                    mat.SetFloat("_ZWrite",   0f);
                    mat.renderQueue = 3000;
                }
                else
                {
                    mat.SetFloat("_SrcBlend", 1f);  // One
                    mat.SetFloat("_DstBlend", 0f);  // Zero
                    mat.SetFloat("_ZWrite",   1f);
                    mat.renderQueue = 2000;
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fix031] DevTex triplanar: {converted} materials converted (Tiling=1.0), {skipped} skipped.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/032 - Wire barrel pivot on ArcPredictTurret_Test")]
        private static void Fix032()
        {
            // EnemyBehaviorArcPredict now has a barrelPivot field. The test turret's "Barrel" child
            // already exists (placed by Fix014) and is the right transform to pitch on local-X.
            // This fix wires it in without touching anything else.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);

            GameObject turretGo = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                turretGo = root.name == "ArcPredictTurret_Test" ? root : null;
                if (turretGo != null) break;

                // Also search one level deep (in case it is under a Content parent).
                var found = root.transform.Find("ArcPredictTurret_Test");
                if (found != null) { turretGo = found.gameObject; break; }
            }

            if (turretGo == null)
            {
                Debug.LogError("[Fix032] ArcPredictTurret_Test not found in TestLevel — run Fix014 first.");
                return;
            }

            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(out var behavior))
            {
                Debug.LogError("[Fix032] EnemyBehaviorArcPredict not found on ArcPredictTurret_Test.");
                return;
            }

            var barrelTransform = turretGo.transform.Find("Barrel");
            if (barrelTransform == null)
            {
                Debug.LogError("[Fix032] No 'Barrel' child found on ArcPredictTurret_Test — expected from Fix014.");
                return;
            }

            var so = new SerializedObject(behavior);
            var pivotProp = so.FindProperty("barrelPivot");
            if (pivotProp == null)
            {
                Debug.LogError("[Fix032] 'barrelPivot' property not found on EnemyBehaviorArcPredict — ensure the script compiled.");
                return;
            }

            if (pivotProp.objectReferenceValue != null)
            {
                Debug.LogWarning("[Fix032] barrelPivot is already set — skipping to avoid overwriting a hand-tuned value.");
                return;
            }

            pivotProp.objectReferenceValue = barrelTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(turretGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = turretGo;
            EditorGUIUtility.PingObject(turretGo);
            Debug.Log("[Fix032] ArcPredictTurret_Test barrelPivot → Barrel. The barrel will now pitch to match the ballistic launch angle.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/033 - Fully wire ArcPredictTurret_Test (registry, muzzle, barrel pivot)")]
        private static void Fix033()
        {
            // Audits and wires every serialized reference the arc turret needs: entityRegistry on
            // EnemyCore + behavior, muzzle, and barrelPivot. Only fills NULL fields — hand-set
            // values are never overwritten. Operates on the currently open scene when it is
            // TestLevel (preserving unsaved edits); otherwise opens TestLevel.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "TestLevel")
            {
                bool testLevelLoaded = false;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (s.name == "TestLevel") { scene = s; testLevelLoaded = true; break; }
                }
                if (!testLevelLoaded)
                    scene = EditorSceneManager.OpenScene("Assets/Scenes/TestLevel.unity", OpenSceneMode.Single);
            }

            GameObject turretGo = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "ArcPredictTurret_Test") { turretGo = root; break; }
                var found = root.transform.Find("ArcPredictTurret_Test");
                if (found != null) { turretGo = found.gameObject; break; }
            }
            if (turretGo == null)
            {
                Debug.LogError("[Fix033] ArcPredictTurret_Test not found in TestLevel — run Fix014 first.");
                return;
            }

            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityRegistrySO>(
                "Assets/ScriptableObjects/Entities/EntityRegistry.asset");
            if (registry == null)
            {
                Debug.LogError("[Fix033] EntityRegistry.asset not found — run Fix001 first.");
                return;
            }

            // Resolve child transforms (Barrel and Barrel/Muzzle come from Fix014's turret build).
            var barrel = turretGo.transform.Find("Barrel");
            var muzzleT = barrel != null ? barrel.Find("Muzzle") : null;

            int wired = 0;

            static bool WireIfNull033(Object target, string propName, Object value, ref int counter)
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(propName);
                if (prop == null) return false;
                if (prop.objectReferenceValue != null) return true; // already set — leave alone
                if (value == null) return false;
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                counter++;
                return true;
            }

            // Required components.
            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyStats>(out _))
                { turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>(); wired++; }
            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyCore>(out var core))
                { core = turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyCore>(); wired++; }
            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(out var behavior))
                { behavior = turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(); wired++; }

            // Serialized references.
            WireIfNull033(core, "entityRegistry", registry, ref wired);
            WireIfNull033(behavior, "entityRegistry", registry, ref wired);

            if (!WireIfNull033(behavior, "muzzle", muzzleT, ref wired))
                Debug.LogWarning("[Fix033] Could not wire 'muzzle' — no Barrel/Muzzle child found. Projectiles will spawn at the turret root.");

            if (!WireIfNull033(behavior, "barrelPivot", barrel, ref wired))
                Debug.LogWarning("[Fix033] Could not wire 'barrelPivot' — no Barrel child found. The barrel will not visually pitch.");

            EditorUtility.SetDirty(turretGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeObject = turretGo;
            EditorGUIUtility.PingObject(turretGo);
            Debug.Log($"[Fix033] ArcPredictTurret_Test wired — {wired} reference(s)/component(s) set. Already-set fields were left untouched.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/034 - Wire 'turret Shooter' as arc-predict turret")]
        private static void Fix034()
        {
            // Finds the developer's hand-built "turret Shooter" object in the loaded scene(s) by
            // case-insensitive name match, then sets it up as an arc-predict turret: EnemyStats +
            // EnemyCore + EnemyBehaviorArcPredict, entityRegistry wired, barrel pivot + muzzle
            // resolved by child-name heuristics (creates a Muzzle empty at the barrel tip if none
            // exists). Only fills null fields — hand-set values are never overwritten. Operates on
            // the live scene state, unsaved edits included. Logs the hierarchy if discovery fails.
            static GameObject FindByFuzzyName034(string mustContainA, string mustContainB)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!s.isLoaded) continue;
                    foreach (var root in s.GetRootGameObjects())
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            var n = t.name.ToLowerInvariant();
                            if (n.Contains(mustContainA) && n.Contains(mustContainB))
                                return t.gameObject;
                        }
                }
                return null;
            }

            static Transform FindChildContaining034(Transform parent, params string[] keywords)
            {
                foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                {
                    if (t == parent) continue;
                    var n = t.name.ToLowerInvariant();
                    foreach (var kw in keywords)
                        if (n.Contains(kw)) return t;
                }
                return null;
            }

            static void DumpHierarchy034(Transform t, System.Text.StringBuilder sb, int depth)
            {
                sb.Append(' ', depth * 2).AppendLine(t.name);
                for (int i = 0; i < t.childCount; i++)
                    DumpHierarchy034(t.GetChild(i), sb, depth + 1);
            }

            var turretGo = FindByFuzzyName034("turret", "shoot");
            if (turretGo == null)
            {
                Debug.LogError("[Fix034] No GameObject matching 'turret'+'shoot' found in any loaded scene. Check the object name and re-run.");
                return;
            }

            var registry = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityRegistrySO>(
                "Assets/ScriptableObjects/Entities/EntityRegistry.asset");
            if (registry == null)
            {
                Debug.LogError("[Fix034] EntityRegistry.asset not found — run Fix001 first.");
                return;
            }

            // --- Resolve barrel pivot + muzzle by child-name heuristics ---------------------------
            var barrel = FindChildContaining034(turretGo.transform, "barrel", "gun", "cannon", "pivot");
            var muzzleT = FindChildContaining034(turretGo.transform, "muzzle", "tip", "spawn");

            if (barrel == null)
            {
                var sb = new System.Text.StringBuilder("[Fix034] No child matching barrel/gun/cannon/pivot found. Hierarchy of '" + turretGo.name + "':\n");
                DumpHierarchy034(turretGo.transform, sb, 0);
                Debug.LogWarning(sb.ToString() + "Wiring components anyway; barrelPivot left empty (no visual pitch). Rename the gun child to contain 'Barrel' and re-run to wire it.");
            }

            // Create a Muzzle empty at the barrel tip if none exists.
            if (muzzleT == null && barrel != null)
            {
                var muzzleGo = new GameObject("Muzzle");
                muzzleGo.transform.SetParent(barrel, false);

                var rends = barrel.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    var f = barrel.forward;
                    // Distance from bounds center to the bounds face farthest along the barrel forward.
                    var d = b.extents.x * Mathf.Abs(f.x) + b.extents.y * Mathf.Abs(f.y) + b.extents.z * Mathf.Abs(f.z);
                    muzzleGo.transform.position = b.center + f * d;
                }
                muzzleT = muzzleGo.transform;
                Debug.Log("[Fix034] Created Muzzle empty at the tip of '" + barrel.name + "'.");
            }

            // --- Components + references (fill-if-null only) --------------------------------------
            int wired = 0;

            static void WireIfNull034(Object target, string propName, Object value, ref int counter)
            {
                if (value == null) return;
                var so = new SerializedObject(target);
                var prop = so.FindProperty(propName);
                if (prop == null || prop.objectReferenceValue != null) return;
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                counter++;
            }

            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyStats>(out _))
                { turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>(); wired++; }
            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyCore>(out var core))
                { core = turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyCore>(); wired++; }
            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(out var behavior))
                { behavior = turretGo.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(); wired++; }

            WireIfNull034(core, "entityRegistry", registry, ref wired);
            WireIfNull034(behavior, "entityRegistry", registry, ref wired);
            WireIfNull034(behavior, "muzzle", muzzleT, ref wired);
            WireIfNull034(behavior, "barrelPivot", barrel, ref wired);

            EditorUtility.SetDirty(turretGo);
            EditorSceneManager.MarkSceneDirty(turretGo.scene);
            EditorSceneManager.SaveScene(turretGo.scene);

            Selection.activeObject = turretGo;
            EditorGUIUtility.PingObject(turretGo);
            Debug.Log($"[Fix034] '{turretGo.name}' wired as arc-predict turret — {wired} component(s)/reference(s) set. " +
                      $"barrelPivot: {(barrel != null ? barrel.name : "NONE")}, muzzle: {(muzzleT != null ? muzzleT.name : "NONE")}.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/035 - Turret Shooter heavy lob (slow fat AoE projectile)")]
        private static void Fix035()
        {
            // Developer-requested tuning (2026-06-12): turn rate 45°/s (half), fire rate 0.5/s
            // (half), projectile speed 6 m/s (half), gravity scale 0.5, projectile 3x size, and a
            // new pooled ArcExplosion (radius 1.5, damage 1) spawned on impact. These overwrite the
            // old defaults on the 'turret Shooter' instance intentionally — explicitly requested.

            static void SetFloatRef035(Object target, string fieldName, float value)
            {
                var so = new SerializedObject(target);
                var useConst = so.FindProperty(fieldName + ".UseConstant");
                var constVal = so.FindProperty(fieldName + ".ConstantValue");
                if (useConst == null || constVal == null)
                {
                    Debug.LogWarning($"[Fix035] FloatReference '{fieldName}' not found on {target.GetType().Name} — skipped.");
                    return;
                }
                if (!useConst.boolValue)
                {
                    Debug.LogWarning($"[Fix035] '{fieldName}' is driven by a Variable SO asset — adjust the asset by hand. Skipped.");
                    return;
                }
                constVal.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- 1) ArcProjectile prefab: 3x size (0.3 → 0.9 sphere) -------------------------------
            const string arcPrefabPath = "Assets/Prefabs/Projectiles/ArcProjectile.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(arcPrefabPath) == null)
            {
                Debug.LogError("[Fix035] ArcProjectile.prefab not found — run Fix014 first.");
                return;
            }
            var arcRoot = PrefabUtility.LoadPrefabContents(arcPrefabPath);
            arcRoot.transform.localScale = Vector3.one * 0.9f;
            PrefabUtility.SaveAsPrefabAsset(arcRoot, arcPrefabPath);
            PrefabUtility.UnloadPrefabContents(arcRoot);

            // --- 2) ArcExplosion prefab (radius 1.5, damage 1) -------------------------------------
            const string explPrefabPath = "Assets/Prefabs/Projectiles/ArcExplosion.prefab";
            var explPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(explPrefabPath);
            if (explPrefab == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ArcExplosion";
                go.transform.localScale = Vector3.one * 3f; // visual diameter 3 = blast radius 1.5
                if (go.TryGetComponent<Collider>(out var col)) Object.DestroyImmediate(col);

                var aoe = go.AddComponent<BlastFrame.Gameplay.Weapons.AoeExplosion>();
                var aoeSo = new SerializedObject(aoe);
                aoeSo.FindProperty("radius.UseConstant").boolValue = true;
                aoeSo.FindProperty("radius.ConstantValue").floatValue = 1.5f;
                aoeSo.FindProperty("damage.UseConstant").boolValue = true;
                aoeSo.FindProperty("damage.ConstantValue").intValue = 1;
                aoeSo.ApplyModifiedPropertiesWithoutUndo();

                explPrefab = PrefabUtility.SaveAsPrefabAsset(go, explPrefabPath);
                Object.DestroyImmediate(go);
            }

            // --- 3) EntityDefinitionSO + pool entry ------------------------------------------------
            const string defPath = "Assets/ScriptableObjects/Entities/ArcExplosion.asset";
            var def = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Entities.EntityDefinitionSO>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BlastFrame.Core.Entities.EntityDefinitionSO>();
                AssetDatabase.CreateAsset(def, defPath);
                var defSo = new SerializedObject(def);
                defSo.FindProperty("id").stringValue = BlastFrame.Core.PoolIds.ArcExplosion;
                defSo.FindProperty("prefab").objectReferenceValue = explPrefab;
                defSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var poolConfig = AssetDatabase.LoadAssetAtPath<BlastFrame.Core.Pooling.PoolConfigSO>(
                "Assets/ScriptableObjects/Pooling/PoolConfig.asset");
            if (poolConfig == null)
            {
                Debug.LogError("[Fix035] PoolConfig.asset not found — run Fix014 first.");
                return;
            }
            var pcSo = new SerializedObject(poolConfig);
            var entries = pcSo.FindProperty("entries");
            bool hasEntry = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var d = entries.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue
                        as BlastFrame.Core.Entities.EntityDefinitionSO;
                if (d != null && d.Id == BlastFrame.Core.PoolIds.ArcExplosion) { hasEntry = true; break; }
            }
            if (!hasEntry)
            {
                int idx = entries.arraySize;
                entries.InsertArrayElementAtIndex(idx);
                var e = entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("definition").objectReferenceValue = def;
                e.FindPropertyRelative("prewarmCount").intValue = 4;
                e.FindPropertyRelative("expandIncrement").intValue = 2;
                pcSo.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();

            // --- 4) Turret instance tuning ---------------------------------------------------------
            GameObject turretGo = null;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount && turretGo == null; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var n = t.name.ToLowerInvariant();
                        if (n.Contains("turret") && n.Contains("shoot")) { turretGo = t.gameObject; break; }
                    }
                    if (turretGo != null) break;
                }
            }
            if (turretGo == null)
            {
                Debug.LogError("[Fix035] 'turret Shooter' not found in any loaded scene — run Fix034 first. Prefab/pool changes were still applied.");
                return;
            }

            if (turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyStats>(out var stats))
            {
                SetFloatRef035(stats, "fireRate", 0.5f);
                SetFloatRef035(stats, "projectileSpeed", 6f);
            }
            if (turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(out var behavior))
            {
                SetFloatRef035(behavior, "rotationSpeed", 45f);
                SetFloatRef035(behavior, "projectileGravityScale", 0.5f);
            }

            EditorUtility.SetDirty(turretGo);
            EditorSceneManager.MarkSceneDirty(turretGo.scene);
            EditorSceneManager.SaveScene(turretGo.scene);

            Selection.activeObject = turretGo;
            EditorGUIUtility.PingObject(turretGo);
            Debug.Log("[Fix035] Heavy lob configured: turn 45°/s, fire 0.5/s, speed 6 m/s, gravity 0.5x, " +
                      "projectile 3x size, ArcExplosion pool (radius 1.5, damage 1) wired.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/036 - Turret Shooter projectile gravity to 0.2x")]
        private static void Fix036()
        {
            // Developer-requested (2026-06-12): arc projectile gravity = 20% of normal (was 0.5
            // from Fix035). Overwrites intentionally. maxLeadTime (new field) needs no wiring —
            // existing instances pick up the code default (1.5s) automatically.
            GameObject turretGo = null;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount && turretGo == null; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var n = t.name.ToLowerInvariant();
                        if (n.Contains("turret") && n.Contains("shoot")) { turretGo = t.gameObject; break; }
                    }
                    if (turretGo != null) break;
                }
            }
            if (turretGo == null)
            {
                Debug.LogError("[Fix036] 'turret Shooter' not found in any loaded scene.");
                return;
            }

            if (!turretGo.TryGetComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>(out var behavior))
            {
                Debug.LogError("[Fix036] EnemyBehaviorArcPredict not found on '" + turretGo.name + "'.");
                return;
            }

            var so = new SerializedObject(behavior);
            var useConst = so.FindProperty("projectileGravityScale.UseConstant");
            var constVal = so.FindProperty("projectileGravityScale.ConstantValue");
            if (useConst == null || constVal == null)
            {
                Debug.LogError("[Fix036] projectileGravityScale FloatReference not found — ensure scripts compiled.");
                return;
            }
            if (!useConst.boolValue)
            {
                Debug.LogWarning("[Fix036] projectileGravityScale is driven by a Variable SO — adjust the asset by hand. Aborting.");
                return;
            }

            constVal.floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(turretGo);
            EditorSceneManager.MarkSceneDirty(turretGo.scene);
            EditorSceneManager.SaveScene(turretGo.scene);

            Selection.activeObject = turretGo;
            EditorGUIUtility.PingObject(turretGo);
            Debug.Log("[Fix036] '" + turretGo.name + "' projectileGravityScale 0.5 → 0.2.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/030 - Convert DevTex materials to URP Lit")]
        private static void Fix030()
        {
            // URP 17 removed the Edit > Rendering material-convert menu items; the Render Pipeline
            // Converter window can't be driven from code. Runs Unity's own StandardUpgrader on just
            // Assets/DevTex/Materials so the transparent trigger/zone materials (_Mode 2/3) and
            // emission map correctly instead of a raw shader swap.
            var upgraders = new System.Collections.Generic.List<UnityEditor.Rendering.MaterialUpgrader>
            {
                new UnityEditor.Rendering.Universal.StandardUpgrader("Standard"),
                new UnityEditor.Rendering.Universal.StandardUpgrader("Standard (Specular setup)"),
            };

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/DevTex/Materials" });
            int converted = 0, skipped = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null ||
                    (mat.shader.name != "Standard" && mat.shader.name != "Standard (Specular setup)"))
                {
                    skipped++;
                    continue;
                }

                UnityEditor.Rendering.MaterialUpgrader.Upgrade(
                    mat, upgraders, UnityEditor.Rendering.MaterialUpgrader.UpgradeFlags.None);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fix030] DevTex materials: {converted} converted Standard → URP Lit, {skipped} skipped (already URP or not a Standard material).");
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/037 - Rebuild Player Camera Child In Core")]
        private static void Fix037()
        {
            // Reconstructs the deleted "Camera" child on the existing Player, exactly as Fix 004 +
            // Fix 012 originally built it: eye-height FP camera (Camera + AudioListener +
            // FirstPersonCamera + CameraShape) plus the shooter rig (ChargeShot + PlayerShooter),
            // since the camera transform doubles as the muzzle. Does NOT touch the Player root or
            // its other components.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Fix037] No 'Player' found in Core — run Fix 004 first.");
                return;
            }

            if (player.transform.Find("Camera") != null)
            {
                Debug.LogWarning("[Fix037] Player already has a 'Camera' child — aborting to avoid duplicates.");
                return;
            }

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var fpc = camGo.AddComponent<FirstPersonCamera>();
            camGo.AddComponent<CameraShake>();

            // Wire the camera's body field to the player root (FirstPersonCamera yaws the body).
            var fso = new SerializedObject(fpc);
            fso.FindProperty("body").objectReferenceValue = player.transform;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // Camera doubles as the muzzle — restore the shooter rig (was added by Fix 012).
            camGo.AddComponent<BlastFrame.Gameplay.Weapons.ChargeShot>();
            camGo.AddComponent<BlastFrame.Gameplay.Player.PlayerShooter>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = camGo;
            EditorGUIUtility.PingObject(camGo);
            Debug.Log("[Fix037] Rebuilt Player/Camera child in Core (FP camera + shake + shooter rig).");
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/038 - Create RAA URP Lit Projection Material")]
        private static void Fix038()
        {
            // Creates a fresh material that uses the "RAA Materials/RAA URP Lit Projection" shader,
            // with triplanar projection ON by default. Does NOT overwrite an existing material —
            // material tuning fields are developer-editable, so it bails if the asset already exists.
            const string dir = "Assets/RAA Materials";
            const string path = dir + "/M_RAA_LitProjection.mat";

            var shader = Shader.Find("RAA Materials/RAA URP Lit Projection");
            if (shader == null)
            {
                Debug.LogError("[Fix038] Shader 'RAA Materials/RAA URP Lit Projection' not found — let the editor compile the shader first, then re-run.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                Debug.LogWarning($"[Fix038] {path} already exists — aborting so hand-tuned values are not overwritten.");
                return;
            }

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var mat = new Material(shader) { name = "M_RAA_LitProjection" };
            // Triplanar ON by default (matches the shader's _Projection default of 1).
            mat.SetFloat("_Projection", 1f);
            mat.EnableKeyword("_RAA_PROJECTION_ON");

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
            Debug.Log($"[Fix038] Created {path} (RAA URP Lit Projection, triplanar ON).");
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/039 - Enable Post Processing On Player Camera")]
        private static void Fix039()
        {
            // The Player's FP Camera had Post Processing disabled on its UniversalAdditionalCameraData,
            // so the Global Volume (tonemapping/bloom/etc.) was being ignored entirely. This flips that
            // single toggle ON. Volume trigger + layer mask were already correct, so this is all that's
            // needed. Does NOT touch antialiasing or any other camera setting.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Fix039] No 'Player' found in Core — run Fix 004 / 037 first.");
                return;
            }

            var camT = player.transform.Find("Camera");
            if (camT == null)
            {
                Debug.LogError("[Fix039] Player has no 'Camera' child — run Fix 037 first.");
                return;
            }

            var camData = camT.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null)
            {
                Debug.LogError("[Fix039] Camera has no UniversalAdditionalCameraData — is URP active?");
                return;
            }

            if (camData.renderPostProcessing)
            {
                Debug.LogWarning("[Fix039] Post Processing already enabled on Player camera — nothing to do.");
                return;
            }

            camData.renderPostProcessing = true;
            EditorUtility.SetDirty(camData);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = camT.gameObject;
            EditorGUIUtility.PingObject(camT.gameObject);
            Debug.Log("[Fix039] Enabled Post Processing on Player camera — Global Volume now applies.");
        }

        [MenuItem("Tools/Blast Frame/Implement Fix/040 - Point Standalone quality tier at PC (stop Play downgrade)")]
        private static void Fix040()
        {
            // ROOT CAUSE: QualitySettings had m_PerPlatformDefaultQuality[Standalone] = 0 (the "Mobile"
            // tier -> Mobile_RPAsset: renderScale 0.8, MSAA off, soft shadows unsupported, 1 cascade,
            // 1024 main shadowmap, no additional-light shadows). Scene view / edit-mode Game view use the
            // active tier (m_CurrentQuality = 1 = "PC" -> PC_RPAsset, full quality), so it LOOKS great until
            // you press Play: Play simulates the Standalone build target, which uses that platform's default
            // tier = Mobile = downgrade. This fix repoints the Standalone default at the "PC" tier so Play
            // matches the editor. The Mobile tier is preserved untouched for any future mobile build.
            int pcIndex = System.Array.IndexOf(QualitySettings.names, "PC");
            if (pcIndex < 0)
            {
                Debug.LogError("[Fix040] No quality level named 'PC' found — aborting. Quality levels: "
                    + string.Join(", ", QualitySettings.names));
                return;
            }

            var so = new SerializedObject(UnityEditor.Unsupported.GetSerializedAssetInterfaceSingleton("QualitySettings"));
            so.Update();

            bool inMap = false;
            bool keyIsStandalone = false;
            bool changed = false;
            string previousValue = "(unchanged)";

            var iter = so.GetIterator();
            while (iter.NextVisible(true))
            {
                if (!iter.propertyPath.StartsWith("m_PerPlatformDefaultQuality"))
                {
                    if (inMap) break; // iterated past the map block
                    continue;
                }
                inMap = true;

                if (iter.propertyType == SerializedPropertyType.String)
                {
                    keyIsStandalone = iter.stringValue == "Standalone";
                }
                else if (iter.propertyType == SerializedPropertyType.Integer && keyIsStandalone)
                {
                    previousValue = iter.intValue.ToString();
                    if (iter.intValue != pcIndex)
                    {
                        iter.intValue = pcIndex;
                        changed = true;
                    }
                    keyIsStandalone = false;
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Fix040] Standalone default quality tier set to index {pcIndex} ('PC') — was index {previousValue}. "
                    + "Re-enter Play mode: lighting/shadows should now match the Scene view. "
                    + "If it still downgrades, set the Standalone default manually (see Manual Steps).");
            }
            else if (!inMap)
            {
                Debug.LogWarning("[Fix040] Could not locate the m_PerPlatformDefaultQuality map via SerializedObject. "
                    + "Set it by hand: Project Settings > Quality, click the dropdown on the Windows/Standalone column "
                    + "header and choose the 'PC' row as the default tier.");
            }
            else
            {
                Debug.Log($"[Fix040] Standalone default quality tier was already index {previousValue} ('PC') — nothing to change. "
                    + "If Play still downgrades, the trigger is elsewhere (active build target group, or a per-camera override).");
            }
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/041 - Viewmodel overlay camera for gun (render in front, no wall clip)")]
        private static void Fix041()
        {
            // Makes the first-person gun render on top of all world geometry so it never clips into
            // walls. URP camera-stacking approach (matches the project's viewmodel = camera-stacking note):
            //   1. Ensure a "Viewmodel" layer exists.
            //   2. Exclude that layer from the base Player camera's culling mask.
            //   3. Add an Overlay camera child ("ViewmodelCamera") that ONLY renders the Viewmodel layer.
            //      Overlay cameras clear depth and composite on top, so the gun always draws in front
            //      regardless of world depth. Tiny near clip stops the barrel clipping at the near plane.
            //   4. Push the overlay onto the base camera's stack.
            // Assigning the gun GameObject(s) to the Viewmodel layer is a manual step (this fix doesn't
            // guess which object is the gun). Does NOT touch the base camera's other settings.
            const string LayerName = "Viewmodel";

            int layer = EnsureLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError("[Fix041] No free user layer slot (8-31) to create 'Viewmodel'. Free one in Project Settings > Tags and Layers, then re-run.");
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Fix041] No 'Player' found in Core — run Fix 004 / 037 first.");
                return;
            }

            var camT = player.transform.Find("Camera");
            if (camT == null)
            {
                Debug.LogError("[Fix041] Player has no 'Camera' child — run Fix 037 first.");
                return;
            }

            var baseCam = camT.GetComponent<Camera>();
            var baseData = camT.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (baseCam == null || baseData == null)
            {
                Debug.LogError("[Fix041] Player camera missing Camera / UniversalAdditionalCameraData — is URP active?");
                return;
            }

            // Base camera stops rendering the Viewmodel layer (the overlay owns it).
            baseCam.cullingMask &= ~(1 << layer);
            baseData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
            EditorUtility.SetDirty(baseCam);
            EditorUtility.SetDirty(baseData);

            // Find or create the overlay camera as a child of the base camera (identity local transform,
            // so it tracks the FP camera exactly).
            var overlayT = camT.Find("ViewmodelCamera");
            GameObject overlayGo;
            if (overlayT == null)
            {
                overlayGo = new GameObject("ViewmodelCamera");
                overlayGo.transform.SetParent(camT, false);
            }
            else
            {
                overlayGo = overlayT.gameObject;
            }

            var overlayCam = overlayGo.GetComponent<Camera>();
            if (overlayCam == null) overlayCam = overlayGo.AddComponent<Camera>();
            overlayCam.cullingMask = 1 << layer;   // ONLY the gun
            overlayCam.nearClipPlane = 0.01f;       // barrel can sit very close without clipping
            overlayCam.fieldOfView = baseCam.fieldOfView;

            // Overlay cameras must not carry an AudioListener (the base camera owns it).
            var stale = overlayGo.GetComponent<AudioListener>();
            if (stale != null) Object.DestroyImmediate(stale);

            var overlayData = overlayGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (overlayData == null) overlayData = overlayGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            overlayData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            EditorUtility.SetDirty(overlayCam);
            EditorUtility.SetDirty(overlayData);

            // Push the overlay onto the base camera's stack (no duplicates).
            if (!baseData.cameraStack.Contains(overlayCam))
                baseData.cameraStack.Add(overlayCam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = overlayGo;
            EditorGUIUtility.PingObject(overlayGo);
            Debug.Log($"[Fix041] Viewmodel overlay camera ready (layer '{LayerName}' = {layer}). "
                + "Now set your gun GameObject (and its children) to the 'Viewmodel' layer — see Manual Steps.");

            // -- inline helper -------------------------------------------------------------------
            int EnsureLayer(string name)
            {
                var tagManager = new SerializedObject(
                    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tagManager.FindProperty("layers");

                // Already present?
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
                }
                // First free user slot (8-31).
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var el = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(el.stringValue))
                    {
                        el.stringValue = name;
                        tagManager.ApplyModifiedPropertiesWithoutUndo();
                        return i;
                    }
                }
                return -1;
            }
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/042 - Exclude Viewmodel layer from Player collisionMask (gun = not a wall)")]
        private static void Fix042()
        {
            // The first-person gun (on the 'Viewmodel' layer, Fix 041) was being swept as a wall by
            // PlayerMotor: its collide-and-slide / WallProbe only skip the player's own capsule, so any
            // other collider parented under the player reads as world geometry. This strips the Viewmodel
            // bit out of PlayerMotor.collisionMask so viewmodel geometry can never count as ground/wall.
            // (Best practice is also to remove the collider from the gun entirely — see Manual Steps.)
            int layer = LayerMask.NameToLayer("Viewmodel");
            if (layer < 0)
            {
                Debug.LogError("[Fix042] No 'Viewmodel' layer — run Fix 041 first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Fix042] No 'Player' found in Core — run Fix 004 / 037 first.");
                return;
            }

            if (!player.TryGetComponent<BlastFrame.Gameplay.Player.PlayerMotor>(out var motor))
            {
                Debug.LogError("[Fix042] Player has no PlayerMotor component.");
                return;
            }

            var so = new SerializedObject(motor);
            var maskProp = so.FindProperty("collisionMask");
            int before = maskProp.intValue;
            int after = before & ~(1 << layer);

            if (after == before)
            {
                Debug.LogWarning("[Fix042] 'Viewmodel' already excluded from PlayerMotor.collisionMask — nothing to do.");
                return;
            }

            maskProp.intValue = after;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = player;
            EditorGUIUtility.PingObject(player);
            Debug.Log($"[Fix042] Excluded 'Viewmodel' (layer {layer}) from PlayerMotor.collisionMask — the gun no longer counts as a wall/ground.");
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/043 - erase scene: bake-match (enable Mixed light, probe box-proj 512, Realtime GI off)")]
        private static void Fix043()
        {
            // Makes the BAKED result match the live Mixed preview in 'erase'. Operates on the CURRENTLY
            // OPEN scene (does NOT reopen — erase is open and dirty with the artist's work). Three surgical
            // changes, all observed wrong via MCP read of the scene:
            //   1. Enable the 'Directional Light' GameObject (it was INACTIVE -> excluded from the bake, so
            //      its colour never baked and it wasn't visible).
            //   2. Reflection Probe: Box Projection ON + Resolution 512 (was off / 128 -> blurry, parallax-
            //      wrong reflections on smooth metal).
            //   3. Realtime Global Illumination OFF on the scene's Lighting Settings (was ON -> the live
            //      Enlighten preview never matches the Progressive GPU bake; off = viewport previews the
            //      true baked path).
            // DELIBERATELY does NOT touch bake-quality tuning the artist may have hand-set (lightmap
            // resolution, bounces, sample counts, atlas size) — adjust those by hand. Marks the scene dirty
            // but does NOT force-save it (so it won't silently bake in other unsaved edits); save with Ctrl+S.
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "erase")
            {
                Debug.LogError($"[Fix043] Active scene is '{scene.name}', not 'erase'. Open Assets/Scenes/erase.unity first, then re-run.");
                return;
            }

            var lights = GameObject.Find("Lights");
            if (lights == null)
            {
                Debug.LogError("[Fix043] No 'Lights' group found in the active scene — aborting.");
                return;
            }

            var changes = new System.Collections.Generic.List<string>();

            // 1. Enable the Mixed directional light. Transform.Find locates INACTIVE children (GameObject.Find would not).
            var dirLightT = lights.transform.Find("Directional Light");
            if (dirLightT == null)
                Debug.LogWarning("[Fix043] 'Lights/Directional Light' not found — skipping light enable.");
            else if (!dirLightT.gameObject.activeSelf)
            {
                dirLightT.gameObject.SetActive(true);
                changes.Add("enabled 'Directional Light' GameObject");
            }

            // 2. Reflection Probe box projection + resolution.
            var probeT = lights.transform.Find("Reflection Probe");
            if (probeT != null && probeT.TryGetComponent(out ReflectionProbe probe))
            {
                if (!probe.boxProjection) { probe.boxProjection = true; changes.Add("Reflection Probe Box Projection ON"); }
                if (probe.resolution < 512) { probe.resolution = 512; changes.Add("Reflection Probe Resolution -> 512"); }
                EditorUtility.SetDirty(probe);
            }
            else
            {
                Debug.LogWarning("[Fix043] 'Lights/Reflection Probe' not found — skipping probe tweaks.");
            }

            // 3. Realtime GI off (single correctness toggle on the lighting settings; leaves bake tuning alone).
            try
            {
                var settings = Lightmapping.lightingSettings;
                if (settings != null && settings.realtimeGI)
                {
                    settings.realtimeGI = false;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    changes.Add("Realtime Global Illumination OFF (lighting settings)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Fix043] Couldn't read the scene's Lighting Settings asset to disable Realtime GI — "
                    + "uncheck it by hand in Window > Rendering > Lighting. (" + e.Message + ")");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = lights;
            EditorGUIUtility.PingObject(lights);

            if (changes.Count == 0)
                Debug.Log("[Fix043] Nothing needed changing — all targets already in the desired state.");
            else
                Debug.Log("[Fix043] Done: " + string.Join("; ", changes)
                    + ". Save the scene (Ctrl+S), then Window > Rendering > Lighting > Generate Lighting (and re-bake APV). "
                    + "Bump lightmap resolution/bounces by hand if you want sharper GI.");
        }

        // ----------------------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Implement Fix/044 - Create Heat Shimmer Particle Prefab")]
        private static void Fix044()
        {
            // Builds a rising heat-shimmer ParticleSystem prefab using the "RAA Heat Distortion" shader
            // (refracts _CameraOpaqueTexture). Drop the prefab over a lava surface and scale its Box
            // emitter to fit. Creates the material if missing (does NOT overwrite a hand-tuned one) and
            // bails if the prefab already exists so tuned settings aren't clobbered.
            const string matPath = "Assets/RAA Materials/M_HeatShimmer.mat";
            const string prefabDir = "Assets/Prefabs/VFX";
            const string prefabPath = prefabDir + "/HeatShimmer.prefab";

            var shader = Shader.Find("RAA Materials/RAA Heat Distortion");
            if (shader == null)
            {
                Debug.LogError("[Fix044] Shader 'RAA Materials/RAA Heat Distortion' not found — let it compile first, then re-run.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.LogWarning($"[Fix044] {prefabPath} already exists — aborting so tuned particle settings aren't overwritten.");
                return;
            }

            // Material (reuse if present so hand-tuned distortion values survive).
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_HeatShimmer" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var go = new GameObject("HeatShimmer");
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f); // motion comes from Velocity-over-Lifetime
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.2831853f); // random spin for variety
            main.startColor = Color.white; // rgb unused by the shader; alpha drives shimmer (set via gradient below)
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // rises in world, doesn't follow the emitter
            main.maxParticles = 250;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 0.1f, 5f); // flat emit area — scale to fit the lava in-scene

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f); // gentle sideways waver
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);    // rise

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.2f));

            // Alpha ramps in then out so the distortion eases in/out (the shader scales by particle alpha).
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sharedMaterial = mat;

            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go); // remove the temp instance from the open scene
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[Fix044] Created {prefabPath} (+ {matPath} if it was missing). Drag it over a lava surface, "
                + "scale the emitter Box to fit, and tune Distortion Strength on the material.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/045 - Weapon vs owner layers (Player/Enemy/PlayerWeapon/EnemyWeapon + matrix + blast masks)")]
        private static void Fix045()
        {
            // Creates four layers (Player, Enemy, PlayerWeapon, EnemyWeapon), disables the two
            // collision-matrix pairs so a side's projectiles pass through that side, re-layers the
            // projectile/explosion prefabs and the scene-side Player + every EnemyCore, and narrows
            // the two AoE blast masks so a blast no longer catches its own owner.
            //   - PlayerWeapon  x Player : OFF  (your shots/explosions never hit you)
            //   - EnemyWeapon   x Enemy  : OFF  (no enemy friendly-fire / self-detonation)
            // Projectiles use TRIGGER colliders; the layer collision matrix governs trigger events
            // too, so no projectile code changes are needed.
            // Layer assignment is SURGICAL: only the root + GameObjects that own a Collider move to
            // the owner layer. The player's camera/viewmodel children have no colliders, so they
            // keep their 'Viewmodel' layer (the gun overlay from Fix041/042 stays intact).

            // ---- 1. Ensure the four layers exist, claiming the first free user slots ------------
            int EnsureLayer(string layerName)
            {
                int existing = LayerMask.NameToLayer(layerName);
                if (existing != -1) return existing;

                var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tm.FindProperty("layers");
                for (int i = 6; i < layers.arraySize; i++) // 0-7 are builtin; start at the first user-usable slot
                {
                    var el = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(el.stringValue))
                    {
                        el.stringValue = layerName;
                        tm.ApplyModifiedProperties();
                        return i;
                    }
                }
                return -1;
            }

            int playerLayer       = EnsureLayer("Player");
            int enemyLayer        = EnsureLayer("Enemy");
            int playerWeaponLayer = EnsureLayer("PlayerWeapon");
            int enemyWeaponLayer  = EnsureLayer("EnemyWeapon");

            if (playerLayer < 0 || enemyLayer < 0 || playerWeaponLayer < 0 || enemyWeaponLayer < 0)
            {
                Debug.LogError("[Fix045] Ran out of free layer slots — free some user layers in Project Settings > Tags and Layers, then re-run.");
                return;
            }

            // ---- 2. Collision matrix: a side's weapon ignores that side ------------------------
            Physics.IgnoreLayerCollision(playerWeaponLayer, playerLayer, true);
            Physics.IgnoreLayerCollision(enemyWeaponLayer, enemyLayer, true);

            // ---- 3. Re-layer the weapon prefabs (root + every collider GameObject) -------------
            void SetOwnerLayer(GameObject root, int layer)
            {
                root.layer = layer;
                foreach (var col in root.GetComponentsInChildren<Collider>(true))
                    col.gameObject.layer = layer; // colliders are what the matrix actually filters
            }

            void RelayerPrefab(string path, int layer, int excludeBlastLayer)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Fix045] Prefab not found, skipped: {path}");
                    return;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                SetOwnerLayer(root, layer);

                // Narrow the blast mask so the explosion's OverlapSphere skips its own owner.
                // (Splash damage is layer-mask driven, NOT collision driven, so the matrix can't do this.)
                if (excludeBlastLayer >= 0)
                {
                    var aoe = root.GetComponentInChildren<AoeExplosion>(true);
                    if (aoe != null)
                    {
                        var so = new SerializedObject(aoe);
                        var maskProp = so.FindProperty("damageLayers");
                        if (maskProp != null) { maskProp.intValue = ~(1 << excludeBlastLayer); so.ApplyModifiedProperties(); }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            const string projDir = "Assets/Prefabs/Projectiles/";
            RelayerPrefab(projDir + "PlayerProjectile.prefab", playerWeaponLayer, -1);
            RelayerPrefab(projDir + "Explosion.prefab",        playerWeaponLayer, playerLayer); // player charged-shot blast: don't hit the player
            RelayerPrefab(projDir + "EnemyMissile.prefab",     enemyWeaponLayer,  -1);
            RelayerPrefab(projDir + "ArcProjectile.prefab",    enemyWeaponLayer,  -1);
            RelayerPrefab(projDir + "ArcExplosion.prefab",     enemyWeaponLayer,  enemyLayer);  // enemy arc blast: don't chain enemies

            // ---- 4. Re-layer the scene-side owners in whatever scenes are currently open -------
            int playersHit = 0, enemiesHit = 0;
            foreach (var motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SetOwnerLayer(motor.gameObject, playerLayer);
                EditorSceneManager.MarkSceneDirty(motor.gameObject.scene);
                playersHit++;
            }
            foreach (var core in Object.FindObjectsByType<BlastFrame.Gameplay.Enemies.EnemyCore>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SetOwnerLayer(core.gameObject, enemyLayer);
                EditorSceneManager.MarkSceneDirty(core.gameObject.scene);
                enemiesHit++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Fix045] Layers: Player={playerLayer} Enemy={enemyLayer} PlayerWeapon={playerWeaponLayer} EnemyWeapon={enemyWeaponLayer}. "
                + $"Matrix: PlayerWeapon x Player OFF, EnemyWeapon x Enemy OFF. Relayered 5 weapon prefabs. "
                + $"Scene owners relayered: {playersHit} player(s), {enemiesHit} enemy(ies). "
                + (playersHit == 0 ? "NOTE: no Player found in any open scene — open Core.unity and re-run to layer it. " : "")
                + (enemiesHit == 0 ? "NOTE: no EnemyCore found — open TestLevel.unity and re-run to layer the turrets." : ""));
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/046 - Enable Read-Write on lava meshes (RockBobber needs hit.textureCoord)")]
        private static void Fix046()
        {
            // RockBobber now reads the REAL mesh UV + surface height under each rock from the downward
            // raycast (RaycastHit.textureCoord / hit.point), instead of guessing the UV from local XZ.
            // textureCoord only returns a real UV when the hit MeshCollider's mesh is Read/Write
            // Enabled — these lava FBXs ship with it OFF. Turn it on and reimport.
            // Import-setting ONLY: touches no hand-tuned material/SO field.
            string[] lavaModels =
            {
                "Assets/Art/Level Assets/Lava Blockout Level/Lava River Plane.fbx",
                "Assets/Art/Level Assets/Lava Blockout Level/Lava Falls.fbx",
                "Assets/Art/Level Assets/Lava Blockout Level/Lava 001 Bridge.fbx",
            };

            int changed = 0, already = 0, missing = 0;
            foreach (string path in lavaModels)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[Fix046] Not a model / not found, skipped: {path}");
                    missing++;
                    continue;
                }
                if (importer.isReadable) { already++; continue; }
                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log($"[Fix046] Lava mesh Read/Write: {changed} enabled, {already} already on, {missing} missing. "
                + "RockBobber can now read the real surface UV under each rock — re-enter Play mode to seat the bobs. "
                + "If a rock still warns about an unreadable mesh, add that FBX path to this fix's lavaModels list.");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/047 - Wire Lava River material into all RockBobbers (bob sourced only from the river)")]
        private static void Fix047()
        {
            // RockBobber now drives the bob ONLY from the V.4 river (no sine fallback, no rest-over-ripple).
            // Assigning the river material to each rock's River Material field makes the churn come from the
            // river even when the rock sits over another lava piece (e.g. a ripple). Wiring only — touches
            // no tuning field.
            const string riverMatPath = "Assets/Art/Materials/Lava World Materials/Lava River.mat";
            var river = AssetDatabase.LoadAssetAtPath<Material>(riverMatPath);
            if (river == null)
            {
                Debug.LogError($"[Fix047] River material not found at {riverMatPath} — fix the path and re-run.");
                return;
            }

            int wired = 0, total = 0;
            foreach (var bob in Object.FindObjectsByType<BlastFrame.Gameplay.Environment.RockBobber>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++;
                var so = new SerializedObject(bob);
                var prop = so.FindProperty("riverMaterial");
                if (prop == null) continue;
                if (prop.objectReferenceValue != river)
                {
                    prop.objectReferenceValue = river;
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(bob.gameObject.scene);
                    wired++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Fix047] River material wired into {wired}/{total} RockBobber(s) in the open scene(s). "
                + (total == 0 ? "NOTE: no RockBobber found — open the lava scene and re-run." : "Re-enter Play mode; the bob is now sourced from the river."));
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/048 - Add LavaFlowRandomizer to lava-flow objects (per-object variety)")]
        private static void Fix048()
        {
            // Adds LavaFlowRandomizer to every renderer whose material is an "RAA Lava Flow" (detected by the
            // _BottomMove property), so several lava falls sharing one material stop scrolling in lockstep.
            // Adds Include Children = false so each renderer is randomised exactly once; idempotent (skips a
            // renderer that already has the component). Component-add only — touches no material asset.
            int added = 0, skipped = 0, scanned = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool isLavaFlow = false;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_BottomMove")) { isLavaFlow = true; break; }
                if (!isLavaFlow) continue;
                scanned++;

                if (r.GetComponent<BlastFrame.Gameplay.Environment.LavaFlowRandomizer>() != null) { skipped++; continue; }

                var comp = Undo.AddComponent<BlastFrame.Gameplay.Environment.LavaFlowRandomizer>(r.gameObject);
                var so = new SerializedObject(comp);
                var incChildren = so.FindProperty("includeChildren");
                if (incChildren != null) { incChildren.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
                added++;
            }

            Debug.Log($"[Fix048] LavaFlowRandomizer: added to {added} lava-flow object(s), {skipped} already had it, "
                + $"of {scanned} lava-flow renderer(s) found. "
                + (scanned == 0
                    ? "NOTE: no lava-flow ('_BottomMove') material in the open scene — open the lava scene and re-run."
                    : "Save the scene, then Play — each fall now offsets/scrolls independently."));
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/049 - Lava ejection glass: Render Face = Both (fix glass not rendering)")]
        private static void Fix049()
        {
            // The ejection glass is a single-sided surface whose faces point away from view, so URP back-face
            // culls it and only the opaque cutout shows. Set Render Face = Both (_Cull = 0). SURGICAL: touches
            // ONLY _Cull + doubleSidedGI on this one material — never rewrites its other (hand-tuned) values.
            const string path = "Assets/Art/Models/Lava Falls Tube/Lava Fall Ejection Glass Material.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"[Fix049] Glass material not found at {path} — fix the path and re-run.");
                return;
            }
            if (!mat.HasProperty("_Cull"))
            {
                Debug.LogWarning($"[Fix049] '{mat.name}' has no _Cull property (not URP Lit?) — set Render Face by hand.");
                return;
            }

            float before = mat.GetFloat("_Cull");
            if (Mathf.Approximately(before, 0f))
            {
                Debug.Log($"[Fix049] '{mat.name}' Render Face is already Both (_Cull 0) — nothing to do.");
                return;
            }

            mat.SetFloat("_Cull", 0f);   // 0 = Both (render both faces); was 2 = Back
            mat.doubleSidedGI = true;    // match what URP's 'Both' toggle does for GI
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fix049] '{mat.name}' Render Face -> Both (_Cull {before} -> 0). The glass should now render. "
                + "If it looks SOLID instead of see-through, lower its Base Color/Base Map alpha by hand (a look choice, not changed here).");
        }

        // ----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Implement Fix/050 - Remove duplicate LavaFlowRandomizers on the Lava Falls prefab")]
        private static void Fix050()
        {
            // The Lava Falls prefab ended up with several LavaFlowRandomizer components stacked on the same
            // objects (they each re-randomise the same renderer — redundant). Keep one per GameObject, remove
            // the extras. Idempotent; removes duplicate components only — changes no values.
            const string path = "Assets/Prefabs/Lava World/Lava Falls Prefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"[Fix050] Prefab not found at {path} — fix the path and re-run.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<BlastFrame.Gameplay.Environment.LavaFlowRandomizer>();
                for (int i = 1; i < comps.Length; i++) // keep [0], remove the rest
                {
                    Object.DestroyImmediate(comps[i]);
                    removed++;
                }
            }
            if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[Fix050] Removed {removed} duplicate LavaFlowRandomizer(s) from '{path}' (kept one per object).");
        }
    }
}
