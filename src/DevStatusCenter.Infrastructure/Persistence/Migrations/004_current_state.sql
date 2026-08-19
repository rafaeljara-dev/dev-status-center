-- Proyección del "valor vigente" separada del histórico.
--
-- Antes el dashboard resolvía el último snapshot con
--   ROW_NUMBER() OVER (PARTITION BY service_id, metric_code ORDER BY captured_at_ms DESC)
-- sobre usage_snapshots/billing_records completos. Esa consulta visita TODAS las filas del
-- histórico, así que el costo de abrir el popup crece linealmente con los meses de uso y
-- termina rompiendo NFR-007 (<250 ms con caché caliente).
--
-- Estas dos tablas guardan exactamente una fila por (servicio, métrica) y por
-- (servicio, moneda). El dashboard las lee enteras: su tamaño depende del número de
-- servicios, no de la antigüedad de la instalación. El histórico sigue intacto para
-- gráficas y detección de anomalías.

CREATE TABLE IF NOT EXISTS current_usage (
    service_id TEXT NOT NULL REFERENCES services(id),
    metric_code TEXT NOT NULL,
    -- Id del snapshot que produjo esta fila. Informativo, NO es clave foránea: el histórico
    -- se poda por retención y esta proyección debe sobrevivir a esa poda.
    snapshot_id TEXT NOT NULL,
    metric_name TEXT NOT NULL,
    metric_kind INTEGER NOT NULL,
    unit TEXT NOT NULL,
    value_decimal TEXT NOT NULL,
    captured_at_ms INTEGER NOT NULL,
    period_start_ms INTEGER NOT NULL,
    period_end_ms INTEGER NOT NULL,
    period_time_zone TEXT NOT NULL,
    source INTEGER NOT NULL,
    accuracy INTEGER NOT NULL,
    PRIMARY KEY (service_id, metric_code)
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS current_billing (
    service_id TEXT NOT NULL REFERENCES services(id),
    currency TEXT NOT NULL,
    record_id TEXT NOT NULL,
    amount_decimal TEXT NOT NULL,
    captured_at_ms INTEGER NOT NULL,
    period_start_ms INTEGER NOT NULL,
    period_end_ms INTEGER NOT NULL,
    period_time_zone TEXT NOT NULL,
    source INTEGER NOT NULL,
    accuracy INTEGER NOT NULL,
    external_invoice_id TEXT NULL,
    PRIMARY KEY (service_id, currency)
) WITHOUT ROWID;

-- Backfill desde el histórico ya almacenado. En una base nueva no hace nada.
INSERT INTO current_usage (
    service_id, metric_code, snapshot_id, metric_name, metric_kind, unit,
    value_decimal, captured_at_ms, period_start_ms, period_end_ms,
    period_time_zone, source, accuracy)
SELECT service_id, metric_code, id, metric_name, metric_kind, unit,
       value_decimal, captured_at_ms, period_start_ms, period_end_ms,
       period_time_zone, source, accuracy
FROM (
    SELECT *, ROW_NUMBER() OVER (
        PARTITION BY service_id, metric_code
        ORDER BY captured_at_ms DESC) AS rn
    FROM usage_snapshots
)
WHERE rn = 1
ON CONFLICT(service_id, metric_code) DO NOTHING;

INSERT INTO current_billing (
    service_id, currency, record_id, amount_decimal, captured_at_ms,
    period_start_ms, period_end_ms, period_time_zone, source, accuracy,
    external_invoice_id)
SELECT service_id, currency, id, amount_decimal, captured_at_ms,
       period_start_ms, period_end_ms, period_time_zone, source, accuracy,
       external_invoice_id
FROM (
    SELECT *, ROW_NUMBER() OVER (
        PARTITION BY service_id, currency
        ORDER BY captured_at_ms DESC) AS rn
    FROM billing_records
)
WHERE rn = 1
ON CONFLICT(service_id, currency) DO NOTHING;

-- Deliberadamente NO se añade un índice por captured_at_ms para la poda: el borrado corre
-- como mucho una vez al día y un scan secuencial sobre unas decenas de miles de filas es
-- más barato que mantener un índice extra en cada inserción de cada refresh.
