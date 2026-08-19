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

## Medición

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
