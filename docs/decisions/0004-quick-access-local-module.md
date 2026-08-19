# ADR 0004: Quick Access como módulo local

- Estado: aceptado
- Fecha: 2026-08-19

## Contexto

Se requieren accesos rápidos a carpetas, proyectos y niveles jerárquicos desde el tray sin perjudicar performance.

## Decisión

Modelar Quick Access fuera del provider engine, persistir adjacency list en SQLite y abrir rutas únicamente por acción del usuario. No usar filesystem watchers.

## Consecuencias

- costo idle cero;
- aparece en popup y menú de tray;
- Explorer/Terminal/Editor son acciones Windows aisladas tras `IQuickAccessLauncher`;
- estado Git, tamaños y cambios quedan fuera de esta capacidad.

