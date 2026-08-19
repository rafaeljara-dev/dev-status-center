using System.Diagnostics;
using System.Windows;

namespace DevStatusCenter.Desktop.Diagnostics;

/// <summary>
/// Escucha los errores de binding de WPF.
///
/// La mayoría de los bindings rotos no lanzan: WPF los escribe en una traza y sigue, dejando la
/// UI con huecos en silencio. Durante el auto-test se convierten en un fallo explícito, que es la
/// única forma de que una regresión de XAML se note sin abrir la ventana a ojo.
/// </summary>
internal sealed class BindingErrorListener : TraceListener
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors => _errors;

    public static BindingErrorListener Attach()
    {
        var listener = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        return listener;
    }

    public override void Write(string? message) => Record(message);

    public override void WriteLine(string? message) => Record(message);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        }

        base.Dispose(disposing);
    }

    private void Record(string? message)
    {
        // WPF escribe la cabecera y el detalle por separado; sólo interesan las líneas con cuerpo.
        if (!string.IsNullOrWhiteSpace(message) && message.Contains("Error", StringComparison.Ordinal))
        {
            _errors.Add(message.Trim());
        }
    }
}
