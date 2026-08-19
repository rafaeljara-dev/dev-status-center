# ADR 0005: Proyección de estado vigente separada del histórico

- Estado: aceptado
- Fecha: 2026-08-19

## Contexto

El dashboard resolvía "el último valor de cada métrica" con una función de ventana sobre el
histórico completo:

```sql
ROW_NUMBER() OVER (PARTITION BY service_id, metric_code ORDER BY captured_at_ms DESC)
```

Esa consulta visita todas las filas de `usage_snapshots` y `billing_records`, así que el costo
de abrir el popup crece linealmente con la antigüedad de la instalación. Con el volumen que
genera el propio `MockProvider` (14 filas de usage + 5 de billing cada 15 minutos):

| Histórico | Filas | Consulta con ventana | Proyección vigente |
|---|---:|---:|---:|
| 7 días | 10.080 | 30,6 ms | 0,33 ms |
| 30 días | 43.200 | **276,9 ms** | 0,18 ms |
| 90 días | 129.600 | 879,9 ms | 0,31 ms |
| 365 días | 525.600 | 1.377,8 ms | 0,06 ms |

El presupuesto de NFR-007 es **menos de 250 ms** de clic a popup con caché caliente. La consulta
anterior lo rompía durante el primer mes de uso, sin que ningún cambio de código lo provocara:
bastaba con dejar la aplicación encendida.

Añadir índices no lo resuelve. `ix_usage_latest` ya existía y la ventana lo aprovecha para
ordenar, pero sigue teniendo que producir una fila por cada snapshot antes de descartarlas.

## Decisión

Dividir la responsabilidad en dos:

- `usage_snapshots` / `billing_records` siguen siendo **append-only** y guardan el histórico para
  gráficas, tendencias y detección de anomalías.
- `current_usage` / `current_billing` guardan **exactamente una fila** por `(service_id,
  metric_code)` y por `(service_id, currency)`. Es lo único que lee el dashboard.

La proyección se actualiza en la misma transacción y en el mismo comando que la inserción
histórica, con `ON CONFLICT … DO UPDATE … WHERE excluded.captured_at_ms >= …`, de modo que una
respuesta que llega tarde nunca hace retroceder el valor vigente.

Ambas tablas son `WITHOUT ROWID`: clave primaria de texto y filas pequeñas, el caso exacto en el
que se ahorra el árbol B secundario.

`snapshot_id` / `record_id` son informativos y **no** son claves foráneas: el histórico se poda y
la proyección debe sobrevivir a esa poda.

## Retención

`ILocalStore.PruneHistoryAsync` borra snapshots e historial de refresh más viejos que la
retención configurada (400 días por defecto: cubre las comparativas de 12 meses del roadmap con
margen para periodos de facturación desfasados).

Se ejecuta desde el loop del scheduler, no desde la ruta de escritura: como mucho una vez cada
24 horas, cinco minutos después del arranque como pronto, sólo cuando no hay refrescos pendientes
y nunca en `Paused` o `Gaming`. Si falla, se traga la excepción y se reintenta al día siguiente;
el mantenimiento nunca debe tumbar el scheduler.

No se añadió índice por `captured_at_ms` para acelerar el borrado: correría una vez al día contra
un scan secuencial barato, mientras que el índice se pagaría en cada inserción de cada refresh.

Tampoco se hace `VACUUM`. Las páginas liberadas las reutilizan las inserciones siguientes, así que
el archivo llega a un tamaño estable por sí solo.

## Consecuencias

- El tiempo de lectura del dashboard depende del **número de servicios**, no de la antigüedad de
  la instalación. Es constante en el tiempo.
- Una escritura de refresh cuesta una sentencia extra por métrica, dentro del mismo comando y el
  mismo binding de parámetros. Se paga cada 15 minutos y se cobra en cada apertura del popup.
- Hay dos lugares donde vive "el valor actual". La transacción compartida es lo que garantiza que
  no se separen; cualquier provider nuevo obtiene el comportamiento gratis porque escribe a través
  de `ApplyProviderRefreshAsync`.
- Podar el histórico ya no puede dejar el popup en blanco. Hay una prueba que lo fija.
