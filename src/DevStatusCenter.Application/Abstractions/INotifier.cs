using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Abstractions;

/// <summary>
/// Entrega una alerta al usuario. Deliberadamente síncrona y sin resultado: notificar no puede
/// fallar de una forma que le importe a quien evalúa las reglas, y no debe poder bloquear el
/// pipeline de refresh.
/// </summary>
public interface INotifier
{
    void Notify(Alert alert);
}
