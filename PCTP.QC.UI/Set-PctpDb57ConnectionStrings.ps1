[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [string]$Server = '192.168.200.57',
    [string]$Database = 'B7R2_FCC',
    [string]$SqlUser = $env:PCTP_QC_SQL_USER,
    [string]$SqlPassword = $env:PCTP_QC_SQL_PASSWORD
)
$ErrorActionPreference = 'Stop'

if ($Server -ne '192.168.200.57' -or $Database -ne 'B7R2_FCC') {
    throw 'PCTP DB57 config rewrite is pinned to 192.168.200.57/B7R2_FCC.'
}
if ([string]::IsNullOrWhiteSpace($SqlUser) -or [string]::IsNullOrWhiteSpace($SqlPassword)) {
    throw 'PCTP_QC_SQL_USER and PCTP_QC_SQL_PASSWORD are required.'
}
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Application config not found: $ConfigPath"
}

function Set-Db57Connection([System.Data.Common.DbConnectionStringBuilder]$Builder) {
    foreach ($key in @('data source', 'server', 'address', 'addr', 'network address')) {
        if ($Builder.ContainsKey($key)) { [void]$Builder.Remove($key) }
    }
    foreach ($key in @('initial catalog', 'database')) {
        if ($Builder.ContainsKey($key)) { [void]$Builder.Remove($key) }
    }
    foreach ($key in @('user id', 'uid', 'user', 'password', 'pwd')) {
        if ($Builder.ContainsKey($key)) { [void]$Builder.Remove($key) }
    }
    $Builder['Data Source'] = $Server
    $Builder['Initial Catalog'] = $Database
    $Builder['User ID'] = $SqlUser
    $Builder['Password'] = $SqlPassword
}

[xml]$xml = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8
$entries = @($xml.configuration.connectionStrings.add)
if ($entries.Count -eq 0) { throw 'Application config has no connectionStrings/add entries.' }

$changed = 0
foreach ($entry in $entries) {
    $original = [string]$entry.connectionString
    if ([string]::IsNullOrWhiteSpace($original)) { continue }

    $outer = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $outer.ConnectionString = $original }
    catch { throw "Cannot parse connection string entry '$($entry.name)'." }

    if ($outer.ContainsKey('provider connection string')) {
        $inner = New-Object System.Data.Common.DbConnectionStringBuilder
        try { $inner.ConnectionString = [string]$outer['provider connection string'] }
        catch { throw "Cannot parse nested provider connection string entry '$($entry.name)'." }
        if ($inner.ContainsKey('data source') -or $inner.ContainsKey('server') -or $inner.ContainsKey('xpoprovider')) {
            Set-Db57Connection $inner
            $outer['provider connection string'] = $inner.ConnectionString
            $entry.connectionString = $outer.ConnectionString
            $changed++
        }
    }
    elseif ($outer.ContainsKey('data source') -or $outer.ContainsKey('server') -or $outer.ContainsKey('xpoprovider')) {
        Set-Db57Connection $outer
        $entry.connectionString = $outer.ConnectionString
        $changed++
    }
}

if ($changed -eq 0) { throw 'No SQL connection strings were found to redirect to DB57.' }

# Fail closed: every SQL connection string in the config must now target DB57/B7R2_FCC and no production alias may remain.
foreach ($entry in @($xml.configuration.connectionStrings.add)) {
    $text = [string]$entry.connectionString
    if ($text -match '(?i)192\.168\.200\.14|BRAVO') {
        throw "Production SQL target remains in connection string '$($entry.name)'."
    }
    if ($text -match '(?i)data source|server=|xpoprovider|provider connection string') {
        if ($text -notmatch '(?i)192\.168\.200\.57' -or $text -notmatch '(?i)B7R2_FCC') {
            throw "SQL connection string '$($entry.name)' did not resolve to DB57/B7R2_FCC."
        }
    }
}

$xml.Save((Resolve-Path -LiteralPath $ConfigPath).Path)
Write-Host "PASS: redirected $changed application SQL connection string(s) to DB57/B7R2_FCC using the QC login. No credentials were printed."
