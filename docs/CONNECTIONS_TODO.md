# Conexiones pendientes

Este archivo separa el código ya listo de la información que debe agregarse después en la laptop. No pegues tokens en este documento ni en `appsettings.example.json`.

## Flujo de configuración — ya implementado

1. Clic derecho en el icono del tray → **Providers & credentials…**.
2. Elegir el provider en la lista.
3. Pegar el token una sola vez en el campo de contraseña.
4. La UI llama `ISecretStore.SetAsync(credentialReference, token)`; `DpapiSecretStore` lo cifra
   con DPAPI bajo la cuenta de Windows actual y lo escribe en `secretsPath`.
5. `appsettings.json` guarda únicamente `credentialReference`. Ni el archivo de configuración ni
   SQLite ven nunca el token.
6. El provider recupera el secreto justo antes de la petición.

El nombre del archivo del secreto es un hash de la referencia, así que ni siquiera el listado del
directorio revela qué providers están configurados. La entropía de DPAPI va ligada a esa misma
referencia: copiar el archivo de un provider sobre el de otro no permite descifrarlo.

Un provider real sólo entra al ciclo de refresh si está habilitado **y** su credencial ya existe.
Habilitarlo sin token dejaría al scheduler golpeando la API con 401 en cada ciclo.

Los cambios de provider se aplican al reiniciar la aplicación.

## Matriz

| Provider | Referencia sugerida | Información por definir | Estado |
|---|---|---|---|
| Neon | `neon-personal` | API key de Neon; Account ID = `org_id` (se descubre solo si se deja vacío) | **Implementado, sin verificar contra la API real** |
| Vercel | `vercel-personal` | token, team ID opcional | Pendiente |
| Cloudflare | `cloudflare-personal` | API token read-only, account ID | Pendiente |
| OpenAI | `openai-personal` | admin/usage credential compatible con endpoints vigentes | Futuro |
| Anthropic | `anthropic-personal` | usage/billing access vigente | Futuro |

## Neon: qué falta para darlo por bueno

El provider está escrito contra la referencia pública de la Neon API v2 (19-ago-2026) y sus
pruebas fijan parsing, conversión de unidades, cálculo de costo y clasificación de errores con
fixtures. Lo que **no** pueden probar es que Neon devuelva exactamente esa forma. Con un token
real hay que confirmar, en este orden:

1. `GET /users/me/organizations` devuelve al menos una organización y su `plan`.
2. `GET /projects?org_id=…` lista los proyectos esperados y la paginación termina.
3. `GET /consumption_history/v2/projects` acepta los ocho `metrics` que se piden y devuelve
   `periods[].consumption[].metrics[]` con esos nombres.
4. Los nombres de métrica coinciden literalmente con
   `NeonBillableUnits.RequestedMetrics`. Si Neon renombra uno, esa métrica se leerá como cero
   **en silencio**: es el fallo más probable y el más difícil de notar.
5. El importe calculado se parece al del panel de Neon. Si no, revisa el plan y la tarifa: el
   costo es un cálculo nuestro, no una factura.

Permisos: la API key de Neon no tiene scopes granulares hoy, así que da acceso de escritura.
Crea una key dedicada a esta aplicación para poder revocarla sin afectar nada más.

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

