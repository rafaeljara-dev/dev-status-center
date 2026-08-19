using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using Brush = System.Windows.Media.Brush;
using Binding = System.Windows.Data.Binding;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace DevStatusCenter.Desktop.Converters;

/// <summary>
/// Compara el valor con el parametro y devuelve Visible o Collapsed. Es lo que decide que
/// pestana se ve: cada panel se compara contra el indice seleccionado, sin duplicar en el
/// ViewModel una propiedad booleana por pestana.
/// </summary>
public sealed class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Matches(value, parameter) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    internal static bool Matches(object? value, object? parameter)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        // El parametro llega de XAML siempre como cadena; el valor es un entero.
        return string.Equals(
            System.Convert.ToString(value, CultureInfo.InvariantCulture),
            System.Convert.ToString(parameter, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }
}

/// <summary>
/// La misma comparacion, pero devolviendo color: resalta la pestana activa sin necesitar un
/// disparador por cada una.
/// </summary>
public sealed class EqualityToBrushConverter : IValueConverter
{
    private Brush _match = Brushes.White;
    private Brush _fallback = Brushes.Gray;

    public Color MatchColor
    {
        get => ((SolidColorBrush)_match).Color;
        set => _match = Freeze(value);
    }

    public Color FallbackColor
    {
        get => ((SolidColorBrush)_fallback).Color;
        set => _fallback = Freeze(value);
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        EqualityToVisibilityConverter.Matches(value, parameter) ? _match : _fallback;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>Oculta lo que depende de un dato opcional (una marca desconocida, un pago que no hay).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
