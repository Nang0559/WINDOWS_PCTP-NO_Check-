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
# Use XPath rather than PowerShell's XML dot adapter: "add" can be resolved
# ambiguously as a member, yielding zero entries even when the config is valid.
$entries = @($xml.SelectNodes('/configuration/connectionStrings/add'))
if ($entries.Count -eq 0) { throw 'Application config has no connectionStrings/add entries (XPath /configuration/connectionStrings/add).' }

$changed = 0
foreach ($entry in $entries) {
    $original = [string]$entry.GetAttribute('connectionString')
    if ([string]::IsNullOrWhiteSpace($original)) { continue }

    $outer = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $outer.ConnectionString = $original }
    catch { throw "Cannot parse connection string entry '$($entry.GetAttribute('name'))'." }

    if ($outer.ContainsKey('provider connection string')) {
        $inner = New-Object System.Data.Common.DbConnectionStringBuilder
        try { $inner.ConnectionString = [string]$outer['provider connection string'] }
        catch { throw "Cannot parse nested provider connection string entry '$($entry.GetAttribute('name'))'." }
        if ($inner.ContainsKey('data source') -or $inner.ContainsKey('server') -or $inner.ContainsKey('xpoprovider')) {
            Set-Db57Connection $inner
            $outer['provider connection string'] = $inner.ConnectionString
            $entry.SetAttribute('connectionString', $outer.ConnectionString)
            $changed++
        }
    }
    elseif ($outer.ContainsKey('data source') -or $outer.ContainsKey('server') -or $outer.ContainsKey('xpoprovider')) {
        Set-Db57Connection $outer
        $entry.SetAttribute('connectionString', $outer.ConnectionString)
        $changed++
    }
}

if ($changed -eq 0) { throw 'No SQL connection strings were found to redirect to DB57.' }

# Fail closed: verify every SQL connection string by parsing it again. Never
# print the connection strings because they contain credentials.
foreach ($entry in $entries) {
    $name = [string]$entry.GetAttribute('name')
    $value = [string]$entry.GetAttribute('connectionString')
    if ([string]::IsNullOrWhiteSpace($value)) { continue }
    if ($value -match '(?i)192\.168\.200\.14|BRAVO') {
        throw "Production SQL target remains in connection string '$name'."
    }

    $outer = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $outer.ConnectionString = $value }
    catch { throw "Cannot validate connection string entry '$name' after rewrite." }
    $candidate = $outer
    if ($outer.ContainsKey('provider connection string')) {
        $candidate = New-Object System.Data.Common.DbConnectionStringBuilder
        try { $candidate.ConnectionString = [string]$outer['provider connection string'] }
        catch { throw "Cannot validate nested provider connection string entry '$name' after rewrite." }
    }

    $isSql = $candidate.ContainsKey('data source') -or $candidate.ContainsKey('server') -or $candidate.ContainsKey('xpoprovider')
    if ($isSql) {
        $source = ''
        $catalog = ''
        foreach ($key in @('data source', 'server', 'address', 'addr', 'network address')) {
            if ($candidate.ContainsKey($key)) { $source = [string]$candidate[$key]; break }
        }
        foreach ($key in @('initial catalog', 'database')) {
            if ($candidate.ContainsKey($key)) { $catalog = [string]$candidate[$key]; break }
        }
        if ($source -ne $Server -or $catalog -ne $Database) {
            throw "SQL connection string '$name' did not resolve to DB57/B7R2_FCC."
        }
        if (-not $candidate.ContainsKey('user id') -and -not $candidate.ContainsKey('uid')) {
            throw "SQL connection string '$name' has no QC SQL login after rewrite."
        }
        $login = if ($candidate.ContainsKey('user id')) { [string]$candidate['user id'] } else { [string]$candidate['uid'] }
        if ($login -ne $SqlUser) { throw "SQL connection string '$name' is not using the configured QC SQL login." }
    }
}

$xml.Save((Resolve-Path -LiteralPath $ConfigPath).Path)
Write-Host "PASS: redirected and verified $changed application SQL connection string(s) to DB57/B7R2_FCC using the QC login. No credentials were printed."
