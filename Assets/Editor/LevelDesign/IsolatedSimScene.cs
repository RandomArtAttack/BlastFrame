using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>
    /// Swaps the editor to an empty scene for a headless physics simulation and restores the designer's exact
    /// scene setup afterwards. Physics.Simulate steps every loaded scene, so simulations must never run with a
    /// level open. Construct only after the user has saved (see <see cref="TryEnter"/>).
    /// </summary>
    public sealed class IsolatedSimScene : IDisposable
    {
        private readonly SceneSetup[] _setup;

        private IsolatedSimScene()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>Prompts to save modified scenes (interactive) and enters the empty scene. Returns null if the
        /// user cancels or the editor is in play mode.</summary>
        public static IsolatedSimScene TryEnter(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[IsolatedSimScene] Exit play mode first.");
                return null;
            }
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            return new IsolatedSimScene();
        }

        public void Dispose()
        {
            if (_setup != null && _setup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }
    }

    /// <summary>Menu entry: measure the live player tuning and cache the result for the gizmo + lint tools.</summary>
    public static class ReachMeasureMenu
    {
        [MenuItem("Tools/Blast Frame/Level Design/Measure Player Reach (real motor)")]
        private static void Measure()
        {
            var profile = MeasureAndCache(interactive: true);
            if (profile == null) return;
            Debug.Log("[Reach] Profile cached at " + ReachProfile.CachePath + "\n" + profile.ToMarkdown());
            EditorUtility.DisplayDialog("Player reach measured",
                "Measured from " + profile.templateSource + ".\nFull table is in the Console.\n\n" +
                $"Run jump (flat): {profile.Get(MoveKind.RunJump)?.GapAt(0f, false):0.00} m\n" +
                $"Dash jump (flat): {profile.Get(MoveKind.DashJump)?.GapAt(0f, false):0.00} m\n" +
                $"Tallest ledge: {profile.maxLedgeHeight:0.00} m", "OK");
        }

        /// <summary>Captures the player template, runs every course, saves the cache. Null on cancel.</summary>
        public static ReachProfile MeasureAndCache(bool interactive)
        {
            var scene = IsolatedSimScene.TryEnter(interactive);
            if (scene == null) return null;
            try
            {
                var template = PlayerTemplate.Capture();
                using var rig = new MotorSimRig(template);
                var p = ReachMeasurer.MeasureAll(rig, interactive
                    ? (what, t) => EditorUtility.DisplayProgressBar("Measuring player reach", what, t)
                    : null);
                p.Save();
                return p;
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
                scene.Dispose();
            }
        }

        /// <summary>The cached profile if it matches current tuning, else null (caller decides whether to re-measure).</summary>
        public static ReachProfile LoadFresh(out bool stale)
        {
            stale = false;
            var cached = ReachProfile.LoadCached();
            if (cached == null) return null;
            // Only compare against a Player that is already loaded — never open Core just to check.
            var loaded = PlayerTemplate.TryCaptureLoaded();
            if (loaded != null) stale = loaded.Hash != cached.templateHash;
            return cached;
        }
    }
}
