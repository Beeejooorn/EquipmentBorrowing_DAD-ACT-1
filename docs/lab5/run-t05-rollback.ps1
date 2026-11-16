param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$databaseName = 'equipmentborrowing_lab5_acceptance'
$psql = Join-Path $PostgresBin 'psql.exe'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$seed = Join-Path $PSScriptRoot 'sql\reset-acceptance-data.sql'
$queryFile = Join-Path $env:TEMP 'lab5-t05-state.sql'
$stdoutFile = Join-Path $env:TEMP 'lab5-t05-api-out.log'
$stderrFile = Join-Path $env:TEMP 'lab5-t05-api-error.log'

if (-not (Test-Path -LiteralPath $psql)) { throw "psql.exe was not found at $psql" }

$oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', 'Process')
$oldPgPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
$oldFault = [Environment]::GetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', 'Process')
$securePassword = Read-Host 'PostgreSQL password for local postgres user' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$apiProcess = $null

try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = 'localhost'
    $connection['Port'] = 5432
    $connection['Database'] = $databaseName
    $connection['Username'] = 'postgres'
    $connection['Password'] = $plainPassword
    $env:ConnectionStrings__EquipmentBorrowing = $connection.ConnectionString
    $env:PGPASSWORD = $plainPassword
    $env:EQUIPMENT_BORROWING_CHECKOUT_FAULT = 'after-first-write'

    Write-Host 'Resetting the disposable acceptance data for T05...'
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $databaseName -f $seed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The seed reset failed.' }

    $stateQuery = @'
SELECT
  (SELECT count(*) FROM "Borrowings" b
   JOIN "Students" s ON s."Id" = b."StudentId"
   JOIN "Equipment" e ON e."Id" = b."EquipmentId"
   WHERE s."StudentNumber" = 'ST-400' AND e."EquipmentCode" IN ('EQ-002', 'EQ-006'))::text
  || ',' ||
  (SELECT count(*) FROM "Equipment"
   WHERE "EquipmentCode" IN ('EQ-002', 'EQ-006') AND "IsAvailable")::text;
'@
    Set-Content -LiteralPath $queryFile -Value $stateQuery -Encoding UTF8
    $before = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The before-state query failed.' }
    Write-Host "Before: matching borrowings, available items = $before"

    Write-Host 'Building the isolated Release API...'
    & dotnet build $apiProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'The API build failed.' }

    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', 'http://localhost:5087') `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($apiProcess.HasExited) { throw "The T05 API exited early. See $stderrFile" }
        try {
            $health = Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:5087/' -TimeoutSec 1
            if ($health.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "The T05 API did not start. See $stderrFile" }

    $students = (Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:5087/api/v1/students').Content | ConvertFrom-Json
    $equipment = (Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:5087/api/v1/equipment').Content | ConvertFrom-Json
    $studentId = ($students | Where-Object StudentNumber -eq 'ST-400').Id
    $equipmentIds = @($equipment | Where-Object { $_.EquipmentCode -in @('EQ-002', 'EQ-006') } | ForEach-Object Id)
    if (-not $studentId -or $equipmentIds.Count -ne 2) { throw 'Could not resolve S4/E2/E6 from the API.' }

    $dueDate = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
    $body = @{ studentId = $studentId; equipmentIds = $equipmentIds; expectedReturnDate = $dueDate } | ConvertTo-Json
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri 'http://localhost:5087/api/v1/checkout-requests' `
            -ContentType 'application/json' -Body $body
        $status = [int]$response.StatusCode
        $responseBody = $response.Content
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        try { $responseBody = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }

    Write-Host "HTTP status: $status"
    Write-Host "Response: $responseBody"
    $after = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The after-state query failed.' }
    Write-Host "After: matching borrowings, available items = $after"

    $responseJson = $responseBody | ConvertFrom-Json
    if ($before -ne '0,2' -or $after -ne '0,2' -or $status -ne 500 -or $responseJson.code -ne 'server_error') {
        throw 'T05 rollback did not meet the expected HTTP and fresh database state.'
    }
    Write-Host 'PASS: the first write was attempted, HTTP 500 was returned, and no partial state persisted.'
    Write-Host "API logs: $stdoutFile and $stderrFile"
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('PGPASSWORD', $oldPgPassword, 'Process')
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', $oldFault, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword, connection -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $queryFile -ErrorAction SilentlyContinue
}
