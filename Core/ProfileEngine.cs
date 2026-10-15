using Microsoft.Win32;

namespace SpecForge.Core;

public sealed record ApplyReport(
    int Applied,
    int Failed,
    IReadOnlyList<string> Lines)
{
    public bool AnyFailed => Failed > 0;

    /// <summary>
    /// Правка INF-файла драйвера требует перезагрузки: имя, которое
    /// покажет Диспетчер задач, драйвер берёт из INF при загрузке.
    /// Без перезагрузки не поможет даже перезапуск Диспетчера задач.
    /// </summary>
    public bool NeedsReboot => Lines.Any(l => l.Contains("INF", StringComparison.Ordinal));
}

/// <summary>
/// Собирает список подменяемых полей, применяет профиль и откатывает его.
///
/// Главное правило: снимок оригинала делается ОДИН РАЗ на значение и никогда
/// не перезаписывается. Иначе второй прогон запишет в «оригинал» уже
/// подменённые данные и откат станет неполным - ровно тот дефект, который
/// обнаружился в первой версии скриптов.
/// </summary>
public static class ProfileEngine
{
    public const string GroupGpu   = "Видеокарта";
    public const string GroupCpu   = "Процессор";
    public const string GroupBoard = "Материнская плата";
    public const string GroupBios  = "BIOS";
    public const string GroupSystem = "Система";

    public static IReadOnlyList<FieldSpec> Discover()
    {
        var fields = new List<FieldSpec>();

        fields.AddRange(BuildGpuFields());
        fields.Add(BuildCpuField());
        fields.AddRange(BuildBoardFields());
        fields.AddRange(BuildBiosFields());
        fields.AddRange(BuildSystemFields());

        foreach (var field in fields)
            field.Current = ReadCurrent(field);

        return fields;
    }

    private static string? ReadCurrent(FieldSpec field)
    {
        if (field.CosmeticOnly) return field.Current;
        return RegistryStore.ReadOrNull(field.RegPath, field.RegName);
    }

    // ------------------------------------------------------------- GPU -----

    private static IEnumerable<FieldSpec> BuildGpuFields()
    {
        foreach (var adapter in SystemDiscovery.FindDisplayAdapters())
        {
            var tag = adapter.DeviceId;
            var infRef = SystemDiscovery.ParseInfReference(
                RegistryStore.ReadOrNull(adapter.EnumPath, "DeviceDesc"));

            // Порядок отвечает тому, что видно человеку: сначала те поля,
            // которые читает большинство программ, дубли уходят вниз.

            yield return new FieldSpec
            {
                Id = $"Gpu.DeviceDesc.{tag}",
                Group = GroupGpu,
                Title = "Имя в диспетчере устройств",
                RegPath = adapter.EnumPath,
                RegName = "DeviceDesc",
                Hint = adapter.Name,
                InfToken = infRef?.Token,
                InfFile = infRef?.InfFile,
                Danger = "Для этого поля нужно снять ACL с ключа Enum\\PCI"
            };

            yield return new FieldSpec
            {
                Id = $"Gpu.DriverDesc.{tag}",
                Group = GroupGpu,
                Title = "Основное название",
                RegPath = adapter.ClassKeyPath,
                RegName = "DriverDesc",
                Hint = adapter.Name,
                InfFile = infRef?.InfFile
            };

            yield return new FieldSpec
            {
                Id = $"Gpu.AdapterString.{tag}",
                Group = GroupGpu,
                Title = "Запасное имя",
                RegPath = adapter.ClassKeyPath,
                RegName = "HardwareInformation.AdapterString",
                Hint = adapter.Name
                            };

            yield return new FieldSpec
            {
                Id = $"Gpu.ChipType.{tag}",
                Group = GroupGpu,
                Title = "Код чипа",
                RegPath = adapter.ClassKeyPath,
                RegName = "HardwareInformation.ChipType",
                Hint = adapter.Name
                            };

            yield return new FieldSpec
            {
                Id = $"Gpu.MemorySize.{tag}",
                Group = GroupGpu,
                Title = "Объём памяти (только для WMI)",
                RegPath = adapter.ClassKeyPath,
                RegName = "HardwareInformation.qwMemorySize",
                Kind = FieldKind.UInt64,
                Hint = "Реальное значение читается с карты",
                InfFile = infRef?.InfFile,
                // Не про вред, а про границу возможного: это единственное
                // место в приложении, где подмена не даёт того, что
                // ожидаешь, поэтому объяснение вынесено сюда.
                Danger =
                    "Это значение читает только WMI - и то с ограничением: поле " +
                    "AdapterRAM в WMI 32-битное, поэтому больше 4 ГБ оно всё равно " +
                    "не покажет. Диспетчер задач объём памяти берёт у драйвера " +
                    "напрямую через DXGI, и на него реестр не влияет вообще. " +
                    "Подменить объём в Диспетчере задач нельзя."
            };

            // Одинаковое имя в каждом дублирующем ключе Control\Video.
            // Их почти всегда несколько, поэтому отдельными карточками
            // они превращаются в шум.
            foreach (var videoKey in adapter.VideoKeyPaths)
            {
                var name = videoKey.Split('\\').Last();
                yield return new FieldSpec
                {
                    Id = $"Gpu.Video.{tag}.{name}",
                    Group = GroupGpu,
                    Title = name,
                    RegPath = videoKey,
                    RegName = "DriverDesc",
                    Hint = "Дублирующий ключ, читают оверлеи и часть игр",
                    Secondary = true
                };
            }
        }
    }

