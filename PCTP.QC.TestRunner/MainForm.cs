using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QC.TestRunner;

public sealed class MainForm : Form
{
    private readonly TextBox _user = new() { Width = 150 };
    private readonly TextBox _password = new() { Width = 150, UseSystemPasswordChar = true };
    private readonly Button _unitRun = new() { Text = "Unit / Regression", AutoSize = true };
    private readonly Button _sqlPreflight = new() { Text = "SQL Preflight", AutoSize = true };
    private readonly Button _sqlRun = new() { Text = "★ FULL SQL TEST", AutoSize = true };
    private readonly Label _status = new() { Text = "Sẵn sàng.", AutoSize = true };
    private readonly TextBox _output = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill, WordWrap = false, Font = new Font("Consolas", 9)
    };

    public MainForm()
    {
        Text = "PCTP QC Test Runner";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 125,
            Padding = new Padding(12),
            WrapContents = true,
            AutoScroll = true
        };

        header.Controls.Add(new Label { Text = "SQL Test Server:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        header.Controls.Add(new Label { Text = "192.168.200.57 / B7R2_FCC", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 8, 18, 3) });
        header.Controls.Add(new Label { Text = "SQL User:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        header.Controls.Add(_user);
        header.Controls.Add(new Label { Text = "Password:", AutoSize = true, Margin = new Padding(12, 8, 3, 3) });
        header.Controls.Add(_password);
        header.Controls.Add(new Label { Text = "Production 192.168.200.14\\BRAVO is blocked.", AutoSize = true, Margin = new Padding(12, 8, 3, 3) });
        header.Controls.Add(_unitRun);
        header.Controls.Add(_sqlPreflight);
        header.Controls.Add(_sqlRun);
        header.Controls.Add(_status);

        Controls.Add(_output);
        Controls.Add(header);

        _unitRun.Click += async (_, _) => await RunUnitTestsAsync();
        _sqlPreflight.Click += async (_, _) => await RunSqlIntegrationAsync(preflightOnly: true);
        _sqlRun.Click += async (_, _) => await RunSqlIntegrationAsync(preflightOnly: false);
    }

    private async Task RunUnitTestsAsync()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "PCTP.QC.Tests", "PCTP.QC.Tests.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.Tests.csproj.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetRunning(true, "Đang chạy Unit / Regression...");
        try
        {
            var exitCode = await RunProcessAsync(root, "dotnet", new[]
            {
                "test", project, "--configuration", "Release", "--logger", "console;verbosity=normal"
            });
            SetFinished(exitCode == 0, exitCode == 0 ? "PASS — Unit / Regression." : "FAIL — Unit / Regression.");
        }
        catch (Exception ex)
        {
            AppendLine(ex.ToString());
            SetFinished(false, "FAIL — không thể chạy Unit / Regression.");
        }
    }

    private async Task RunSqlIntegrationAsync(bool preflightOnly)
    {
        if (string.IsNullOrWhiteSpace(_user.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Nhập SQL User và Password của môi trường Test 192.168.200.57.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "PCTP.QC.SqlIntegration", "PCTP.QC.SqlIntegration.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.SqlIntegration.csproj.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetRunning(true, preflightOnly ? "Đang kiểm tra SQL Preflight..." : "Đang chạy FULL SQL TEST...");
        try
        {
            var arguments = new[]
            {
                "run", "--project", project, "--configuration", "Release", "--",
                "--user", _user.Text,
                "--password", _password.Text,
                ...(preflightOnly ? new[] { "--preflight" } : Array.Empty<string>())
            };

            var exitCode = await RunProcessAsync(root, "dotnet", arguments);
            SetFinished(exitCode == 0,
                exitCode == 0
                    ? (preflightOnly ? "PASS — SQL Preflight." : "PASS — FULL SQL TEST.")
                    : (preflightOnly ? "FAIL — SQL Preflight." : "FAIL — FULL SQL TEST."));
        }
        catch (Exception ex)
        {
            AppendLine(ex.ToString());
            SetFinished(false, "FAIL — không thể chạy SQL Test.");
        }
    }

    private async Task<int> RunProcessAsync(string workingDirectory, string fileName, string[] arguments)
    {
        _output.Clear();
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => AppendLine(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await Task.Run(() => process.WaitForExit());
        AppendLine("Exit code: " + process.ExitCode);
        return process.ExitCode;
    }

    private void SetRunning(bool running, string status)
    {
        _unitRun.Enabled = !running;
        _sqlPreflight.Enabled = !running;
        _sqlRun.Enabled = !running;
        _status.Text = status;
        if (running) _output.Clear();
    }

    private void SetFinished(bool success, string status)
    {
        _unitRun.Enabled = true;
        _sqlPreflight.Enabled = true;
        _sqlRun.Enabled = true;
        _status.Text = status;
    }

    private void AppendLine(string? line)
    {
        if (line == null) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLine), line);
            return;
        }
        _output.AppendText(line + Environment.NewLine);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WINDOWS_PCTP(NO_Check).sln")))
                return directory.FullName;
        return Directory.GetCurrentDirectory();
    }
}
