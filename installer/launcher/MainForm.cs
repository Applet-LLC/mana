using System.Diagnostics;
using System.Globalization;
using System.Reflection;

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

        const int contentWidth = 440;

        _languageLink = new LinkLabel
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0),
        };
        _languageLink.Click += (_, _) =>
        {
            Strings.IsJapanese = !Strings.IsJapanese;
            ApplyLanguage();
        };

        var languageRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Width = contentWidth,
            Height = 22,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0),
        };
        languageRow.Controls.Add(_languageLink);

        _description = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, 8, 0, 12),
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(contentWidth, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        // 既定 Margin(3) だと FlowLayoutPanel 内で縦位置がずれるため明示する
        _installButton = new Button { Margin = new Padding(0), AutoSize = false };
        _installButton.Click += OnInstallClicked;
        _cancelButton = new Button { Margin = new Padding(0), AutoSize = false };
        _cancelButton.Click += (_, _) => Close();

        // RightToLeft: 先に追加したコントロールが右端（[インストール][キャンセル] でキャンセルが右）
        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Width = contentWidth,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _installButton.Margin = new Padding(0, 0, 8, 0);
        buttonRow.Controls.Add(_cancelButton);
        buttonRow.Controls.Add(_installButton);

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
        // 最低でも標準ダイアログボタン相当の高さを確保
        shared.Height = Math.Max(shared.Height, 28);

        foreach (Button b in new[] { _installButton, _cancelButton })
        {
            b.AutoSize = false;
            b.Size = shared;
            b.Margin = b == _installButton ? new Padding(0, 0, 8, 0) : new Padding(0);
        }

        if (_installButton.Parent is FlowLayoutPanel row)
        {
            row.Height = shared.Height;
        }
    }

    private static string DisplayVersion
    {
        get
        {
            string? info = typeof(MainForm).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            // "1.00+git" などが付く場合は '+' より前だけ使う
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info.IndexOf('+');
                if (plus >= 0)
                {
                    info = info[..plus];
                }

                info = info.Trim();
            }

            return string.IsNullOrWhiteSpace(info) ? "1.00" : info;
        }
    }

    private static string MsiFileName =>
        Strings.IsJapanese
            ? $"manaSetup-{DisplayVersion}-ja-JP.msi"
            : $"manaSetup-{DisplayVersion}-en-US.msi";

    /// <summary>
    /// 単一ファイル公開や管理者昇格後でも、exe のあるフォルダを返す。
    /// AppContext.BaseDirectory だけだと一時展開先や System32 側を指すことがある。
    /// </summary>
    private static string GetAppDirectory()
    {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            string? dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir;
            }
        }

        try
        {
            string? dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir;
            }
        }
        catch
        {
        }

        return AppContext.BaseDirectory;
    }

    private static string GetMsiPath() => Path.Combine(GetAppDirectory(), MsiFileName);

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
            // UseShellExecute=true だと msiexec の待機ハンドルが取れず、UI を出さずにすぐ戻ることがある。
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                Arguments = $"/i \"{msiPath}\"",
                UseShellExecute = false,
                WorkingDirectory = GetAppDirectory(),
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

            // ユーザーが MSI の UI でキャンセルした場合
            if (process.ExitCode == 1602)
            {
                SetBusy(false, string.Empty);
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
