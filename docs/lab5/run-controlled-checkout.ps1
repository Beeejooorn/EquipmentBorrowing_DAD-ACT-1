param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$databaseName = 'equipmentborrowing_lab5_acceptance'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$psql = Join-Path $PostgresBin 'psql.exe'
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$seed = Join-Path $PSScriptRoot 'sql\reset-acceptance-data.sql'
$queryFile = Join-Path $env:TEMP 'lab5-controlled-checkout-query.sql'
$stdoutFile = Join-Path $env:TEMP 'lab5-controlled-checkout-api-out.log'
$stderrFile = Join-Path $env:TEMP 'lab5-controlled-checkout-api-error.log'
$baseUrl = 'http://localhost:5087'

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected $expected but got $actual" }
    Write-Host "PASS $label = $actual"
}

function Request([string]$method, [string]$path, $payload = $null) {
    $parameters = @{ UseBasicParsing = $true; Method = $method; Uri = ($baseUrl + $path); TimeoutSec = 8 }
    if ($null -ne $payload) {
        $parameters['ContentType'] = 'application/json'
        $parameters['Body'] = ConvertTo-Json -InputObject $payload -Depth 6 -Compress
    }
    try {
        $response = Invoke-WebRequest @parameters
        return @{ Status = [int]$response.StatusCode; Body = $response.Content }
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $response = $_.Exception.Response
        $stream = $response.GetResponseStream()
        $body = ''
        if ($null -ne $stream) {
            $reader = New-Object System.IO.StreamReader($stream)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return @{ Status = [int]$response.StatusCode; Body = $body }
    }
}

function Db-Query([string]$sql) {
    Set-Content -LiteralPath $queryFile -Value $sql -Encoding UTF8
    $value = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Independent PostgreSQL read failed.' }
    return $value
}

function Db-State {
    return Db-Query 'SELECT (SELECT count(*) FROM "Borrowings" b JOIN "Students" s ON s."Id"=b."StudentId" JOIN "Equipment" e ON e."Id"=b."EquipmentId" WHERE s."StudentNumber" IN (''ST-100'',''ST-300'',''ST-400'') AND e."EquipmentCode" IN (''EQ-002'',''EQ-006''))::text || '','' || (SELECT count(*) FROM "Equipment" WHERE "EquipmentCode" IN (''EQ-002'',''EQ-006'') AND "IsAvailable")::text;'
}

function Reset-Seed([string]$label) {
    Write-Host "Resetting disposable acceptance data for $label..."
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $databaseName -f $seed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Seed reset failed.' }
    Assert-Equal (Db-State) '0,2' "$label before PostgreSQL rows,available"
}

function Read-Ids {
    $students = (Request GET '/api/v1/students').Body | ConvertFrom-Json
    $equipment = (Request GET '/api/v1/equipment').Body | ConvertFrom-Json
    return @{
        S1 = ($students | Where-Object StudentNumber -eq 'ST-100').Id
        S2 = ($students | Where-Object StudentNumber -eq 'ST-200').Id
        S3 = ($students | Where-Object StudentNumber -eq 'ST-300').Id
        S4 = ($students | Where-Object StudentNumber -eq 'ST-400').Id
        E1 = ($equipment | Where-Object EquipmentCode -eq 'EQ-001').Id
        E2 = ($equipment | Where-Object EquipmentCode -eq 'EQ-002').Id
        E6 = ($equipment | Where-Object EquipmentCode -eq 'EQ-006').Id
    }
}

function Check-Rejection([string]$label, [int]$expected, [string]$path, $body) {
    $before = Db-State
    Assert-Equal (Request POST $path $body).Status $expected "$label HTTP"
    Assert-Equal (Db-State) $before "$label unchanged PostgreSQL rows,available"
}

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
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', $null, 'Process')
    & dotnet build $apiProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($apiProcess.HasExited) { throw "API exited early. See $stderrFile" }
        try {
            if ((Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/') -TimeoutSec 1).StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "API did not start. See $stderrFile" }
    $dueDate = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
    $pastDate = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')

    Reset-Seed 'T05 step 1'
    $ids = Read-Ids
    $response = Request POST '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E2,$ids.E6); expectedReturnDate=$dueDate }
    Assert-Equal $response.Status 201 'T05 S4 E2/E6 checkout HTTP'
    Assert-Equal @((($response.Body | ConvertFrom-Json).borrowings)).Count 2 'T05 S4 checkout response rows'
    Assert-Equal (Db-State) '2,0' 'T05 S4 checkout PostgreSQL rows,available'

    Reset-Seed 'T05 2a'
    $ids = Read-Ids
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S3; equipmentId=$ids.E2; expectedReturnDate=$dueDate }).Status 201 'T05 2a S3 single HTTP'
    Assert-Equal (Db-State) '1,1' 'T05 2a PostgreSQL rows,available'

    Reset-Seed 'T05 2b'
    $ids = Read-Ids
    Assert-Equal (Request POST '/api/v1/checkout-requests' @{ studentId=$ids.S1; equipmentIds=@($ids.E2,$ids.E6); expectedReturnDate=$dueDate }).Status 201 'T05 2b S1 two-item HTTP'
    Assert-Equal (Db-State) '2,0' 'T05 2b PostgreSQL rows,available'

    Reset-Seed 'T05 2c and invalid cases'
    $ids = Read-Ids
    Check-Rejection 'T05 2c S3 over limit' 409 '/api/v1/checkout-requests' @{ studentId=$ids.S3; equipmentIds=@($ids.E2,$ids.E6); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 duplicate item' 400 '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E2,$ids.E2); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 one item' 400 '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E2); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 unavailable E1' 409 '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E1,$ids.E2); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 restricted S2' 409 '/api/v1/checkout-requests' @{ studentId=$ids.S2; equipmentIds=@($ids.E2,$ids.E6); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 missing item' 404 '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E2,999999); expectedReturnDate=$dueDate }
    Check-Rejection 'T05 invalid date' 400 '/api/v1/checkout-requests' @{ studentId=$ids.S4; equipmentIds=@($ids.E2,$ids.E6); expectedReturnDate=$pastDate }
    Write-Host 'PASS: controlled T05 success, limit and known-failure checks completed. The separate rollback check covers injected HTTP 500.'
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
