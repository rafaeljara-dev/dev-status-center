# CLAUDE.md — Dev Status Center

Notas para trabajar en este repo. Lo que ya está explicado en el código no se repite aquí; esto
son decisiones y pendientes que el código no puede contar por sí solo.

**Empieza por [docs/ESTADO.md](docs/ESTADO.md).**

## Ciclo de trabajo

```powershell
./scripts/run.ps1            # compila y muestra en la terminal lo que mostraría el popup
./scripts/run.ps1 -Window    # abre la ventana de verdad
./scripts/verify.ps1         # restore bloqueado + build sin warnings + pruebas + publish + auto-test
./scripts/install.ps1        # copia al perfil del usuario y arranca
```

`run.ps1` usa la misma base de datos y las mismas credenciales que la versión instalada, así que
lo que sale en la terminal es lo que va a salir en la ventana. Para iterar, `run.ps1`; para
entregar, `verify.ps1` y luego `install.ps1`.

## Pendientes anotados

### Neon: desglose por proyecto

Hoy Neon se presenta como **una sola fila con todo sumado**. Con 18 proyectos, desglosar convierte
el popup en una lista que hay que leer entera para saber lo único que se pregunta de un vistazo:
cuánto va este mes.

El dato por proyecto **sí se pide y sí llega** — la agregación ocurre en `NeonProvider`, no en la
API — así que el desglose es cuestión de decidir dónde ponerlo, no de traer nada nuevo. La idea
sería un drill-down al hacer clic en la fila de Neon, no una lista siempre visible.

Cuidado al hacerlo: la franquicia de 500 GB de salida pública es **por proyecto**, y por eso se
aplica antes de sumar (`WithFreeTransferApplied`). Si algún día se suma primero, dieciocho
franquicias se vuelven una y aparece un cargo de red que Neon no cobra.

### Neon: calibrar las tarifas contra la factura

Neon expone **consumo, no importes**: su API nunca devuelve dólares. El costo que muestra el
dashboard es un cálculo nuestro y se marca como `DataAccuracy.Calculated`.

Contraste del 19-ago-2026 contra la factura real:

| | Factura | Calculado |
|---|---|---|
| Compute 267,45 CU-h | 28,19 | 28,35 |
| Storage root + child | 0,11 | 0,11 |
| **Total** | **28,30** | **28,46** |

Un 0,6 % alto. La tarifa de compute que implica la factura es **~0,1054 USD/CU-hora**, no los
0,106 de lista que están escritos. Falta exponer las tarifas en `appsettings.json` para poder
calibrarlas sin recompilar.

### Servicios huérfanos en la base

Si un provider deja de reportar un servicio — se apagó el provider, o cambió la forma de
agregarlos, como pasó al unir Neon en una fila — las filas viejas **se quedan en la base y siguen
sumando**. Hoy hay que borrarlas a mano. Falta que un refresh exitoso retire los servicios de ese
provider que no vinieron en el resultado.

## Reglas que ya se rompieron una vez

- **Neon, rango de fechas**: con `granularity=monthly` la API trunca `from` y `to` al inicio de su
  mes. Recortar `to` a "ahora" hacía que ambos cayeran en el mismo instante y respondía 400. El
  `to` es siempre el fin del periodo; hay una prueba que lo fija.
- **Logos**: SVG rellena con `nonzero` y el mini lenguaje de trazados de WPF usa `evenodd`. Todo
  trazado de marca necesita el prefijo `F1`.
- **Secretos**: nunca en el repo, nunca en el JSON, nunca en un commit. Van al almacén DPAPI por
  la ventana de *Providers & credentials*.
