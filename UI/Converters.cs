using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SpecForge.Avalonia.UI;

/// <summary>
/// Рамка карточки: если значение изменено - акцентный цвет, иначе - обычная граница.
///
/// В WPF это делал стиль CardChanged через DataTrigger по IsChanged.
/// У стилей Avalonia нет триггеров на данные, поэтому условие вынесено сюда.
/// </summary>
public sealed class ChangedBorderConverter : IValueConverter
{
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#4E7FFC"));
    private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#2D303A"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Accent : Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

/// <summary>
/// Ширина карточки в сетке из двух колонок.
///
/// Раньше для этого был самодельный ResponsiveWrapPanel, который считал
/// ширину и присваивал ItemWidth внутри MeasureOverride. Это вызывало
/// бесконечный цикл измерения: присвоение свойства внутри измерения
/// немедленно инвалидирует измерение. Приложение при этом забирало целое
/// ядро процессора, а после применения профиля окно переставало отвечать.
///
/// Теперь ширина приходит привязкой к ширине ItemsControl, то есть меняется
/// только при реальном изменении размера окна и не трогает процесс раскладки.
/// </summary>
public sealed class CardWidthConverter : IValueConverter
{
    private const double Gutter = 10;
    private const double MinWidth = 160;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double available) return double.NaN;
        if (double.IsNaN(available) || double.IsInfinity(available) || available <= 0)
            return double.NaN;

        var width = (available - Gutter) / 2;
        return width > MinWidth ? width : double.NaN;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
