#Requires -Version 7.0
<#
.SYNOPSIS
    Instala la build publicada en el perfil del usuario y la deja corriendo en la bandeja.

.DESCRIPTION
    No hay instalador MSI a proposito: la aplicacion es un unico ejecutable dependiente del
    framework mas la libreria nativa de SQLite. Instalar es copiar esos dos archivos a una ruta
    estable, crear el acceso directo y arrancarla. Sin registro, sin permisos de administrador,
    sin nada que desinstalar despues salvo borrar la carpeta.

    Requiere el runtime .NET Desktop 10 (viene con el SDK, que ya esta en esta maquina).
#>
[CmdletBinding()]
param(
    # Por defecto no toca el arranque de Windows: eso ya se activa desde el menu de la bandeja.
    [switch]$StartWithWindows
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot 'artifacts/win-x64'
$target = Join-Path $env:LOCALAPPDATA 'Programs/DevStatusCenter'
$exeName = 'DevStatusCenter.exe'

if (-not (Test-Path (Join-Path $source $exeName))) {
    throw "No hay build publicada en $source. Corre ./scripts/verify.ps1 primero."
}

# Una copia sobre el ejecutable en uso falla con acceso denegado, asi que primero se cierra.
$running = Get-Process -Name 'DevStatusCenter' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host 'Cerrando la instancia en ejecucion...'
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 700
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Force -Recurse
Write-Host "Instalado en $target"

$exePath = Join-Path $target $exeName
$startMenu = Join-Path $env:APPDATA 'Microsoft/Windows/Start Menu/Programs/Dev Status Center.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenu)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'Gasto de IA y nube en la bandeja del sistema'
$shortcut.Save()
Write-Host 'Acceso directo creado en el menu Inicio.'

if ($StartWithWindows) {
    Set-ItemProperty `
        -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
        -Name 'DevStatusCenter' `
        -Value "`"$exePath`""
    Write-Host 'Arranque con Windows activado.'
}

Start-Process -FilePath $exePath -WorkingDirectory $target
Write-Host 'Corriendo. Busca el icono en el area de notificaciones (puede estar en el desplegable ^).'
