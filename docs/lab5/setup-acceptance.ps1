param(
    [string]$DatabaseName = 'equipmentborrowing_lab5_acceptance',
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin'
)

$ErrorActionPreference = 'Stop'

if (-not $DatabaseName.EndsWith('_lab5_acceptance', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The acceptance reset requires a disposable database ending in _lab5_acceptance.'
}

$psql = Join-Path $PostgresBin 'psql.exe'
if (-not (Test-Path -LiteralPath $psql)) {
    throw "psql.exe was not found at $psql. Pass -PostgresBin with your PostgreSQL bin directory."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'EquipmentBorrowing\src\EquipmentBorrowing.Infrastructure\EquipmentBorrowing.Infrastructure.csproj'
$seed = Join-Path $PSScriptRoot 'sql\reset-acceptance-data.sql'

$oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', 'Process')
$oldPgPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
$securePassword = Read-Host 'PostgreSQL password for local postgres user' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)

try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = 'localhost'
    $connection['Port'] = 5432
    $connection['Database'] = $DatabaseName
    $connection['Username'] = 'postgres'
    $connection['Password'] = $plainPassword

    $env:ConnectionStrings__EquipmentBorrowing = $connection.ConnectionString
    $env:PGPASSWORD = $plainPassword

    Write-Host 'Applying EF Core migrations to the disposable acceptance database...'
    & dotnet ef database update --project $project
    if ($LASTEXITCODE -ne 0) { throw 'The migration failed; the seed was not run.' }

    Write-Host 'Resetting S1-S4, E1-E7 and borrowing records...'
    & $psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U postgres -d $DatabaseName -f $seed
    if ($LASTEXITCODE -ne 0) { throw 'The acceptance seed failed.' }
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EquipmentBorrowing', $oldConnection, 'Process')
    [Environment]::SetEnvironmentVariable('PGPASSWORD', $oldPgPassword, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword, connection -ErrorAction SilentlyContinue
}
