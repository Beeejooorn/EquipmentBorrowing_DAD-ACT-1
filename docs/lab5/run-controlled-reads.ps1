param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$databaseName = 'equipmentborrowing_lab5_acceptance'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$psql = Join-Path $PostgresBin 'psql.exe'
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$seed = Join-Path $PSScriptRoot 'sql\reset-acceptance-data.sql'
$stateFile = Join-Path $env:TEMP 'lab5-controlled-read-state.sql'
$stdoutFile = Join-Path $env:TEMP 'lab5-controlled-read-api-out.log'
$stderrFile = Join-Path $env:TEMP 'lab5-controlled-read-api-error.log'
$baseUrl = 'http://localhost:5087'

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected $expected but got $actual" }
    Write-Host "PASS $label = $actual"
}

function Request([string]$method, [string]$path) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method $method -Uri ($baseUrl + $path) -TimeoutSec 8
        return @{ Status = [int]$response.StatusCode; Body = $response.Content }
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $response = $_.Exception.Response
        $stream = $response.GetResponseStream()
        if ($null -eq $stream) { $body = '' }
        else {
            $reader = New-Object System.IO.StreamReader($stream)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return @{ Status = [int]$response.StatusCode; Body = $body }
    }
}

function Array-Count([string]$json) {
    if ($json.Trim() -eq '[]') { return 0 }
    return @((ConvertFrom-Json -InputObject $json)).Count
}

function Database-State {
    $value = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $stateFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The independent database read failed.' }
    return $value
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

    $stateQuery = @'
SELECT (SELECT count(*) FROM "Students")::text || ',' ||
       (SELECT count(*) FROM "Equipment")::text || ',' ||
       (SELECT count(*) FROM "Borrowings" WHERE "Status" = 'Active')::text || ',' ||
       (SELECT count(*) FROM "Borrowings" WHERE "Status" = 'Returned')::text;
'@
    Set-Content -LiteralPath $stateFile -Value $stateQuery -Encoding UTF8

    Write-Host 'Resetting disposable data for T01...'
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $databaseName -f $seed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The T01 seed reset failed.' }
    $before = Database-State
    Assert-Equal $before '4,7,3,1' 'T01 baseline counts'

    & dotnet build $apiProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'The API build failed.' }
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($apiProcess.HasExited) { throw "The API exited early. See $stderrFile" }
        try {
            if ((Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/') -TimeoutSec 1).StatusCode -eq 200) {
                $ready = $true; break
            }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "The API did not start. See $stderrFile" }

    $equipment = Request GET '/api/v1/equipment'
    Assert-Equal $equipment.Status 200 'T01 equipment status'
    Assert-Equal (Array-Count $equipment.Body) 7 'T01 equipment count'
    $available = Request GET '/api/v1/equipment?available=true'
    Assert-Equal (Array-Count $available.Body) 4 'T01 available equipment count'
    $students = Request GET '/api/v1/students'
    Assert-Equal (Array-Count $students.Body) 4 'T01 student count'
    $s1 = (($students.Body | ConvertFrom-Json) | Where-Object StudentNumber -eq 'ST-100').Id
    $s4 = (($students.Body | ConvertFrom-Json) | Where-Object StudentNumber -eq 'ST-400').Id
    $e1 = (($equipment.Body | ConvertFrom-Json) | Where-Object EquipmentCode -eq 'EQ-001').Id
    $e3 = (($equipment.Body | ConvertFrom-Json) | Where-Object EquipmentCode -eq 'EQ-003').Id
    if (-not $s1 -or -not $s4 -or -not $e1 -or -not $e3) { throw 'Could not resolve seed IDs from the API.' }
    Assert-Equal (Request GET "/api/v1/students/$s1").Status 200 'T01 S1 lookup'
    Assert-Equal (Request GET "/api/v1/equipment/$e1").Status 200 'T01 E1 lookup'
    Assert-Equal (Array-Count (Request GET "/api/v1/students/$s1/borrowings").Body) 2 'T01 S1 history count'
    Assert-Equal (Array-Count (Request GET "/api/v1/equipment/$e1/borrowings").Body) 1 'T01 E1 history count'
    Assert-Equal (Array-Count (Request GET "/api/v1/students/$s4/borrowings").Body) 0 'T01 empty S4 history'
    Assert-Equal (Request GET '/api/v1/students/999999').Status 404 'T01 missing student'
    Assert-Equal (Request GET '/api/v1/unknown-route').Status 404 'T01 wrong route'
    Assert-Equal (Request POST "/api/v1/equipment/$e1").Status 405 'T01 unsupported method'
    $openApi = Request GET '/openapi/v1.json'
    Assert-Equal $openApi.Status 200 'T01 OpenAPI status'
    $spec = $openApi.Body | ConvertFrom-Json
    Assert-Equal $spec.openapi '3.0.3' 'T01 OpenAPI version'
    $operationCount = 0
    foreach ($path in $spec.paths.PSObject.Properties) { $operationCount += @($path.Value.PSObject.Properties).Count }
    Assert-Equal $operationCount 16 'T01 OpenAPI operation count'
    Assert-Equal (Database-State) $before 'T01 unchanged database counts'

    Write-Host 'Resetting disposable data for T06...'
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $databaseName -f $seed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The T06 seed reset failed.' }
    $before = Database-State
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?status=Active').Body) 3 'T06 active filter count'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?status=Returned').Body) 1 'T06 returned filter count'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?search=cAmErA').Body) 1 'T06 mixed-case equipment search'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?search=st-100').Body) 2 'T06 student-number search'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?search=ada').Body) 2 'T06 student-name search'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?search=tripod').Body) 0 'T06 no matches'
    Assert-Equal (Request GET '/api/v1/borrowings?status=invalid').Status 400 'T06 invalid status'
    Assert-Equal (Array-Count (Request GET "/api/v1/equipment/$e3/borrowings").Body) 1 'T06 E3 returned history'
    Assert-Equal (Request GET '/api/v1/equipment/999999/borrowings').Status 404 'T06 missing equipment history'
    foreach ($sort in @('borrowedDate', 'expectedReturnDate')) {
        foreach ($direction in @('asc', 'desc')) {
            $sorted = Request GET "/api/v1/borrowings?sort=$sort&direction=$direction"
            Assert-Equal $sorted.Status 200 "T06 sort $sort $direction"
            Assert-Equal (Array-Count $sorted.Body) 4 "T06 sort count $sort $direction"
        }
    }
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings?search=ada&status=Active&sort=expectedReturnDate&direction=desc').Body) 1 'T06 combined filters'
    Assert-Equal (Array-Count (Request GET '/api/v1/borrowings').Body) 4 'T06 reset to defaults'
    Assert-Equal (Database-State) $before 'T06 unchanged database counts'
    Write-Host 'PASS: controlled T01 and T06 read-only checks completed against the PDF seed.'
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('PGPASSWORD', $oldPgPassword, 'Process')
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', $oldFault, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword, connection -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stateFile -ErrorAction SilentlyContinue
}
