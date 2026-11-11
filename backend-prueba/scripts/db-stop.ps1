$bin  = 'C:\Program Files\PostgreSQL\18\bin'
$data = Join-Path $env:USERPROFILE 'pgdata-prestamos'
& "$bin\pg_ctl.exe" -D $data -m fast stop
