using System.Diagnostics;
using System.IO;

namespace NightreignSaveSwapper;

/// <summary>
/// The single application window: shows both saves' timestamps/sizes (highlighting the newer
/// one) and offers one button per swap direction, each gated by a confirmation dialog.
/// </summary>
public sealed class MainForm : Form
{
    private string? _folder;

    private readonly Label _folderLabel = new();
    private readonly Label _vanillaLabel = new();
    private readonly Label _seamlessLabel = new();
    private readonly Button _toSeamlessButton = new();
    private readonly Button _toVanillaButton = new();
    private readonly Label _statusLabel = new();

    private static readonly Color RiskRed = Color.FromArgb(178, 34, 34);

    public MainForm()
    {
        Text = "Nightreign Save Swapper";
        Icon = LoadAppIcon();
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 380);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        Load += (_, _) => InitializeFolder();
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "Swap your Nightreign save between Vanilla and Seamless Co-op",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(16, 12),
            Size = new Size(508, 26),
        };

        _folderLabel.AutoSize = false;
        _folderLabel.Location = new Point(16, 42);
        _folderLabel.Size = new Size(508, 20);
        _folderLabel.ForeColor = Color.DimGray;
        _folderLabel.AutoEllipsis = true;

        var savesBox = new GroupBox
        {
            Text = "Current saves",
            Location = new Point(16, 70),
            Size = new Size(508, 96),
        };
        _vanillaLabel.AutoSize = false;
        _vanillaLabel.Location = new Point(14, 26);
        _vanillaLabel.Size = new Size(480, 28);
        _seamlessLabel.AutoSize = false;
        _seamlessLabel.Location = new Point(14, 56);
        _seamlessLabel.Size = new Size(480, 28);
        savesBox.Controls.Add(_vanillaLabel);
        savesBox.Controls.Add(_seamlessLabel);

        _toSeamlessButton.Text = "Vanilla → Seamless   (.sl2 → .co2)";
        _toSeamlessButton.Location = new Point(16, 182);
        _toSeamlessButton.Size = new Size(508, 44);
        _toSeamlessButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _toSeamlessButton.Click += (_, _) => DoSwap(Direction.VanillaToSeamless);

        _toVanillaButton.Text = "Seamless → Vanilla   (.co2 → .sl2)";
        _toVanillaButton.Location = new Point(16, 234);
        _toVanillaButton.Size = new Size(508, 44);
        _toVanillaButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _toVanillaButton.FlatStyle = FlatStyle.Flat;
        _toVanillaButton.FlatAppearance.BorderColor = RiskRed;
        _toVanillaButton.FlatAppearance.BorderSize = 2;
        _toVanillaButton.ForeColor = RiskRed;
        _toVanillaButton.Click += (_, _) => DoSwap(Direction.SeamlessToVanilla);

        var riskNote = new Label
        {
            Text = "⚠  Seamless → Vanilla affects your online / anti-cheat save. Use deliberately.",
            ForeColor = RiskRed,
            AutoSize = false,
            Location = new Point(16, 282),
            Size = new Size(508, 18),
            Font = new Font("Segoe UI", 8.5f),
        };

        var refreshButton = new Button
        {
            Text = "Refresh",
            Location = new Point(16, 312),
            Size = new Size(120, 30),
        };
        refreshButton.Click += (_, _) => RefreshDisplay();

        var openBackupsButton = new Button
        {
            Text = "Open backups folder",
            Location = new Point(146, 312),
            Size = new Size(160, 30),
        };
        openBackupsButton.Click += (_, _) => OpenBackupsFolder();

        _statusLabel.AutoSize = false;
        _statusLabel.Location = new Point(16, 350);
        _statusLabel.Size = new Size(508, 22);
        _statusLabel.ForeColor = Color.DimGray;

        Controls.AddRange(new Control[]
        {
            title, _folderLabel, savesBox, _toSeamlessButton, _toVanillaButton,
            riskNote, refreshButton, openBackupsButton, _statusLabel,
        });
    }

    /// <summary>Load the embedded window/taskbar icon; fall back to default if unavailable.</summary>
    private static Icon? LoadAppIcon()
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("toast.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    // ---- Folder discovery -------------------------------------------------

    private void InitializeFolder()
    {
        try
        {
            var candidates = SaveLocator.FindCandidateFolders();
            switch (candidates.Count)
            {
                case 0:
                    Fail("No Nightreign save was found.\n\n" +
                         $"Looked under:\n{SaveLocator.BaseDir}\n\n" +
                         "Make sure the game has been run and a save exists.");
                    return;
                case 1:
                    _folder = candidates[0];
                    break;
                default:
                    _folder = ChooseFolder(candidates);
                    if (_folder is null) { Fail("No profile folder was selected."); return; }
                    break;
            }

            _folderLabel.Text = "Folder: " + _folder;
            RefreshDisplay();
        }
        catch (SaveLocatorException ex)
        {
            Fail(ex.Message);
        }
    }

    /// <summary>Rare multi-profile case: let the user pick which numbered folder to use.</summary>
    private string? ChooseFolder(IReadOnlyList<string> candidates)
    {
        using var dialog = new Form
        {
            Text = "Choose Nightreign profile",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 220),
        };
        var prompt = new Label
        {
            Text = "More than one Nightreign profile folder was found. Choose the one to use:",
            Location = new Point(12, 12),
            Size = new Size(436, 20),
        };
        var list = new ListBox { Location = new Point(12, 38), Size = new Size(436, 130) };
        foreach (var c in candidates) list.Items.Add(Path.GetFileName(c));
        list.SelectedIndex = 0;
        var ok = new Button
        {
            Text = "Use this folder",
            DialogResult = DialogResult.OK,
            Location = new Point(348, 178),
            Size = new Size(100, 30),
        };
        dialog.Controls.AddRange(new Control[] { prompt, list, ok });
        dialog.AcceptButton = ok;

        return dialog.ShowDialog(this) == DialogResult.OK && list.SelectedIndex >= 0
            ? candidates[list.SelectedIndex]
            : null;
    }

    // ---- Display ----------------------------------------------------------

    private void RefreshDisplay()
    {
        if (_folder is null) return;

        var vanilla = SaveLocator.Describe(_folder, SaveLocator.VanillaExt);
        var seamless = SaveLocator.Describe(_folder, SaveLocator.SeamlessExt);

        bool vanillaNewer = vanilla.Exists && seamless.Exists &&
                            vanilla.ModifiedLocal > seamless.ModifiedLocal;
        bool seamlessNewer = vanilla.Exists && seamless.Exists &&
                             seamless.ModifiedLocal > vanilla.ModifiedLocal;

        ApplySaveLabel(_vanillaLabel, "Vanilla  (.sl2)", vanilla, vanillaNewer);
        ApplySaveLabel(_seamlessLabel, "Seamless (.co2)", seamless, seamlessNewer);

        bool canToSeamless = vanilla.Exists;   // need a source .sl2
        bool canToVanilla = seamless.Exists;   // need a source .co2
        _toSeamlessButton.Enabled = canToSeamless;
        _toVanillaButton.Enabled = canToVanilla;

        _statusLabel.Text = "Ready.";
    }

    private static void ApplySaveLabel(Label label, string name, SaveInfo info, bool newer)
    {
        if (!info.Exists)
        {
            label.Text = $"{name}:  (not present)";
            label.ForeColor = Color.Gray;
            label.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            return;
        }

        var stamp = info.ModifiedLocal!.Value.ToString("yyyy-MM-dd HH:mm:ss");
        var sizeMb = info.Bytes / (1024.0 * 1024.0);
        var marker = newer ? "   ◀ newer" : "";
        label.Text = $"{name}:  {stamp}   ({sizeMb:0.0} MB){marker}";
        label.ForeColor = newer ? Color.FromArgb(0, 110, 0) : Color.Black;
        label.Font = new Font("Segoe UI", 9.5f, newer ? FontStyle.Bold : FontStyle.Regular);
    }

    // ---- Swap -------------------------------------------------------------

    private void DoSwap(Direction direction)
    {
        if (_folder is null) return;

        bool risky = direction == Direction.SeamlessToVanilla;
        string srcName = risky ? "NR0000.co2 (Seamless)" : "NR0000.sl2 (Vanilla)";
        string destName = risky ? "NR0000.sl2 (Vanilla)" : "NR0000.co2 (Seamless)";
        string dirText = risky ? "Seamless → Vanilla" : "Vanilla → Seamless";

        if (ProcessGuard.IsGameRunning())
        {
            var go = MessageBox.Show(this,
                "Elden Ring Nightreign appears to be RUNNING.\n\n" +
                "If you swap now, the game will overwrite the file when it closes and undo " +
                "this swap. Please close the game completely first.\n\nContinue anyway?",
                "Game appears to be running",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (go != DialogResult.Yes) return;
        }

        var message =
            $"Direction:  {dirText}\n\n" +
            $"This will COPY:\n    {srcName}\n" +
            $"OVER (replace):\n    {destName}\n\n" +
            "A timestamped backup of the file being replaced is saved first, so this can be undone.\n\n" +
            "Make sure Elden Ring Nightreign is fully closed before continuing.";

        if (risky)
        {
            message +=
                "\n\n⚠ WARNING — RISKIER DIRECTION ⚠\n" +
                "This overwrites your VANILLA save, which is used for online play with " +
                "anti-cheat. Seamless saves may contain state not present in vanilla, and the " +
                "community has reported ban risk when carrying co-op progress back to vanilla. " +
                "Only continue if you understand and accept this.";
        }

        var icon = risky ? MessageBoxIcon.Warning : MessageBoxIcon.Question;
        var confirm = MessageBox.Show(this, message,
            risky ? "Confirm Seamless → Vanilla (risky)" : "Confirm Vanilla → Seamless",
            MessageBoxButtons.OKCancel, icon, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.OK) return;

        try
        {
            var result = SwapService.Swap(_folder, direction);
            RefreshDisplay();
            _statusLabel.Text = $"Done. Backed up {result.FilesBackedUp} file(s).";
            MessageBox.Show(this,
                $"Swap complete: {dirText}.\n\n" +
                $"{destName} now matches {srcName}.\n\n" +
                $"Backup of the replaced file(s) saved to:\n{result.BackupFolder}",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (SwapException ex)
        {
            _statusLabel.Text = "Swap failed — no changes (or fully backed up).";
            MessageBox.Show(this, ex.Message, "Swap failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Unexpected error.";
            MessageBox.Show(this, "An unexpected error occurred:\n\n" + ex,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private void OpenBackupsFolder()
    {
        try
        {
            Directory.CreateDirectory(SwapService.BackupRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = SwapService.BackupRoot,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the backups folder:\n\n" + ex.Message,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Fail(string message)
    {
        _folderLabel.Text = "Folder: (not found)";
        _statusLabel.Text = "Save folder not available.";
        _toSeamlessButton.Enabled = false;
        _toVanillaButton.Enabled = false;
        MessageBox.Show(this, message, "Nightreign Save Swapper",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
