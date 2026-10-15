using System.Text.Json;

namespace SpecForge.Core;

public enum FieldKind { Text, UInt64, Mac }

/// <summary>
/// Одно подменяемое значение: где оно лежит в реестре и что сейчас там написано.
/// </summary>
public sealed class FieldSpec
{
    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Title { get; init; }
    public string RegPath { get; init; } = "";
    public string RegName { get; init; } = "";

    public FieldKind Kind { get; init; } = FieldKind.Text;
    public string? Hint { get; init; }

    /// <summary>Токен строки в INF, если значение продублировано в файле драйвера.</summary>
    public string? InfToken { get; init; }
    public string? InfFile { get; init; }

    /// <summary>Предупреждение, если правка может навредить работе системы.</summary>
    public string? Danger { get; init; }

    /// <summary>Изменение требует перезагрузки, чтобы вступило в силу.</summary>
    public bool NeedsReboot { get; init; }

    /// <summary>Это поле только показывается в интерфейсе и никуда не пишется.</summary>
    public bool CosmeticOnly { get; init; }

    /// <summary>
    /// Дублирующий ключ. Показывается свёрнутым списком внизу группы,
    /// потому что в виде отдельных карточек он только шумит.
    /// </summary>
    public bool Secondary { get; init; }

    public string? Current { get; set; }
}

/// <summary>Сохранённый профиль: какие значения подменять и как.</summary>
public sealed class ProfileDocument
{
    public Dictionary<string, string> Overrides { get; set; } = new();
    public bool AutoStart { get; set; } = true;
    public bool AutoRevert { get; set; } = false;

    public string this[string id]
    {
        get => Overrides.TryGetValue(id, out var v) ? v : string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) Overrides.Remove(id);
            else Overrides[id] = value.Trim();
        }
    }

    public bool Has(string id) => Overrides.ContainsKey(id);

    public void Clear() => Overrides.Clear();
}

/// <summary>Снимок оригинальной строки в INF драйвера.</summary>
public sealed record InfSnapshotEntry(
    string InfFile,
    string Token,
    string? Value,
    bool Existed);

/// <summary>Файл снимка: всё, что приложение успело изменить.</summary>
public sealed class SnapshotFile
{
    public List<SnapshotEntry> Registry { get; set; } = [];
    public List<InfSnapshotEntry> Inf { get; set; } = [];

    public bool IsEmpty => Registry.Count == 0 && Inf.Count == 0;

    public bool HasRegistry(string path, string name) =>
        Registry.Any(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool HasInf(string infFile, string token) =>
        Inf.Any(e => string.Equals(e.InfFile, infFile, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(e.Token, token, StringComparison.OrdinalIgnoreCase));

    public void Add(SnapshotEntry entry) => Registry.Add(entry);
    public void Add(InfSnapshotEntry entry) => Inf.Add(entry);
}

/// <summary>Где лежат профиль и снимок оригинала.</summary>
public static class ProfileStore
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SpecForge");

    public static string ProfilePath => Path.Combine(DataDir, "profile.json");
    public static string SnapshotPath => Path.Combine(DataDir, "snapshot.json");
    public static string LogPath => Path.Combine(DataDir, "specforge.log");
    public static string ExePath => Environment.ProcessPath ?? "SpecForge.exe";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void EnsureDir()
    {
        try { Directory.CreateDirectory(DataDir); } catch { /* нет прав - работаем без файлов */ }
    }

    public static ProfileDocument LoadProfile()
    {
        try
        {
            if (File.Exists(ProfilePath))
                return JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllText(ProfilePath)) ?? new ProfileDocument();
        }
        catch (Exception ex) { AppLog.Error($"Чтение профиля: {ex.Message}"); }
        return new ProfileDocument();
    }

    public static void SaveProfile(ProfileDocument profile)
    {
        try
        {
            EnsureDir();
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile, Json));
        }
        catch (Exception ex) { AppLog.Error($"Сохранение профиля: {ex.Message}"); }
    }

    public static SnapshotFile LoadSnapshot()
    {
        try
        {
            if (File.Exists(SnapshotPath))
                return JsonSerializer.Deserialize<SnapshotFile>(File.ReadAllText(SnapshotPath)) ?? new SnapshotFile();
        }
        catch (Exception ex) { AppLog.Error($"Чтение снимка: {ex.Message}"); }
        return new SnapshotFile();
    }

    public static void SaveSnapshot(SnapshotFile snapshot)
    {
        try
        {
            EnsureDir();
            File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(snapshot, Json));
        }
        catch (Exception ex) { AppLog.Error($"Сохранение снимка: {ex.Message}"); }
    }

    public static void ClearSnapshot()
    {
        try { if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath); }
        catch { }
    }

    public static void AppendLog(string line)
    {
        try
        {
            EnsureDir();
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch { }
    }
}