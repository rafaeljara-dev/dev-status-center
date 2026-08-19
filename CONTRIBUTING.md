# Contribuir

## Antes de código

1. Ubica el requisito `FR/NFR` afectado.
2. Si cambia una decisión estructural, crea o actualiza un ADR.
3. Mantén provider DTOs fuera de Domain/Application.
4. No agregues secretos ni responses sin anonimizar.

## Validación

```powershell
./scripts/verify.ps1
```

Warnings son errores. Agrega pruebas para dominio, forecast, mappings y migraciones. Cambios de UI deben probarse a 100%, 125%, 150% y 200% DPI.

## Commits y PRs

- commits cortos y orientados a una decisión;
- PR describe impacto funcional, energía, seguridad y validación;
- una integración real por PR vertical;
- no mezclar refactor global con provider nuevo.

## Dependencias

Antes de agregar un paquete documenta:

- función que no cubre BCL/código existente;
- tamaño y dependencias transitivas;
- impacto en startup/AOT/trimming;
- mantenimiento y licencia;
- alternativa rechazada.

