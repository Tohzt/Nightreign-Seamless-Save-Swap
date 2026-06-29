using System.IO;
using NightreignSaveSwapper;

// Standalone verification of the safety-critical SwapService against a sandbox folder.
// Never touches the real Nightreign save. Cleans up after itself, including any backup
// folders created under %AppData%\Nightreign\_save_swapper_backups.

int failures = 0;
var createdBackups = new List<string>();

void Check(string label, bool ok)
{
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label}");
    if (!ok) failures++;
}

string sandbox = Path.Combine(Path.GetTempPath(), "nrss_verify_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);

string P(string ext) => Path.Combine(sandbox, $"NR0000.{ext}");

try
{
    // --- Setup: vanilla + its .bak companion, and a seamless save -------------
    File.WriteAllText(P("sl2"), "VANILLA");
    File.WriteAllText(P("sl2") + ".bak", "VANILLA_BAK");
    File.WriteAllText(P("co2"), "SEAMLESS");

    Console.WriteLine("Test 1: companion discovery");
    var sl2Companions = SwapService.GetCompanions(sandbox, "sl2");
    var co2Companions = SwapService.GetCompanions(sandbox, "co2");
    Check("sl2 discovers main + .bak (2 files)", sl2Companions.Count == 2);
    Check("sl2 glob does NOT pick up .co2", sl2Companions.All(f => !f.EndsWith(".co2")));
    Check("co2 discovers only main (1 file)", co2Companions.Count == 1);

    Console.WriteLine("Test 2: Vanilla -> Seamless copies main only, backs up destination");
    var r1 = SwapService.Swap(sandbox, Direction.VanillaToSeamless);
    createdBackups.Add(r1.BackupFolder);
    Check("co2 now holds vanilla bytes", File.ReadAllText(P("co2")) == "VANILLA");
    Check("sl2 (source) unchanged", File.ReadAllText(P("sl2")) == "VANILLA");
    Check("backup captured 1 destination file (old co2)", r1.FilesBackedUp == 1);
    Check("backup folder contains old co2 content",
        File.Exists(Path.Combine(r1.BackupFolder, "NR0000.co2")) &&
        File.ReadAllText(Path.Combine(r1.BackupFolder, "NR0000.co2")) == "SEAMLESS");
    Check("no leftover .swaptmp", !File.Exists(P("co2") + ".swaptmp"));

    Console.WriteLine("Test 3: Seamless -> Vanilla backs up main + .bak companion");
    File.WriteAllText(P("co2"), "NEWSEAMLESS");
    var r2 = SwapService.Swap(sandbox, Direction.SeamlessToVanilla);
    createdBackups.Add(r2.BackupFolder);
    Check("sl2 now holds seamless bytes", File.ReadAllText(P("sl2")) == "NEWSEAMLESS");
    Check("backup captured 2 destination files (sl2 + sl2.bak)", r2.FilesBackedUp == 2);
    Check("sl2.bak backed up with original content",
        File.Exists(Path.Combine(r2.BackupFolder, "NR0000.sl2.bak")) &&
        File.ReadAllText(Path.Combine(r2.BackupFolder, "NR0000.sl2.bak")) == "VANILLA_BAK");

    Console.WriteLine("Test 4: missing source throws SwapException, makes no changes");
    File.Delete(P("co2"));
    bool threw = false;
    try { SwapService.Swap(sandbox, Direction.SeamlessToVanilla); }
    catch (SwapException) { threw = true; }
    Check("Swap with missing co2 source throws SwapException", threw);

    Console.WriteLine("Test 5: first-ever transfer into a missing destination works");
    // Remove co2 entirely; copy sl2 -> co2 should create it with empty backup.
    var r3 = SwapService.Swap(sandbox, Direction.VanillaToSeamless);
    createdBackups.Add(r3.BackupFolder);
    Check("co2 created from sl2", File.Exists(P("co2")) && File.ReadAllText(P("co2")) == "NEWSEAMLESS");
    Check("backup of absent destination is empty (0 files)", r3.FilesBackedUp == 0);
}
finally
{
    try { Directory.Delete(sandbox, recursive: true); } catch { }
    foreach (var b in createdBackups)
    {
        try { if (Directory.Exists(b)) Directory.Delete(b, recursive: true); } catch { }
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
