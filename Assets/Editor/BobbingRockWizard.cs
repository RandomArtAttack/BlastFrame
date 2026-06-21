using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Environment;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Creates a rock that rides the lava's visual (vertex-shader) surface via RockBobber.
    /// Spawns a cube placeholder at the Scene-view pivot, adds RockBobber, points its Lava Mask at the
    /// "Lava" layer, and pings it. Swap the cube mesh for a real rock model and tune the bob in the
    /// Inspector. Drop it so it sits over a lava surface (a Lava-layer collider must be below it).
    /// </summary>
    public static class BobbingRockWizard
    {
        [MenuItem("Tools/Blast Frame/Hazards/Create Bobbing Rock")]
        private static void CreateBobbingRock()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BobbingRock";

            // Place at the Scene view pivot if there is one, else world origin.
            var view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;

            var bobber = go.AddComponent<RockBobber>();

            // Point Lava Mask at the "Lava" layer if it exists (the component also auto-falls back to it).
            int lavaLayer = LayerMask.NameToLayer("Lava");
            if (lavaLayer != -1)
            {
                var so = new SerializedObject(bobber);
                var maskProp = so.FindProperty("lavaMask");
                if (maskProp != null) { maskProp.intValue = 1 << lavaLayer; so.ApplyModifiedProperties(); }
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Bobbing Rock");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            if (lavaLayer == -1)
                Debug.LogWarning("[BobbingRockWizard] No 'Lava' layer found — set the RockBobber's Lava Mask " +
                    "to whatever layer your lava collider is on, or it will use a simple fallback bob.");
        }
    }
}
