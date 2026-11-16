param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$psql = Join-Path $PostgresBin 'psql.exe'
$queryFile = Join-Path $env:TEMP 'lab5-controlled-recovery.sql'
$apiOut = Join-Path $env:TEMP 'lab5-controlled-recovery-api-out.log'
$apiError = Join-Path $env:TEMP 'lab5-controlled-recovery-api-error.log'
$baseUrl = 'http://localhost:5087'
$databaseName = 'equipmentborrowing_lab5_acceptance'

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected $expected but got $actual" }
    Write-Host "PASS $label = $actual"
}

function Db-State {
    $sql = @'
SELECT (SELECT "Name" FROM "Students" WHERE "StudentNumber"='ST-500') || '|'
    || (SELECT "IsAllowedToBorrow"::text FROM "Students" WHERE "StudentNumber"='ST-500') || '|'
    || (SELECT "Name" FROM "Equipment" WHERE "EquipmentCode"='EQ-008') || '|'
    || (SELECT "IsAvailable"::text FROM "Equipment" WHERE "EquipmentCode"='EQ-008') || '|'
    || (SELECT "Status" FROM "Borrowings" b JOIN "Equipment" e ON e."Id"=b."EquipmentId" WHERE e."EquipmentCode"='EQ-008');
'@
    Set-Content -LiteralPath $queryFile -Value $sql -Encoding UTF8
    $value = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Fresh PostgreSQL read failed.' }
    return $value
}

function Set-ApiDatabase([string]$name) {
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = 'localhost'
    $connection['Port'] = 5432
    $connection['Database'] = $name
    $connection['Username'] = 'postgres'
    $connection['Password'] = $plainPassword
    $env:ConnectionStrings__EquipmentBorrowing = $connection.ConnectionString
}

function Start-TestApi {
    $script:apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $apiOut -RedirectStandardError $apiError
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($script:apiProcess.HasExited) { throw "API exited early. See $apiError" }
        try {
            if ((Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/') -TimeoutSec 1).StatusCode -eq 200) { return }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "API did not start. See $apiError"
}

function Stop-TestApi {
    if ($null -ne $script:apiProcess -and -not $script:apiProcess.HasExited) {
        Stop-Process -Id $script:apiProcess.Id
        $script:apiProcess.WaitForExit()
    }
    $script:apiProcess = $null
}

if (-not (Test-Path -LiteralPath $psql)) { throw "psql.exe was not found at $psql" }
$oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', 'Process')
$oldPgPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
$securePassword = Read-Host 'PostgreSQL password for local postgres user' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$apiProcess = $null

try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $env:PGPASSWORD = $plainPassword
    $before = Db-State
    Write-Host "Before fresh PostgreSQL state = $before"
    if ($before -notmatch '^Eva Updated\|(false|f)\|Headset Updated\|(true|t)\|Returned$') {
        throw 'T07 committed state is missing. Do not run T08 against an unexpected database.'
    }

    try {
        Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/api/v1/equipment') -TimeoutSec 2 | Out-Null
        throw 'Port 5087 is still serving. Close the T07 desktop so its launcher stops the API, then rerun.'
    } catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { throw 'Port 5087 returned HTTP; close the T07 desktop launcher before running T08.' }
        Write-Host 'PASS T08 stopped API = transport failure with no HTTP response'
    }
    Assert-Equal (Db-State) $before 'T08 database unchanged after transport failure'

    & dotnet build $apiProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    Set-ApiDatabase 'equipmentborrowing_lab5_acceptance_missing'
    Start-TestApi
    try {
        Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/api/v1/equipment') -TimeoutSec 8 | Out-Null
        throw 'Expected HTTP 500 from the reachable API with an invalid database.'
    } catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) { throw 'The API was unreachable; expected a received HTTP 500.' }
        Assert-Equal ([int]$_.Exception.Response.StatusCode) 500 'T08 reachable API database error HTTP'
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($body -notmatch 'server_error' -or $body -match 'Password=') { throw 'API error body was unsafe or unexpected.' }
        Write-Host "PASS T08 safe API error body = $body"
    }
    Assert-Equal (Db-State) $before 'T08 database unchanged after HTTP 500'
    Stop-TestApi

    Set-ApiDatabase $databaseName
    Start-TestApi
    $equipment = Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/api/v1/equipment') -TimeoutSec 8
    Assert-Equal ([int]$equipment.StatusCode) 200 'T08 recovered API equipment HTTP'
    if (-not (($equipment.Content | ConvertFrom-Json) | Where-Object { $_.equipmentCode -eq 'EQ-008' })) {
        throw 'Recovered API did not return EQ-008.'
    }
    Write-Host 'PASS T08 recovered API contains EQ-008'
    Assert-Equal (Db-State) $before 'T08 committed PostgreSQL state after API restart'
    Write-Host 'PASS: T08 transport failure, safe server/database error and recovery completed.'
    Write-Host "API logs: $apiOut and $apiError"
}
finally {
    Stop-TestApi
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('PGPASSWORD', $oldPgPassword, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $queryFile -ErrorAction SilentlyContinue
}
