using System.IO;

namespace NightreignSaveSwapper;

public enum Direction
{
    /// <summary>Vanilla → Seamless: copy NR0000.sl2 onto NR0000.co2.</summary>
    VanillaToSeamless,
    /// <summary>Seamless → Vanilla: copy NR0000.co2 onto NR0000.sl2 (riskier direction).</summary>
    SeamlessToVanilla,
}

/// <summary>Raised for expected, user-facing swap problems (shown verbatim in the UI).</summary>
public sealed class SwapException : Exception
{
    public SwapException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Outcome of a successful swap, used to build the success message.</summary>
public sealed record SwapResult(string BackupFolder, int FilesBackedUp);

/// <summary>
/// Safety-critical core: always back up the destination side first, then overwrite the
/// destination main save with the source's bytes. Never edits save contents, never touches
/// the source, and backups are timestamped and never auto-deleted.
/// </summary>
public static class SwapService
{
    /// <summary>%AppData%\Nightreign\_save_swapper_backups</summary>
    public static string BackupRoot => Path.Combine(SaveLocator.BaseDir, "_save_swapper_backups");

    private static (string srcExt, string destExt) Exts(Direction d) => d switch
    {
        Direction.VanillaToSeamless => (SaveLocator.VanillaExt, SaveLocator.SeamlessExt),
        Direction.SeamlessToVanilla => (SaveLocator.SeamlessExt, SaveLocator.VanillaExt),
        _ => throw new ArgumentOutOfRangeException(nameof(d)),
    };

    private static string MainPath(string folder, string ext) =>
        Path.Combine(folder, $"{SaveLocator.BaseName}.{ext}");

    /// <summary>
    /// All files for a given extension that share the NR0000.&lt;ext&gt; base — i.e. the main
    /// save plus any companion files such as NR0000.sl2.bak. Companions are discovered, not
    /// hardcoded, because their presence and naming vary per install.
    /// </summary>
    public static IReadOnlyList<string> GetCompanions(string folder, string ext)
    {
        // e.g. "NR0000.sl2*" matches NR0000.sl2 and NR0000.sl2.bak but never NR0000.co2.
        return Directory.GetFiles(folder, $"{SaveLocator.BaseName}.{ext}*");
    }

    /// <summary>
    /// Perform the swap for the given direction. Steps:
    /// 1) verify the source main exists; 2) back up every destination-side file into a fresh
    /// timestamped folder; 3) atomically overwrite the destination main with the source bytes.
    /// </summary>
    public static SwapResult Swap(string folder, Direction direction)
    {
        var (srcExt, destExt) = Exts(direction);
        var srcMain = MainPath(folder, srcExt);
        var destMain = MainPath(folder, destExt);

        // 1. Source must exist.
        if (!File.Exists(srcMain))
        {
            throw new SwapException(
                $"The source save is missing, so there is nothing to copy:\n{srcMain}\n\n" +
                "Make sure you have played in that mode at least once.");
        }

        // 2. Back up the destination side (main + all companions). Always, before any write.
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var backupFolder = Path.Combine(BackupRoot, stamp);
        int backedUp = 0;
        try
        {
            Directory.CreateDirectory(backupFolder);
            foreach (var file in GetCompanions(folder, destExt))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(backupFolder, name), overwrite: true);
                backedUp++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SwapException(
                "Could not create the safety backup, so no changes were made.\n\n" +
                $"Backup folder:\n{backupFolder}\n\n{ex.Message}", ex);
        }

        // 3. Overwrite the destination main only, via temp-then-move so an interrupted write
        //    can never leave a half-written (corrupt) destination save.
        var temp = destMain + ".swaptmp";
        try
        {
            File.Copy(srcMain, temp, overwrite: true);
            File.Move(temp, destMain, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new SwapException(
                "Could not write the destination save. It may be locked by the game.\n\n" +
                "Close Elden Ring Nightreign completely and try again.\n" +
                $"(Your backup is safe at:\n{backupFolder})\n\n{ex.Message}", ex);
        }

        return new SwapResult(backupFolder, backedUp);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
