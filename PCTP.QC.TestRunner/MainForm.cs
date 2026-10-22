using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QC.TestRunner;

public sealed class MainForm : Form
{
    private readonly Button _run = new() { Text = "Chạy toàn bộ Unit / Regression Tests", AutoSize = true };
    private readonly Label _status = new() { Text = "Sẵn sàng — không kết nối cơ sở dữ liệu.", AutoSize = true };
    private readonly TextBox _output = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill, WordWrap = false, Font = new Font("Consolas", 9)
    };

    public MainForm()
    {
        Text = "PCTP QC Test Runner";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(12), WrapContents = true };
        header.Controls.Add(_run);
        header.Controls.Add(_status);
        Controls.Add(_output);
        Controls.Add(header);
        _run.Click += async (_, _) => await RunTestsAsync();
    }

    private async Task RunTestsAsync()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "PCTP.QC.Tests", "PCTP.QC.Tests.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.Tests.csproj. Hãy chạy TestRunner trong working tree của repository.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _run.Enabled = false;
        _output.Clear();
        _status.Text = "Đang chạy...";
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("test");
            start.ArgumentList.Add(project);
            start.ArgumentList.Add("--configuration");
            start.ArgumentList.Add("Release");
            start.ArgumentList.Add("--logger");
            start.ArgumentList.Add("console;verbosity=normal");

            using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => AppendLine(e.Data);
            process.ErrorDataReceived += (_, e) => AppendLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit());
            _status.Text = process.ExitCode == 0 ? "PASS — toàn bộ test đã hoàn thành." : "FAIL — xem log chi tiết.";
            AppendLine("Exit code: " + process.ExitCode);
        }
        catch (Exception ex)
        {
            _status.Text = "Không thể chạy test.";
            AppendLine(ex.ToString());
        }
        finally
        {
            _run.Enabled = true;
        }
    }

    private void AppendLine(string line)
    {
        if (line == null) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLine), line); return; }
        _output.AppendText(line + Environment.NewLine);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && directory != null; i++, directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WINDOWS_PCTP(NO_Check).sln")))
                return directory.FullName;
        return Directory.GetCurrentDirectory();
    }
}