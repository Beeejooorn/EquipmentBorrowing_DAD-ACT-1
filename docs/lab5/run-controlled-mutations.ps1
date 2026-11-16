param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$databaseName = 'equipmentborrowing_lab5_acceptance'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$psql = Join-Path $PostgresBin 'psql.exe'
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$seed = Join-Path $PSScriptRoot 'sql\reset-acceptance-data.sql'
$queryFile = Join-Path $env:TEMP 'lab5-controlled-mutation-query.sql'
$stdoutFile = Join-Path $env:TEMP 'lab5-controlled-mutation-api-out.log'
$stderrFile = Join-Path $env:TEMP 'lab5-controlled-mutation-api-error.log'
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
        return @{ Status = [int]$response.StatusCode; Body = $response.Content; Headers = $response.Headers }
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $response = $_.Exception.Response
        $stream = $response.GetResponseStream()
        if ($null -eq $stream) { $body = '' }
        else {
            $reader = New-Object System.IO.StreamReader($stream)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return @{ Status = [int]$response.StatusCode; Body = $body; Headers = $response.Headers }
    }
}

function Db-Query([string]$sql) {
    Set-Content -LiteralPath $queryFile -Value $sql -Encoding UTF8
    $value = (& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'An independent PostgreSQL query failed.' }
    return $value
}

function Db-Counts {
    return Db-Query 'SELECT (SELECT count(*) FROM "Students")::text || '','' || (SELECT count(*) FROM "Equipment")::text || '','' || (SELECT count(*) FROM "Borrowings" WHERE "Status" = ''Active'')::text || '','' || (SELECT count(*) FROM "Borrowings" WHERE "Status" = ''Returned'')::text;'
}

