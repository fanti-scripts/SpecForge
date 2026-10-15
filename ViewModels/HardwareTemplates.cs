using SpecForge.Core;

namespace SpecForge.Avalonia.ViewModels;

/// <summary>
/// Готовая подмена для группы.
///
/// Шаблон хранит значения по именам параметров реестра, а не одно
/// общее название. Иначе у материнской платы строка
/// «ASUS ROG CROSSHAIR X870E HERO» попала бы и в «Производитель платы»,
/// и в «Модель платы» - то есть в поле, где нужен только «ASUS».
///
/// Ключи словаря - это RegName из <see cref="FieldSpec"/>, поэтому шаблон
/// работает на любой машине: у ключей нет привязки к конкретному
/// устройству.
/// </summary>
public sealed record HardwareTemplate(
    string Label,
    IReadOnlyDictionary<string, string> Values,
    uint? MemoryGb = null)
{
    public override string ToString() => Label;
}

/// <summary>
/// Объём видеопамяти отдельным списком: у одной модели встречаются разные
/// объёмы, а подставить хочется любой.
/// </summary>
public sealed record MemoryOption(uint Gigabytes)
{
    public override string ToString() => $"{Gigabytes} ГБ";
}

/// <summary>
/// Списки популярных моделей железа для быстрой подмены.
///
/// Модели перечислены от самых мощных к самым бюджетным внутри каждой
/// линейки, NVIDIA перед AMD.
/// </summary>
public static class HardwareTemplates
{
    // Имена параметров реестра, используемые в шаблонах.
    private const string DriverDesc = "DriverDesc";
    private const string DeviceDesc = "DeviceDesc";
    private const string AdapterString = "HardwareInformation.AdapterString";
    private const string ChipType = "HardwareInformation.ChipType";
    private const string CpuName = "ProcessorNameString";
    private const string BoardManufacturer = "BaseBoardManufacturer";
    private const string BoardProduct = "BaseBoardProduct";
    private const string BoardVersion = "BaseBoardVersion";
    private const string BiosVendor = "BIOSVendor";
    private const string SystemManufacturer = "SystemManufacturer";
    private const string SystemProduct = "SystemProductName";
    private const string SystemFamily = "SystemFamily";
    private const string SystemVersion = "SystemVersion";

    public static IReadOnlyList<HardwareTemplate> ForGroup(string group) => group switch
    {
        ProfileEngine.GroupGpu => Gpu,
        ProfileEngine.GroupCpu => Cpu,
        ProfileEngine.GroupBoard => Board,
        ProfileEngine.GroupSystem => System,
        ProfileEngine.GroupBios => Bios,
        _ => []
    };

    /// <summary>Список объёмов показывается только там, где есть поле памяти.</summary>
    public static bool HasMemoryField(string group) => group == ProfileEngine.GroupGpu;

    /// <summary>Объёмы видеопамяти, которые встречаются у карт из списка ниже.</summary>
    public static readonly IReadOnlyList<uint> MemoryGb =
        [2, 3, 4, 6, 8, 10, 11, 12, 16, 20, 24, 32, 48];

    /// <summary>
    /// Видеокарта: одно название пишется во все четыре ключа, которыми
    /// разные программы читают имя карты. Плюс объём памяти.
    /// </summary>
    private static HardwareTemplate Card(string name, uint gb) => new(
        name,
        new Dictionary<string, string>
        {
            [DeviceDesc] = name,
            [DriverDesc] = name,
            [AdapterString] = name,
            [ChipType] = name
        },
        gb);

    public static readonly IReadOnlyList<HardwareTemplate> Gpu =
    [
        Card("NVIDIA GeForce RTX 5090", 32),
        Card("NVIDIA GeForce RTX 5080", 16),
        Card("NVIDIA GeForce RTX 5070 Ti", 16),
        Card("NVIDIA GeForce RTX 5070", 12),
        Card("NVIDIA GeForce RTX 5060 Ti", 16),
        Card("NVIDIA GeForce RTX 5060", 8),
        Card("NVIDIA GeForce RTX 4090", 24),
        Card("NVIDIA GeForce RTX 4080 SUPER", 16),
        Card("NVIDIA GeForce RTX 4070 Ti SUPER", 16),
        Card("NVIDIA GeForce RTX 4070", 12),
        Card("NVIDIA GeForce RTX 4060 Ti", 8),
        Card("NVIDIA GeForce RTX 4060", 8),
        Card("NVIDIA GeForce RTX 3090 Ti", 24),
        Card("NVIDIA GeForce RTX 3080 Ti", 10),
        Card("NVIDIA GeForce RTX 3070", 8),
        Card("NVIDIA GeForce RTX 3060 Ti", 8),
        Card("NVIDIA GeForce GTX 1660 SUPER", 6),
        Card("NVIDIA GeForce GTX 1650 SUPER", 4),
        Card("NVIDIA GeForce GTX 1080 Ti", 11),
        Card("NVIDIA GeForce GTX 1070", 8),
        Card("AMD Radeon RX 9070 XT", 16),
        Card("AMD Radeon RX 9070", 16),
        Card("AMD Radeon RX 7900 XTX", 24),
        Card("AMD Radeon RX 7900 XT", 20),
        Card("AMD Radeon RX 7800 XT", 16),
        Card("AMD Radeon RX 7700 XT", 12),
        Card("AMD Radeon RX 7600", 8),
        Card("AMD Radeon RX 6800 XT", 16),
        Card("AMD Radeon RX 6700 XT", 12),
        Card("AMD Radeon RX 6600 XT", 8)
    ];

