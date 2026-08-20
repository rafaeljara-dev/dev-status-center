#Requires -Version 7.0
<#
.SYNOPSIS
    Compila y muestra en la terminal lo mismo que mostraria el popup, sin publicar ni instalar.

.DESCRIPTION
    El ciclo corto para trabajar: cambiar algo, ver el dato real, decidir. Usa el mismo codigo y
    el mismo DashboardSnapshot que la ventana, y la misma base de datos y credenciales que la
    version instalada — asi que lo que sale aqui es lo que va a salir alli.

    Fuerza un refresh contra los providers reales en cada ejecucion.

.EXAMPLE
    ./scripts/run.ps1
    ./scripts/run.ps1 -Window     # abre la ventana en vez de imprimir
#>
[CmdletBinding()]
param(
    # Abre el popup de verdad en lugar del informe de texto. Util para mirar el diseno.
    [switch]$Window
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    # Build normal, no publish: es lo que hace la diferencia entre segundos y medio minuto.
    dotnet build src/DevStatusCenter.Desktop -c Debug --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo." }

    $exe = Join-Path $repoRoot 'src/DevStatusCenter.Desktop/bin/Debug/net10.0-windows/win-x64/DevStatusCenter.exe'
    if (-not (Test-Path $exe)) { throw "No se encontro $exe" }

    # Una instancia a la vez: el guardia de instancia unica haria salir a la nueva en silencio.
    $running = Get-Process -Name 'DevStatusCenter' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'Cerrando la instancia en ejecucion...'
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 700
    }

    if ($Window) {
        Start-Process -FilePath $exe
        Write-Host 'Abierta. El icono esta en el area de notificaciones.'
        return
    }

    # El proceso es WinExe y se engancha a esta consola; el pipe fuerza a esperar su salida.
    & $exe --console | Out-Host
}
finally {
    Pop-Location
}
