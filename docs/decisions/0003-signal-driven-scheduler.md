# ADR 0003: Scheduler dirigido por señales

- Estado: aceptado
- Fecha: 2026-08-19

## Contexto

Billing cambia lentamente y los providers tienen frecuencias/rate limits diferentes. Un timer global frecuente contradice el objetivo energético.

## Decisión

Usar un `Channel` de comandos, calcular el deadline más cercano y dormir hasta comando o vencimiento. Un CTS por ciclo permite pausa inmediata.

## Consecuencias

- cero polling activo;
- reanudación y refresh manual despiertan el loop;
- concurrencia y backoff centralizados;
- el scheduler es más elaborado que varios `DispatcherTimer`, pero su estado es explícito y testeable.

