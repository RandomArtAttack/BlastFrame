using UnityEditor;

namespace RAA.BlenderMode
{
    /// <summary>Menu commands under Tools/Blender Mode.</summary>
    static class BlenderModeMenu
    {
        const string EnabledPath = "Tools/Blender Mode/Enabled";
        const string PreferencesPath = "Tools/Blender Mode/Preferences...";

        // "%&b" = Ctrl+Alt+B.
        [MenuItem(EnabledPath + " %&b", false, 0)]
        static void ToggleEnabled()
        {
            BlenderModePrefs.Enabled = !BlenderModePrefs.Enabled;
        }

        [MenuItem(EnabledPath + " %&b", true)]
        static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(EnabledPath, BlenderModePrefs.Enabled);
            return true;
        }

        [MenuItem(PreferencesPath, false, 20)]
        static void OpenPreferences()
        {
            SettingsService.OpenUserPreferences("Preferences/Blender Mode");
        }
    }
}
