using System.Diagnostics;
using System.Globalization;

namespace Mana.Launcher;

internal sealed class MainForm : Form
{
    private const int ErrorSuccessRebootInitiated = 1641;
    private const int ErrorSuccessRebootRequired = 3010;

    private readonly Label _description;
    private readonly Button _installButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly LinkLabel _languageLink;

    private bool _msiMissing;

    public MainForm()
    {
        Strings.IsJapanese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Yu Gothic UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        Padding = new Padding(16);
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        _languageLink = new LinkLabel
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, 8),
        };
        _languageLink.Click += (_, _) =>
        {
            Strings.IsJapanese = !Strings.IsJapanese;
            ApplyLanguage();
        };

        var languageRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        languageRow.Controls.Add(_languageLink);

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 8, 0, 12),
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        _installButton = new Button { Margin = new Padding(0, 0, 8, 0) };
        _installButton.Click += OnInstallClicked;
        _cancelButton = new Button();
        _cancelButton.Click += (_, _) => Close();

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
        };
        buttonRow.Controls.Add(_installButton);
        buttonRow.Controls.Add(_cancelButton);

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            WrapContents = false,
        };
        root.Controls.Add(languageRow);
        root.Controls.Add(_description);
        root.Controls.Add(_statusLabel);
        root.Controls.Add(buttonRow);

        Controls.Add(root);
        AcceptButton = _installButton;
        CancelButton = _cancelButton;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Strings.WindowTitle;
        _languageLink.Text = Strings.LanguageSwitchLabel;
        _description.Text = Strings.Description;
        _installButton.Text = Strings.InstallButton;
        _cancelButton.Text = Strings.CancelButton;
        UpdateButtonSizes();

        _msiMissing = !File.Exists(GetMsiPath());
        if (_msiMissing)
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = Strings.MsiNotFoundStatus(MsiFileName);
            _installButton.Enabled = false;
            return;
        }

        _statusLabel.ForeColor = SystemColors.ControlText;
        _statusLabel.Text = string.Empty;
        _installButton.Enabled = true;
    }

    private void UpdateButtonSizes()
    {
        foreach (Button b in new[] { _installButton, _cancelButton })
        {
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        Size shared = new(
            Math.Max(_installButton.PreferredSize.Width, _cancelButton.PreferredSize.Width),
            Math.Max(_installButton.PreferredSize.Height, _cancelButton.PreferredSize.Height));

        foreach (Button b in new[] { _installButton, _cancelButton })
        {
            b.AutoSize = false;
            b.Size = shared;
        }
    }

    private static string MsiFileName => Strings.IsJapanese ? "manaSetup.ja-JP.msi" : "manaSetup.en-US.msi";

    private static string GetMsiPath() => Path.Combine(AppContext.BaseDirectory, MsiFileName);

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        string msiPath = GetMsiPath();
        if (!File.Exists(msiPath))
        {
            MessageBox.Show(this, Strings.MsiNotFoundDialog(MsiFileName), Strings.ErrorTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetBusy(true, Strings.InstallingStatus);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{msiPath}\"",
                UseShellExecute = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                throw new InvalidOperationException(Strings.StartFailed);
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Close();
                return;
            }

            if (process.ExitCode == ErrorSuccessRebootRequired || process.ExitCode == ErrorSuccessRebootInitiated)
            {
                MessageBox.Show(this, Strings.RebootMessage, Strings.RebootTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            MessageBox.Show(this, Strings.FailedMessage(process.ExitCode), Strings.ErrorTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Strings.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    private void SetBusy(bool busy, string status)
    {
        UseWaitCursor = busy;
        _installButton.Enabled = !busy && !_msiMissing;
        _cancelButton.Enabled = !busy;
        _languageLink.Enabled = !busy;
        if (!string.IsNullOrEmpty(status))
        {
            _statusLabel.ForeColor = SystemColors.ControlText;
            _statusLabel.Text = status;
        }
        else if (!_msiMissing)
        {
            _statusLabel.Text = string.Empty;
        }
    }
}
