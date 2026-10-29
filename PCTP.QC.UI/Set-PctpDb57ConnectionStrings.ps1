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

function Get-BuilderKey([System.Data.Common.DbConnectionStringBuilder]$Builder, [string]$Name) {
    foreach ($key in $Builder.Keys) {
        if ([string]::Equals([string]$key, $Name, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [string]$key
        }
    }
    return $null
}

function Test-BuilderKey([System.Data.Common.DbConnectionStringBuilder]$Builder, [string]$Name) {
    return $null -ne (Get-BuilderKey $Builder $Name)
}

function Get-BuilderValue([System.Data.Common.DbConnectionStringBuilder]$Builder, [string]$Name) {
    $actualKey = Get-BuilderKey $Builder $Name
    if ($null -eq $actualKey) { return $null }
    return $Builder[$actualKey]
}

function Remove-BuilderKey([System.Data.Common.DbConnectionStringBuilder]$Builder, [string]$Name) {
    $actualKey = Get-BuilderKey $Builder $Name
    if ($null -ne $actualKey) { [void]$Builder.Remove($actualKey) }
}

function Set-BuilderValue([System.Data.Common.DbConnectionStringBuilder]$Builder, [string]$Name, [object]$Value) {
    $actualKey = Get-BuilderKey $Builder $Name
    if ($null -eq $actualKey) { $actualKey = $Name }
    $Builder[$actualKey] = $Value
}

function Set-Db57Connection([System.Data.Common.DbConnectionStringBuilder]$Builder) {
    foreach ($key in @('data source', 'server', 'address', 'addr', 'network address')) {
        Remove-BuilderKey $Builder $key
    }
    foreach ($key in @('initial catalog', 'database')) {
        Remove-BuilderKey $Builder $key
    }
    foreach ($key in @('user id', 'uid', 'user', 'password', 'pwd')) {
        Remove-BuilderKey $Builder $key
    }
    Set-BuilderValue $Builder 'Data Source' $Server
    Set-BuilderValue $Builder 'Initial Catalog' $Database
    Set-BuilderValue $Builder 'User ID' $SqlUser
    Set-BuilderValue $Builder 'Password' $SqlPassword
}

[xml]$xml = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8
# Use XPath and explicit case-insensitive connection-string keyword lookup.
$entries = @($xml.SelectNodes('/configuration/connectionStrings/add'))
if ($entries.Count -eq 0) { throw 'Application config has no connectionStrings/add entries (XPath /configuration/connectionStrings/add).' }

$changed = 0
foreach ($entry in $entries) {
    $original = [string]$entry.GetAttribute('connectionString')
    if ([string]::IsNullOrWhiteSpace($original)) { continue }

    $outer = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $outer.set_ConnectionString($original) }
    catch { throw "Cannot parse connection string entry '$($entry.GetAttribute('name'))'." }

    if (Test-BuilderKey $outer 'provider connection string') {
        $inner = New-Object System.Data.Common.DbConnectionStringBuilder
        try { $inner.set_ConnectionString([string](Get-BuilderValue $outer 'provider connection string')) }
        catch { throw "Cannot parse nested provider connection string entry '$($entry.GetAttribute('name'))'." }
        if ((Test-BuilderKey $inner 'data source') -or (Test-BuilderKey $inner 'server') -or (Test-BuilderKey $inner 'xpoprovider')) {
            Set-Db57Connection $inner
            Set-BuilderValue $outer 'provider connection string' $inner.get_ConnectionString()
            $entry.SetAttribute('connectionString', $outer.get_ConnectionString())
            $changed++
        }
    }
    elseif ((Test-BuilderKey $outer 'data source') -or (Test-BuilderKey $outer 'server') -or (Test-BuilderKey $outer 'xpoprovider')) {
        Set-Db57Connection $outer
        $entry.SetAttribute('connectionString', $outer.get_ConnectionString())
        $changed++
    }
}

if ($changed -eq 0) { throw 'No SQL connection strings were found to redirect to DB57. Parsed connectionStrings/add entries, but no SQL/XPO provider keywords were recognized.' }

# Fail closed: verify every SQL connection string by parsing it again. Never
# print connection strings because they contain credentials.
foreach ($entry in $entries) {
    $name = [string]$entry.GetAttribute('name')
    $value = [string]$entry.GetAttribute('connectionString')
    if ([string]::IsNullOrWhiteSpace($value)) { continue }
    if ($value -match '(?i)192\.168\.200\.14|BRAVO') {
        throw "Production SQL target remains in connection string '$name'."
    }

    $outer = New-Object System.Data.Common.DbConnectionStringBuilder
    try { $outer.set_ConnectionString($value) }
    catch { throw "Cannot validate connection string entry '$name' after rewrite." }
    $candidate = $outer
    if (Test-BuilderKey $outer 'provider connection string') {
        $candidate = New-Object System.Data.Common.DbConnectionStringBuilder
        try { $candidate.set_ConnectionString([string](Get-BuilderValue $outer 'provider connection string')) }
        catch { throw "Cannot validate nested provider connection string entry '$name' after rewrite." }
    }

    $isSql = (Test-BuilderKey $candidate 'data source') -or (Test-BuilderKey $candidate 'server') -or (Test-BuilderKey $candidate 'xpoprovider')
    if ($isSql) {
        $source = ''
        $catalog = ''
        foreach ($key in @('data source', 'server', 'address', 'addr', 'network address')) {
            if (Test-BuilderKey $candidate $key) { $source = [string](Get-BuilderValue $candidate $key); break }
        }
        foreach ($key in @('initial catalog', 'database')) {
            if (Test-BuilderKey $candidate $key) { $catalog = [string](Get-BuilderValue $candidate $key); break }
        }
        if ($source -ne $Server -or $catalog -ne $Database) {
            throw "SQL connection string '$name' did not resolve to DB57/B7R2_FCC."
        }
        if (-not (Test-BuilderKey $candidate 'user id') -and -not (Test-BuilderKey $candidate 'uid')) {
            throw "SQL connection string '$name' has no QC SQL login after rewrite."
        }
        $login = if (Test-BuilderKey $candidate 'user id') { [string](Get-BuilderValue $candidate 'user id') } else { [string](Get-BuilderValue $candidate 'uid') }
        if ($login -ne $SqlUser) { throw "SQL connection string '$name' is not using the configured QC SQL login." }
    }
}

$xml.Save((Resolve-Path -LiteralPath $ConfigPath).Path)
Write-Host "PASS: redirected and verified $changed application SQL connection string(s) to DB57/B7R2_FCC using the QC login. No credentials were printed."
