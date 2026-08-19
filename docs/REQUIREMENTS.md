# Requisitos y criterios de aceptación

Los identificadores de esta página sirven para issues, pruebas y PRs.

## Funcionales

### Tray y popup

- **FR-001** La aplicación inicia sin mostrar ventana principal.
- **FR-002** Debe existir un único icono de tray por sesión de usuario.
- **FR-003** Clic izquierdo alterna el popup; clic derecho abre el menú nativo.
- **FR-004** El popup se posiciona dentro del working area del monitor activo y respeta DPI.
- **FR-005** Abrir el popup muestra caché local y no espera red.
- **FR-006** El icono usa verde, amarillo, naranja, rojo, gris o morado según presupuesto/power mode.

### Datos y providers

- **FR-010** Todo provider implementa `IProvider` y produce `ProviderRefreshResult` normalizado.
- **FR-011** Usage, billing, quota y subscriptions son capacidades separadas.
- **FR-012** Cada cifra registra `DataSourceKind` y `DataAccuracy`.
- **FR-013** Una falla afecta solo al provider que falló.
- **FR-014** Un fallo conserva el último snapshot válido.
- **FR-015** La arquitectura permite múltiples cuentas por provider.
- **FR-016** Refresh manual respeta `MinimumInterval`.
- **FR-017** HTTP soporta timeout, cancellation, 429/5xx, `Retry-After`, backoff y jitter.

### Persistencia

- **FR-020** SQLite almacena cuentas, servicios, usage, billing, subscriptions, payments, budgets, estados e históricos.
- **FR-021** Las escrituras de un resultado de provider son atómicas.
- **FR-022** Los importes decimales se almacenan sin redondeo binario.
- **FR-023** Timestamps se guardan en UTC y el periodo conserva su zona declarada.
- **FR-024** Las migraciones son incrementales e idempotentes.
- **FR-025** SQLite nunca contiene el secreto real.

### Forecast y monedas

- **FR-030** El forecast separa consumo variable de obligaciones fijas.
- **FR-031** Los pagos enlazados a una suscripción no se cuentan dos veces.
- **FR-032** La proyección nunca es menor al costo actual.
- **FR-033** Montos en monedas distintas no se suman sin una conversión explícita.
- **FR-034** El forecast calculado se marca como estimado.

### Power

- **FR-040** Normal usa el intervalo normal de cada provider.
- **FR-041** Eco usa el intervalo eco de cada provider.
- **FR-042** Paused cancela el ciclo actual y no agenda otro.
- **FR-043** Gaming cancela red/timers/notificaciones y conserva UI de caché.
- **FR-044** Reanudar agenda refresh inmediato de providers stale.
- **FR-045** El modo seleccionado persiste entre ejecuciones.

### Quick Access

- **FR-050** Se pueden crear grupos, carpetas y proyectos.
- **FR-051** Un elemento puede pertenecer a otro grupo y formar niveles.
- **FR-052** Cada ruta elige Explorer, Terminal o Editor como acción predeterminada.
- **FR-053** Las rutas se pasan como argumentos, no como comandos de shell concatenados.
- **FR-054** Eliminar un grupo elimina sus hijos después de confirmación.
- **FR-055** Quick Access no monitorea ni indexa filesystem en background.
- **FR-056** Los accesos aparecen en popup y menú de tray.

### Alertas

- **FR-070** Al cruzar un umbral de presupuesto se notifica solo el mas alto alcanzado.
- **FR-071** Una proyeccion que excede el limite avisa antes de gastarlo.
- **FR-072** Un pago programado avisa dentro de los 3 dias previos.
- **FR-073** Un provider que necesita credenciales avisa de inmediato; un error transitorio espera 3 fallos.
- **FR-074** Una alerta ya notificada calla durante su enfriamiento (12 h por defecto).
- **FR-075** El usuario puede silenciar una alerta concreta y deja de entregarse.
- **FR-076** No se evalua ninguna alerta antes del primer sync exitoso.

### Seguridad

- **FR-060** DPAPI usa scope del usuario actual.
- **FR-061** Los providers solicitan permisos mínimos y de solo lectura.
- **FR-062** Logs y mensajes no incluyen headers, tokens ni bodies sensibles.
- **FR-063** Configuración versionada solo incluye `credentialReference`.

## No funcionales

- **NFR-001** CPU idle objetivo: indistinguible de 0% en una muestra de 60 segundos.
- **NFR-002** Red idle: 0 requests fuera de vencimientos, reanudación o acción manual.
- **NFR-003** Disco idle: 0 escrituras entre eventos programados.
- **NFR-004** GPU idle: 0 cuando el popup está cerrado.
- **NFR-005** No existen timers con periodo menor a un minuto en producción.
- **NFR-006** La UI no usa WebView, Chromium o animaciones permanentes.
- **NFR-007** Un clic con caché caliente debe renderizar en menos de 250 ms en hardware objetivo.
- **NFR-008** Toda operación de red acepta `CancellationToken`.
- **NFR-009** El repositorio compila sin warnings y trata warnings como errores.
- **NFR-010** Cambios de dominio, forecast y SQLite tienen pruebas automatizadas.

## Criterio de salida de MVP 0

MVP 0 está terminado cuando en Windows:

1. inicia solo en tray;
2. el primer refresh mock crea cinco servicios;
3. el popup muestra current, projected, budget, cuotas y pagos;
4. cerrar/abrir vuelve a mostrar caché inmediatamente;
5. Paused detiene red/timers y Normal reanuda;
6. un Quick Access abre una ruta con la acción configurada;
7. `scripts/verify.ps1` termina correctamente;
8. `scripts/measure-idle.ps1` produce una muestra guardable en un issue.

## Criterio de salida del primer provider real

- credencial guardada por DPAPI;
- scopes de solo lectura documentados;
- request y parsing probados con fixtures anonimizados;
- billing reportado separado de estimaciones;
- 401, 403, 429, 5xx, timeout y cancelación verificados;
- una cuenta con varios recursos normalizada;
- caché sigue visible sin internet;
- ningún secreto aparece en repo, DB, error o log.

