# Performance y energía

## Presupuesto

| Estado | CPU | Red | Disco | GPU |
|---|---:|---:|---:|---:|
| Normal idle | ≈ 0% | 0 entre refresh | 0 entre refresh | 0 popup cerrado |
| Eco idle | ≈ 0% | 0 entre refresh largos | 0 entre refresh | 0 popup cerrado |
| Paused | ≈ 0% | 0 | 0 | 0 popup cerrado |
| Gaming | ≈ 0% | 0 | 0 | 0 popup cerrado |
| Popup abierto | bajo, por eventos | 0 salvo refresh manual | lecturas locales | composición WPF mínima |

RAM depende de runtime, arquitectura y publicación. Se medirá antes de fijar un límite duro; el objetivo inicial de working set es menor a 80 MB framework-dependent y se optimizará con datos reales.

## Decisiones que protegen el presupuesto

- WPF, no Electron/WebView;
- SQLite directo, no EF change tracker;
- composition root manual, no Generic Host;
- `HttpClient` y `SocketsHttpHandler` reutilizados;
- no timers por segundo;
- no animaciones;
- no gráficos cuando popup está cerrado;
- Channel + delay hasta el siguiente deadline;
- concurrencia acotada;
- caché local antes de red;
- Quick Access sin watchers.

## Medición ya realizada

### Lectura del dashboard (19-ago-2026)

La consulta que resolvía el último valor por métrica usaba una función de ventana sobre el
histórico completo. Reproducida con el volumen que genera `MockProvider` (14 filas de usage +
5 de billing cada 15 minutos), mediana de 25 ejecuciones con caché de páginas caliente:

| Histórico | Filas | Ventana sobre histórico | Proyección vigente | Factor |
|---|---:|---:|---:|---:|
| 7 días | 10.080 | 30,6 ms | 0,33 ms | 93× |
| 30 días | 43.200 | **276,9 ms** | 0,18 ms | 1.562× |
| 90 días | 129.600 | 879,9 ms | 0,31 ms | 2.825× |
| 365 días | 525.600 | 1.377,8 ms | 0,06 ms | 21.528× |

NFR-007 pide menos de 250 ms. La versión anterior lo rompía dentro del primer mes sólo por
dejar la aplicación encendida. Ver [ADR 0005](decisions/0005-current-state-projection.md).

### Consumo en reposo (19-ago-2026)

Build publicada, modo Normal, popup cerrado, muestra de 60 s tras el primer refresh
(`scripts/measure-idle.ps1 -SampleSeconds 60`):

| Métrica | Medido |
|---|---:|
| CPU promedio | **0,0 %** |
| Working set | 87,4 MB |
| Memoria privada | 29,8 MB |
| Hilos | 14 |
| Handles | 483 |

NFR-001 se cumple: la CPU en reposo es indistinguible de cero. El working set queda por encima
del objetivo de 80 MB que este documento fijó **antes** de tener datos; ese número se escribió a
ojo. Lo dominan las imágenes mapeadas de WPF y del runtime compartido, que ninguna opción de GC
mueve. La cifra que refleja el costo real de la aplicación es la memoria privada: ~30 MB.

**Resultado negativo, anotado para no repetirlo:** se probó
`ConcurrentGarbageCollection=false` + `ServerGarbageCollection=false` + `TieredPGO`. Diferencia
medida: 87,6 → 87,4 MB de working set y 30,05 → 29,83 MB de privada, con el mismo número de
hilos. Está dentro del ruido, así que se revirtió en lugar de dejar configuración sin
justificación. `InvariantGlobalization` sí ahorraría memoria pero rompería el formato de moneda y
fecha según la cultura del usuario: descartado.

### Tamaño del ejecutable

El publicado pesaba **27 MB**, de los cuales 24,9 MB eran `Microsoft.Windows.SDK.NET.dll`: las
proyecciones WinRT que arrastra el sufijo `10.0.19041.0` del TFM, en una aplicación que no llama a
una sola API WinRT. Con `net10.0-windows` a secas:

| | Antes | Ahora |
|---|---:|---:|
| `DevStatusCenter.exe` | 26,33 MB | **0,87 MB** |
| Total publicado | 27,0 MB | **2,76 MB** |

## Medición pendiente

1. Compilar Release y ejecutar la build publicada.
2. Esperar a que termine un refresh.
3. Activar Paused y cerrar popup.
4. Ejecutar:

```powershell
./scripts/measure-idle.ps1 -SampleSeconds 120
```

5. Repetir en Normal entre intervalos.
6. Registrar CPU promedio, working set, private memory y threads.

Para red usar Resource Monitor o Windows Performance Recorder. La aceptación de Paused/Gaming es cero conexiones iniciadas por la app durante la muestra.

## Benchmarks a agregar

- tiempo cold start hasta icono visible;
- tiempo clic → popup con caché de 10/100/1000 servicios;
- consulta latest snapshot con 12 meses de histórico;
- tiempo de commit de un refresh con 1000 métricas;
- memoria después de 24 horas;
- cancelación Paused durante request lento.

## Reglas de revisión

Todo nuevo timer debe declarar intervalo mínimo y razón. Toda dependencia debe justificar impacto de startup/memoria. Toda visualización debe dejar de renderizar cuando se oculta la ventana.
