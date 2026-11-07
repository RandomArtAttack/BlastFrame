using UnityEditor;
using UnityEngine;
using RAA.BlenderMode.Navigation;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Entry point for Blender Mode. Subscribes to <see cref="SceneView.duringSceneGui"/>, enforces the
    /// master toggle and suspend conditions, owns the single active <see cref="ModalOperator"/>, and pumps
    /// events into the <see cref="InputRouter"/>. When the master toggle is off it early-outs before
    /// touching any event, so Unity behaves 100% stock.
    /// </summary>
    [InitializeOnLoad]
    public static class BlenderModeManager
    {
        /// <summary>The one active modal operator, or null.</summary>
        public static ModalOperator ActiveOperator { get; private set; }

        static BlenderModeManager()
        {
            // Register module action handlers up front.
            ViewCommands.Register();

            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        /// <summary>Begin a modal operator, replacing any current one.</summary>
        public static void StartOperator(ModalOperator op, SceneView sceneView)
        {
            if (op == null) return;
            if (ActiveOperator != null)
                ActiveOperator.Cancel();
            ActiveOperator = op;
            op.OnStart(sceneView);
            sceneView.Repaint();
        }

        /// <summary>Clear the active operator if it matches (called by the operator when it ends).</summary>
        public static void EndOperator(ModalOperator op)
        {
            if (ActiveOperator == op)
                ActiveOperator = null;
            SceneView.RepaintAll();
        }

        static void OnSceneGui(SceneView sceneView)
        {
            // Master toggle: consume nothing when off.
            if (!BlenderModePrefs.Enabled)
                return;

            // Suspend conditions (input ignored even when enabled).
            if (BlenderModePrefs.SuspendInPlayMode && EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Text field editing takes precedence unless we're mid-modal.
            if (ActiveOperator == null && EditorGUIUtility.editingTextField)
                return;

            InputRouter.Process(sceneView);

            // Draw modal handles/HUD after processing so state reflects this event.
            ModalOperator active = ActiveOperator;
            if (active != null)
            {
                active.DrawHandles(sceneView);
                Hud.DrawFooter(sceneView, active.HudText());
                sceneView.Repaint();
            }
        }
    }
}
