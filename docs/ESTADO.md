# Estado y continuación

Fotografía del proyecto al **19-ago-2026**, escrita para retomarlo sin contexto previo.
Si algo aquí contradice al código, gana el código.

---

## 1. Qué es y dónde vive

| | |
|---|---|
| Código | `C:\Users\rafael\dev\PersonalProjects\system-bar` |
| Repo | `rafaeljara-dev/dev-status-center` (privado) |
| Toolchain | .NET 10 SDK **10.0.400** en `C:\Program Files\dotnet` |
| Ejecutable | `artifacts/win-x64/DevStatusCenter.exe` — 0,88 MB (ignorado por git) |
| Lienzo de diseño | https://claude.ai/code/artifact/20244647-e138-45d1-9e62-f888166c0a02 |

> `dotnet` no está en el PATH de terminales abiertas antes de la instalación. Si no lo encuentra,
> abre una terminal nueva o usa la ruta completa.

**Validar cualquier cambio:**

```powershell
./scripts/verify.ps1
```

Hace restore con `--locked-mode`, build sin warnings, 71 pruebas, publish y **auto-test que
renderiza el popup de verdad**. Si tocas XAML, ese auto-test es el que caza bindings rotos.

---

## 2. Estado actual

- Compila con **0 warnings** bajo `TreatWarningsAsErrors`.
- **71/71 pruebas** verdes.
- CI verde en GitHub Actions.
- **0,0 % de CPU en reposo** medido sobre 60 s; 29,8 MB de memoria privada; 14 hilos.
- La app **corre de verdad**: arranca, refresca, muestra el popup y sale limpiamente.

Alcance implementado más allá del MVP 0 original: configuración real, DPAPI cableado con su
ventana, provider de **Neon**, motor de **alertas** con enfriamiento, `crash.log` y `--selftest`.

---

## 3. Defectos encontrados y corregidos

Ordenados por gravedad. Los tres primeros solo aparecieron al **ejecutar** la app; ninguna prueba
unitaria los habría encontrado.

| # | Defecto | Consecuencia |
|---|---|---|
| 1 | `App.Dispose` bloqueaba el hilo de UI mientras `RefreshScheduler.DisposeAsync` reanudaba en ese mismo dispatcher | **"Exit" del tray colgaba el proceso para siempre**: invisible, 0 % CPU, y reteniendo el mutex de instancia única, así que no se podía reiniciar |
| 2 | `ProgressBar.Value` enlaza TwoWay por defecto contra `BudgetPercent`, que es de solo lectura | El popup **reventaba la app en cada arranque** |
| 3 | TFM `net10.0-windows10.0.19041.0` arrastraba `Microsoft.Windows.SDK.NET.dll` | El .exe pesaba **27 MB**; 24,9 MB eran proyecciones WinRT que la app nunca usa → ahora 2,77 MB |
| 4 | `Microsoft.Data.Sqlite 10.0.8` → `SQLitePCLRaw` con GHSA-2m69-gcr7-jv3q | Vulnerabilidad **alta**. Bump a 10.0.11 |
| 5 | Consulta del dashboard con función de ventana sobre todo el histórico | **277 ms al primer mes** (el objetivo son 250), 1,4 s al año. Ahora constante en ~0,2 ms |
| 6 | `synchronous` es pragma **por conexión** y solo se aplicaba a la de migración | Todas las demás corrían en `FULL`: un fsync por commit |
| 7 | Dos semáforos de escritura distintos contra el mismo archivo SQLite | Un guardado de settings podía chocar con un refresh |
| 8 | Quick Access abría el IDE en vez de la carpeta | Dos causas: el editor proponía `Editor` por defecto, y `UseShellExecute = true` delegaba en el handler de la clase Directory, que Visual Studio se apropia |

Detalle completo en los mensajes de commit y en `docs/PERFORMANCE.md`.

---

## 4. Decisiones de diseño tomadas

Ver el lienzo (página **Propuesta**). Resumen de lo acordado:

- **Estructura del popup**: encabezado de "consola" (línea de estado monoespaciada, cifra grande
  con `tabular-nums`, medidor de bloques) sobre un cuerpo de pestañas con aire.
