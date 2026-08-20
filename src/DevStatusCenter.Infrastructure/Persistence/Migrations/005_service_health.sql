-- Salud publicada de servicios de terceros.
--
-- Una fila por servicio, sin historial: lo que importa es el estado de ahora, y guardar cada
-- comprobacion generaria miles de filas identicas diciendo "todo bien". WITHOUT ROWID porque la
-- clave ya es el identificador natural y la tabla se lee entera de una vez.
CREATE TABLE IF NOT EXISTS service_health (
    service_key     TEXT PRIMARY KEY,
    display_name    TEXT NOT NULL,
    indicator       INTEGER NOT NULL,
    description     TEXT NOT NULL DEFAULT '',
    status_page_url TEXT NOT NULL,
    incident_title  TEXT NULL,
    incident_url    TEXT NULL,
    checked_at_ms   INTEGER NOT NULL
) WITHOUT ROWID;