    public static readonly IReadOnlyList<HardwareTemplate> Cpu =
    [
        new("AMD Ryzen 9 9950X3D", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 9 9950X3D" }),
        new("AMD Ryzen 9 9900X3D", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 9 9900X3D" }),
        new("AMD Ryzen 9 9950X", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 9 9950X" }),
        new("AMD Ryzen 7 9800X3D", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 7 9800X3D" }),
        new("AMD Ryzen 7 9700X", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 7 9700X" }),
        new("AMD Ryzen 5 9600X", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 5 9600X" }),
        new("AMD Ryzen 5 5600X", new Dictionary<string, string> { [CpuName] = "AMD Ryzen 5 5600X" }),
        new("Intel Core Ultra 9 285K", new Dictionary<string, string> { [CpuName] = "Intel Core Ultra 9 285K" }),
        new("Intel Core Ultra 7 265K", new Dictionary<string, string> { [CpuName] = "Intel Core Ultra 7 265K" }),
        new("Intel Core Ultra 5 245K", new Dictionary<string, string> { [CpuName] = "Intel Core Ultra 5 245K" }),
        new("Intel Core i9-14900KS", new Dictionary<string, string> { [CpuName] = "Intel Core i9-14900KS" }),
        new("Intel Core i9-14900K", new Dictionary<string, string> { [CpuName] = "Intel Core i9-14900K" }),
        new("Intel Core i7-14700K", new Dictionary<string, string> { [CpuName] = "Intel Core i7-14700K" }),
        new("Intel Core i5-14600K", new Dictionary<string, string> { [CpuName] = "Intel Core i5-14600K" }),
        new("Intel Core i5-14400F", new Dictionary<string, string> { [CpuName] = "Intel Core i5-14400F" }),
        new("Intel Core i3-14100F", new Dictionary<string, string> { [CpuName] = "Intel Core i3-14100F" })
    ];

    /// <summary>
    /// Плата. Производитель и модель разнесены по разным полям - ровно так,
    /// как они лежат в SMBIOS.
    /// </summary>
    private static HardwareTemplate Mainboard(string vendor, string model) => new(
        $"{vendor} {model}",
        new Dictionary<string, string>
        {
            [BoardManufacturer] = vendor,
            [BoardProduct] = model,
            [BoardVersion] = "1.0"
        });

    public static readonly IReadOnlyList<HardwareTemplate> Board =
    [
        Mainboard("ASUS", "ROG CROSSHAIR X870E HERO"),
        Mainboard("ASUS", "ROG STRIX B650E-F"),
        Mainboard("ASUS", "TUF GAMING B650-PLUS WIFI"),
        Mainboard("ASUS", "PRIME B650M-K"),
        Mainboard("MSI", "MAG Z790 TOMAHAWK WIFI"),
        Mainboard("MSI", "MAG B650 TOMAHAWK WIFI"),
        Mainboard("MSI", "PRO Z790-P WIFI"),
        Mainboard("GIGABYTE", "X670E AORUS MASTER"),
        Mainboard("GIGABYTE", "B650 AORUS ELITE AX"),
        Mainboard("ASRock", "X670E Taichi Lite")
    ];

    /// <summary>Система: то же железо, но раскладывается по полям SMBIOS.</summary>
    private static HardwareTemplate Rig(string vendor, string model, string family) => new(
        $"{vendor} {model}",
        new Dictionary<string, string>
        {
            [SystemManufacturer] = vendor,
            [SystemProduct] = model,
            [SystemFamily] = family,
            [SystemVersion] = "1.0"
        });

    public static readonly IReadOnlyList<HardwareTemplate> System =
    [
        Rig("ASUS", "ROG CROSSHAIR X870E HERO", "ROG"),
        Rig("ASUS", "ROG STRIX B650E-F", "ROG"),
        Rig("ASUS", "TUF GAMING B650-PLUS WIFI", "TUF"),
        Rig("MSI", "MAG Z790 TOMAHAWK WIFI", "MPG"),
        Rig("MSI", "MAG B650 TOMAHAWK WIFI", "MPG"),
        Rig("GIGABYTE", "X670E AORUS MASTER", "AORUS"),
        Rig("GIGABYTE", "B650 AORUS ELITE AX", "AORUS"),
        Rig("ASRock", "X670E Taichi Lite", "Taichi"),
        Rig("Dell Inc.", "XPS 8960", "XPS"),
        Rig("HP", "OMEN 9015", "OMEN")
    ];

    /// <summary>
    /// Прошивка. Заполняется только производитель: версии BIOS у каждой
    /// платы свои, и выдуманная строка в поле «Версия BIOS» только вредит.
    /// </summary>
    public static readonly IReadOnlyList<HardwareTemplate> Bios =
    [
        new("American Megatrends", new Dictionary<string, string> { [BiosVendor] = "American Megatrends International, LLC." }),
        new("AMI", new Dictionary<string, string> { [BiosVendor] = "American Megatrends International, LLC." }),
        new("Award Software", new Dictionary<string, string> { [BiosVendor] = "Award Software, Inc." }),
        new("Phoenix Technologies", new Dictionary<string, string> { [BiosVendor] = "Phoenix Technologies LTD" }),
        new("Dell Inc.", new Dictionary<string, string> { [BiosVendor] = "Dell Inc." }),
        new("Hewlett-Packard", new Dictionary<string, string> { [BiosVendor] = "Hewlett-Packard" }),
        new("LENOVO", new Dictionary<string, string> { [BiosVendor] = "LENOVO" })
    ];
}
