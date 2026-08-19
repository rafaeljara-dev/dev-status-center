CREATE TABLE IF NOT EXISTS provider_accounts (
    id TEXT PRIMARY KEY,
    provider_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    external_account_id TEXT NULL,
    credential_reference TEXT NULL,
    is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
    updated_at_ms INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS services (
    id TEXT PRIMARY KEY,
    provider_id TEXT NOT NULL,
    provider_account_id TEXT NOT NULL REFERENCES provider_accounts(id),
    external_id TEXT NOT NULL,
    name TEXT NOT NULL,
    category INTEGER NOT NULL,
    cost_behavior INTEGER NOT NULL,
    is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
    updated_at_ms INTEGER NOT NULL,
    UNIQUE(provider_account_id, external_id)
);

CREATE TABLE IF NOT EXISTS usage_snapshots (
    id TEXT PRIMARY KEY,
    service_id TEXT NOT NULL REFERENCES services(id),
    metric_code TEXT NOT NULL,
    metric_name TEXT NOT NULL,
    metric_kind INTEGER NOT NULL,
    unit TEXT NOT NULL,
    value_decimal TEXT NOT NULL,
    captured_at_ms INTEGER NOT NULL,
    period_start_ms INTEGER NOT NULL,
    period_end_ms INTEGER NOT NULL,
    period_time_zone TEXT NOT NULL,
    source INTEGER NOT NULL,
    accuracy INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS billing_records (
    id TEXT PRIMARY KEY,
    service_id TEXT NOT NULL REFERENCES services(id),
    amount_decimal TEXT NOT NULL,
    currency TEXT NOT NULL,
    captured_at_ms INTEGER NOT NULL,
    period_start_ms INTEGER NOT NULL,
    period_end_ms INTEGER NOT NULL,
    period_time_zone TEXT NOT NULL,
    source INTEGER NOT NULL,
    accuracy INTEGER NOT NULL,
    external_invoice_id TEXT NULL
);

CREATE TABLE IF NOT EXISTS subscriptions (
    id TEXT PRIMARY KEY,
    service_id TEXT NULL REFERENCES services(id),
    name TEXT NOT NULL,
    amount_decimal TEXT NOT NULL,
    currency TEXT NOT NULL,
    cadence INTEGER NOT NULL,
    next_renewal_at_ms INTEGER NOT NULL,
    source INTEGER NOT NULL,
    is_active INTEGER NOT NULL CHECK (is_active IN (0, 1)),
    updated_at_ms INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS payments (
    id TEXT PRIMARY KEY,
    subscription_id TEXT NULL REFERENCES subscriptions(id),
    name TEXT NOT NULL,
    amount_decimal TEXT NOT NULL,
    currency TEXT NOT NULL,
    due_at_ms INTEGER NOT NULL,
    status INTEGER NOT NULL,
    updated_at_ms INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS budgets (
    id TEXT PRIMARY KEY,
    service_id TEXT NULL REFERENCES services(id),
    category INTEGER NULL,
    name TEXT NOT NULL,
    amount_decimal TEXT NOT NULL,
    currency TEXT NOT NULL,
    warning_percent INTEGER NOT NULL,
    important_percent INTEGER NOT NULL,
    critical_percent INTEGER NOT NULL,
    updated_at_ms INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS provider_states (
    provider_id TEXT PRIMARY KEY,
    status INTEGER NOT NULL,
    last_attempt_at_ms INTEGER NULL,
    last_success_at_ms INTEGER NULL,
    next_refresh_at_ms INTEGER NULL,
    consecutive_failures INTEGER NOT NULL DEFAULT 0,
    error_code TEXT NULL,
    error_message TEXT NULL,
    updated_at_ms INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS refresh_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    provider_id TEXT NOT NULL,
    completed_at_ms INTEGER NOT NULL,
    observation_count INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS alerts (
    id TEXT PRIMARY KEY,
    service_id TEXT NULL REFERENCES services(id),
    severity INTEGER NOT NULL,
    rule_type TEXT NOT NULL,
    threshold_decimal TEXT NOT NULL,
    is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
    last_triggered_at_ms INTEGER NULL
);

CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at_ms INTEGER NOT NULL
);

