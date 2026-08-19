# ADR 0002: SQLite directo sin Entity Framework

- Estado: aceptado
- Fecha: 2026-08-19

## Contexto

La aplicación tiene consultas controladas, snapshots append-only y un objetivo estricto de startup/memoria. No necesita tracking de entidades ni LINQ complejo.

## Decisión

Usar `Microsoft.Data.Sqlite` directamente, migraciones SQL embebidas y mappers explícitos.

## Consecuencias

- menos objetos y dependencias en memoria;
- SQL e índices son visibles y medibles;
- más código de mapping manual;
- cambios de schema requieren migración y prueba explícitas;
- los importes se guardan como texto decimal invariant.

