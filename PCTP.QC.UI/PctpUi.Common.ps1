# Shared helpers for Invoke-PctpUiJourney.ps1 (dot-source only).
# Windows PowerShell 5.1 compatible: no ternary, no ??, no ForEach-Object -Parallel.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('PctpUi.Native' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace PctpUi {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public static class Native {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    }
}
"@
}

$script:Process = $null
$script:ProcName = $null
$script:PreExisting = New-Object 'System.Collections.Generic.HashSet[int]'
$script:Pids = New-Object 'System.Collections.Generic.HashSet[int]'

function ConvertTo-SafeInt($value) {
    try {
        $d = [double]$value
        if ([double]::IsNaN($d) -or [double]::IsInfinity($d)) { return 0 }
        return [int][math]::Round($d)
    } catch { return 0 }
}

function Update-PctpPids {
    $ids = New-Object 'System.Collections.Generic.HashSet[int]'
    if ($script:Process) { [void]$ids.Add($script:Process.Id) }
    # The app may relaunch itself (splash/updater/restart); follow new processes with the same name,
    # but never touch instances that were already running before the test started.
    foreach ($p in @(Get-Process -Name $script:ProcName -ErrorAction SilentlyContinue)) {
        if (-not $script:PreExisting.Contains($p.Id)) { [void]$ids.Add($p.Id) }
    }
    $script:Pids = $ids
}

function Get-PctpWindows {
    Update-PctpPids
    $found = New-Object System.Collections.Generic.List[object]
    $all = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $all) {
        try {
            $c = $item.Current
            if ($script:Pids.Contains([int]$c.ProcessId) -and $c.ControlType -eq [System.Windows.Automation.ControlType]::Window) {
                $found.Add($item)
            }
        } catch { }
    }
    return $found.ToArray()
}

function Get-CurrentPctpWindow {
    $windows = @(Get-PctpWindows)
    if ($windows.Count -eq 0) { return $null }
    $visible = @($windows | Where-Object { try { -not $_.Current.IsOffscreen } catch { $false } })
    if ($visible.Count -gt 0) { return $visible[$visible.Count - 1] }
    return $windows[0]
}

function Get-ElementTexts($element, [int]$max = 15) {
    $texts = New-Object System.Collections.Generic.List[string]
    try {
        $type = [System.Windows.Automation.AutomationElement]::ControlTypeProperty
        $c1 = New-Object System.Windows.Automation.PropertyCondition($type, [System.Windows.Automation.ControlType]::Text)
        $c2 = New-Object System.Windows.Automation.PropertyCondition($type, [System.Windows.Automation.ControlType]::Button)
        $or = New-Object System.Windows.Automation.OrCondition($c1, $c2)
        foreach ($e in $element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $or)) {
            $n = $e.Current.Name
            if (-not [string]::IsNullOrWhiteSpace($n) -and -not $texts.Contains($n)) { $texts.Add($n) }
            if ($texts.Count -ge $max) { break }
        }
    } catch { }
    return $texts.ToArray()
}

function Get-WindowInventory {
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($w in @(Get-PctpWindows)) {
        try {
            $c = $w.Current
            $r = $c.BoundingRectangle
            $rows.Add([pscustomobject]@{
                Name = $c.Name; ClassName = $c.ClassName; AutomationId = $c.AutomationId
                ProcessId = $c.ProcessId; IsOffscreen = $c.IsOffscreen
                X = (ConvertTo-SafeInt $r.X); Y = (ConvertTo-SafeInt $r.Y)
                Width = (ConvertTo-SafeInt $r.Width); Height = (ConvertTo-SafeInt $r.Height)
                Texts = @(Get-ElementTexts $w 12)
            })
        } catch { }
    }
    return $rows.ToArray()
}

