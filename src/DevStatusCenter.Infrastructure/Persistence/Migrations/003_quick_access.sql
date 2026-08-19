CREATE TABLE IF NOT EXISTS quick_access_entries (
    id TEXT PRIMARY KEY,
    parent_id TEXT NULL REFERENCES quick_access_entries(id) ON DELETE CASCADE,
    display_name TEXT NOT NULL,
    kind INTEGER NOT NULL,
    path TEXT NULL,
    default_action INTEGER NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_pinned INTEGER NOT NULL CHECK (is_pinned IN (0, 1)),
    updated_at_ms INTEGER NOT NULL,
    CHECK ((kind = 0 AND path IS NULL) OR (kind <> 0 AND path IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_quick_access_parent
    ON quick_access_entries(parent_id, is_pinned, sort_order, display_name);

