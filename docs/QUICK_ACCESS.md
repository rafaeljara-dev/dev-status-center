# Quick Access

## Propósito

Reducir el tiempo entre “quiero trabajar en este proyecto” y tener abierta la herramienta correcta. Convive con el dashboard porque es una acción breve y frecuente desde el tray, pero no participa en billing ni scheduler.

## Modelo

Tipos:

- `Group`: contenedor sin ruta;
- `Folder`: directorio general;
- `Project`: directorio tratado como proyecto.

Acciones:

- `Explorer`: abre con shell de Windows;
- `Terminal`: ejecuta `wt.exe -d <ruta>`;
- `Editor`: ejecuta `code <ruta>`.

Ejemplo:

```text
Clientes
├── Keymex
│   ├── Paneles
│   └── API
└── Sitios
Personal
└── Dev Status Center
```

## Seguridad

Las rutas se normalizan con `Path.GetFullPath`, deben existir y se pasan mediante `ProcessStartInfo.ArgumentList`. No se construye `cmd.exe /c "..."`, por lo que espacios y caracteres especiales no convierten la ruta en un comando.

## Performance

No existe `FileSystemWatcher`, indexación, cálculo de tamaño, lectura de git status ni detección de cambios. El módulo hace una consulta SQLite al cargar el dashboard y una validación de existencia al hacer clic.

## Extensiones futuras compatibles

- acciones adicionales configurables por proyecto;
- abrir solución `.slnx` específica;
- perfiles de terminal;
- iconos locales cacheados;
- “recent projects” opcional basado en acciones dentro de la app;
- plantillas de acciones (`editor + terminal + browser`) iniciadas solo por clic.

No agregar monitoreo de repositorios al módulo. Si se desea estado Git, debe ser un provider con frecuencia explícita y power modes.

