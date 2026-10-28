<#
 Blocks and proves the block of outbound traffic to DB57, production and the Oracle/IFS host for the programs
 that run the isolated G5/G6 gates. Windows PowerShell 5.1 compatible. Needs an elevated session
 (GitHub-hosted Windows runners are; on a self-hosted runner run the service as administrator).

   -Mode Block   : add outbound Windows Firewall rules (program-scoped, remote-address-scoped), then Assert
   -Mode Assert  : TCP-connect canary from THIS process to every blocked endpoint; any success = breach (exit 1)
   -Mode Unblock : remove every rule created by Block

 Exit codes: 0 ok, 1 breach/failed, 3 BLOCKED (not elevated / no firewall cmdlets).
 The canary runs from powershell.exe, which is covered by the same rule mechanism as PCTP.exe; it proves the
 rules are active, and the app is additionally forced to the fixture by QcSqlOverride.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Block', 'Assert', 'Unblock')][string]$Mode,
    [string[]]$ProgramPath = @(),
    [string[]]$BlockedAddress = @('192.168.200.57', '192.168.200.14', '192.168.2.10'),
    [hashtable]$CanaryPorts = @{ '192.168.200.57' = 1433; '192.168.200.14' = 1433; '192.168.2.10' = 8088 },
    [int]$TimeoutMs = 3000
)
$ErrorActionPreference = 'Stop'
$prefix = 'PCTP-QC-ISOLATION-'

function Test-Elevated {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-Reachable([string]$address, [int]$port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect($address, $port, $null, $null)
        if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) { return $false }
        try { $client.EndConnect($iar) } catch { return $false }
        return $client.Connected
    } catch { return $false } finally { $client.Close() }
}

if ($Mode -ne 'Assert') {
    if (-not (Get-Command New-NetFirewallRule -ErrorAction SilentlyContinue)) { Write-Host 'BLOCKED: NetSecurity cmdlets unavailable.'; exit 3 }
    if (-not (Test-Elevated)) { Write-Host 'BLOCKED: elevation is required to manage firewall rules.'; exit 3 }
}

if ($Mode -eq 'Unblock') {
    Get-NetFirewallRule -DisplayName "$prefix*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Write-Host 'Isolation firewall rules removed.'
    exit 0
}

if ($Mode -eq 'Block') {
    $programs = New-Object System.Collections.Generic.List[string]
    foreach ($p in $ProgramPath) { if ($p -and (Test-Path -LiteralPath $p)) { $programs.Add((Resolve-Path -LiteralPath $p).Path) } }
    $self = (Get-Process -Id $PID).Path
    if ($self) { $programs.Add($self) }
    foreach ($candidate in @("$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe")) { if (Test-Path -LiteralPath $candidate) { $programs.Add($candidate) } }
    $i = 0
    foreach ($prog in @($programs | Select-Object -Unique)) {
        $i++
        New-NetFirewallRule -DisplayName ("{0}{1}-{2}" -f $prefix, $PID, $i) -Direction Outbound -Action Block `
            -RemoteAddress $BlockedAddress -Program $prog -Profile Any | Out-Null
        Write-Host "Blocked outbound to $($BlockedAddress -join ', ') for $prog"
    }
}

$breach = New-Object System.Collections.Generic.List[string]
foreach ($addr in $BlockedAddress) {
    $port = if ($CanaryPorts.ContainsKey($addr)) { [int]$CanaryPorts[$addr] } else { 1433 }
    if (Test-Reachable $addr $port) { $breach.Add("${addr}:$port") }
    else { Write-Host "PASS: ${addr}:$port is not reachable from this process." }
}
if ($breach.Count -gt 0) {
    Write-Host ("ISOLATION BREACH: reachable -> " + ($breach -join ', '))
    exit 1
}
exit 0
