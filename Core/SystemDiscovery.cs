using System.Text.RegularExpressions;

namespace SpecForge.Core;

public sealed record DisplayAdapter(
    string VendorId,
    string DeviceId,
    string Name,
    string ClassKeyPath,   // HKLM\SYSTEM\...\Class\{4d36e968-...}\0000
    string EnumPath,       // HKLM\SYSTEM\...\Enum\PCI\VEN_...\&lt;instance&gt;
    string InstanceId,
    IReadOnlyList<string> VideoKeyPaths);

/// <summary>
/// Поиск реальных устройств в системе. Ничего не зашито: адаптеры находятся
/// по ClassGUID, а не по конкретному ID модели.
/// </summary>
public static class SystemDiscovery
{
    public const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string PciEnumBase = @"HKLM\SYSTEM\CurrentControlSet\Enum\PCI";
    private const string VideoBase  = @"HKLM\SYSTEM\CurrentControlSet\Control\Video";
    private const string ClassBase  = @"HKLM\SYSTEM\CurrentControlSet\Control\Class";
    private const string HwBase     = @"HKLM\HARDWARE\DESCRIPTION\System";

    public static IReadOnlyList<DisplayAdapter> FindDisplayAdapters()
    {
        var found = new List<DisplayAdapter>();

        foreach (var hardwareId in RegistryStore.SubKeyNames(PciEnumBase))
        {
            if (!Regex.IsMatch(hardwareId, @"^VEN_([0-9A-Fa-f]{4})&DEV_([0-9A-Fa-f]{4})"))
                continue;

            var vendorId = Regex.Match(hardwareId, @"VEN_([0-9A-Fa-f]{4})").Groups[1].Value.ToUpperInvariant();
            var deviceId = Regex.Match(hardwareId, @"DEV_([0-9A-Fa-f]{4})").Groups[1].Value.ToUpperInvariant();

            foreach (var instanceId in RegistryStore.SubKeyNames($@"{PciEnumBase}\{hardwareId}"))
            {
                var enumPath = $@"{PciEnumBase}\{hardwareId}\{instanceId}";
                var classGuid = RegistryStore.ReadOrNull(enumPath, "ClassGUID");
                if (classGuid is null || !classGuid.Contains(DisplayClassGuid, StringComparison.OrdinalIgnoreCase))
                    continue;

                var driverRef = RegistryStore.ReadOrNull(enumPath, "Driver");
                if (string.IsNullOrEmpty(driverRef)) continue;

                var deviceDesc = RegistryStore.ReadOrNull(enumPath, "DeviceDesc");
                var name = FriendlyTail(deviceDesc) ?? $"{vendorId}:{deviceId}";
                var classPath = $@"{ClassBase}\{driverRef}";

                var videoKeys = FindVideoKeys(vendorId, deviceId);

                found.Add(new DisplayAdapter(vendorId, deviceId, name, classPath, enumPath, instanceId, videoKeys));

                // на одной карте бывает несколько экземпляров Enum\PCI - берём первый
                break;
            }
        }

        return found;
    }

    public static IReadOnlyList<string> FindVideoKeys(string vendorId, string deviceId)
    {
        var prefix = $@"pci\ven_{vendorId.ToLowerInvariant()}&dev_{deviceId.ToLowerInvariant()}";
        var result = new List<string>();

        foreach (var guidKey in RegistryStore.SubKeyNames(VideoBase))
        {
            var guidPath = $@"{VideoBase}\{guidKey}";
            foreach (var sub in RegistryStore.SubKeyNames(guidPath))
            {
                var path = $@"{guidPath}\{sub}";
                var matchingId = RegistryStore.ReadOrNull(path, "MatchingDeviceId");
                if (matchingId is null) continue;
                if (matchingId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(path);
            }
        }

        return result;
    }

    /// <summary>DeviceDesc бывает вида @oem4.inf,%token%;Отображаемое имя</summary>
    public static string? FriendlyTail(string? deviceDesc) =>
        deviceDesc is null ? null : deviceDesc.Split(';').Last().Trim();

    /// <summary>Разбирает DeviceDesc на файл INF и токен строки.</summary>
    public static (string InfFile, string Token)? ParseInfReference(string? deviceDesc)
    {
        if (deviceDesc is null) return null;
        var match = Regex.Match(deviceDesc, @"^@(?<inf>[^,]+),(?<token>[^;]+);");
        if (!match.Success) return null;
        return (match.Groups["inf"].Value, match.Groups["token"].Value.Trim('%'));
    }

    // ---- статические строки описания системы ----

    public static string BiosKey => $@"{HwBase}\BIOS";
    public static string CpuKey => $@"{HwBase}\CentralProcessor\0";
}