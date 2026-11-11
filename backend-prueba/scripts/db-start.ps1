$bin  = 'C:\Program Files\PostgreSQL\18\bin'
$data = Join-Path $env:USERPROFILE 'pgdata-prestamos'

if (-not (Test-Path $data)) { throw "No existe $data. Ver README (sección Base de datos)." }

if (Get-NetTCPConnection -LocalPort 5433 -State Listen -ErrorAction SilentlyContinue) {
    Write-Host 'PostgreSQL del proyecto ya está corriendo en el puerto 5433.'
    return
}

Remove-Item (Join-Path $data 'postmaster.pid') -ErrorAction SilentlyContinue
Start-Process -FilePath "$bin\postgres.exe" `
    -ArgumentList @('-D', $data, '-p', '5433', '-c', 'listen_addresses=localhost') `
    -WindowStyle Hidden -RedirectStandardError (Join-Path $data 'stderr.log')
Start-Sleep 4
Write-Host 'PostgreSQL del proyecto iniciado en localhost:5433.'
