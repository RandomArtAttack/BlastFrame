# unity-jiggler.ps1
# ---------------------------------------------------------------------------
# Keeps the Unity editor fed with REAL input so the mcp-unity bridge's
# EditorApplication.update tick keeps running and MCP requests get serviced
# while you're not actively touching Unity.
#
# WHY THIS EXISTS: the editor's update tick (which pumps the MCP command queue)
# goes dormant when Unity is not the active app. Window-poking does NOT wake it
# (verified) -- only real input does, and real input only reaches the FOREGROUND
# window. So this script only injects input while Unity.exe is the foreground
# window; it never touches input when any other app (your browser, etc.) is in
# front. Keep Unity focused on one monitor and watch video on another; let the
# cursor rest over the Unity window for the mouse nudge to land on it.
#
# Run:   powershell -ExecutionPolicy Bypass -File .\unity-jiggler.ps1
# Stop:  Ctrl+C
#
# Params:
#   -IntervalMs   how often to nudge while Unity is foreground (default 100ms)
#   -ProcessName  foreground process that enables nudging (default "Unity")
#   -NoMouse      send only the F15 keystroke, skip the mouse nudge (no cursor
#                 twitch). Try this first; add the mouse nudge only if the tick
#                 still doesn't wake.
# ---------------------------------------------------------------------------
param(
    [int]$IntervalMs = 100,
    [string]$ProcessName = "Unity",
    [switch]$NoMouse
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Jig {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint MOUSE_MOVE = 0x0001;
    public const uint KEY_UP     = 0x0002;
    public const byte VK_F15     = 0x7E; // no-op key: no physical keyboard has it, apps ignore it
}
"@

function Get-ForegroundProcessName {
    $h = [Jig]::GetForegroundWindow()
    if ($h -eq [IntPtr]::Zero) { return $null }
    [uint32]$procId = 0
    [void][Jig]::GetWindowThreadProcessId($h, [ref]$procId)
    try { return (Get-Process -Id $procId -ErrorAction Stop).ProcessName } catch { return $null }
}

$mode = if ($NoMouse) { "F15 only" } else { "F15 + 1px mouse nudge" }
Write-Host "Unity jiggler running ($mode). Active only while '$ProcessName' is the foreground window. Ctrl+C to stop." -ForegroundColor Green

$lastActive = $null
while ($true) {
    $fg = Get-ForegroundProcessName
    $active = ($fg -eq $ProcessName)

    if ($active -ne $lastActive) {
        if ($active) {
            Write-Host ("{0}  '{1}' foreground -> feeding input" -f (Get-Date -Format HH:mm:ss), $fg) -ForegroundColor Cyan
        } else {
            $name = if ($fg) { $fg } else { "(none)" }
            Write-Host ("{0}  '{1}' foreground -> idle (Unity not focused)" -f (Get-Date -Format HH:mm:ss), $name) -ForegroundColor DarkGray
        }
        $lastActive = $active
    }

    if ($active) {
        # Keystroke: goes to the focused window (Unity) regardless of cursor position.
        [Jig]::keybd_event([Jig]::VK_F15, 0, 0, [UIntPtr]::Zero)
        [Jig]::keybd_event([Jig]::VK_F15, 0, [Jig]::KEY_UP, [UIntPtr]::Zero)

        if (-not $NoMouse) {
            # Net-zero 1px mouse nudge: lands on whatever window is under the cursor,
            # so keep the cursor over the Unity window. Proven to wake the editor tick.
            [Jig]::mouse_event([Jig]::MOUSE_MOVE, 1, 0, 0, [UIntPtr]::Zero)
            [Jig]::mouse_event([Jig]::MOUSE_MOVE, -1, 0, 0, [UIntPtr]::Zero)
        }
    }

    Start-Sleep -Milliseconds $IntervalMs
}
