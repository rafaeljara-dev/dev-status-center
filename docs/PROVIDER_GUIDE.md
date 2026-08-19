# Guía para implementar providers

## Regla principal

Un provider traduce un contrato externo al modelo universal. No decide colores, layout, forecast global ni persistencia. DTOs y nombres del proveedor se quedan dentro de su proyecto.

## Estructura recomendada

```text
src/DevStatusCenter.Providers.Neon/
├── NeonProvider.cs
├── NeonClient.cs
├── NeonMapper.cs
├── NeonOptions.cs
├── Dtos/
└── README.md
```

## Pasos

1. Confirmar qué APIs oficiales existen hoy y documentar links/versiones.
2. Definir el scope mínimo de solo lectura.
3. Crear una referencia de credencial; guardar el secreto mediante `ISecretStore`.
4. Implementar cliente sobre `SharedHttpTransport` + `ResilientHttpExecutor`.
5. Mantener DTOs externos separados del dominio.
6. Mapear cuenta, recursos, métricas, periodo, moneda, fuente y precisión.
7. Construir `ProviderRefreshResult` completo en memoria.
8. Dejar que `ILocalStore` lo aplique atómicamente.
9. Agregar fixtures anonimizados para respuestas válidas, parciales e inválidas.
10. Verificar 401, 403, 429, timeout, 5xx y cancelación.

## Jerarquía de datos

| Prioridad | Entrada | `DataSourceKind` | Etiqueta esperada |
|---|---|---|---|
| 1 | Billing API oficial | `OfficialBillingApi` | Provider billed |
| 2 | Usage API + pricing versionado | `OfficialUsageApi` | Usage estimate |
| 3 | Factura/recibo | `Invoice` | Invoice |
| 4 | Configuración manual | `Manual` | Manual |

No mezclar una cifra reportada con otra calculada en un único `BillingRecord`. Si se muestran juntas, deben ser líneas independientes.

## Primer provider recomendado: Neon

Objetivo de la vertical:

- una o más cuentas;
- listado de proyectos;
- compute, storage y transferencia cuando la API lo permita;
- costo reportado o estimado por proyecto;
- total de cuenta;
- periodo y timestamps correctos;
- forecast local;
- estado offline y errores.

Antes de escribir código se debe verificar la documentación oficial vigente porque endpoints, scopes y disponibilidad por plan pueden cambiar.

## Vercel

Normalizar cuando estén disponibles:

- Fluid Compute;
- bandwidth/data transfer;
- edge requests;
- image optimization;
- functions;
- projects/teams.

No asumir que usage equivale a importe facturado. Si el endpoint solo devuelve unidades, el pricing model debe tener versión y fecha de vigencia.

## Cloudflare

Usar un API token restringido a las cuentas necesarias y permisos de lectura. Productos pueden incluir Workers, R2, D1, Images, Stream y Domains. Cada producto debe ser `UsageMetric`; el importe agregado puede ser `BillingRecord` si la API lo reporta.

## Checklist de PR

- [ ] No hay secreto, fixture real ni account ID sensible.
- [ ] Scope de token documentado.
- [ ] `HttpClient` no se crea por request.
- [ ] Todas las operaciones aceptan cancellation.
- [ ] No hay retries de 4xx salvo 429.
- [ ] `Retry-After` tiene prioridad.
- [ ] Bodies de error no se muestran sin sanitizar.
- [ ] Source/accuracy correctos.
- [ ] IDs estables e idempotentes.
- [ ] Tests de mapping y fallos.
- [ ] Datos anteriores permanecen ante error.

