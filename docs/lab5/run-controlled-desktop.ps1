param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\EquipmentBorrowing.Api.csproj'
$desktopProject = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Desktop\EquipmentBorrowing.Desktop.csproj'
$apiDll = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Api\bin\Release\net10.0\EquipmentBorrowing.Api.dll'
$desktopExe = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Desktop\bin\Release\net10.0\EquipmentBorrowing.Desktop.exe'
$psql = Join-Path $PostgresBin 'psql.exe'
$baseUrl = 'http://localhost:5087'
$apiOut = Join-Path $env:TEMP 'lab5-controlled-desktop-api-out.log'
$apiError = Join-Path $env:TEMP 'lab5-controlled-desktop-api-error.log'

if (-not (Test-Path -LiteralPath $psql)) { throw "psql.exe was not found at $psql" }
$oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', 'Process')
$oldApiUrl = [Environment]::GetEnvironmentVariable('EQUIPMENT_BORROWING_API_URL', 'Process')
$oldFault = [Environment]::GetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', 'Process')
$securePassword = Read-Host 'PostgreSQL password for local postgres user' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$apiProcess = $null
$desktopProcess = $null

try {
    & dotnet build $apiProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    & dotnet build $desktopProject -c Release -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }

    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = 'localhost'
    $connection['Port'] = 5432
    $connection['Database'] = 'equipmentborrowing_lab5_acceptance'
    $connection['Username'] = 'postgres'
    $connection['Password'] = $plainPassword
    $env:ConnectionStrings__EquipmentBorrowing = $connection.ConnectionString
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', $null, 'Process')
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $apiOut -RedirectStandardError $apiError
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $null, 'Process')

    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ($apiProcess.HasExited) { throw "API exited early. See $apiError" }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + '/api/v1/equipment') -TimeoutSec 1
            if ($response.StatusCode -eq 200 -and @(($response.Content | ConvertFrom-Json)).Count -eq 7) {
                $ready = $true
                break
            }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "API did not return the seven seeded equipment items. See $apiOut and $apiError" }
    Write-Host 'PASS T07 API-alone equipment read = 200, seven seeded items'

    $env:EQUIPMENT_BORROWING_API_URL = $baseUrl
    $desktopProcess = Start-Process -FilePath $desktopExe -PassThru
    Write-Host "T07 desktop launched against $baseUrl and the disposable acceptance database."
    Write-Host 'Keep this PowerShell window open. Close the desktop window when finished; this script will then stop the isolated API.'
    $desktopProcess.WaitForExit()
}
finally {
    if ($null -ne $desktopProcess -and -not $desktopProcess.HasExited) { Stop-Process -Id $desktopProcess.Id }
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_API_URL', $oldApiUrl, 'Process')
    [Environment]::SetEnvironmentVariable('EQUIPMENT_BORROWING_CHECKOUT_FAULT', $oldFault, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword, connection -ErrorAction SilentlyContinue
}
