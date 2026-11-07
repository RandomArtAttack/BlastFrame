using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>CLAUDE.md "Coding Conventions" + "Component Self-Containment Rule", checked by reflection.</summary>
    public class ConventionTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly string[] Lifecycle = { "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate" };

        private static IEnumerable<Type> GameTypes() =>
            typeof(BlastFrame.Core.ServiceLocator).Assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("BlastFrame"));

        private static bool IsUnityObject(Type t) => typeof(UnityEngine.Object).IsAssignableFrom(t);
        private static bool IsSerializableData(Type t) => !IsUnityObject(t) && t.IsDefined(typeof(SerializableAttribute), false) && !t.IsEnum;

        private static IEnumerable<FieldInfo> SerializedFields(Type t) =>
            t.GetFields(Inst).Where(f => !f.IsStatic && !f.IsDefined(typeof(NonSerializedAttribute))
                                         && !f.Name.Contains("k__BackingField")
                                         && (f.IsDefined(typeof(SerializeField)) || f.IsPublic));

        [Test]
        public void EverySerializedField_HasTooltip()
        {
            var missing = new List<string>();
            // Save DTOs (BlastFrame.Core.Save) are JSON payloads, never shown in the Inspector.
            foreach (var t in GameTypes().Where(t => (IsUnityObject(t) || IsSerializableData(t)) && t.Namespace != "BlastFrame.Core.Save"))
            foreach (var f in SerializedFields(t))
            {
                if (f.IsDefined(typeof(TooltipAttribute))) continue;
                if (f.IsDefined(typeof(HideInInspector))) continue;
                missing.Add($"{t.FullName}.{f.Name}");
            }
            Assert.IsEmpty(missing, $"{missing.Count} serialized field(s) without [Tooltip]:\n" + string.Join("\n", missing));
        }

        [Test]
        public void ComponentsAndSOs_UseSerializeFieldPrivate_NotPublicFields()
        {
            var bad = GameTypes().Where(IsUnityObject)
                .SelectMany(t => t.GetFields(Inst).Where(f => f.IsPublic && !f.IsStatic && !f.IsLiteral && !f.IsInitOnly
                                                              && !f.IsDefined(typeof(NonSerializedAttribute))
                                                              && !f.IsDefined(typeof(HideInInspector)))
                    .Select(f => $"{t.FullName}.{f.Name}"))
                .ToList();
            Assert.IsEmpty(bad, "public serialized fields (use [SerializeField] private):\n" + string.Join("\n", bad));
        }

        [Test]
        public void LifecycleMethods_AreNotPublic()
        {
            var bad = GameTypes().Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(Inst).Where(m => m.IsPublic && Lifecycle.Contains(m.Name) && m.GetParameters().Length == 0)
                    .Select(m => $"{t.FullName}.{m.Name}"))
                .ToList();
            Assert.IsEmpty(bad, "public Unity lifecycle methods:\n" + string.Join("\n", bad));
        }

        /// <summary>Scene objects whose BlastFrame components reference ANOTHER scene object outside their own
        /// root hierarchy through a serialized field. Report category: opens every scene.</summary>
        [Test, Category("Report")]
        public void Scenes_NoCrossObjectSerializedReferences()
        {
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("_Recovery")).ToList();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var problems = new List<string>();
            try
            {
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        var ns = mb.GetType().Namespace;
                        if (ns == null || !ns.StartsWith("BlastFrame")) continue;
                        var it = new SerializedObject(mb).GetIterator();
                        while (it.Next(true))
                        {
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var target = it.objectReferenceValue;
                            Transform tt = target is Component c ? c.transform : target is GameObject g ? g.transform : null;
                            if (tt == null || !tt.gameObject.scene.IsValid()) continue; // asset reference — fine
                            if (tt.root == mb.transform.root) continue;                  // own hierarchy — fine
                            problems.Add($"{path}: {Path(mb.transform)}.{mb.GetType().Name}.{it.propertyPath} -> {Path(tt)}");
                        }
                    }
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            foreach (var p in problems) TestContext.WriteLine(p);
            Assert.IsEmpty(problems, $"{problems.Count} cross-object reference(s):\n" + string.Join("\n", problems));
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
