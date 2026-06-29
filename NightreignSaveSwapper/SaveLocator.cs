using System.IO;

namespace NightreignSaveSwapper;

/// <summary>
/// Raised for expected, user-facing problems locating the save folder.
/// The UI shows the message verbatim in a MessageBox rather than crashing.
/// </summary>
public sealed class SaveLocatorException : Exception
{
    public SaveLocatorException(string message) : base(message) { }
}

/// <summary>Snapshot of one save file (e.g. NR0000.sl2) for display.</summary>
public sealed record SaveInfo(string Ext, bool Exists, DateTime? ModifiedLocal, long Bytes);

/// <summary>
/// Finds the Nightreign save folder under %AppData%\Nightreign\&lt;SteamID&gt;\ and
/// describes the two saves. No user input is required in the normal single-folder case.
/// </summary>
public static class SaveLocator
{
    public const string BaseName = "NR0000";
    public const string VanillaExt = "sl2";   // official / vanilla save
    public const string SeamlessExt = "co2";  // Seamless Co-op mod save

    /// <summary>%AppData%\Nightreign (Roaming).</summary>
    public static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nightreign");

    private static string MainFile(string ext) => $"{BaseName}.{ext}";

    /// <summary>True if the folder contains at least one of the NR0000 saves.</summary>
    private static bool HasSaves(string folder) =>
        File.Exists(Path.Combine(folder, MainFile(VanillaExt))) ||
        File.Exists(Path.Combine(folder, MainFile(SeamlessExt)));

    /// <summary>
    /// Returns every numbered profile folder that actually contains a save.
    /// Empty result and multi-folder results are handled by the caller.
    /// </summary>
    public static IReadOnlyList<string> FindCandidateFolders()
    {
        if (!Directory.Exists(BaseDir))
        {
            throw new SaveLocatorException(
                $"Nightreign save folder not found:\n{BaseDir}\n\n" +
                "Has Elden Ring Nightreign been run at least once on this PC?");
        }

        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(BaseDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SaveLocatorException(
                $"Could not read the Nightreign folder:\n{BaseDir}\n\n{ex.Message}");
        }

        return subDirs.Where(HasSaves).ToList();
    }

    /// <summary>Describe a single save (existence, last-modified local time, size).</summary>
    public static SaveInfo Describe(string folder, string ext)
    {
        var path = Path.Combine(folder, MainFile(ext));
        if (!File.Exists(path))
            return new SaveInfo(ext, false, null, 0);

        var fi = new FileInfo(path);
        return new SaveInfo(ext, true, fi.LastWriteTime, fi.Length);
    }
}
