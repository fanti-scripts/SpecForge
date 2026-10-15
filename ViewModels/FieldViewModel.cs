using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SpecForge.Core;

namespace SpecForge.Avalonia.ViewModels;

/// <summary>
/// Одно поле в интерфейсе: заголовок, подсказка, текущее значение из реестра
/// и то, что пользователь ввёл вместо него.
/// </summary>
/// <remarks>
/// Перенесён из WPF-проекта без изменений: INotifyPropertyChanged одинаков
/// в обеих технологиях, а System.Windows здесь не участвует.
/// </remarks>
public sealed class FieldViewModel : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public FieldViewModel(FieldSpec spec, string? saved)
    {
        Id = spec.Id;
        Group = spec.Group;
        Title = spec.Title;
        Danger = spec.Danger;
        Current = spec.Current ?? string.Empty;
        CurrentDisplay = FormatCurrent(spec);
        CosmeticOnly = spec.CosmeticOnly;
        Secondary = spec.Secondary;
        Kind = spec.Kind;
        RegistryPath = spec.RegPath;
        RegistryName = spec.RegName;
        _value = saved ?? string.Empty;
    }

    /// <summary>
    /// Значение из реестра приводится к виду, который что-то значит:
    /// память переводится из байтов в гигабайты, а из DeviceDesc
    /// выбрасывается ссылка на INF - в нём впереди стоит
    /// «@oem#.inf,%nvidia_dev...%;», и человеку она ничего не говорит.
    /// </summary>
    private static string FormatCurrent(FieldSpec spec)
    {
        var raw = spec.Current;
        if (raw is null) return string.Empty;

        if (raw.StartsWith('@'))
        {
            var cut = raw.LastIndexOf(';');
            if (cut >= 0) raw = raw[(cut + 1)..].Trim();
        }

        return FormatValue(raw, spec.Kind);
    }

    public string Id { get; }
    public string Group { get; }
    public string Title { get; }

    // Предупреждение показывается подсказкой на поле ввода.
    public string? Danger { get; }

    public string Current { get; }

    /// <summary>
    /// Что сейчас лежит в реестре, в понятном виде.
    /// </summary>
    public string CurrentDisplay { get; }

    /// <summary>Что введено в поле, тоже в понятных единицах.</summary>
    public string NewDisplay => FormatValue(Value, Kind);

    /// <summary>
    /// Строка под названием поля.
    ///
    /// Показывает, что произойдёт: «4 ГБ → 12 ГБ» вместо молчаливого
    /// «4 ГБ». Раньше старая строка не менялась до нажатия «Применить»,
    /// и выглядело так, будто выбор модели не сработал.
    /// </summary>
    public string StateLine
    {
        get
        {
            var from = CurrentDisplay;
            var to = NewDisplay;

            if (string.IsNullOrWhiteSpace(to)) return from;
            if (string.IsNullOrWhiteSpace(from)) return to;
            return from == to ? from : $"{from} → {to}";
        }
    }

    private static string FormatValue(string? value, FieldKind kind)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // В поле объём памяти вводится в гигабайтах, в реестре он хранится
        // в байтах. Показываем в тех же единицах, что и заголовок поля.
        if (kind == FieldKind.UInt64 &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gb))
        {
            // Значения прошлых сборок остались в байтах - узнаём их по размеру.
            if (gb >= Gigabyte) gb /= Gigabyte;
            return $"{gb:0.##} ГБ";
        }

        return value;
    }

    private const double Gigabyte = 1024 * 1024 * 1024;

    public FieldKind Kind { get; }

    /// <summary>Дублирующий ключ, показывается свёрнутым.</summary>
    public bool Secondary { get; }

    public bool CosmeticOnly { get; }
    public string RegistryPath { get; }
    public string RegistryName { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
            OnPropertyChanged(nameof(NewDisplay));
            OnPropertyChanged(nameof(StateLine));
        }
    }

    public bool IsChanged => !string.IsNullOrWhiteSpace(Value);
    public bool HasCurrent => !string.IsNullOrWhiteSpace(CurrentDisplay);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