function Reset-Seed([string]$test) {
    Write-Host "Resetting disposable acceptance data for $test..."
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $databaseName -f $seed | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The $test seed reset failed." }
    Assert-Equal (Db-Counts) '4,7,3,1' "$test baseline PostgreSQL counts"
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

function Start-TestApi {
    $script:apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($script:apiProcess.HasExited) { throw "The API exited early. See $stderrFile" }
        try {
            if ((Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/') -TimeoutSec 1).StatusCode -eq 200) { return }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "The API did not start. See $stderrFile"
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
    if ($LASTEXITCODE -ne 0) { throw 'The API build failed.' }
    Start-TestApi
    $dueDate = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
    $pastDate = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')

    Reset-Seed T02
    $ids = Read-Ids
    $created = Request POST '/api/v1/students' @{ studentNumber = 'ST-500'; fullName = 'Eva Test'; contact = $null }
    Assert-Equal $created.Status 201 'T02 student create'
    $newStudentId = ($created.Body | ConvertFrom-Json).id
    Assert-Equal (Request GET "/api/v1/students/$newStudentId").Status 200 'T02 student GET'
    Assert-Equal (Request PUT "/api/v1/students/$newStudentId" @{ studentNumber='ST-500'; fullName='Eva Updated'; contact='eva@example.test' }).Status 200 'T02 student edit'
    Assert-Equal (Request PUT "/api/v1/students/$newStudentId" @{ studentNumber='ST-500'; fullName=' '; contact=$null }).Status 400 'T02 blank name'
    Assert-Equal (Request POST '/api/v1/students' @{ studentNumber=' st-100 '; fullName='Duplicate'; contact=$null }).Status 409 'T02 duplicate number'
    Assert-Equal (Request PUT "/api/v1/students/$newStudentId" @{ studentNumber=' st-100 '; fullName='Eva Updated'; contact=$null }).Status 409 'T02 duplicate number on update'
    Assert-Equal (Request PATCH "/api/v1/students/$($ids.S1)/eligibility" @{ isEligible=$false }).Status 200 'T02 restrict S1'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S1; equipmentId=$ids.E2; expectedReturnDate=$dueDate }).Status 409 'T02 restricted issue'
    $s1History = (Request GET "/api/v1/students/$($ids.S1)/borrowings").Body | ConvertFrom-Json
    $activeE1 = $s1History | Where-Object { $_.equipmentId -eq $ids.E1 -and $_.status -eq 'Active' }
    Assert-Equal (Request POST "/api/v1/borrowings/$($activeE1.id)/return").Status 200 'T02 return E1 while restricted'
    Assert-Equal (Request PATCH "/api/v1/students/$($ids.S1)/eligibility" @{ isEligible=$true }).Status 200 'T02 permit S1'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S1; equipmentId=$ids.E2; expectedReturnDate=$dueDate }).Status 201 'T02 issue after permit'
    Assert-Equal (Request PATCH "/api/v1/students/$($ids.S1)/eligibility" @{ isEligible=$true }).Status 200 'T02 repeated eligibility'
    Assert-Equal (Db-Counts) '5,7,3,2' 'T02 final PostgreSQL counts'
    Assert-Equal (Db-Query 'SELECT "Name" FROM "Students" WHERE "StudentNumber" = ''ST-500'';') 'Eva Updated' 'T02 persisted student edit'
    Stop-Process -Id $apiProcess.Id
    $apiProcess.WaitForExit()
    Start-TestApi
    Assert-Equal ((Request GET "/api/v1/students/$newStudentId").Body | ConvertFrom-Json).fullName 'Eva Updated' 'T02 student edit after API restart'
    Assert-Equal (Db-Counts) '5,7,3,2' 'T02 PostgreSQL counts after restart'

    Reset-Seed T03
    $ids = Read-Ids
    $created = Request POST '/api/v1/equipment' @{ equipmentCode='EQ-008'; name='Headset'; description=$null }
    Assert-Equal $created.Status 201 'T03 equipment create'
    $newEquipmentId = ($created.Body | ConvertFrom-Json).id
    Assert-Equal (Request GET "/api/v1/equipment/$newEquipmentId").Status 200 'T03 equipment GET'
    Assert-Equal (Request PUT "/api/v1/equipment/$newEquipmentId" @{ equipmentCode='EQ-008'; name='Headset Updated'; description=$null }).Status 200 'T03 equipment edit'
    Assert-Equal (Request PUT "/api/v1/equipment/$($ids.E1)" @{ equipmentCode='EQ-001'; name='Camera Updated'; description='New description' }).Status 200 'T03 E1 descriptive edit'
    Assert-Equal (Request PUT "/api/v1/equipment/$newEquipmentId" @{ equipmentCode='EQ-008'; name=' '; description=$null }).Status 400 'T03 blank name'
    Assert-Equal (Request PUT "/api/v1/equipment/$newEquipmentId" @{ equipmentCode=' eq-001 '; name='Headset'; description=$null }).Status 409 'T03 duplicate code'
    Assert-Equal (Request PUT "/api/v1/equipment/$($ids.E1)" @{ equipmentCode='EQ-001'; name='Camera'; description=$null; isAvailable=$true }).Status 400 'T03 protected availability'
    Assert-Equal (Db-Counts) '4,8,3,1' 'T03 final PostgreSQL counts'
    Assert-Equal (Db-Query 'SELECT "IsAvailable" FROM "Equipment" WHERE "EquipmentCode" = ''EQ-001'';') 'f' 'T03 E1 remains unavailable'
    Assert-Equal (Db-Query 'SELECT count(*) FROM "Borrowings" b JOIN "Equipment" e ON e."Id"=b."EquipmentId" WHERE e."EquipmentCode"=''EQ-001'';') '1' 'T03 E1 history retained'
    Stop-Process -Id $apiProcess.Id
    $apiProcess.WaitForExit()
    Start-TestApi
    Assert-Equal ((Request GET "/api/v1/equipment/$newEquipmentId").Body | ConvertFrom-Json).name 'Headset Updated' 'T03 equipment edit after API restart'
    Assert-Equal (Db-Counts) '4,8,3,1' 'T03 PostgreSQL counts after restart'

    Reset-Seed T04
    $ids = Read-Ids
    $created = Request POST '/api/v1/borrowings' @{ studentId=$ids.S4; equipmentId=$ids.E2; expectedReturnDate=$dueDate }
    Assert-Equal $created.Status 201 'T04 single issue'
    $borrowingId = ($created.Body | ConvertFrom-Json).id
    Assert-Equal (Request GET "/api/v1/borrowings/$borrowingId").Status 200 'T04 created borrowing GET'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S2; equipmentId=$ids.E6; expectedReturnDate=$dueDate }).Status 409 'T04 ineligible S2'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S4; equipmentId=$ids.E1; expectedReturnDate=$dueDate }).Status 409 'T04 unavailable E1'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=999999; equipmentId=$ids.E6; expectedReturnDate=$dueDate }).Status 404 'T04 missing student'
    Assert-Equal (Request POST '/api/v1/borrowings' @{ studentId=$ids.S4; equipmentId=$ids.E6; expectedReturnDate=$pastDate }).Status 400 'T04 invalid date'
    Assert-Equal (Request POST "/api/v1/borrowings/$borrowingId/return").Status 200 'T04 return'
    Assert-Equal ((Request GET "/api/v1/borrowings/$borrowingId").Body | ConvertFrom-Json).status 'Returned' 'T04 returned readback'
    Assert-Equal (Request POST "/api/v1/borrowings/$borrowingId/return").Status 409 'T04 repeat return'
    Assert-Equal (Db-Counts) '4,7,3,2' 'T04 final PostgreSQL counts'
    Assert-Equal (Db-Query 'SELECT "IsAvailable" FROM "Equipment" WHERE "EquipmentCode" = ''EQ-002'';') 't' 'T04 E2 available after return'

    Stop-Process -Id $apiProcess.Id
    $apiProcess.WaitForExit()
    Start-TestApi
    Assert-Equal ((Request GET "/api/v1/borrowings/$borrowingId").Body | ConvertFrom-Json).status 'Returned' 'T04 persisted after API restart'
    Assert-Equal (Db-Counts) '4,7,3,2' 'T04 PostgreSQL counts after restart'
    Write-Host 'PASS: controlled T02, T03 and T04 mutation checks completed against the PDF seed.'
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
