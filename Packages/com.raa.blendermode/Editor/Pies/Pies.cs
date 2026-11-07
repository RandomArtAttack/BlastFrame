using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// A generic modal that drives a configured <see cref="PieMenu"/>: mouse moves aim, RMB/Esc cancels,
    /// clicking or releasing the trigger key confirms the highlighted slot (per <see cref="PieConfirmMode"/>).
    /// Used by all Phase 3 pies. See DESIGN.md §8.
    /// </summary>
    public class PieOperator : ModalOperator
    {
        readonly PieMenu _pie;
        readonly KeyCode _triggerKey;
        Vector2 _mouse;
        bool _prevWantsMouseMove;

        public PieOperator(PieMenu pie, KeyCode triggerKey)
        {
            _pie = pie;
            _triggerKey = triggerKey;
        }

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);
            _mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            _pie.SetCenter(_mouse);
            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
        }

        public override void HandleEvent(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    _mouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseDown:
                    if (e.button == 1) { Cancel(); e.Use(); return; }
                    if (e.button == 0 && BlenderModePrefs.PieConfirm != PieConfirmMode.ReleaseOnly)
                    { Confirm(); e.Use(); return; }
                    return;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                    e.Use();
                    return;

                case EventType.KeyUp:
                    if (BlenderModePrefs.PieConfirm != PieConfirmMode.ClickOnly && e.keyCode == _triggerKey)
                    {
                        if (_pie.HighlightedIndex(_mouse) >= 0) { Confirm(); e.Use(); return; }
                    }
                    e.Use();
                    return;
            }
        }

        public override void Confirm()
        {
            int i = _pie.HighlightedIndex(_mouse);
            _pie.Invoke(i);
            Finish();
        }

        public override void Cancel() => Finish();

        void Finish()
        {
            if (SceneView != null) SceneView.wantsMouseMove = _prevWantsMouseMove;
            End();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            if (Event.current.type == EventType.Repaint)
                _pie.Draw(_mouse);
        }

        public override string HudText() => "Pie  (release to confirm, RMB/Esc to cancel)";
    }

    /// <summary>
    /// The Phase 3 pie menus: Z shading, ` view, . pivot-point, , orientation. Each builds a
    /// <see cref="PieMenu"/> and starts a <see cref="PieOperator"/>. See DESIGN.md §8.
    /// </summary>
    [InitializeOnLoad]
    public static class Pies
    {
        static Pies() { Register(); }

        static bool _registered;

        /// <summary>Register the four pie action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.PieShading, (sv, e) => { if (!BlenderModePrefs.ModulePies) return false; OpenShading(sv); return true; });
            ActionRegistry.Register(Actions.PieView, (sv, e) => { if (!BlenderModePrefs.ModulePies) return false; OpenView(sv); return true; });
            ActionRegistry.Register(Actions.PiePivot, (sv, e) => { if (!BlenderModePrefs.ModulePies) return false; OpenPivot(sv); return true; });
            ActionRegistry.Register(Actions.PieOrientation, (sv, e) => { if (!BlenderModePrefs.ModulePies) return false; OpenOrientation(sv); return true; });
        }

        // ---------------------------------------------------------------- shading

        static void OpenShading(SceneView sv)
        {
            var pie = new PieMenu();
            Texture wire = IconLibrary.Get("shade_wire");
            Texture solid = IconLibrary.Get("shade_solid");
            Texture mat = IconLibrary.Get("shade_material");
            Texture rend = IconLibrary.Get("shade_rendered");
            Texture eye = IconLibrary.Get("eye");

            // Shared ShadingModes so the pie and the toolbar shading pills stay in sync.
            pie.SetSlot(0, "Wireframe", wire, () => ShadingModes.Apply(sv, ShadingMode.Wireframe));
            pie.SetSlot(2, "Solid", solid, () => ShadingModes.Apply(sv, ShadingMode.Solid));
            pie.SetSlot(4, "Material Preview", mat, () => ShadingModes.Apply(sv, ShadingMode.MaterialPreview));
            pie.SetSlot(6, "Rendered", rend, () => ShadingModes.Apply(sv, ShadingMode.Rendered));
            pie.SetSlot(1, "Toggle Lighting", eye, () =>
            {
                sv.sceneLighting = !sv.sceneLighting;
                sv.Repaint();
                BlenderModePrefs.RaiseChanged();
            });

            BlenderModeManager.StartOperator(new PieOperator(pie, KeyCode.Z), sv);
        }

        // ---------------------------------------------------------------- view

        static void OpenView(SceneView sv)
        {
            var pie = new PieMenu();
            Texture cam = IconLibrary.Get("camera");
            Texture eye = IconLibrary.Get("eye");

            // Reuse the navigation handlers so behaviour stays identical to the numpad keys.
            pie.SetSlot(0, "Top", eye, () => Invoke(Actions.ViewTop, sv));
            pie.SetSlot(1, "Back", eye, () => Invoke(Actions.ViewBack, sv));
            pie.SetSlot(2, "Right", eye, () => Invoke(Actions.ViewRight, sv));
            pie.SetSlot(3, "Frame Selected", eye, () => Invoke(Actions.FrameSelected, sv));
            pie.SetSlot(4, "Bottom", eye, () => Invoke(Actions.ViewBottom, sv));
            pie.SetSlot(5, "Camera", cam, () => Invoke(Actions.ViewCamera, sv));
            pie.SetSlot(6, "Left", eye, () => Invoke(Actions.ViewLeft, sv));
            pie.SetSlot(7, "Front", eye, () => Invoke(Actions.ViewFront, sv));

            BlenderModeManager.StartOperator(new PieOperator(pie, KeyCode.BackQuote), sv);
        }

        static void Invoke(string action, SceneView sv)
        {
            ActionRegistry.Invoke(action, sv, Event.current);
        }

        // ---------------------------------------------------------------- pivot point

        static void OpenPivot(SceneView sv)
        {
            var pie = new PieMenu();
            Texture piv = IconLibrary.Get("pivot");
            Texture cur = IconLibrary.Get("cursor");

            pie.SetSlot(0, "Median Point", piv, () => PivotOrientation.Pivot = PivotMode.MedianPoint);
            pie.SetSlot(2, "Active Element", piv, () => PivotOrientation.Pivot = PivotMode.ActiveElement);
            pie.SetSlot(4, "Individual Origins", piv, () => PivotOrientation.Pivot = PivotMode.IndividualOrigins);
            pie.SetSlot(6, "3D Cursor", cur, () => PivotOrientation.Pivot = PivotMode.Cursor3D);
            pie.SetSlot(1, "Bounding Box", piv, () => PivotOrientation.Pivot = PivotMode.BoundingBoxCenter);

            BlenderModeManager.StartOperator(new PieOperator(pie, KeyCode.Period), sv);
        }

        // ---------------------------------------------------------------- orientation

        static void OpenOrientation(SceneView sv)
        {
            var pie = new PieMenu();
            Texture orient = IconLibrary.Get("orientation");

            pie.SetSlot(0, "Global", orient, () => PivotOrientation.Orientation = TransformOrientationMode.Global);
            pie.SetSlot(1, "Local", orient, () => PivotOrientation.Orientation = TransformOrientationMode.Local);
            pie.SetSlot(2, "Normal", orient, () => PivotOrientation.Orientation = TransformOrientationMode.Normal);
            pie.SetSlot(3, "View", orient, () => PivotOrientation.Orientation = TransformOrientationMode.View);
            pie.SetSlot(6, "Custom", orient, () => PivotOrientation.Orientation = TransformOrientationMode.Custom);
            pie.SetSlot(4, "New from Active", orient, () => PivotOrientation.CreateFromActive("Orientation"));

            BlenderModeManager.StartOperator(new PieOperator(pie, KeyCode.Comma), sv);
        }
    }
}
