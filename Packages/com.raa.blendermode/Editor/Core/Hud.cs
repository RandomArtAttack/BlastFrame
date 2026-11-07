using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Viewport HUD drawn during modal operations, Blender-header style. Rendered inside a
    /// <see cref="Handles.BeginGUI"/> pass so it overlays the scene without being part of an Overlay.
    /// </summary>
    public static class Hud
    {
        static GUIStyle _footerStyle;
        static GUIStyle _headerStyle;

        static GUIStyle FooterStyle
        {
            get
            {
                if (_footerStyle == null)
                {
                    _footerStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(8, 8, 4, 4),
                        richText = true,
                        fontSize = 11
                    };
                    _footerStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
                }
                return _footerStyle;
            }
        }

        static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(FooterStyle) { alignment = TextAnchor.MiddleLeft };
                }
                return _headerStyle;
            }
        }

        /// <summary>Draw a footer bar with the given text at the bottom-left of the scene view.</summary>
        public static void DrawFooter(SceneView sceneView, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!BlenderModePrefs.OverlaysVisible) return;

            Handles.BeginGUI();
            Vector2 size = FooterStyle.CalcSize(new GUIContent(text));
            float w = Mathf.Max(size.x + 16f, 160f);
            var rect = new Rect(8f, sceneView.position.height - 30f, w, 22f);
            DrawBackground(rect);
            GUI.Label(rect, text, FooterStyle);
            Handles.EndGUI();
        }

        /// <summary>Draw a header bar with the given text at the top-left of the scene view.</summary>
        public static void DrawHeader(SceneView sceneView, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!BlenderModePrefs.OverlaysVisible) return;

            Handles.BeginGUI();
            Vector2 size = HeaderStyle.CalcSize(new GUIContent(text));
            float w = Mathf.Max(size.x + 16f, 160f);
            var rect = new Rect(8f, 8f, w, 22f);
            DrawBackground(rect);
            GUI.Label(rect, text, HeaderStyle);
            Handles.EndGUI();
        }

        static void DrawBackground(Rect rect)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Show a transient scene-view notification (used for one-shot messages).</summary>
        public static void Notify(SceneView sceneView, string message)
        {
            if (sceneView != null && !string.IsNullOrEmpty(message))
                sceneView.ShowNotification(new GUIContent(message));
        }
    }
}
