# Setup en Windows

## Herramientas

Instala .NET 10 SDK y opcionalmente Visual Studio con **.NET desktop development**. Confirma:

```powershell
dotnet --info
git --version
```

## Restaurar y ejecutar

```powershell
dotnet restore DevStatusCenter.slnx --use-lock-file
dotnet build DevStatusCenter.slnx -c Debug
dotnet run --project src/DevStatusCenter.Desktop/DevStatusCenter.Desktop.csproj
```

La base se crea en:

```text
%LOCALAPPDATA%\DevStatusCenter\dev-status-center.db
```

Secretos futuros:

```text
%LOCALAPPDATA%\DevStatusCenter\secrets\
```

## Publicar

```powershell
./scripts/verify.ps1
```

El resultado framework-dependent single-file queda en `artifacts/win-x64`. Se elige framework-dependent inicialmente para evitar duplicar el runtime en cada actualización. Una release puede ofrecer además self-contained si la instalación sin prerrequisitos lo justifica.

## Reset de datos de desarrollo

1. Salir desde el menú del tray.
2. Mover `%LOCALAPPDATA%\DevStatusCenter` a una carpeta de respaldo.
3. Reiniciar la aplicación.

No borres esa carpeta si ya configuraste providers reales sin haber respaldado/revocado credenciales.

## Problemas comunes

### No aparece el icono

Revisa la sección de iconos ocultos de Windows. Confirma que no existe otro proceso `DevStatusCenter`; la app permite una instancia.

### Terminal no abre

Quick Access usa `wt.exe`. Instala Windows Terminal o cambia la acción a Explorer/Editor.

### VS Code no abre

Confirma que `code` está en PATH. La clase `WindowsQuickAccessLauncher` permite inyectar otro ejecutable en una futura pantalla Advanced.

### SQLite queda bloqueado en debug

Detén la instancia anterior. WAL crea archivos `-wal` y `-shm`; son normales y están ignorados por Git.