function Export-PctpTree($root, [string]$path, [int]$max = 1500) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $queue = New-Object 'System.Collections.Generic.Queue[object]'
    $queue.Enqueue(@($root, 0))
    $rows = New-Object System.Collections.Generic.List[object]
    while ($queue.Count -gt 0 -and $rows.Count -lt $max) {
        $pair = $queue.Dequeue()
        $el = $pair[0]; $depth = $pair[1]
        try {
            $c = $el.Current
            $value = $null
            $vp = $null
            if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $value = $vp.Current.Value }
            $patterns = ''
            try { $patterns = (@($el.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName -replace 'Identifiers\.Pattern$', '' }) -join ',') } catch { }
            $r = $c.BoundingRectangle
            $rows.Add([pscustomobject]@{
                Depth = $depth; AutomationId = $c.AutomationId; Name = $c.Name
                ControlType = ($c.ControlType.ProgrammaticName -replace '^ControlType\.', '')
                ClassName = $c.ClassName; FrameworkId = $c.FrameworkId
                IsEnabled = $c.IsEnabled; IsOffscreen = $c.IsOffscreen; Value = $value; Patterns = $patterns
                X = (ConvertTo-SafeInt $r.X); Y = (ConvertTo-SafeInt $r.Y)
                W = (ConvertTo-SafeInt $r.Width); H = (ConvertTo-SafeInt $r.Height)
            })
            $child = $walker.GetFirstChild($el)
            while ($child -and ($rows.Count + $queue.Count) -lt $max) {
                $queue.Enqueue(@($child, ($depth + 1)))
                $child = $walker.GetNextSibling($child)
            }
        } catch { }
    }
    ConvertTo-Json -InputObject @($rows.ToArray()) -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    return $rows.Count
}

function Expand-PctpTokens($text, $data) {
    if ($null -eq $text) { return $null }
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        $key = $m.Groups[1].Value
        $prop = $null
        if ($data) { $prop = $data.PSObject.Properties[$key] }
        if (-not $prop) { throw "Unknown test-data token '{{$key}}' (define it in manifest.testData)." }
        return [string]$prop.Value
    }
    return [regex]::Replace([string]$text, '\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}', $evaluator)
}

function Format-PctpTestData($data) {
    if (-not $data) { return '' }
    $pairs = @($data.PSObject.Properties | ForEach-Object { '{0}={1}' -f $_.Name, $_.Value })
    return ($pairs -join '; ')
}

function Find-PctpTarget($step) {
    $candidates = New-Object System.Collections.Generic.List[object]
    if ($step.automationId) { $candidates.Add(@('AutomationId', [string]$step.automationId, [System.Windows.Automation.AutomationElement]::AutomationIdProperty)) }
    if ($step.name) { $candidates.Add(@('Name', [string]$step.name, [System.Windows.Automation.AutomationElement]::NameProperty)) }
    if ($step.fallbackName) { $candidates.Add(@('FallbackName', [string]$step.fallbackName, [System.Windows.Automation.AutomationElement]::NameProperty)) }
    if ($candidates.Count -eq 0) { throw "Step '$($step.id)' must specify automationId, name or fallbackName." }
    $windows = @(Get-PctpWindows)
    foreach ($cand in $candidates) {
        $cond = New-Object System.Windows.Automation.PropertyCondition($cand[2], $cand[1])
        foreach ($root in $windows) {
            $t = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
            if ($t) { return [pscustomobject]@{ Element = $t; FoundBy = ("{0}='{1}'" -f $cand[0], $cand[1]) } }
        }
    }
    return $null
}