    // ------------------------------------------------- остальные группы -----

    private static FieldSpec BuildCpuField() => new()
    {
        Id = "Cpu.Name",
        Group = GroupCpu,
        Title = "Название процессора",
        RegPath = SystemDiscovery.CpuKey,
        RegName = "ProcessorNameString",
        Hint = "Видно в Диспетчере задач, WMI, systeminfo, msinfo32",
        Danger = "HKLM\\HARDWARE пересобирается при каждой загрузке - нужен автозапуск"
    };

    private static IEnumerable<FieldSpec> BuildBoardFields()
    {
        yield return Simple("Board.Manufacturer", GroupBoard, "Производитель платы", "BaseBoardManufacturer");
        yield return Simple("Board.Product", GroupBoard, "Модель платы", "BaseBoardProduct");
        yield return Simple("Board.Version", GroupBoard, "Версия платы", "BaseBoardVersion");
    }

    private static IEnumerable<FieldSpec> BuildBiosFields()
    {
        yield return Simple("Bios.Vendor", GroupBios, "Производитель BIOS", "BIOSVendor");
        yield return Simple("Bios.Version", GroupBios, "Версия BIOS", "BIOSVersion");
        yield return Simple("Bios.ReleaseDate", GroupBios, "Дата BIOS", "BIOSReleaseDate");
    }

    private static IEnumerable<FieldSpec> BuildSystemFields()
    {
        yield return Simple("Sys.Manufacturer", GroupSystem, "Производитель системы", "SystemManufacturer");
        yield return Simple("Sys.Product", GroupSystem, "Название системы", "SystemProductName");
        yield return Simple("Sys.Family", GroupSystem, "Семейство", "SystemFamily");
        yield return Simple("Sys.SKU", GroupSystem, "Артикул (SKU)", "SystemSKU");
        yield return Simple("Sys.Version", GroupSystem, "Версия системы", "SystemVersion");
    }

    private static FieldSpec Simple(string id, string group, string title, string regName) => new()
    {
        Id = id,
        Group = group,
        Title = title,
        RegPath = SystemDiscovery.BiosKey,
        RegName = regName,
        Hint = "HKLM\\HARDWARE - пересобирается при загрузке, нужен автозапуск",
    };

    // ------------------------------------------------------- применение -----

    /// <summary>
    /// Приводит значение из профиля к тому, что ожидает реестр.
    ///
    /// Два случая требуют вмешательства:
    ///
    /// 1. DeviceDesc хранится в структурированном виде
    ///    «@oem4.inf,%токен%;Название». Windows берёт имя из INF-файла
    ///    драйвера, а «%токен%» - ссылка на конкретную строку в нём.
    ///    Если записать только название, ссылка пропадёт: драйвер при
    ///    следующей загрузке вернёт настоящее имя, и приложение вообще
    ///    перестанет находить свой INF.
    ///
    /// 2. Объём видеопамяти человек вводит в гигабайтах, а qwMemorySize
    ///    хранит байты. Значение считается байтами, если оно уже не
    ///    меньше гигабайта - так распознаются записи от прошлых сборок.
    /// </summary>
    private static string ToRegistryValue(FieldSpec field, string value)
    {
        if (field.Kind == FieldKind.UInt64)
        {
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out var number))
                return value;

