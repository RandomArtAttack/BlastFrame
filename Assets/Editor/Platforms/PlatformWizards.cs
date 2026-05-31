using UnityEditor;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Gameplay.Platforms;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// One-click editor wizards for creating prototype platform GameObjects.
    /// Each wizard creates a cube platform with required components, adds child waypoints where
    /// needed, and pings the result in the Hierarchy.
    /// </summary>
    public static class PlatformWizards
    {
        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Platforms/Create Moving Platform")]
        private static void CreateMovingPlatform()
        {
            // Platform root — cube + kinematic Rigidbody + MovingPlatform.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "MovingPlatform";
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(3f, 0.3f, 3f);
            ApplyProtoMaterial(root, new Color(0.3f, 0.55f, 0.85f));

            SetupKinematicRigidbody(root);

            var platform = root.AddComponent<MovingPlatform>();

            // Create two child waypoints so the platform is ready to use.
            var wp0 = CreateWaypoint("Waypoint_0", root.transform, Vector3.zero);
            var wp1 = CreateWaypoint("Waypoint_1", root.transform, new Vector3(0f, 0f, 6f));

            // Wire the waypoints into the serialized list.
            var so = new SerializedObject(platform);
            var waypointsProp = so.FindProperty("waypoints");
            waypointsProp.arraySize = 2;
            waypointsProp.GetArrayElementAtIndex(0).objectReferenceValue = wp0;
            waypointsProp.GetArrayElementAtIndex(1).objectReferenceValue = wp1;
            so.ApplyModifiedPropertiesWithoutUndo();

            Finish(root, "MovingPlatform created with 2 child waypoints. Adjust waypoint positions in the Scene.");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Platforms/Create Rotating Platform")]
        private static void CreateRotatingPlatform()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "RotatingPlatform";
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(4f, 0.3f, 4f);
            ApplyProtoMaterial(root, new Color(0.85f, 0.55f, 0.2f));

            SetupKinematicRigidbody(root);
            root.AddComponent<RotatingPlatform>();

            Finish(root, "RotatingPlatform created. Default axis is Y at 45 deg/s — tune in Inspector.");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Platforms/Create Treadmill")]
        private static void CreateTreadmill()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "TreadmillPlatform";
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(3f, 0.3f, 6f);
            ApplyProtoMaterial(root, new Color(0.2f, 0.75f, 0.35f));

            // TreadmillPlatform is static — no kinematic Rigidbody required.
            root.AddComponent<TreadmillPlatform>();

            Finish(root, "TreadmillPlatform created. Default push is +Z at 4 m/s — tune in Inspector.");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Platforms/Create Spring Board")]
        private static void CreateSpringBoard()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "SpringBoard";
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(2f, 0.3f, 2f);
            ApplyProtoMaterial(root, new Color(0.9f, 0.2f, 0.25f));

            // SpringBoard is static — no kinematic Rigidbody required.
            root.AddComponent<SpringBoard>();

            Finish(root, "SpringBoard created. Default launch is +Y at 12 m/s — tune in Inspector.");
        }

        // ------------------------------------------------------------------ helpers
        private static void SetupKinematicRigidbody(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private static Transform CreateWaypoint(string waypointName, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(waypointName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static void ApplyProtoMaterial(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            if (go.TryGetComponent<MeshRenderer>(out var mr))
                mr.sharedMaterial = mat;
        }

        private static void Finish(GameObject go, string logMessage)
        {
            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log($"[PlatformWizard] {logMessage}");
        }
    }
}
