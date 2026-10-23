using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QC.TestRunner;

public sealed class MainForm : Form
{
    private readonly TextBox _user = new() { Width = 135 };
    private readonly TextBox _password = new() { Width = 145, UseSystemPasswordChar = true };
    private readonly Button _unitRun = new() { Text = "Unit / Regression", AutoSize = true };
    private readonly Button _sqlPreflight = new() { Text = "SQL 57 Read-only Check", AutoSize = true };
    private readonly Button _deliveryPreflight = new() { Text = "SQL Delivery SP Check", AutoSize = true };
    private readonly Button _sqlRun = new() { Text = "Full SQL Integration", AutoSize = true };
    private readonly Button _stop = new() { Text = "Dừng kiểm thử", AutoSize = true, Enabled = false };
    private readonly Button _export = new() { Text = "Xuất báo cáo HTML", AutoSize = true };
    private readonly Button _openFolder = new() { Text = "Mở thư mục báo cáo", AutoSize = true };
    private readonly Button _clearOutput = new() { Text = "Xóa log màn hình", AutoSize = true };
    private readonly Label _status = new() { Text = "Sẵn sàng.", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Label _runCount = new() { AutoSize = true };
    private readonly Label _passCount = new() { AutoSize = true, ForeColor = Color.SeaGreen };
    private readonly Label _failCount = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Label _infoCount = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _caseSummary = new() { AutoSize = true, ForeColor = Color.DarkSlateBlue, Text = "Test cases: chưa có dữ liệu" };
    private readonly TextBox _output = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill, WordWrap = false, Font = new Font("Consolas", 9)
    };
    private readonly DataGridView _events = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false
    };
    private readonly DataGridView _historyGrid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false
    };
    private readonly BindingSource _eventSource = new();
    private readonly BindingSource _historySource = new();
    private readonly BindingSource _matrixSource = new();
    private readonly DataGridView _matrixGrid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false
    };
    private readonly ComboBox _matrixFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly Label _matrixSummary = new() { AutoSize = true };
    private readonly List<TestEvent> _eventItems = new();
    private readonly List<RunRecord> _history = new();
    private readonly List<MatrixRow> _matrixRows = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private Process _activeProcess;
    private RunRecord _activeRun;
    private string _root;
    private string _reportDirectory;
    private int _pass;
    private int _fail;
    private int _info;

    public MainForm()
    {
        Text = "PCTP QC — Test Dashboard";
        Width = 1280;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 650);
        Font = new Font("Segoe UI", 9);

        _root = FindRepositoryRoot();
        _reportDirectory = Path.Combine(_root, "TestResults", "PCTP.QC.TestRunner");
        Directory.CreateDirectory(_reportDirectory);
        ConfigureGrids();
        BuildLayout();
        LoadHistory();
        LoadMatrix();

        _unitRun.Click += async (_, _) => await RunUnitTestsAsync();
        _sqlPreflight.Click += async (_, _) => await RunSqlIntegrationAsync(preflightOnly: true);
        _deliveryPreflight.Click += async (_, _) => await RunSqlDeliveryPreflightAsync();
        _sqlRun.Click += async (_, _) => await RunSqlIntegrationAsync(preflightOnly: false);
        _stop.Click += (_, _) => StopActiveProcess();
        _export.Click += (_, _) => ExportHtmlReport();
        _openFolder.Click += (_, _) => OpenReportFolder();
        _clearOutput.Click += (_, _) => _output.Clear();
        _historyGrid.CellDoubleClick += (_, _) => OpenSelectedHistory();
        _events.SelectionChanged += (_, _) => HighlightSelectedEvent();
        UpdateCounters();
    }

    private void BuildLayout()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 6) };
        header.Controls.Add(new Label { Text = "SQL Test:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        header.Controls.Add(new Label { Text = "192.168.200.57 / B7R2_FCC", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 8, 14, 3) });
        header.Controls.Add(new Label { Text = "SQL User:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        header.Controls.Add(_user);
        header.Controls.Add(new Label { Text = "Password:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) });
        header.Controls.Add(_password);
        header.Controls.Add(_unitRun);
        header.Controls.Add(_sqlPreflight);
        header.Controls.Add(_deliveryPreflight);
        header.Controls.Add(_sqlRun);
        header.Controls.Add(_stop);
        header.Controls.Add(new Label { Text = "Production 192.168.200.14\\BRAVO: blocked", AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(8, 8, 3, 3) });

        var summary = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 8) };
        summary.Controls.Add(_status);
        summary.Controls.Add(new Label { Text = "   |   ", AutoSize = true });
        summary.Controls.Add(_runCount);
        summary.Controls.Add(new Label { Text = "   |   ", AutoSize = true });
        summary.Controls.Add(_passCount);
        summary.Controls.Add(new Label { Text = "   |   ", AutoSize = true });
        summary.Controls.Add(_failCount);
        summary.Controls.Add(new Label { Text = "   |   ", AutoSize = true });
        summary.Controls.Add(_infoCount);
        summary.Controls.Add(new Label { Text = "   |   ", AutoSize = true });
        summary.Controls.Add(_caseSummary);
        summary.Controls.Add(_export);
        summary.Controls.Add(_openFolder);
        summary.Controls.Add(_clearOutput);

        var eventPage = new TabPage("Chi tiết PASS / FAIL / INFO");
        _events.DataSource = _eventSource;
        eventPage.Controls.Add(_events);

        var logPage = new TabPage("Log trực tiếp");
        logPage.Controls.Add(_output);

        var historyPage = new TabPage("Lịch sử chạy (double-click để mở log)");
        _historyGrid.DataSource = _historySource;
        historyPage.Controls.Add(_historyGrid);

        var matrixPage = new TabPage("Ma trận kiểm thử");
        var matrixLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6) };
        matrixLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        matrixLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var matrixToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        matrixToolbar.Controls.Add(new Label { Text = "Lọc trạng thái:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        _matrixFilter.Items.AddRange(new object[] { "Tất cả", "implemented", "partial", "planned", "blocked" });
        _matrixFilter.SelectedIndex = 0;
        _matrixFilter.SelectedIndexChanged += (_, _) => ApplyMatrixFilter();
        matrixToolbar.Controls.Add(_matrixFilter);
        matrixToolbar.Controls.Add(_matrixSummary);
        matrixLayout.Controls.Add(matrixToolbar, 0, 0);
        _matrixGrid.DataSource = _matrixSource;
        matrixLayout.Controls.Add(_matrixGrid, 0, 1);
        matrixPage.Controls.Add(matrixLayout);

        _tabs.TabPages.Add(eventPage);
        _tabs.TabPages.Add(logPage);
        _tabs.TabPages.Add(historyPage);
        _tabs.TabPages.Add(matrixPage);

        outer.Controls.Add(header, 0, 0);
        outer.Controls.Add(summary, 0, 1);
        outer.Controls.Add(_tabs, 0, 2);
        Controls.Add(outer);
    }

    private void ConfigureGrids()
    {
        _events.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TestEvent.Time), HeaderText = "Thời gian", FillWeight = 18 });
        _events.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TestEvent.Level), HeaderText = "Kết quả", FillWeight = 12 });
        _events.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TestEvent.Source), HeaderText = "Nhóm kiểm thử", FillWeight = 24 });
        _events.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TestEvent.Message), HeaderText = "Chi tiết", FillWeight = 70 });

        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunRecord.StartedAt), HeaderText = "Bắt đầu", FillWeight = 22 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunRecord.TestName), HeaderText = "Bài kiểm thử", FillWeight = 30 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunRecord.Status), HeaderText = "Trạng thái", FillWeight = 15 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunRecord.Duration), HeaderText = "Thời lượng", FillWeight = 15 });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RunRecord.LogPath), HeaderText = "Log", FillWeight = 50 });

        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.Id), HeaderText = "ID", FillWeight = 10 });
        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.Module), HeaderText = "Module", FillWeight = 20 });
        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.Status), HeaderText = "Trạng thái", FillWeight = 14 });
        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.Scenario), HeaderText = "Kịch bản", FillWeight = 38 });
        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.Expected), HeaderText = "Kết quả mong đợi", FillWeight = 38 });
        _matrixGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MatrixRow.TestEvidence), HeaderText = "Bằng chứng test", FillWeight = 38 });
    }

    private async Task RunUnitTestsAsync()
    {
        var project = Path.Combine(_root, "PCTP.QC.Tests", "PCTP.QC.Tests.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.Tests.csproj.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await RunTestAsync("Unit / Regression", "test", new[]
        {
            "test", project, "--configuration", "Release", "--logger", "console;verbosity=normal"
        }, needsSqlCredentials: false);
    }

    private async Task RunSqlIntegrationAsync(bool preflightOnly)
    {
        if (string.IsNullOrWhiteSpace(_user.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Nhập SQL User và Password của môi trường Test 192.168.200.57.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var project = Path.Combine(_root, "PCTP.QC.SqlIntegration", "PCTP.QC.SqlIntegration.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.SqlIntegration.csproj.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var args = new List<string> { "run", "--project", project, "--configuration", "Release", "--" };
        if (preflightOnly) args.Add("--preflight");
        await RunTestAsync(preflightOnly ? "SQL 57 Read-only Check" : "Full SQL Integration", "sql", args.ToArray(), needsSqlCredentials: true);
    }

    private async Task RunSqlDeliveryPreflightAsync()
    {
        if (string.IsNullOrWhiteSpace(_user.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Nhập SQL User và Password của môi trường Test 192.168.200.57.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var project = Path.Combine(_root, "PCTP.QC.SqlIntegration", "PCTP.QC.SqlIntegration.csproj");
        if (!File.Exists(project))
        {
            MessageBox.Show(this, "Không tìm thấy PCTP.QC.SqlIntegration.csproj.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await RunTestAsync("SQL Delivery Procedure Check", "sql",
            new[] { "run", "--project", project, "--configuration", "Release", "--", "--delivery-preflight" },
            needsSqlCredentials: true);
    }

    private async Task RunTestAsync(string testName, string source, string[] arguments, bool needsSqlCredentials)
    {
        if (_activeProcess != null)
        {
            MessageBox.Show(this, "Một bài kiểm thử đang chạy. Hãy đợi hoặc dừng bài hiện tại.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _eventItems.Clear();
        _eventSource.DataSource = null;
        _eventSource.DataSource = _eventItems;
        _pass = _fail = _info = 0;
        _caseSummary.Text = "Test cases: đang chờ kết quả";
        _output.Clear();
        _activeRun = new RunRecord
        {
            TestName = testName,
            StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = "RUNNING",
            LogPath = Path.Combine(_reportDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + SafeFileName(testName) + ".log")
        };
        var started = DateTime.UtcNow;
        SetRunning(true, "Đang chạy: " + testName);
        AppendLine("=== " + testName + " ===");
        AppendLine("Bắt đầu: " + _activeRun.StartedAt);
        AppendLine("Repository: " + _root);
        AppendLine("Target: " + (needsSqlCredentials ? "192.168.200.57/B7R2_FCC" : "local test suite"));

        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = _root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);

            // Keep SQL credentials out of process arguments and persisted logs.
            if (needsSqlCredentials)
            {
                start.Environment["PCTP_QC_SQL_USER"] = _user.Text.Trim();
                start.Environment["PCTP_QC_SQL_PASSWORD"] = _password.Text;
            }

            var process = new Process { StartInfo = start, EnableRaisingEvents = true };
            _activeProcess = process;
            process.OutputDataReceived += (_, e) => AppendLine(e.Data);
            process.ErrorDataReceived += (_, e) => AppendLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit());
            await Task.Delay(100); // allow redirected output callbacks to drain
            var exitCode = process.ExitCode;
            process.Dispose();
            _activeProcess = null;

            var success = exitCode == 0;
            _activeRun.Status = success ? "PASS" : "FAIL";
            _activeRun.ExitCode = exitCode;
            AppendLine("Exit code: " + exitCode);
            AppendLine("Kết thúc: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            if (!success && _fail == 0)
                AddEvent("FAIL", testName, "Tiến trình kết thúc với exit code " + exitCode);
            else if (success && _pass == 0)
                AddEvent("PASS", testName, "Tiến trình kết thúc thành công (exit code 0).");
            _status.Text = success ? "PASS — " + testName : "FAIL — " + testName;
        }
        catch (Exception ex)
        {
            AppendLine(ex.ToString());
            AddEvent("FAIL", testName, ex.Message);
            _activeRun.Status = "FAIL";
            _activeRun.ExitCode = -1;
            _status.Text = "FAIL — không thể chạy " + testName;
        }
        finally
        {
            _activeProcess?.Dispose();
            _activeProcess = null;
            _activeRun.Duration = (DateTime.UtcNow - started).ToString(@"hh\:mm\:ss");
            _activeRun.PassCount = _pass;
            _activeRun.FailCount = _fail;
            _activeRun.InfoCount = _info;
            SaveActiveRun();
            SetRunning(false, _status.Text);
            UpdateCounters();
        }
    }

    private void AppendLine(string line)
    {
        if (line == null) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLine), line);
            return;
        }

        _output.AppendText(line + Environment.NewLine);
        if (_activeRun?.LogPath is string logPath)
        {
            try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); }
            catch { /* UI output must continue even if a log file cannot be written. */ }
        }

        var trimmed = line.Trim();
        var testSummary = Regex.Match(trimmed,
            @"Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)",
            RegexOptions.IgnoreCase);
        if (testSummary.Success)
        {
            var failed = int.Parse(testSummary.Groups[1].Value);
            var passed = int.Parse(testSummary.Groups[2].Value);
            var skipped = int.Parse(testSummary.Groups[3].Value);
            var total = int.Parse(testSummary.Groups[4].Value);
            _caseSummary.Text = $"Test cases: {passed}/{total} PASS, {failed} FAIL, {skipped} SKIP";
            if (_activeRun != null)
            {
                _activeRun.TestCasesPassed = passed;
                _activeRun.TestCasesFailed = failed;
                _activeRun.TestCasesSkipped = skipped;
                _activeRun.TestCasesTotal = total;
            }
            AddEvent(failed == 0 ? "PASS" : "FAIL", "Unit / Regression",
                $"Test case summary: passed={passed}, failed={failed}, skipped={skipped}, total={total}");
        }
        if (trimmed.StartsWith("PASS:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Passed!", StringComparison.OrdinalIgnoreCase))
            AddEvent("PASS", CurrentSource(trimmed), trimmed);
        else if (trimmed.StartsWith("FAIL:", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("Failed!", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            AddEvent("FAIL", CurrentSource(trimmed), trimmed);
        else if (trimmed.StartsWith("INFO:", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("BLOCKED:", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
            AddEvent(trimmed.StartsWith("INFO:", StringComparison.OrdinalIgnoreCase) ? "INFO" : "WARN", CurrentSource(trimmed), trimmed);
    }

    private static string CurrentSource(string line)
    {
        if (line.IndexOf("StockTp", StringComparison.OrdinalIgnoreCase) >= 0) return "StockTpRepository";
        if (line.IndexOf("BulkStockSlot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("LOT", StringComparison.OrdinalIgnoreCase) >= 0) return "Kho / LOT";
        if (line.IndexOf("delivery", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("giao hàng", StringComparison.OrdinalIgnoreCase) >= 0) return "Giao hàng";
        if (line.IndexOf("rollback", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("transaction", StringComparison.OrdinalIgnoreCase) >= 0) return "Transaction";
        if (line.IndexOf("schema", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("SQL connection", StringComparison.OrdinalIgnoreCase) >= 0) return "SQL / Schema";
        if (line.IndexOf("workflow", StringComparison.OrdinalIgnoreCase) >= 0) return "Workflow";
        if (line.IndexOf("Passed!", StringComparison.OrdinalIgnoreCase) >= 0) return "Unit / Regression";
        return "SQL Integration";
    }

    private void AddEvent(string level, string source, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, string, string>(AddEvent), level, source, message);
            return;
        }
        var item = new TestEvent
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Level = level,
            Source = source,
            Message = message
        };
        _eventItems.Add(item);
        _eventSource.ResetBindings(false);
        switch (level)
        {
            case "PASS": _pass++; break;
            case "FAIL": _fail++; break;
            default: _info++; break;
        }
        UpdateCounters();
    }

    private void SetRunning(bool running, string status)
    {
        _unitRun.Enabled = !running;
        _sqlPreflight.Enabled = !running;
        _deliveryPreflight.Enabled = !running;
        _sqlRun.Enabled = !running;
        _stop.Enabled = running;
        _status.Text = status;
    }

    private void StopActiveProcess()
    {
        try
        {
            if (_activeProcess != null && !_activeProcess.HasExited)
            {
                _status.Text = "Đang yêu cầu dừng tiến trình...";
                _activeProcess.Kill(entireProcessTree: true);
                AppendLine("WARN: Người dùng yêu cầu dừng tiến trình kiểm thử.");
            }
        }
        catch (Exception ex)
        {
            AppendLine("Không thể dừng tiến trình: " + ex.Message);
        }
    }

    private void LoadMatrix()
    {
        var path = Path.Combine(_root, "PCTP.QC.Tests", "TestMatrix.json");
        try
        {
            if (!File.Exists(path))
            {
                _matrixSummary.Text = "Không tìm thấy TestMatrix.json";
                return;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (!document.RootElement.TryGetProperty("modules", out var modules) || modules.ValueKind != JsonValueKind.Array)
            {
                _matrixSummary.Text = "Ma trận không có mảng modules hợp lệ.";
                return;
            }

            _matrixRows.Clear();
            foreach (var module in modules.EnumerateArray())
            {
                _matrixRows.Add(new MatrixRow
                {
                    Id = ReadJsonString(module, "id"),
                    Module = ReadJsonString(module, "module"),
                    Status = ReadJsonString(module, "status"),
                    Scenario = ReadJsonString(module, "scenario"),
                    Expected = ReadJsonString(module, "expected"),
                    TestEvidence = ReadJsonString(module, "testEvidence")
                });
            }
            ApplyMatrixFilter();
        }
        catch (Exception ex)
        {
            _matrixSummary.Text = "Lỗi đọc ma trận: " + ex.Message;
        }
    }

    private static string ReadJsonString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private void ApplyMatrixFilter()
    {
        var selected = _matrixFilter.SelectedItem?.ToString() ?? "Tất cả";
        var filtered = selected == "Tất cả"
            ? _matrixRows.ToList()
            : _matrixRows.Where(row => string.Equals(row.Status, selected, StringComparison.OrdinalIgnoreCase)).ToList();
        _matrixSource.DataSource = null;
        _matrixSource.DataSource = filtered;
        _matrixSummary.Text = $"Hiển thị {filtered.Count}/{_matrixRows.Count} | implemented: {_matrixRows.Count(x => x.Status == "implemented")} | partial: {_matrixRows.Count(x => x.Status == "partial")} | planned: {_matrixRows.Count(x => x.Status == "planned")}";
    }

    private void UpdateCounters()
    {
        _runCount.Text = "Lần chạy đã lưu: " + _history.Count;
        _passCount.Text = "PASS: " + _pass;
        _failCount.Text = "FAIL: " + _fail;
        _infoCount.Text = "INFO/WARN: " + _info;
    }

    private void SaveActiveRun()
    {
        if (_activeRun == null) return;
        try
        {
            _history.Insert(0, _activeRun);
            if (_history.Count > 200) _history.RemoveRange(200, _history.Count - 200);
            File.WriteAllText(Path.Combine(_reportDirectory, "history.json"),
                JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            _historySource.DataSource = null;
            _historySource.DataSource = _history;
            _historyGrid.DataSource = _historySource;
        }
        catch (Exception ex)
        {
            AppendLine("WARN: Không lưu được lịch sử: " + ex.Message);
        }
    }

    private void LoadHistory()
    {
        var path = Path.Combine(_reportDirectory, "history.json");
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<List<RunRecord>>(File.ReadAllText(path, Encoding.UTF8));
                if (loaded != null) _history.AddRange(loaded.Take(200));
            }
        }
        catch (Exception ex)
        {
            AppendLine("WARN: Không đọc được lịch sử cũ: " + ex.Message);
        }
        _historySource.DataSource = _history;
        _historyGrid.DataSource = _historySource;
    }

    private void ExportHtmlReport()
    {
        try
        {
            var path = Path.Combine(_reportDirectory, "PCTP-QC-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".html");
            var html = new StringBuilder();
            html.AppendLine("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>PCTP QC Report</title>");
            html.AppendLine("<style>body{font:14px Segoe UI,Arial;margin:24px;color:#202938}table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border:1px solid #d7dce2;padding:8px;text-align:left}th{background:#eef2f7}.PASS{color:#16803c}.FAIL{color:#b42318}.WARN{color:#9a6700}pre{white-space:pre-wrap;background:#f6f8fa;padding:12px}</style></head><body>");
            html.AppendLine("<h1>PCTP QC Test Report</h1><p>Xuất lúc: " + WebUtility.HtmlEncode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "</p>");
            html.AppendLine("<p>Target test cố định: 192.168.200.57 / B7R2_FCC. Production 192.168.200.14\\BRAVO bị chặn.</p>");
            html.AppendLine("<h2>Lịch sử chạy</h2><table><tr><th>Bắt đầu</th><th>Bài kiểm thử</th><th>Trạng thái</th><th>Thời lượng</th><th>Exit code</th><th>Test cases</th></tr>");
            foreach (var run in _history.Take(200))
                html.AppendLine("<tr><td>" + E(run.StartedAt) + "</td><td>" + E(run.TestName) + "</td><td class='" + E(run.Status) + "'>" + E(run.Status) + "</td><td>" + E(run.Duration) + "</td><td>" + run.ExitCode + "</td><td>" + run.TestCasesPassed + "/" + run.TestCasesTotal + " (fail " + run.TestCasesFailed + ", skip " + run.TestCasesSkipped + ")</td></tr>");
            html.AppendLine("</table><h2>Sự kiện của phiên hiện tại</h2><table><tr><th>Thời gian</th><th>Kết quả</th><th>Nhóm</th><th>Chi tiết</th></tr>");
            foreach (var item in _eventItems)
                html.AppendLine("<tr><td>" + E(item.Time) + "</td><td class='" + E(item.Level) + "'>" + E(item.Level) + "</td><td>" + E(item.Source) + "</td><td>" + E(item.Message) + "</td></tr>");
            html.AppendLine("</table><h2>Log phiên hiện tại</h2><pre>" + E(_output.Text) + "</pre></body></html>");
            File.WriteAllText(path, html.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Đã xuất báo cáo:\n" + path, "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không thể xuất báo cáo: " + ex.Message, "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string E(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private void OpenReportFolder()
    {
        Directory.CreateDirectory(_reportDirectory);
        Process.Start(new ProcessStartInfo(_reportDirectory) { UseShellExecute = true });
    }

    private void OpenSelectedHistory()
    {
        if (_historyGrid.CurrentRow?.DataBoundItem is not RunRecord selected) return;
        if (!string.IsNullOrWhiteSpace(selected.LogPath) && File.Exists(selected.LogPath))
            Process.Start(new ProcessStartInfo(selected.LogPath) { UseShellExecute = true });
        else
            MessageBox.Show(this, "Không tìm thấy log cho lần chạy đã chọn.", "PCTP QC", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void HighlightSelectedEvent()
    {
        if (_events.CurrentRow?.DataBoundItem is not TestEvent item) return;
        _status.Text = item.Level + " — " + item.Source;
    }

    private static string SafeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Replace(' ', '-');
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WINDOWS_PCTP(NO_Check).sln")))
                return directory.FullName;
        return Directory.GetCurrentDirectory();
    }

    private sealed class MatrixRow
    {
        public string Id { get; set; }
        public string Module { get; set; }
        public string Status { get; set; }
        public string Scenario { get; set; }
        public string Expected { get; set; }
        public string TestEvidence { get; set; }
    }

    private sealed class TestEvent
    {
        public string Time { get; set; }
        public string Level { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }

    private sealed class RunRecord
    {
        public string TestName { get; set; }
        public string StartedAt { get; set; }
        public string Status { get; set; }
        public string Duration { get; set; }
        public int ExitCode { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public int InfoCount { get; set; }
        public int TestCasesPassed { get; set; }
        public int TestCasesFailed { get; set; }
        public int TestCasesSkipped { get; set; }
        public int TestCasesTotal { get; set; }
        public string LogPath { get; set; }
    }
}
