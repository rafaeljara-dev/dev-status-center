using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Desktop.Diagnostics;

/// <summary>
/// Imprime en la terminal lo mismo que muestra el popup.
///
/// Existe para cerrar el ciclo de trabajo: cambiar algo, verlo con datos reales y decidir, sin
/// publicar ni instalar. La aplicacion es un <c>WinExe</c> y por tanto arranca sin consola, asi
/// que hay que engancharse a la del proceso que la lanzo — de ahi el <c>AttachConsole</c>.
///
/// Lee del mismo <see cref="DashboardSnapshot"/> que la ventana. Si aqui sale un numero, es el
/// numero que va a salir en la ventana: no es una vista paralela que pueda desviarse.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class ConsoleReport
{
    private const int AttachParentProcess = -1;

    private const string Reset = "[0m";
    private const string Dim = "[2m";
    private const string Bold = "[1m";
    private const string Green = "[32m";
    private const string Yellow = "[33m";
    private const string Red = "[31m";
    private const string Cyan = "[36m";

    /// <summary>
    /// Se cuelga de la consola que lanzo el proceso; si no habia ninguna (doble clic en el .exe),
    /// abre una propia para que el informe no se pierda en el vacio.
    /// </summary>
    public static void Attach()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            AllocConsole();
        }

        Console.OutputEncoding = Encoding.UTF8;
    }

    public static void Write(DashboardSnapshot snapshot, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var writer = Console.Out;
        writer.WriteLine();
        writer.WriteLine($"{Bold}DEV STATUS CENTER{Reset}  {Dim}{DateTimeOffset.Now:yyyy-MM-dd HH:mm}{Reset}");
        writer.WriteLine(new string('─', 64));

        var sync = snapshot.LastSuccessfulSync is { } stamp
            ? stamp.ToLocalTime().ToString("HH:mm", culture)
            : "nunca";
        var down = snapshot.Health.Count(x => x.IsDisrupted);
        writer.WriteLine(
            $"{Dim}sync {sync} · {snapshot.Services.Count} servicios · " +
            $"{(snapshot.IsStale ? "cache vieja" : "cache al dia")}{Reset}" +
            (down > 0 ? $"  {Red}{down} caidos{Reset}" : string.Empty));
        writer.WriteLine();

        var budget = snapshot.BudgetPercent ?? 0m;
        writer.WriteLine(
            $"  {Bold}{snapshot.Currency} {snapshot.CurrentSpend.Amount,10:N2}{Reset}" +
            $"   proyectado {Colour(budget)}{snapshot.ProjectedSpend.Amount:N2}{Reset}");
        if (snapshot.MonthlyBudget is { } limit)
        {
            writer.WriteLine(
                $"  {Dim}{Bar(budget)} {budget:0.#}% de {limit.Amount:N2}{Reset}");
        }

        Section(writer, "SERVICIOS");
        foreach (var service in snapshot.Services.OrderByDescending(x => x.Current.Amount))
        {
            var value = service.TracksCost
                ? $"{service.Current.Amount,9:N2}"
                : $"{Quota(service),8:0.#}%";
            writer.WriteLine(
                $"  {service.Name,-22} {value}  {Dim}{service.Source}/{service.Accuracy}{Reset}");
        }

        if (snapshot.UpcomingPayments.Count > 0)
        {
            Section(writer, "PROXIMOS CARGOS");
            foreach (var payment in snapshot.UpcomingPayments.Take(5))
            {
                writer.WriteLine(
                    $"  {payment.DueAt.ToLocalTime():MMM dd}  {payment.Name,-24} {payment.Amount.Amount,9:N2}");
            }
        }

        if (snapshot.Health.Count > 0)
        {
            Section(writer, "ESTADO DE TERCEROS");
            foreach (var service in snapshot.Health)
            {
                var mark = service.Indicator switch
                {
                    HealthIndicator.Operational => $"{Green}●{Reset}",
                    HealthIndicator.Maintenance => $"{Cyan}◔{Reset}",
                    HealthIndicator.Unknown => $"{Dim}?{Reset}",
                    HealthIndicator.MajorOutage => $"{Red}■{Reset}",
                    _ => $"{Yellow}▲{Reset}"
                };
                writer.WriteLine($"  {mark} {service.DisplayName,-16} {Dim}{service.Description}{Reset}");
            }
        }

        if (snapshot.ProviderStates.Count > 0)
        {
            Section(writer, "PROVIDERS");
            foreach (var provider in snapshot.ProviderStates)
            {
                var detail = provider.ErrorMessage is { Length: > 0 } message
                    ? $"  {Red}{provider.ErrorCode}: {message}{Reset}"
                    : string.Empty;
                writer.WriteLine(
                    $"  {provider.ProviderId,-14} {provider.Status,-24} " +
                    $"{Dim}fallos {provider.ConsecutiveFailures}{Reset}{detail}");
            }
        }

        writer.WriteLine();
        writer.Flush();
    }

    private static decimal Quota(DashboardServiceRow row) =>
        row.Usage.FirstOrDefault(x => x.Metric.Kind == MetricKind.QuotaConsumed)?.Value ?? 0m;

    private static void Section(TextWriter writer, string title)
    {
        writer.WriteLine();
        writer.WriteLine($"{Dim}{title}{Reset}");
    }

    /// <summary>El mismo medidor de bloques del popup, en texto.</summary>
    private static string Bar(decimal percent)
    {
        const int cells = 20;
        var filled = (int)Math.Round(Math.Clamp(percent, 0m, 100m) / 100m * cells);
        return string.Concat(new string('█', filled), new string('·', cells - filled));
    }

    private static string Colour(decimal percent) => percent switch
    {
        >= 95m => Red,
        >= 70m => Yellow,
        _ => Green
    };

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();
}