function Invoke-PctpMouseClick($element) {
    $pt = $null
    $p = New-Object System.Windows.Point
    if ($element.TryGetClickablePoint([ref]$p)) { $pt = $p } else {
        $r = $element.Current.BoundingRectangle
        if ([double]::IsInfinity($r.X) -or $r.Width -le 0 -or $r.Height -le 0) { throw 'element has no on-screen bounds to click.' }
        $pt = New-Object System.Windows.Point(($r.X + $r.Width / 2), ($r.Y + $r.Height / 2))
    }
    [void][PctpUi.Native]::SetCursorPos([int]$pt.X, [int]$pt.Y)
    Start-Sleep -Milliseconds 80
    [PctpUi.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [PctpUi.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}

function Test-PctpExpect($exp, $data) {
    $prop = if ($exp.property) { ([string]$exp.property).ToLowerInvariant() } else { 'name' }
    $label = if ($exp.automationId) { [string]$exp.automationId } else { [string]$exp.name }
    $found = Find-PctpTarget $exp
    $expectedText = ''
    $actual = $null
    $ok = $false
    if ($prop -eq 'exists') {
        $want = $true
        if ($null -ne $exp.equals) { $want = [bool]$exp.equals }
        $actual = [bool]$found
        $expectedText = "exists = $want"
        $ok = ($actual -eq $want)
    } elseif (-not $found) {
        $expectedText = "$prop (element not found)"
        $actual = '<element not found>'
    } else {
        $el = $found.Element
        switch ($prop) {
            'name' { $actual = $el.Current.Name }
            'enabled' { $actual = [string]$el.Current.IsEnabled }
            'value' {
                $vp = $null
                if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $actual = $vp.Current.Value }
                else { $actual = '<no ValuePattern>' }
            }
            default { throw "Unsupported expect.property '$prop' (use name|value|enabled|exists)." }
        }
        $actualText = [string]$actual
        if ($null -ne $exp.equals) {
            $e = Expand-PctpTokens ([string]$exp.equals) $data
            $expectedText = "$prop = '$e'"; $ok = ($actualText -eq $e)
        } elseif ($null -ne $exp.contains) {
            $e = Expand-PctpTokens ([string]$exp.contains) $data
            $expectedText = "$prop contains '$e'"; $ok = ($actualText.IndexOf($e, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        } elseif ($null -ne $exp.matches) {
            $e = Expand-PctpTokens ([string]$exp.matches) $data
            $expectedText = "$prop matches /$e/"; $ok = ($actualText -match $e)
        } else { throw "expect for '$label' needs equals, contains or matches." }
    }
    return [pscustomobject]@{
        Target = $label; Property = $prop; Expected = $expectedText
        Actual = $(if ($null -eq $actual) { '' } else { [string]$actual })
        Status = $(if ($ok) { 'PASS' } else { 'FAIL' })
    }
}

function Save-PctpScreenshot {
    param([string]$Path, [string[]]$BannerLines, $Window)
    $result = [pscustomobject]@{ Path = $null; Error = $null; Source = $null }
    $capture = $null; $final = $null; $font = $null
    try {
        $x = 0; $y = 0; $w = 0; $h = 0; $source = 'window'
        if ($Window) {
            try {
                $hwnd = [IntPtr](ConvertTo-SafeInt $Window.Current.NativeWindowHandle)
                if ($hwnd -ne [IntPtr]::Zero) {
                    if ([PctpUi.Native]::IsIconic($hwnd)) { [void][PctpUi.Native]::ShowWindow($hwnd, 9); Start-Sleep -Milliseconds 300 }
                    [void][PctpUi.Native]::SetForegroundWindow($hwnd)
                    Start-Sleep -Milliseconds 250
                    $rc = New-Object PctpUi.RECT
                    if ([PctpUi.Native]::GetWindowRect($hwnd, [ref]$rc)) {
                        $x = $rc.Left; $y = $rc.Top; $w = $rc.Right - $rc.Left; $h = $rc.Bottom - $rc.Top
                    }
                }
            } catch { }
        }
        $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
        if ($w -le 20 -or $h -le 20 -or $w -gt 20000 -or $h -gt 20000) {
            $x = $vs.X; $y = $vs.Y; $w = $vs.Width; $h = $vs.Height; $source = 'virtual-screen'
        }
        $result.Source = $source

        $capture = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($capture)
        try { $g.CopyFromScreen($x, $y, 0, 0, $capture.Size) } finally { $g.Dispose() }

        $lines = @($BannerLines | Where-Object { $_ })
        $bannerWidth = [math]::Max($w, 1100)
        $font = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Point)
        $probe = New-Object System.Drawing.Bitmap(10, 10)
        $gp = [System.Drawing.Graphics]::FromImage($probe)
        $heights = New-Object System.Collections.Generic.List[single]
        $total = 12
        foreach ($line in $lines) {
            $lh = [single][math]::Ceiling($gp.MeasureString($line, $font, ($bannerWidth - 20)).Height)
            $heights.Add($lh); $total += $lh + 2
        }
        $gp.Dispose(); $probe.Dispose()

        $final = New-Object System.Drawing.Bitmap($bannerWidth, [int]($total + $h))
        $gb = [System.Drawing.Graphics]::FromImage($final)
        try {
            $gb.Clear([System.Drawing.Color]::FromArgb(28, 28, 30))
            $yy = 6.0
            for ($i = 0; $i -lt $lines.Count; $i++) {
                $color = [System.Drawing.Color]::White
                if ($lines[$i].StartsWith('[FAIL]')) { $color = [System.Drawing.Color]::FromArgb(255, 99, 99) }
                elseif ($lines[$i].StartsWith('[PASS]')) { $color = [System.Drawing.Color]::FromArgb(120, 220, 120) }
                elseif ($lines[$i].StartsWith('Test data')) { $color = [System.Drawing.Color]::FromArgb(130, 200, 255) }
                elseif ($lines[$i].StartsWith('Expected')) { $color = [System.Drawing.Color]::FromArgb(255, 220, 120) }
                $brush = New-Object System.Drawing.SolidBrush($color)
                $rect = New-Object System.Drawing.RectangleF(10, $yy, ($bannerWidth - 20), $heights[$i])
                $gb.DrawString($lines[$i], $font, $brush, $rect)
                $brush.Dispose()
                $yy += $heights[$i] + 2
            }
            $gb.DrawImage($capture, 0, [int]$total)
        } finally { $gb.Dispose() }
        $final.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $result.Path = $Path
    } catch {
        $result.Error = $_.Exception.Message
    } finally {
        if ($capture) { $capture.Dispose() }
        if ($final) { $final.Dispose() }
        if ($font) { $font.Dispose() }
    }
    return $result
}

function New-PctpHtmlReport($report, [string]$path) {
    $enc = { param($s) [System.Net.WebUtility]::HtmlEncode([string]$s) }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><title>PCTP UI journey</title>')
    [void]$sb.AppendLine('<style>body{font-family:Segoe UI,Arial,sans-serif;margin:16px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:6px;vertical-align:top;font-size:13px}th{background:#eee}.PASS{color:#0a7a0a;font-weight:bold}.FAIL{color:#c00;font-weight:bold}img{max-width:520px;border:1px solid #999}code{background:#f3f3f3;padding:1px 3px}</style></head><body>')
    [void]$sb.AppendLine('<h2>' + (& $enc $report.Test) + ': <span class="' + $report.Status + '">' + $report.Status + '</span></h2>')
    if ($report.ScenarioId -or $report.ScenarioTitle) { [void]$sb.AppendLine('<p><b>Scenario:</b> ' + (& $enc ("$($report.ScenarioId) $($report.ScenarioTitle)")) + '</p>') }
    if ($report.ScenarioDescription) { [void]$sb.AppendLine('<p>' + (& $enc $report.ScenarioDescription) + '</p>') }
    if ($report.TestDataText) { [void]$sb.AppendLine('<p><b>Test data:</b> <code>' + (& $enc $report.TestDataText) + '</code></p>') }
    if ($report.FailureSummary) { [void]$sb.AppendLine('<p class="FAIL">' + (& $enc $report.FailureSummary) + '</p>') }
    [void]$sb.AppendLine('<table><tr><th>#</th><th>Step</th><th>Expected / checks</th><th>Observed</th><th>Screenshot</th></tr>')
    foreach ($r in @($report.Results)) {
        $exp = New-Object System.Text.StringBuilder
        if ($r.Description) { [void]$exp.Append('<div>' + (& $enc $r.Description) + '</div>') }
        if ($r.Expected) { [void]$exp.Append('<div><b>Expected:</b> ' + (& $enc $r.Expected) + '</div>') }
        foreach ($c in @($r.Checks)) {
            if ($c) { [void]$exp.Append('<div><span class="' + $c.Status + '">' + $c.Status + '</span> ' + (& $enc "$($c.Target): $($c.Expected) | actual: '$($c.Actual)'") + '</div>') }
        }
        $obs = New-Object System.Text.StringBuilder
        if ($r.ActiveWindowTitle) { [void]$obs.Append('<div><b>Window:</b> ' + (& $enc $r.ActiveWindowTitle) + '</div>') }
        if ($r.Error) { [void]$obs.Append('<div class="FAIL">' + (& $enc $r.Error) + '</div>') }
        if ($r.FoundBy) { [void]$obs.Append('<div>found by ' + (& $enc $r.FoundBy) + ' via ' + (& $enc $r.Method) + '</div>') }
        if ($r.WindowTexts) { [void]$obs.Append('<div><b>Texts on screen:</b> ' + (& $enc ((@($r.WindowTexts)) -join ' | ')) + '</div>') }
        if ($r.TreeDump) { [void]$obs.Append('<div>control tree: <code>' + (& $enc ([System.IO.Path]::GetFileName([string]$r.TreeDump))) + '</code></div>') }
        $img = ''
        if ($r.Screenshot) { $f = [System.IO.Path]::GetFileName([string]$r.Screenshot); $img = '<a href="' + (& $enc $f) + '"><img src="' + (& $enc $f) + '"></a>' }
        elseif ($r.ScreenshotError) { $img = '<span class="FAIL">screenshot failed: ' + (& $enc $r.ScreenshotError) + '</span>' }
        [void]$sb.AppendLine('<tr><td>' + $r.Step + '</td><td><span class="' + $r.Status + '">' + $r.Status + '</span><br>' + (& $enc $r.Id) + '<br><i>' + (& $enc $r.Action) + '</i></td><td>' + $exp.ToString() + '</td><td>' + $obs.ToString() + '</td><td>' + $img + '</td></tr>')
    }
    [void]$sb.AppendLine('</table></body></html>')
    Set-Content -LiteralPath $path -Value $sb.ToString() -Encoding UTF8
}
