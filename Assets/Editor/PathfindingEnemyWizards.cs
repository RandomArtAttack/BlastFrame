using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Events;
using BlastFrame.Core.Pooling;
using BlastFrame.Gameplay.Enemies;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Wizards that build the five pathfinding enemy prefabs (Lobber, Skirmisher, Missiler, Biter, Bat)
    /// plus the pooled HomingMissile projectile. Each is primitive-built with a distinct material,
    /// EnemyStats/EnemyCore wired to the EntityRegistry + OnEnemyKilled event, ground units carrying an
    /// EnemyNavMotor (+ NavMeshAgent), and an EntityDefinitionSO per enemy. Saves to
    /// Assets/Prefabs/Enemies/ and pings the result. Modeled on FlyingEnemyWizard.
    /// </summary>
    public static class PathfindingEnemyWizards
    {
        // ===== Menu items ======================================================================

        [MenuItem("Tools/Blast Frame/Enemies/Create Lobber Prefab")]
        private static void CreateLobber()
        {
            const string path = EnemyBuild.PrefabFolder + "/Lobber.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            var root = EnemyBuild.NewGroundEnemy("Lobber", capsuleRadius: 0.5f, capsuleHeight: 1.8f);
            EnemyBuild.SetStats(root, health: 4, move: 3.5f, fire: 0.6f, dmg: 1, projSpeed: 16f, range: 22f, reward: 2);
            EnemyBuild.ConfigureAgent(root, radius: 0.5f, height: 1.8f, stopDist: 0.6f);

            var mat = EnemyBuild.Mat("M_Lobber", new Color(0.9f, 0.45f, 0.15f));
            EnemyBuild.AddVisual(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 1.3f, 0.9f), mat);
            EnemyBuild.AddVisual(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 1.25f, 0.4f), new Vector3(0.18f, 0.4f, 0.18f), mat, rotation: Quaternion.Euler(90f, 0f, 0f));
            var muzzle = EnemyBuild.AddEmpty(root.transform, "Muzzle", new Vector3(0f, 1.3f, 0.7f));

            var behavior = root.GetComponent<EnemyBehaviorLobber>();
            EnemyBuild.WireBehavior(behavior);
            EnemyBuild.SetRef(behavior, "muzzle", muzzle);

            EnemyBuild.Finish(root, path, "Lobber");
        }

        [MenuItem("Tools/Blast Frame/Enemies/Create Skirmisher Prefab")]
        private static void CreateSkirmisher()
        {
            const string path = EnemyBuild.PrefabFolder + "/Skirmisher.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            var root = EnemyBuild.NewGroundEnemy("Skirmisher", capsuleRadius: 0.4f, capsuleHeight: 1.7f);
            EnemyBuild.SetStats(root, health: 3, move: 4.5f, fire: 1f, dmg: 1, projSpeed: 26f, range: 24f, reward: 2);
            EnemyBuild.ConfigureAgent(root, radius: 0.4f, height: 1.7f, stopDist: 0.4f);

            var mat = EnemyBuild.Mat("M_Skirmisher", new Color(0.25f, 0.75f, 0.35f));
            EnemyBuild.AddVisual(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.85f, 0.7f), mat);
            var muzzle = EnemyBuild.AddEmpty(root.transform, "Muzzle", new Vector3(0f, 1.2f, 0.45f));

            var behavior = root.GetComponent<EnemyBehaviorSkirmisher>();
            EnemyBuild.WireBehavior(behavior);
            EnemyBuild.SetRef(behavior, "muzzle", muzzle);

            EnemyBuild.Finish(root, path, "Skirmisher");
        }

        [MenuItem("Tools/Blast Frame/Enemies/Create Missiler Prefab")]
        private static void CreateMissiler()
        {
            const string path = EnemyBuild.PrefabFolder + "/Missiler.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            var root = EnemyBuild.NewGroundEnemyBox("Missiler", boxSize: new Vector3(1.6f, 1.2f, 2.0f), boxCenterY: 0.6f);
            EnemyBuild.SetStats(root, health: 10, move: 1.5f, fire: 1f, dmg: 1, projSpeed: 18f, range: 28f, reward: 4);
            EnemyBuild.ConfigureAgent(root, radius: 0.7f, height: 1.4f, stopDist: 1.0f);

            var mat = EnemyBuild.Mat("M_Missiler", new Color(0.45f, 0.5f, 0.55f));
            var turretMat = EnemyBuild.Mat("M_MissilerTurret", new Color(0.6f, 0.2f, 0.2f));
            EnemyBuild.AddVisual(root.transform, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 0.8f, 2.0f), mat);

            var turret = EnemyBuild.AddEmpty(root.transform, "Turret", new Vector3(0f, 1.0f, 0f));
            EnemyBuild.AddVisual(turret, "TurretBody", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.6f, 0.9f), turretMat);
            EnemyBuild.AddVisual(turret, "TurretBarrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.7f), new Vector3(0.15f, 0.5f, 0.15f), turretMat, rotation: Quaternion.Euler(90f, 0f, 0f));
            var muzzle = EnemyBuild.AddEmpty(turret, "Muzzle", new Vector3(0f, 0f, 1.2f));

            var behavior = root.GetComponent<EnemyBehaviorMissiler>();
            EnemyBuild.WireBehavior(behavior);
            EnemyBuild.SetRef(behavior, "turret", turret);
            EnemyBuild.SetRef(behavior, "muzzle", muzzle);

            EnemyBuild.Finish(root, path, "Missiler");
        }

        [MenuItem("Tools/Blast Frame/Enemies/Create Biter Prefab")]
        private static void CreateBiter()
        {
            const string path = EnemyBuild.PrefabFolder + "/Biter.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            var root = EnemyBuild.NewGroundEnemySphere("Biter", sphereRadius: 0.4f, sphereCenterY: 0.4f);
            EnemyBuild.SetStats(root, health: 2, move: 6f, fire: 1f, dmg: 1, projSpeed: 12f, range: 30f, reward: 1);
            EnemyBuild.ConfigureAgent(root, radius: 0.35f, height: 0.9f, stopDist: 0.2f);

            var mat = EnemyBuild.Mat("M_Biter", new Color(0.9f, 0.85f, 0.2f));
            EnemyBuild.AddVisual(root.transform, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.7f, 0.7f), mat);
            EnemyBuild.AddVisual(root.transform, "Jaw", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0.35f), new Vector3(0.45f, 0.2f, 0.25f), mat);

            var behavior = root.GetComponent<EnemyBehaviorBiter>();
            EnemyBuild.WireBehavior(behavior);

            EnemyBuild.Finish(root, path, "Biter");
        }

        [MenuItem("Tools/Blast Frame/Enemies/Create Bat Prefab")]
        private static void CreateBat()
        {
            const string path = EnemyBuild.PrefabFolder + "/Bat.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            // Flyer-style: kinematic interpolated Rigidbody, sphere collider, NO nav agent.
            var root = new GameObject("Bat");
            EnemyBuild.SetEnemyLayer(root);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = 0.5f; sc.isTrigger = false;
            root.AddComponent<EnemyStats>();
            root.AddComponent<EnemyCore>();
            root.AddComponent<EnemyBehaviorBat>();

            EnemyBuild.SetStats(root, health: 3, move: 6f, fire: 1f, dmg: 1, projSpeed: 12f, range: 40f, reward: 2);

            var mat = EnemyBuild.Mat("M_Bat", new Color(0.5f, 0.3f, 0.7f));
            var bombMat = EnemyBuild.Mat("M_BatBomb", new Color(0.15f, 0.15f, 0.15f));
            EnemyBuild.AddVisual(root.transform, "Body", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(1.0f, 0.7f, 1.0f), mat);
            EnemyBuild.AddVisual(root.transform, "WingL", PrimitiveType.Cube, new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0.08f, 0.5f), mat);
            EnemyBuild.AddVisual(root.transform, "WingR", PrimitiveType.Cube, new Vector3(0.6f, 0f, 0f), new Vector3(0.6f, 0.08f, 0.5f), mat);
            var dropPoint = EnemyBuild.AddEmpty(root.transform, "DropPoint", new Vector3(0f, -0.5f, 0f));
            var bombVisual = EnemyBuild.AddVisual(root.transform, "BombVisual", PrimitiveType.Sphere, new Vector3(0f, -0.55f, 0f), new Vector3(0.35f, 0.35f, 0.35f), bombMat);

            var behavior = root.GetComponent<EnemyBehaviorBat>();
            EnemyBuild.WireBehavior(behavior);
            EnemyBuild.SetRef(behavior, "dropPoint", dropPoint);
            EnemyBuild.SetRef(behavior, "bombVisual", bombVisual.transform);

            EnemyBuild.Finish(root, path, "Bat");
        }

        [MenuItem("Tools/Blast Frame/Enemies/Create Homing Missile (Projectile + Pool)")]
        private static void CreateHomingMissile()
        {
            const string path = "Assets/Prefabs/Projectiles/HomingMissile.prefab";
            if (!EnemyBuild.ConfirmOverwrite(path)) return;

            var root = new GameObject("HomingMissile");
            int ewLayer = LayerMask.NameToLayer("EnemyWeapon");
            if (ewLayer != -1) root.layer = ewLayer;
            else Debug.LogWarning("[HomingMissileWizard] No 'EnemyWeapon' layer — set the prefab layer manually.");

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.28f; col.isTrigger = true;

            var missile = root.AddComponent<HomingMissile>();

            var mat = EnemyBuild.Mat("M_HomingMissile", new Color(1f, 0.3f, 0.1f));
            EnemyBuild.AddVisual(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.32f, 0.22f), mat, rotation: Quaternion.Euler(90f, 0f, 0f));

            EnemyBuild.SetRef(missile, "entityRegistry", null, EnemyBuild.FindFirstAsset<EntityRegistrySO>());

            EnemyBuild.EnsureFolder(EnemyBuild.PrefabFolder); // ensures parent chain
            EnemyBuild.EnsureFolder("Assets/Prefabs/Projectiles");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            var def = EnemyBuild.CreateEntityDef("HomingMissile", prefab);
            EnemyBuild.AddPoolEntry(def, prewarm: 8, expand: 8);

            BuildHomingPop();

            if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
            Debug.Log("[HomingMissileWizard] HomingMissile + HomingPop prefabs + EntityDefinitions + PoolConfig entries created.");
        }

        // Pure-visual pooled pop (VfxPop — NO physics/damage) used as the harmless "shot down" pop for a
        // HomingMissile. Must NOT be an AoeExplosion: PopHarmless can fire re-entrantly from inside
        // another explosion's overlap loop, and AoeExplosion's shared static buffer would be corrupted.
        private static void BuildHomingPop()
        {
            const string path = "Assets/Prefabs/Projectiles/HomingPop.prefab";
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HomingPop";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var mat = EnemyBuild.Mat("M_HomingPop", new Color(1f, 0.85f, 0.3f));
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
            go.transform.localScale = Vector3.one * 1.2f;

            var pop = go.AddComponent<BlastFrame.Gameplay.Weapons.VfxPop>();
            var so = new SerializedObject(pop);
            so.FindProperty("lifetime.UseConstant").boolValue = true;
            so.FindProperty("lifetime.ConstantValue").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnemyBuild.EnsureFolder("Assets/Prefabs/Projectiles");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            var def = EnemyBuild.CreateEntityDef("HomingPop", prefab);
            EnemyBuild.AddPoolEntry(def, prewarm: 8, expand: 8);
        }
    }

    // ===== Shared build helpers ================================================================

    internal static class EnemyBuild
    {
        public const string PrefabFolder = "Assets/Prefabs/Enemies";
        public const string EntityFolder = "Assets/ScriptableObjects/Entities";
        public const string MatFolder = "Assets/Art/Materials";
        public const string PoolConfigPath = "Assets/ScriptableObjects/Pooling/PoolConfig.asset";

        public static bool ConfirmOverwrite(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return true;
            return EditorUtility.DisplayDialog("Prefab Exists",
                "A prefab already exists at " + path + ".\n\nOverwrite it?", "Overwrite", "Cancel");
        }

        public static void SetEnemyLayer(GameObject go)
        {
            int enemy = LayerMask.NameToLayer("Enemy");
            if (enemy == -1) { Debug.LogWarning("[EnemyBuild] No 'Enemy' layer — set the root layer manually."); return; }
            SetLayerRecursive(go, enemy);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        // --- Ground-enemy skeletons (collider + core stack + nav motor) -------------------------

        public static GameObject NewGroundEnemy(string name, float capsuleRadius, float capsuleHeight)
        {
            var root = new GameObject(name);
            var cc = root.AddComponent<CapsuleCollider>();
            cc.radius = capsuleRadius; cc.height = capsuleHeight; cc.center = new Vector3(0f, capsuleHeight * 0.5f, 0f);
            cc.isTrigger = false;
            AddGroundStack(root);
            return root;
        }

        public static GameObject NewGroundEnemyBox(string name, Vector3 boxSize, float boxCenterY)
        {
            var root = new GameObject(name);
            var bc = root.AddComponent<BoxCollider>();
            bc.size = boxSize; bc.center = new Vector3(0f, boxCenterY, 0f); bc.isTrigger = false;
            AddGroundStack(root);
            return root;
        }

        public static GameObject NewGroundEnemySphere(string name, float sphereRadius, float sphereCenterY)
        {
            var root = new GameObject(name);
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = sphereRadius; sc.center = new Vector3(0f, sphereCenterY, 0f); sc.isTrigger = false;
            AddGroundStack(root);
            return root;
        }

        private static void AddGroundStack(GameObject root)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
            root.AddComponent<EnemyStats>();
            root.AddComponent<EnemyCore>();
            root.AddComponent<EnemyNavMotor>(); // pulls in NavMeshAgent via RequireComponent

            // Attach the matching behavior by enemy name.
            switch (root.name)
            {
                case "Lobber": root.AddComponent<EnemyBehaviorLobber>(); break;
                case "Skirmisher": root.AddComponent<EnemyBehaviorSkirmisher>(); break;
                case "Missiler": root.AddComponent<EnemyBehaviorMissiler>(); break;
                case "Biter": root.AddComponent<EnemyBehaviorBiter>(); break;
            }
        }

        public static void ConfigureAgent(GameObject root, float radius, float height, float stopDist)
        {
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent == null) return;
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.stoppingDistance = stopDist;
            agent.acceleration = 24f;
            agent.angularSpeed = 720f;
            agent.autoBraking = true;
        }

        // --- Stats -----------------------------------------------------------------------------

        public static void SetStats(GameObject root, int health, float move, float fire, int dmg, float projSpeed, float range, int reward)
        {
            var stats = root.GetComponent<EnemyStats>();
            var so = new SerializedObject(stats);
            SetInt(so, "health", health);
            SetFloat(so, "moveSpeed", move);
            SetFloat(so, "fireRate", fire);
            SetInt(so, "damage", dmg);
            SetFloat(so, "projectileSpeed", projSpeed);
            SetFloat(so, "range", range);
            SetInt(so, "rewardCurrency", reward);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(SerializedObject so, string field, float v)
        {
            so.FindProperty(field + ".UseConstant").boolValue = true;
            so.FindProperty(field + ".ConstantValue").floatValue = v;
        }

        private static void SetInt(SerializedObject so, string field, int v)
        {
            so.FindProperty(field + ".UseConstant").boolValue = true;
            so.FindProperty(field + ".ConstantValue").intValue = v;
        }

        // --- Wiring ----------------------------------------------------------------------------

        public static void WireBehavior(Component behavior)
        {
            var reg = FindFirstAsset<EntityRegistrySO>();
            SetRef(behavior, "entityRegistry", null, reg);

            var core = behavior.GetComponent<EnemyCore>();
            var coreSo = new SerializedObject(core);
            if (reg != null) coreSo.FindProperty("entityRegistry").objectReferenceValue = reg;
            var killed = FindEvent("OnEnemyKilled");
            if (killed != null) coreSo.FindProperty("onEnemyKilledEvent").objectReferenceValue = killed;
            coreSo.ApplyModifiedPropertiesWithoutUndo();

            if (reg == null)
                Debug.LogWarning("[EnemyBuild] No EntityRegistrySO asset found — wire it manually into EnemyCore and the behavior.");
        }

        /// <summary>Set a Transform/Object serialized ref on a component. Pass either a Transform (transformRef) or an Object (objRef).</summary>
        public static void SetRef(Component comp, string field, Transform transformRef, Object objRef = null)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[EnemyBuild] Field '{field}' not found on {comp.GetType().Name}."); return; }
            prop.objectReferenceValue = objRef != null ? objRef : (Object)transformRef;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Visuals ---------------------------------------------------------------------------

        public static GameObject AddVisual(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, Quaternion rotation = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            bool noRotation = rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f;
            go.transform.localRotation = noRotation ? Quaternion.identity : rotation;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
            return go;
        }

        public static Transform AddEmpty(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public static Material Mat(string name, Color color)
        {
            EnsureFolder(MatFolder);
            string path = MatFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // --- Save + assets ---------------------------------------------------------------------

        public static void Finish(GameObject root, string prefabPath, string entityId)
        {
            SetEnemyLayer(root);
            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                CreateEntityDef(entityId, prefab);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[EnemyBuild] {entityId} prefab saved to {prefabPath}.");
            }
            else Debug.LogError($"[EnemyBuild] Failed to save prefab at {prefabPath}.");
        }

        public static EntityDefinitionSO CreateEntityDef(string id, GameObject prefab)
        {
            EnsureFolder(EntityFolder);
            string path = EntityFolder + "/" + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<EntityDefinitionSO>();
                AssetDatabase.CreateAsset(def, path);
            }
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }

        public static void AddPoolEntry(EntityDefinitionSO def, int prewarm, int expand)
        {
            var config = AssetDatabase.LoadAssetAtPath<PoolConfigSO>(PoolConfigPath);
            if (config == null) { Debug.LogWarning("[EnemyBuild] PoolConfig asset not found at " + PoolConfigPath + " — add the pool entry manually."); return; }
            var so = new SerializedObject(config);
            var list = so.FindProperty("entries");

            for (int i = 0; i < list.arraySize; i++)
            {
                var d = list.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue;
                if (d == def) { Debug.Log("[EnemyBuild] PoolConfig already has an entry for " + def.name + "."); return; }
            }

            list.arraySize++;
            var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("definition").objectReferenceValue = def;
            entry.FindPropertyRelative("prewarmCount").intValue = prewarm;
            entry.FindPropertyRelative("expandIncrement").intValue = expand;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            Debug.Log("[EnemyBuild] Added PoolConfig entry for " + def.name + ".");
        }

        // --- Asset lookup ----------------------------------------------------------------------

        public static T FindFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static GameEventSO FindEvent(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName + " t:GameEventSO");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var evt = AssetDatabase.LoadAssetAtPath<GameEventSO>(path);
                if (evt != null && evt.name == assetName) return evt;
            }
            return null;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