            return number >= Gigabyte
                ? ((ulong)number).ToString()
                : ((ulong)(number * Gigabyte)).ToString();
        }

        if (field.InfToken is not null && field.InfFile is not null)
            return $"@{field.InfFile},%{field.InfToken}%;{value}";

        return value;
    }

    private const double Gigabyte = 1024 * 1024 * 1024;

    public static ApplyReport Apply(ProfileDocument profile, IReadOnlyList<FieldSpec> fields)
    {
        var snapshot = ProfileStore.LoadSnapshot();
        var log = new List<string>();
        int applied = 0, failed = 0;

        foreach (var field in fields)
        {
            var entered = profile[field.Id];
            var wanted = ToRegistryValue(field, entered);
            if (string.IsNullOrWhiteSpace(wanted)) continue;
            if (field.CosmeticOnly) continue;

            // снимок - только при первом касании значения
            if (!snapshot.HasRegistry(field.RegPath, field.RegName))
                snapshot.Add(RegistryStore.Capture(field.RegPath, field.RegName));

            var kind = RegistryStore.TryRead(field.RegPath, field.RegName, out var k, out _)
                ? k : RegistryValueKind.String;

            var ok = RegistryStore.Write(field.RegPath, field.RegName, kind, wanted);
            if (ok)
            {
                applied++;
                // В лог идёт то, что ввёл человек, а не байты в реестре.
                log.Add($"OK  {field.Title}: {entered}");
            }
            else
            {
                failed++;
                log.Add($"!!  {field.Title}: не удалось");
            }

            // дублируем в INF, чтобы драйвер не вернул настоящее имя
            if (field.InfToken is not null && field.InfFile is not null)
            {
                if (!snapshot.HasInf(field.InfFile, field.InfToken))
                {
                    var existed = InfStore.ReadLine(field.InfFile, field.InfToken, out var original);
                    snapshot.Add(new InfSnapshotEntry(field.InfFile, field.InfToken, original, existed));
                }

                if (InfStore.EnsureWritable(field.InfFile) &&
                    InfStore.WriteLine(field.InfFile, field.InfToken, wanted))
                {
                    log.Add($"OK  INF {field.InfFile}: {field.InfToken}");
                }
                else
                {
                    log.Add($"--  INF {field.InfFile}: не удалось изменить (нет прав или токен не найден)");
                }
            }
        }

        ProfileStore.SaveSnapshot(snapshot);
        ProfileStore.AppendLog($"Применено: {applied}, ошибок: {failed}");

        // Построчный отчёт обязателен: без него не видно, что запись
        // в INF не удалась, а ведь именно от неё зависит имя,
        // которое покажет Диспетчер задач.
        foreach (var line in log)
            ProfileStore.AppendLog("  " + line);

        return new ApplyReport(applied, failed, log);
    }

    public static ApplyReport Revert()
    {
        var snapshot = ProfileStore.LoadSnapshot();
        var log = new List<string>();
        int applied = 0, failed = 0;

        foreach (var entry in snapshot.Registry)
        {
            if (RegistryStore.Restore(entry))
            {
                applied++;
                log.Add($"OK  {entry.Path}\\{entry.Name} = {entry.Value ?? "(нет значения)"}");
            }
            else
            {
                failed++;
                log.Add($"!!  {entry.Path}\\{entry.Name}");
            }
        }

        foreach (var entry in snapshot.Inf)
        {
            if (!entry.Existed)
            {
                log.Add($"--  INF {entry.InfFile}: строки {entry.Token} изначально не было");
                continue;
            }

            if (InfStore.EnsureWritable(entry.InfFile) &&
                InfStore.WriteLine(entry.InfFile, entry.Token, entry.Value ?? string.Empty))
            {
                applied++;
                log.Add($"OK  INF {entry.InfFile}: восстановлено");
            }
            else
            {
                failed++;
                log.Add($"!!  INF {entry.InfFile}: {entry.Token}");
            }
        }

        ProfileStore.ClearSnapshot();
        ProfileStore.AppendLog($"Откат: восстановлено {applied}, ошибок: {failed}");

        foreach (var line in log)
            ProfileStore.AppendLog("  " + line);

        return new ApplyReport(applied, failed, log);
    }

    /// <summary>Тихий прогон при входе в систему: применяет профиль, ничего не показывая.</summary>
    public static int ApplySilent()
    {
        var profile = ProfileStore.LoadProfile();
        if (profile.Overrides.Count == 0) return 0;
        if (profile.AutoRevert) { Revert(); return 0; }

        var fields = Discover();
        var report = Apply(profile, fields);
        return report.AnyFailed ? 1 : 0;
    }
}