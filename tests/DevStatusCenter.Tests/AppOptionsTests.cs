using DevStatusCenter.Application.Configuration;
using DevStatusCenter.Infrastructure.Configuration;

namespace DevStatusCenter.Tests;

public sealed class AppOptionsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dev-status-center-options",
        Guid.NewGuid().ToString("N"));

    public AppOptionsTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(99, 8)]
    public void Create_ClampsConcurrencyIntoAUsefulRange(int configured, int expected)
    {
        var options = AppOptions.Create(_root, normalConcurrency: configured);

        Assert.Equal(expected, options.NormalConcurrency);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(400, 400)]
    [InlineData(99_999, 3_650)]
    public void Create_ClampsRetentionIntoAUsefulRange(int configured, int expected)
    {
        var options = AppOptions.Create(_root, historyRetentionDays: configured);

        Assert.Equal(expected, options.HistoryRetentionDays);
        Assert.Equal(TimeSpan.FromDays(expected), options.HistoryRetention);
    }

    [Theory]
    [InlineData("mxn", "MXN")]
    [InlineData("  eur ", "EUR")]
    [InlineData("dollars", "USD")]
    [InlineData("", "USD")]
    [InlineData(null, "USD")]
    public void Create_NormalizesTheDisplayCurrency(string? configured, string expected)
    {
        var options = AppOptions.Create(_root, displayCurrency: configured);

        Assert.Equal(expected, options.DisplayCurrency);
    }

    [Fact]
    public void Create_ResolvesPathsAgainstTheLocalRootAndExpandsVariables()
    {
        var options = AppOptions.Create(_root, databasePath: "%LOCALAPPDATA%/dsc-test/cache.db");

        Assert.DoesNotContain("%", options.DatabasePath, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(options.DatabasePath));
        Assert.Equal(Path.Combine(_root, "secrets"), options.SecretsPath);
    }

    [Fact]
    public void For_TreatsAnUnknownProviderAsDisabled()
    {
        var options = AppOptionsStore.Defaults(_root);

        Assert.False(options.IsEnabled("does-not-exist"));
        Assert.True(options.IsEnabled("mock"));
        Assert.False(options.IsEnabled("neon"));

        // El archivo guarda la referencia lógica, nunca el secreto.
        Assert.Equal("neon-personal", options.For("NEON").CredentialReference);
    }

    [Fact]
    public void EnsureTemplate_WritesOnceAndRoundTrips()
    {
        var defaults = AppOptionsStore.Defaults(_root);

        Assert.True(AppOptionsStore.EnsureTemplate(_root, defaults));
        Assert.False(AppOptionsStore.EnsureTemplate(_root, defaults));

        var loaded = AppOptionsStore.Load(_root, out var error);

        Assert.Null(error);
        Assert.Equal(defaults.DisplayCurrency, loaded.DisplayCurrency);
        Assert.Equal(defaults.NormalConcurrency, loaded.NormalConcurrency);
        Assert.Equal(defaults.HistoryRetentionDays, loaded.HistoryRetentionDays);
        Assert.Equal(defaults.DatabasePath, loaded.DatabasePath);
        Assert.Equal(defaults.Providers.Count, loaded.Providers.Count);
    }

    [Fact]
    public void Load_UsesTheEditedValues()
    {
        File.WriteAllText(AppOptionsStore.PathFor(_root), """
            {
              // Los comentarios y la coma final se toleran a propósito: este archivo se edita a mano.
              "displayCurrency": "mxn",
              "normalConcurrency": 2,
              "historyRetentionDays": 90,
              "providers": {
                "neon": { "enabled": true, "credentialReference": "neon-work" },
              }
            }
            """);

        var options = AppOptionsStore.Load(_root, out var error);

        Assert.Null(error);
        Assert.Equal("MXN", options.DisplayCurrency);
        Assert.Equal(2, options.NormalConcurrency);
        Assert.Equal(90, options.HistoryRetentionDays);
        Assert.True(options.IsEnabled("neon"));
        Assert.Equal("neon-work", options.For("neon").CredentialReference);
        Assert.False(options.IsEnabled("mock"));
    }

    [Fact]
    public void Load_FallsBackToDefaultsWhenTheFileIsBroken()
    {
        File.WriteAllText(AppOptionsStore.PathFor(_root), "{ esto no es json");

        var options = AppOptionsStore.Load(_root, out var error);

        // Arrancar con valores por defecto es preferible a no llegar al tray por una coma.
        Assert.NotNull(error);
        Assert.Contains(AppOptions.FileName, error, StringComparison.Ordinal);
        Assert.Equal("USD", options.DisplayCurrency);
        Assert.True(options.IsEnabled("mock"));
    }
}
