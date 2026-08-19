CREATE INDEX IF NOT EXISTS ix_services_provider ON services(provider_id, provider_account_id);
CREATE INDEX IF NOT EXISTS ix_usage_latest ON usage_snapshots(service_id, metric_code, captured_at_ms DESC);
CREATE INDEX IF NOT EXISTS ix_billing_latest ON billing_records(service_id, currency, captured_at_ms DESC);
CREATE INDEX IF NOT EXISTS ix_billing_period ON billing_records(period_start_ms, period_end_ms);
CREATE INDEX IF NOT EXISTS ix_subscriptions_renewal ON subscriptions(is_active, next_renewal_at_ms);
CREATE INDEX IF NOT EXISTS ix_payments_due ON payments(status, due_at_ms);
CREATE INDEX IF NOT EXISTS ix_refresh_history_provider ON refresh_history(provider_id, completed_at_ms DESC);

