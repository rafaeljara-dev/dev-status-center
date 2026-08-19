# Validacion completa: restore reproducible, build sin warnings, pruebas, publicacion y
# auto-test de la build publicada. Es lo que debe pasar antes de entregar cualquier cambio.
$ErrorActionPreference = "Stop"

dotnet restore DevStatusCenter.slnx --locked-mode
dotnet build DevStatusCenter.slnx -c Release --no-restore
dotnet test DevStatusCenter.slnx -c Release --no-build --collect:"XPlat Code Coverage"
dotnet publish src/DevStatusCenter.Desktop/DevStatusCenter.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=None `
  -o artifacts/win-x64

# Las pruebas unitarias no tocan XAML. El auto-test arranca la build real, renderiza el popup y
# falla si WPF reporta cualquier binding roto: es la unica forma de que una regresion de UI se
# note sin abrir la ventana a ojo.
Write-Host "Ejecutando auto-test de la build publicada..." -ForegroundColor Cyan
$process = Start-Process ./artifacts/win-x64/DevStatusCenter.exe -ArgumentList "--selftest" -PassThru
if (-not $process.WaitForExit(60000)) {
  Stop-Process -Id $process.Id -Force
  throw "El auto-test no termino en 60 s. Revisa un posible interbloqueo al apagar."
}

if ($process.ExitCode -ne 0) {
  $log = Join-Path $env:LOCALAPPDATA "DevStatusCenter\crash.log"
  if (Test-Path $log) { Get-Content $log -Tail 40 }
  throw "El auto-test fallo con codigo $($process.ExitCode). Revisa $log."
}

Write-Host "Validacion completa. La build esta en artifacts/win-x64." -ForegroundColor Green
