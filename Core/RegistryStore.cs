using System.Text;
using Microsoft.Win32;

namespace SpecForge.Core;

/// <summary>
/// Запись состояния одного значения реестра - достаточно для полного отката.
/// </summary>
/// <param name="Existed">false - значения по этому пути не было, при откате его нужно удалить.</param>
public sealed record SnapshotEntry(
    string Path,
    string Name,
    RegistryValueKind Kind,
    string? Value,
    bool Existed)
{
    public string Key => Path;

    public string Display =>
        $"{(Existed ? "было" : "отсутствовало")}: {Name}={Value}";
}

public static class RegistryStore
{
    // Работаем явно в 64-битном представлении. Приложение x64, но если
    // система отдаст 32-битный вид, значения окажутся не там.
    private const RegistryView View = RegistryView.Registry64;

    /// <summary>
    /// Открывает подраздел HKLM. Возвращает null, если ключа нет и он не создан.
    /// </summary>
    public static RegistryKey? OpenHklm(string path, bool writable)
    {
        var (hive, sub) = Split(path);
        if (hive != RegistryHive.LocalMachine)
            throw new ArgumentException($"Ожидался HKLM, получено {hive}: {path}");

        var root = RegistryKey.OpenBaseKey(hive, View);
        return writable ? root.OpenSubKey(sub, true) ?? root.CreateSubKey(sub)
                        : root.OpenSubKey(sub, false);
    }

    public static (RegistryHive Hive, string SubKey) Split(string path)
    {
        var parts = path.Split('\\', 2);
        if (parts.Length != 2) throw new ArgumentException($"Некорректный путь: {path}");

        var hive = parts[0].ToUpperInvariant() switch
        {
            "HKLM" => RegistryHive.LocalMachine,
            "HKCU" => RegistryHive.CurrentUser,
            "HKCR" => RegistryHive.ClassesRoot,
            "HKU"  => RegistryHive.Users,
            _ => throw new ArgumentException($"Неизвестный куст: {parts[0]}")
        };
        return (hive, parts[1]);
    }

    public static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');

    public static bool TryRead(string path, string name, out RegistryValueKind kind, out string? value)
    {
        kind = default;
        value = null;
        try
        {
            using var key = OpenHklm(path, writable: false);
            if (key?.GetValue(name) is not object raw) return false;

            kind = key.GetValueKind(name);
            value = Describe(raw, kind);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? ReadOrNull(string path, string name) =>
        TryRead(path, name, out _, out var v) ? v : null;

    private static string? Describe(object raw, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.MultiString => string.Join(";", (string[])raw),
        RegistryValueKind.Binary => Convert.ToHexString((byte[])raw),
        RegistryValueKind.DWord or RegistryValueKind.QWord => raw.ToString(),
        _ => raw.ToString()
    };

    private static object Parse(string value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => value,
        RegistryValueKind.MultiString => value.Split(';', StringSplitOptions.RemoveEmptyEntries),
        RegistryValueKind.Binary => Convert.FromHexString(value),
        RegistryValueKind.DWord => unchecked((int)ulong.Parse(value)),
        RegistryValueKind.QWord => ulong.Parse(value),
        _ => value
    };

    public static bool Write(string path, string name, RegistryValueKind kind, string value)
    {
        try
        {
            using var key = OpenHklm(path, writable: true);
            if (key is null) return false;
            key.SetValue(name, Parse(value, kind), kind);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Запись {path}\\{name}: {ex.Message}");
            return false;
        }
    }

    public static bool Delete(string path, string name)
    {
        try
        {
            using var key = OpenHklm(path, writable: true);
            if (key is null) return false;
            if (!key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
                return true; // значения и так нет - считаем успехом
            key.DeleteValue(name, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Удаление {path}\\{name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Снимает состояние значения, ничего не меняя.</summary>
    public static SnapshotEntry Capture(string path, string name)
    {
        var exists = TryRead(path, name, out var kind, out var value);
        return new SnapshotEntry(Normalize(path), name, exists ? kind : RegistryValueKind.String, value, exists);
    }

    /// <summary>Возвращает значение к состоянию из снимка.</summary>
    public static bool Restore(SnapshotEntry entry)
    {
        if (!entry.Existed) return Delete(entry.Path, entry.Name);
        return Write(entry.Path, entry.Name, entry.Kind, entry.Value ?? string.Empty);
    }

    public static bool KeyExists(string path)
    {
        try
        {
            using var key = OpenHklm(path, writable: false);
            return key is not null;
        }
        catch { return false; }
    }

    public static IEnumerable<string> SubKeyNames(string path)
    {
        try
        {
            using var key = OpenHklm(path, writable: false);
            return key?.GetSubKeyNames() ?? [];
        }
        catch { return []; }
    }

    public static IEnumerable<string> ValueNames(string path)
    {
        try
        {
            using var key = OpenHklm(path, writable: false);
            return key?.GetValueNames() ?? [];
        }
        catch { return []; }
    }
}

public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly List<string> Lines = [];

    public static void Info(string message) => Add("INFO ", message);
    public static void Error(string message) => Add("ERROR", message);

    private static void Add(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {level} {message}";
        lock (Gate)
        {
            Lines.Add(line);
            if (Lines.Count > 500) Lines.RemoveAt(0);
        }
        System.Diagnostics.Debug.WriteLine(line);
    }

    public static IReadOnlyList<string> Snapshot()
    {
        lock (Gate) return Lines.ToArray();
    }
}