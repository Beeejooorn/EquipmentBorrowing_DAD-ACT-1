param([string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin')

$ErrorActionPreference = 'Stop'
$psql = Join-Path $PostgresBin 'psql.exe'
$databaseName = 'equipmentborrowing_lab5_acceptance'
$queryFile = Join-Path $env:TEMP 'lab5-desktop-readback.sql'
if (-not (Test-Path -LiteralPath $psql)) { throw "psql.exe was not found at $psql" }

$oldPgPassword = [Environment]::GetEnvironmentVariable('PGPASSWORD', 'Process')
$securePassword = Read-Host 'PostgreSQL password for local postgres user' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $env:PGPASSWORD = $plainPassword
    $sql = @'
SELECT 'student|' || "StudentNumber" || '|' || "Name" || '|' || "IsAllowedToBorrow"::text
FROM "Students" WHERE "StudentNumber"='ST-500';
SELECT 'equipment|' || "EquipmentCode" || '|' || "Name" || '|' || "IsAvailable"::text
FROM "Equipment" WHERE "EquipmentCode"='EQ-008';
SELECT 'borrowing|' || s."StudentNumber" || '|' || e."EquipmentCode" || '|' || b."Status" || '|' || b."ReturnedAt"::text
FROM "Borrowings" b
JOIN "Students" s ON s."Id"=b."StudentId"
JOIN "Equipment" e ON e."Id"=b."EquipmentId"
WHERE s."StudentNumber"='ST-500' AND e."EquipmentCode"='EQ-008';
'@
    Set-Content -LiteralPath $queryFile -Value $sql -Encoding UTF8
    $rows = @(& $psql -v ON_ERROR_STOP=1 -t -A -h localhost -p 5432 -U postgres -d $databaseName -f $queryFile)
    if ($LASTEXITCODE -ne 0) { throw 'Fresh PostgreSQL readback failed.' }
    $rows | ForEach-Object { Write-Host $_ }
    if ($rows.Count -ne 3) { throw "Expected exactly three relevant rows, got $($rows.Count)." }
    if ($rows[0] -notmatch '^student\|ST-500\|Eva Updated\|(f|false)$') { throw 'Student database state did not match T07.' }
    if ($rows[1] -notmatch '^equipment\|EQ-008\|Headset Updated\|(t|true)$') { throw 'Equipment database state did not match T07.' }
    if ($rows[2] -notmatch '^borrowing\|ST-500\|EQ-008\|Returned\|\d{4}-\d{2}-\d{2}$') { throw 'Returned borrowing database state did not match T07.' }
    Write-Host 'PASS: T07 desktop changes and independent HTTP restriction persisted in fresh PostgreSQL reads.'
}
finally {
    [Environment]::SetEnvironmentVariable('PGPASSWORD', $oldPgPassword, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable plainPassword, securePassword -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $queryFile -ErrorAction SilentlyContinue
}
