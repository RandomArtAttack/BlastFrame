using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Base class for modal operators (grab, rotate, box-select, pie, ...). Exactly one is active at a
    /// time; while active it receives every SceneView event exclusively via the <see cref="InputRouter"/>.
    /// Phase 2+ derives concrete operators; Phase 1 only needs the lifecycle contract in place.
    /// </summary>
    public abstract class ModalOperator
    {
        /// <summary>The SceneView this operator was started in.</summary>
        protected SceneView SceneView { get; private set; }

        /// <summary>Passive control id claimed as hotControl so Unity tools/gizmos don't fight the modal.</summary>
        protected int HotControlId { get; private set; }

        /// <summary>Called once when the operator becomes active.</summary>
        public virtual void OnStart(SceneView sceneView)
        {
            SceneView = sceneView;
            HotControlId = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = HotControlId;
        }

        /// <summary>Handle a single event while active. Implementations should call <see cref="End"/> on confirm/cancel.</summary>
        public abstract void HandleEvent(Event e);

        /// <summary>Apply the operation and finish (collapses one undo group).</summary>
        public abstract void Confirm();

        /// <summary>Abort and revert to the pre-operation state with no undo entry.</summary>
        public abstract void Cancel();

        /// <summary>Draw scene-space handles/gizmos for the modal. Runs inside duringSceneGui.</summary>
        public virtual void DrawHandles(SceneView sceneView) { }

        /// <summary>Text shown in the modal HUD footer, or null for none.</summary>
        public virtual string HudText() => null;

        /// <summary>True when the event is a confirm gesture (LMB or Enter/NumpadEnter).</summary>
        protected static bool IsConfirm(Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0) return true;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) return true;
            return false;
        }

        /// <summary>True when the event is a cancel gesture (RMB or Escape).</summary>
        protected static bool IsCancel(Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 1) return true;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) return true;
            return false;
        }

        /// <summary>Release the claimed hotControl and detach from the manager.</summary>
        protected void End()
        {
            if (GUIUtility.hotControl == HotControlId)
                GUIUtility.hotControl = 0;
            BlenderModeManager.EndOperator(this);
        }
    }
}