- **Pestañas**: `[carita] · IA · CLOUD · PAGOS`, con teclas 1-4.
- **Memoria de pestaña**: guardar la última vista en `app_settings` (clave `ui.lastTab`), igual que
  hoy se guarda el modo de energía. **Excepción acordada:** si hay una alerta activa, abrir en la
  pestaña de esa alerta y no en la recordada.
- **La carita NO es indicador de estado.** Es una mascota: parpadea y de vez en cuando saca una
  laptop, un celular, un café — gags cómicos **sin relación con las acciones ni con el estado**.
  El estado lo siguen cargando el icono del tray, la línea mono y el medidor.
- **Logos de marca en monocromo.** La silueta ya identifica; cinco colores de marca competirían
  con los colores de umbral, que sí significan algo. El color de marca se reserva para cuando ese
  servicio es el que disparó una alerta.
- **Colores de estado sin cambios**: se conservan los del código (70/85/95 %, pausa, juego).
- **Tipografía**: pendiente de decisión. Los mockups usan Space Grotesk + JetBrains Mono; empotrar
  ambas suma ~150-250 KB al .exe. La alternativa sin costo es Segoe UI Variable + Cascadia Mono,
  que ya vienen en Windows 11.

### Cristal (Acrylic)

El "liquid glass" de la web (`backdrop-filter` + `feDisplacementMap`) no existe en WPF y meter un
WebView contradiría el proyecto. Pero Windows lo trae de fábrica:

- **Mica** (`DWMSBT_MAINWINDOW = 2`) muestrea el wallpaper una vez. No sirve: el popup aparece
  sobre lo que sea que esté abierto.
- **Acrylic** (`DWMSBT_TRANSIENTWINDOW = 3`) desenfoca en tiempo real. Microsoft dice que se use
  **solo en superficies transitorias que se cierran al perder el foco**. El popup ya se oculta en
  `Window_Deactivated`: es exactamente ese caso.

```csharp
DwmSetWindowAttribute(hwnd, 33, ref round,    4);  // CORNER_PREFERENCE = ROUND
DwmSetWindowAttribute(hwnd, 38, ref acrylic,  4);  // SYSTEMBACKDROP    = ACRYLIC
```

**Estorbo conocido:** `DashboardWindow.xaml` lleva `AllowsTransparency="True"` para redondear
esquinas, y esa bandera es **incompatible** con los backdrops del DWM. Hay que quitarla y pedirle
las esquinas a Windows. Además hace falta
`HwndSource.CompositionTarget.BackgroundColor = Colors.Transparent` para que WPF no pinte un fondo
opaco encima. En Windows 10 la llamada falla sin ruido → fondo sólido, sin código extra.

Riesgo real sin verificar: legibilidad del texto sobre una ventana **clara** detrás, y que quitar
`AllowsTransparency` no rompa el posicionamiento junto al tray.

---

## 5. EN CURSO — rediseño de la UI

Trabajo empezado, **no terminado**. Lo hecho:

- `src/DevStatusCenter.Desktop/Branding/BrandGlyphs.cs` — **listo y compilando**. Trazados de
  OpenAI, Anthropic/Claude, Vercel, Neon y Cloudflare como `Geometry` congelada, resueltos por
  `providerId` y con respaldo por `externalId`. Todos los comandos que usan (`M L H V C A Z`) los
  soporta `Geometry.Parse`.
- `design/` — los artboards `.dc.html`, `canvas.json`, `gen.py` y `logos.json` que generan el
  lienzo. Versionados para poder regenerarlo.

Lo que **falta**:

1. `Controls/MascotFace` — carita con parpadeo y gags. Storyboards de XAML, **no** `DispatcherTimer`:
   deben detenerse al ocultarse el popup (NFR-004) y respetar "reducir movimiento" del sistema.
2. `Infrastructure/Windows/WindowBackdrop.cs` — Acrylic + esquinas nativas, con degradación en
   Windows 10 y un interruptor en el menú del tray por si estorba.
3. `DashboardViewModel` — `SelectedTab` persistido, `TopSpend`, bloques del medidor, partes de la
   línea de estado (modo, sync, nº de servicios, nº de alertas).
4. `DashboardServiceRow` necesita **`ProviderId` y `ExternalId`** para poder resolver la marca; hoy
   no los expone.
5. `DashboardWindow.xaml` — reescritura completa según la propuesta.
6. `App.xaml` — tokens nuevos (familia mono, opacidades del cristal).

