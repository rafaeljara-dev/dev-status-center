# Roadmap

## MVP 0 — base local

- [x] modelos universales;
- [x] provider contracts;
- [x] SQLite + migraciones;
- [x] scheduler dirigido por señales;
- [x] Normal / Eco / Paused / Gaming manual;
- [x] tray + popup;
- [x] MockProvider;
- [x] DPAPI secret store;
- [x] forecast inicial;
- [x] Quick Access jerárquico;
- [x] pruebas y CI;
- [ ] ejecutar y medir en Windows real;
- [ ] capturar screenshot y baseline de performance.

## MVP 1 — infraestructura real

1. Neon end-to-end.
2. Editor de cuentas/credenciales.
3. Drill-down por proyecto.
4. Vercel end-to-end.
5. Cloudflare end-to-end.
6. Alertas de presupuesto y toast nativo.

## MVP 2 — IA

- OpenAI usage/billing conforme a APIs vigentes;
- Anthropic usage/billing/cuotas disponibles;
- tokens input/output/cached;
- periodos today/week/billing;
- diferencia clara cuota vs dinero.

## MVP 3 — gastos fijos

- editor de subscriptions/payments/budgets;
- Google Cloud Billing;
- Google One manual/invoice;
- monedas originales + FX explícito;
- dominios y renovaciones.

## MVP 4 — recibos

- Gmail OAuth con scopes mínimos;
- parser local;
- confirmación antes de crear suscripción;
- reglas ignore/provider mapping;
- retención configurable.

## MVP 5 — inteligencia local

- Gaming Mode automático con allowlist de procesos;
- delay al salir de juego;
- anomalías estadísticas;
- forecast con moving average/weekday;
- históricos 7D/30D/3M/12M renderizados solo al abrir.

## Backlog controlado

- empaquetado MSIX y alternativa moderna a la clave Run;
- MSIX e identidad para toast;
- import/export cifrado;
- acciones compuestas de Quick Access;
- monitoreo opcional de dominios/deployments;
- backend multi-device solo después de validar necesidad.
