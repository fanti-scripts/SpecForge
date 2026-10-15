using System.Text;
using System.Text.RegularExpressions;

namespace SpecForge.Core;

/// <summary>
/// Правка строк в [Strings] файла INF драйвера, чтобы имя не вернулось
/// при переустановке или обновлении устройства.
///
/// Файл INF - системный, читать его может администратор, а писать не всегда.
/// Все правки идут через снимок: оригинальная строка запоминается один раз.
/// </summary>
public static class InfStore
{
    public static string InfPath(string infFile) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", infFile);

    public static bool ReadLine(string infFile, string token, out string? value)
    {
        value = null;
        try
        {
            var path = InfPath(infFile);
            if (!File.Exists(path)) return false;

            foreach (var line in File.ReadLines(path))
            {
                var match = Regex.Match(line, $@"^\s*{Regex.Escape(token)}\s*=\s*""(?<v>[^""]*)""",
                                        RegexOptions.IgnoreCase);
                if (match.Success) { value = match.Groups["v"].Value; return true; }
            }
        }
        catch (Exception ex) { AppLog.Error($"Чтение INF {infFile}: {ex.Message}"); }
        return false;
    }

    public static bool WriteLine(string infFile, string token, string newValue)
    {
        try
        {
            var path = InfPath(infFile);
            if (!File.Exists(path)) return false;

            // Драйверные INF пишутся в ASCII; для русских имён нужен Unicode,
            // поэтому используем UTF-8 без BOM только если файл уже такой.
            var encoding = DetectEncoding(path);
            var text = File.ReadAllText(path, encoding);

            var rx = new Regex($@"^(\s*{Regex.Escape(token)}\s*=\s*)""[^""]*""",
                               RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!rx.IsMatch(text)) return false;

            var updated = rx.Replace(text, $@"$1""{newValue}""", 1);
            File.WriteAllText(path, updated, encoding);
            return true;
        }
        catch (Exception ex) { AppLog.Error($"Запись INF {infFile}: {ex.Message}"); }
        return false;
    }

    private static Encoding DetectEncoding(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var bom = new byte[3];
            var read = stream.Read(bom, 0, 3);
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(true);
            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        }
        catch { }
        return new UTF8Encoding(false);
    }

    /// <summary>
/// Снимает атрибут "только чтение" с файла INF в System32.
/// Если папка принадлежит TrustedInstaller, этого может не хватить -
/// тогда запись вернёт отказ и поле будет помечено как не удавшееся.
/// </summary>
    public static bool EnsureWritable(string infFile)
    {
        var path = InfPath(infFile);
        try
        {
            if (!File.Exists(path)) return false;

            var info = new FileInfo(path);
            if (info.IsReadOnly) info.IsReadOnly = false;
            return !new FileInfo(path).IsReadOnly;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Разблокировка INF {infFile}: {ex.Message}");
            return false;
        }
    }
}