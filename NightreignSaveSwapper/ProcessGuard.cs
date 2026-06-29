using System.Diagnostics;

namespace NightreignSaveSwapper;

/// <summary>
/// Best-effort detection of a running Nightreign process. Both vanilla and Seamless Co-op
/// launch the same game executable, so checking these names covers both modes. This is an
/// extra guard only: the confirmation dialog always also reminds the user to close the game,
/// because the exact process name can vary and cannot be fully verified while the game is closed.
/// </summary>
public static class ProcessGuard
{
    private static readonly string[] ProcessNames =
    {
        "nightreign",            // the game itself (vanilla and Seamless both run this)
        "start_protected_game",  // EAC anti-cheat wrapper the game launches under
    };

    public static bool IsGameRunning()
    {
        foreach (var name in ProcessNames)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                    return true;
            }
            catch
            {
                // Enumeration can fail for permission reasons; ignore and fall back to
                // the always-present "close the game" warning in the dialog.
            }
        }
        return false;
    }
}
