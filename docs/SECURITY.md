# Seguridad

## Modelo de amenazas inicial

Activos:

- tokens de providers;
- IDs de cuentas/proyectos;
- historial de gasto;
- rutas de proyectos;
- facturas futuras.

Adversarios considerados:

- lectura accidental del repositorio o backup;
- malware/proceso de otro usuario sin acceso a la sesión;
- logs y mensajes de error que filtran datos;
- token con permisos excesivos;
- manipulación de una ruta de Quick Access.

Fuera de alcance: proteger secretos contra malware ejecutándose como el mismo usuario y con acceso completo a su sesión. DPAPI `CurrentUser` reduce exposición en disco, pero el provider debe descifrar el token brevemente para usarlo.

## Secret store

`DpapiSecretStore`:

- cifra con `ProtectedData.Protect`;
- usa `DataProtectionScope.CurrentUser`;
- deriva entropy por referencia lógica;
- nombra archivos con SHA-256 de la referencia;
- intenta limpiar buffers de bytes después de usarlos;
- escribe primero a archivo temporal y luego reemplaza.

Los archivos viven en `%LOCALAPPDATA%\DevStatusCenter\secrets`. SQLite guarda algo como `cloudflare-personal`, nunca el token.

## Permisos

Cada provider debe documentar:

- recurso al que accede;
- scopes exactos;
- por qué son necesarios;
- si el token puede restringirse por cuenta;
- proceso de revocación.

No se aceptan scopes write/admin para una integración de lectura.

## Red

- TLS según configuración de Windows/.NET;
- timeout por request;
- connections reutilizadas;
- máximo de conexiones por host;
- respuestas leídas como stream;
- bodies no incluidos automáticamente en excepciones;
- cancelación al pausar.

## SQLite

La base no está cifrada en MVP 0. Contiene datos operativos, rutas y referencias, no secretos. Si el threat model requiere cifrar todo el historial, evaluar SQLCipher o cifrado por columna en un ADR; no introducirlo sin medir tamaño, startup y distribución nativa.

## Quick Access

- valida ruta existente antes de abrir;
- no concatena shell commands;
- no eleva privilegios;
- no abre grupos;
- el usuario confirma borrado recursivo de grupos.

## Reporte

No abras un issue público con tokens o responses reales. Revoca primero la credencial y comparte una reproducción anonimizada.

