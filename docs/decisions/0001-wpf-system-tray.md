# ADR 0001: WPF y System Tray

- Estado: aceptado
- Fecha: 2026-08-19

## Contexto

Se necesita integración profunda con Windows, popup rápido, background suspendible y bajo consumo. Quick Settings no ofrece una API pública general para controles arbitrarios.

## Decisión

Usar C#/.NET 10, WPF y `System.Windows.Forms.NotifyIcon` sobre el área de notificaciones soportada por Windows.

## Consecuencias

- UI y Win32 directos, sin runtime web;
- target Windows explícito;
- el tray usa WinForms solo para `NotifyIcon`/menú;
- no se modifica Explorer ni Quick Settings;
- WPF permanece reemplazable porque Application/Domain no dependen de UI.

