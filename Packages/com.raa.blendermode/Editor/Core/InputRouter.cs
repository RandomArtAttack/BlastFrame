using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RAA.BlenderMode.Navigation;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Registry mapping action ids to handlers. Modules register their handlers on load; the router
    /// invokes them by id. Unregistered ids simply no-op (returns false, event is not consumed), which
    /// is how later-phase bindings coexist in the keymap before their handlers exist.
    /// </summary>
    public static class ActionRegistry
    {
        static readonly Dictionary<string, Func<SceneView, Event, bool>> Handlers =
            new Dictionary<string, Func<SceneView, Event, bool>>();

        /// <summary>Register a handler. Return true from the handler when it consumed the action.</summary>
        public static void Register(string actionId, Func<SceneView, Event, bool> handler)
        {
            if (string.IsNullOrEmpty(actionId) || handler == null) return;
            Handlers[actionId] = handler;
        }

        public static bool Has(string actionId) => actionId != null && Handlers.ContainsKey(actionId);

        /// <summary>Invoke the handler for an action; returns true if it existed and consumed the event.</summary>
        public static bool Invoke(string actionId, SceneView sceneView, Event e)
        {
            if (actionId != null && Handlers.TryGetValue(actionId, out var h))
            {
                try
                {
                    return h(sceneView, e);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Priority dispatch of SceneView events. Order per event: (1) an active ModalOperator gets it
    /// exclusively; (2) otherwise a keymap lookup starts an operator or runs an instant command;
    /// (3) otherwise the navigation module handles MMB/scroll.
    /// </summary>
    public static class InputRouter
    {
        public static void Process(SceneView sceneView)
        {
            Event e = Event.current;
            if (e == null || e.type == EventType.Used) return;

            // (1) Active modal owns everything.
            ModalOperator active = BlenderModeManager.ActiveOperator;
            if (active != null)
            {
                active.HandleEvent(e);
                sceneView.Repaint();
                return;
            }

            // (2) Keymap dispatch on discrete key/mouse presses.
            if ((e.type == EventType.KeyDown || e.type == EventType.MouseDown) && !EditorGUIUtility.editingTextField)
            {
                // Keyboard actions require the pointer to be over the scene view rect.
                bool keyboardOk = e.type != EventType.KeyDown || MouseOverSceneRect(sceneView);
                if (keyboardOk)
                {
                    string action = BlenderModePrefs.Keymap.Match(e, BlenderModePrefs.EmulateNumpad);
                    if (action != null && ModuleEnabledFor(action) && ActionRegistry.Invoke(action, sceneView, e))
                    {
                        e.Use();
                        sceneView.Repaint();
                        return;
                    }
                }
            }

            // (3) Free navigation (MMB orbit/pan/zoom, wheel, Alt+MMB snap).
            if (BlenderModePrefs.ModuleNavigation)
                ViewNavigation.Handle(sceneView);
        }

        /// <summary>True when the mouse is inside the scene view's drawable rect.</summary>
        internal static bool MouseOverSceneRect(SceneView sceneView)
        {
            Vector2 m = Event.current.mousePosition;
            return m.x >= 0f && m.y >= 0f &&
                   m.x <= sceneView.position.width && m.y <= sceneView.position.height;
        }

        /// <summary>Gate an action by its owning module toggle so disabling a module frees its keys.</summary>
        static bool ModuleEnabledFor(string action)
        {
            if (action.StartsWith("view.") || action.StartsWith("orbit.") || action.StartsWith("pan."))
                return BlenderModePrefs.ModuleNavigation;
            // Delete / parent are selection-oriented object ops (DESIGN.md §7) even though they share the
            // "object." prefix with duplicate; gate them under Selection so disabling it frees their keys.
            if (action == Actions.Delete || action == Actions.DeleteAlt ||
                action == Actions.Parent || action == Actions.ClearParent)
                return BlenderModePrefs.ModuleSelection;
            if (action.StartsWith("transform.") || action.StartsWith("object."))
                return BlenderModePrefs.ModuleTransforms;
            if (action.StartsWith("select.") || action.StartsWith("visibility."))
                return BlenderModePrefs.ModuleSelection;
            if (action.StartsWith("pie."))
                return BlenderModePrefs.ModulePies;
            if (action.StartsWith("cursor."))
                return BlenderModePrefs.ModuleCursor;
            if (action.StartsWith("panel."))
                return BlenderModePrefs.ModuleOverlays;
            if (action.StartsWith("bookmark.") || action.StartsWith("set."))
                return BlenderModePrefs.ModuleBookmarks;
            return true;
        }
    }
}
