# Conexiones pendientes

Este archivo separa el código ya listo de la información que debe agregarse después en la laptop. No pegues tokens en este documento ni en `appsettings.example.json`.

## Flujo de configuración futuro

1. Abrir Settings → Providers.
2. Elegir provider y cuenta.
3. Ingresar token una sola vez.
4. La UI llama `ISecretStore.SetAsync("provider-account", token)`.
5. SQLite guarda únicamente `credential_reference`.
6. El provider recupera el secreto justo antes del request.

## Matriz

| Provider | Referencia sugerida | Información por definir | Estado |
|---|---|---|---|
| Neon | `neon-personal` | API token read-only, account/org ID si aplica | Pendiente |
| Vercel | `vercel-personal` | token, team ID opcional | Pendiente |
| Cloudflare | `cloudflare-personal` | API token read-only, account ID | Pendiente |
| OpenAI | `openai-personal` | admin/usage credential compatible con endpoints vigentes | Futuro |
| Anthropic | `anthropic-personal` | usage/billing access vigente | Futuro |

## Configuración versionable

Solo referencias:

```json
{
  "providers": {
    "neon": {
      "enabled": true,
      "credentialReference": "neon-personal"
    }
  }
}
```

## Variables de desarrollo opcionales

Si se agregan variables temporales para pruebas locales, deben terminar en un archivo ignorado o en User Secrets. Convención propuesta:

```text
DSC_NEON_TOKEN
DSC_NEON_ACCOUNT_ID
DSC_VERCEL_TOKEN
DSC_VERCEL_TEAM_ID
DSC_CLOUDFLARE_TOKEN
DSC_CLOUDFLARE_ACCOUNT_ID
```

El ejecutable final no debe depender de estas variables; son solo bootstrap de desarrollo para migrarlas a DPAPI.

## No hacer

- no poner tokens en `config.json`;
- no usar Global API Keys si existe token con scopes;
- no pedir write/delete/admin;
- no imprimir request headers;
- no commitear responses reales sin anonimizar;
- no usar scraping de dashboard como si fuera una API estable.

