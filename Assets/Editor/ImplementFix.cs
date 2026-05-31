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

                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var col = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
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

            var healthRT          = healthGo.GetComponent<UnityEngine.RectTransform>();
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

            var dashRT            = dashGo.GetComponent<UnityEngine.RectTransform>();
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

            var chargeRT          = chargeGo.GetComponent<UnityEngine.RectTransform>();
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

                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var col = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
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
    }
}