---

## 6. Investigación: límites de Claude Code y Codex (locales)

**Hallazgo central: ambos escriben datos de uso en disco, así que un provider local no necesita
ninguna credencial.** Encaja perfecto con la tesis privacy-first del proyecto.

### Codex — el límite completo, exacto

`~/.codex/sessions/**/rollout-*.jsonl` contiene, repetido a lo largo de la sesión:

```json
"rate_limits": {
  "limit_id": "codex",
  "primary":   { "used_percent": 12.0, "window_minutes": 10080, "resets_at": 1786169514 },
  "secondary": null,
  "credits":   { "has_credits": false, "unlimited": false, "balance": "0" },
  "plan_type": "plus",
  "rate_limit_reached_type": null
}
```

- `window_minutes: 10080` = 7 días (límite semanal). `resets_at` es epoch en segundos.
- La **última ocurrencia del archivo de sesión más reciente** es el estado vigente.
- Esto es `MetricKind.QuotaConsumed` con `DataSourceKind.OfficialUsageApi` /
  `DataAccuracy.ProviderReported`: es el dato del proveedor, no un cálculo nuestro.

### Claude Code — el consumo sí, el porcentaje del límite no

Cada mensaje en `~/.claude/projects/**/*.jsonl` trae:

```
input_tokens · output_tokens · cache_creation_input_tokens · cache_read_input_tokens
output_tokens_details (incluye thinking_tokens) · server_tool_use · service_tier
```

**No hay ningún campo de rate limit ni de ventana de 5 h / semanal.** Ese porcentaje lo entrega la
API en cabeceras de respuesta y solo lo muestra `/usage` en vivo; no se persiste.

Existe además `~/.claude/stats-cache.json` con agregados por modelo (`inputTokens`,
`outputTokens`, `cacheReadInputTokens`, `cacheCreationInputTokens`, `costUSD`) más `dailyActivity`
y `dailyModelTokens`. **Ojo: puede estar rancio** — se recalcula bajo demanda, no de forma
continua (en esta máquina su `lastComputedDate` iba dos meses atrasado).

**Conclusión de diseño:** para Claude Code se muestra **consumo calculado por nosotros** desde los
transcripts (`DataAccuracy.Calculated`), no un porcentaje de límite que no tenemos. Presentarlo
como "% del límite" sería inventar precisión — justo lo que prohíbe el §72 del brief.

### Restricción de rendimiento, importante

En esta máquina: **706 archivos, 491 MB** de transcripts de Claude Code. Reparsear eso en cada
refresh es inviable. El provider tiene que ser **incremental**: guardar por archivo su tamaño y el
offset ya leído, y en cada ciclo leer únicamente lo añadido. Los `.jsonl` son append-only, así que
funciona. Conviene una tabla nueva para esos offsets.

---

## 7. Siguientes pasos, en orden

1. **Terminar el rediseño de la UI** (sección 5). Al acabar: `./scripts/verify.ps1` y mirar el
   popup de verdad — el auto-test valida bindings, no estética.
2. **Provider local `codex`**: leer `rate_limits` de la sesión más reciente. Es el más barato y da
   un dato exacto sin credenciales.
3. **Provider local `claude-code`**: parseo incremental de transcripts → tokens por modelo y por
   día. Requiere la tabla de offsets.
4. **Conectar Neon con token real** y correr las 5 comprobaciones de `docs/CONNECTIONS_TODO.md`.
   El fallo más probable y más difícil de notar: si Neon renombró una métrica, se lee como **cero
   en silencio**.
5. Pulir con datos reales ya en pantalla.
6. Después: Vercel, Cloudflare, OpenAI/Anthropic (los de pago, que sí necesitan credencial).

---

## 8. Lo que sigue sin existir

Vercel, Cloudflare, OpenAI/Anthropic como providers · drill-down por proyecto · gráficas
históricas · Gaming Mode automático por proceso · conversión de monedas (hoy cada consulta filtra
por moneda, así que **una suscripción en MXN simplemente no aparece** en un dashboard en USD, sin
avisar) · parser de recibos de correo · precios de Neon configurables desde `appsettings.json`.

Todo está en `docs/ROADMAP.md` como MVP 2-5.
