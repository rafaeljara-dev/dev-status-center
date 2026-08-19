# Contexto del proyecto

## Problema

El gasto y los límites de un entorno personal de desarrollo están fragmentados entre dashboards de IA, cloud, bases de datos, dominios y suscripciones. Consultarlos exige abrir varios sitios y recordar renovaciones. Dev Status Center concentra el estado operativo que sí merece estar disponible inmediatamente desde el tray de Windows.

## Resultado esperado

Después de iniciar Windows, un clic debe responder en menos de un segundo:

- cuánto se ha gastado en el periodo;
- cuánto se proyecta gastar;
- qué porcentaje del presupuesto se consumió;
- qué cuota de IA está más cerca del límite;
- qué servicio cuesta más;
- cuál es el siguiente pago;
- cuándo se sincronizó por última vez;
- qué provider necesita autenticación;
- cómo abrir rápidamente un proyecto frecuente.

## Identidad del producto

No es un dashboard empresarial, un gestor financiero general, un explorador de archivos ni una aplicación web empaquetada. Es una extensión compacta de Windows con una base local confiable.

La interacción ideal es:

```mermaid
flowchart LR
    Click["Clic en tray"] --> Cache["Lee caché local"] --> Answer["Entiende estado"] --> Close["Cierra popup"]
```

## Alcance de MVP 0

MVP 0 valida arquitectura y comportamiento energético con datos mock. Incluye el pipeline entero, pero ninguna credencial real. Esto permite cambiar modelos y UX sin estar acoplados a contratos externos todavía.

Incluido:

- dominio universal;
- provider engine;
- SQLite e históricos;
- scheduler y modos de energía;
- forecast inicial;
- tray y popup;
- MockProvider;
- secret store DPAPI;
- Quick Access local;
- pruebas y documentación.

No incluido todavía:

- providers reales;
- editor completo de presupuestos y suscripciones;
- conversión automática de monedas;
- notificaciones toast con identidad MSIX;
- Gaming Mode automático por procesos;
- parser de recibos;
- anomaly detection;
- gráficas históricas;
- backend o sincronización entre dispositivos.

## Pregunta de control de scope

Antes de agregar una función se debe responder:

> ¿Esta información o acción necesita estar disponible inmediatamente desde el tray?

Si no, debe vivir fuera del núcleo o esperar. Quick Access sí pasa esta prueba porque reduce navegación repetitiva y no agrega trabajo background.

## Decisiones ya cerradas

| Área | Decisión |
|---|---|
| Plataforma | Windows |
| Runtime | .NET 10 |
| UI | WPF |
| Integración | System Tray, no hacks de Explorer/Quick Settings |
| Persistencia | SQLite directo |
| Backend | Ninguno |
| Red | `HttpClient` reutilizado |
| Secretos | DPAPI `CurrentUser` y referencias lógicas |
| Arquitectura | Providers desacoplados y modelo normalizado |
| Monedas | Originales; sin sumar monedas no convertidas |
| Estado offline | Mostrar último snapshot y su antigüedad |
| Power | Normal / Eco / Paused / Gaming |

