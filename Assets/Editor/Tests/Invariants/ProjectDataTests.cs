using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Pooling;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;
using BlastFrame.Gameplay.Projectiles;
using BlastFrame.Gameplay.Weapons;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>Asset / settings invariants that break silently at runtime if they drift.</summary>
    public class ProjectDataTests
    {
        private static int L(string name)
        {
            int l = LayerMask.NameToLayer(name);
            Assert.GreaterOrEqual(l, 0, $"layer '{name}' missing from TagManager");
            return l;
        }

        private static IEnumerable<T> AllAssets<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null);

        [Test]
        public void EveryPoolId_HasAPoolConfigEntryWithPrefab()
        {
            var ids = typeof(PoolIds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()).ToList();
            var configured = AllAssets<PoolConfigSO>().SelectMany(c => c.Entries)
                .Where(e => e?.definition != null && e.definition.Prefab != null)
                .Select(e => e.definition.Id).ToHashSet();
            var missing = ids.Where(id => !configured.Contains(id)).ToList();
            Assert.IsEmpty(missing, "PoolIds with no PoolConfig entry (Spawn would log 'No pool registered'): " + string.Join(", ", missing));
        }

        [Test]
        public void PoolConfig_SizesArePositive()
        {
            foreach (var c in AllAssets<PoolConfigSO>())
            foreach (var e in c.Entries)
            {
                if (e?.definition == null) { Assert.Fail($"{c.name}: entry with no definition"); continue; }
                Assert.Greater(e.expandIncrement, 0, $"{e.definition.Id}: expandIncrement");
                Assert.GreaterOrEqual(e.prewarmCount, 0, $"{e.definition.Id}: prewarmCount");
            }
        }

        [Test]
        public void EntityDefinitions_HaveUniqueIdsAndPrefabs()
        {
            var seen = new Dictionary<string, string>();
            var problems = new List<string>();
            foreach (var d in AllAssets<EntityDefinitionSO>())
            {
                if (string.IsNullOrWhiteSpace(d.Id)) problems.Add($"{d.name}: empty id");
                else if (seen.TryGetValue(d.Id, out var other)) problems.Add($"{d.name}: id '{d.Id}' duplicates {other}");
                else seen[d.Id] = d.name;
                if (d.Prefab == null) problems.Add($"{d.name}: no prefab");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void WeaponLayers_IgnoreTheirOwners()
        {
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(L("PlayerWeapon"), L("Player")), "player weapons must not hit the player");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(L("EnemyWeapon"), L("Enemy")), "enemy weapons must not hit enemies");
        }

        [Test]
        public void ProjectilePrefabs_CollidersOnTheirWeaponLayer()
        {
            int pw = L("PlayerWeapon"), ew = L("EnemyWeapon");
            var problems = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int expect = go.GetComponentInChildren<PlayerProjectile>(true) != null ? pw
                    : go.GetComponentInChildren<EnemyMissile>(true) != null || go.GetComponentInChildren<ArcProjectile>(true) != null
                      || go.GetComponentInChildren<HomingMissile>(true) != null ? ew : -1;
                if (expect < 0) continue;
                foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    if (col.gameObject.layer != expect)
                        problems.Add($"{path}: collider on '{col.name}' is on layer {LayerMask.LayerToName(col.gameObject.layer)} (expected {LayerMask.LayerToName(expect)})");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void ProjectilePrefabs_KinematicWithTrigger()
        {
            var problems = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Projectiles" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool moving = go.GetComponentInChildren<EnemyMissile>(true) || go.GetComponentInChildren<ArcProjectile>(true)
                              || go.GetComponentInChildren<ProjectileBase>(true) || go.GetComponentInChildren<HomingMissile>(true);
                if (!moving) continue;
                var rb = go.GetComponentInChildren<Rigidbody>(true);
                if (rb == null || !rb.isKinematic) problems.Add($"{path}: needs a kinematic Rigidbody");
                if (!go.GetComponentsInChildren<Collider>(true).Any(c => c.isTrigger)) problems.Add($"{path}: needs a trigger collider");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void PlayerProbes_IgnorePlayerViewmodelAndWeapons()
        {
            // Glossary: "Wall detection is level geometry only — never the player's own gun."
            int forbidden = (1 << L("Player")) | (1 << L("Viewmodel")) | (1 << L("PlayerWeapon")) | (1 << L("EnemyWeapon"));
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var core = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Additive);
            try
            {
                var player = core.GetRootGameObjects().Select(r => r.GetComponentInChildren<PlayerController>(true)).First(p => p != null);
                var problems = new List<string>();
                void Check(Component c, string field)
                {
                    if (c == null) return;
                    int mask = new SerializedObject(c).FindProperty(field).intValue;
                    int bad = mask & forbidden;
                    if (bad != 0)
                        problems.Add($"{c.GetType().Name}.{field} includes: " + string.Join(", ",
                            Enumerable.Range(0, 32).Where(i => (bad & (1 << i)) != 0).Select(LayerMask.LayerToName)));
                }
                Check(player.GetComponent<PlayerMotor>(), "collisionMask");
                Check(player.GetComponent<LedgeVaultModule>(), "vaultMask");
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally
            {
                EditorSceneManager.CloseScene(core, true);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        [Test]
        public void CorePlayer_TestModeIsOff()
        {
            // PlayerTestMode = multi-jump to reach anywhere. Fine while building levels, but it silently
            // invalidates every reach budget and must be off before any playtest build.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var core = EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Additive);
            try
            {
                var tm = core.GetRootGameObjects().Select(r => r.GetComponentInChildren<BlastFrame.Debugging.PlayerTestMode>(true)).FirstOrDefault(p => p != null);
                if (tm == null) return;
                bool on = tm.enabled && new SerializedObject(tm).FindProperty("enabledTestMode").boolValue;
                if (on) Assert.Inconclusive("PlayerTestMode multi-jump is ENABLED on the Core Player (press its toggle key or untick it before a playtest build).");
            }
            finally
            {
                EditorSceneManager.CloseScene(core, true);
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
