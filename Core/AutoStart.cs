using System.Diagnostics;

namespace SpecForge.Core;

/// <summary>
/// Задача в Планировщике: применяет профиль при каждом входе в систему.
///
/// Зачем это нужно: ветка HKLM\HARDWARE\DESCRIPTION\System пересобирается
/// при каждой загрузке из данных прошивки. Без повторного применения имя
/// процессора и материнской платы вернётся к настоящему после первого
/// же перезапуска.
///
/// Задача создаётся от SYSTEM с наивысшими правами, чтобы при входе
/// не появлялось окно UAC.
/// </summary>
public static class AutoStart
{
    public const string TaskName = "SpecForge AutoStart";
    private const string Flag = "--autostart";

    private static int Run(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return -1;
            process.WaitForExit(15_000);
            var error = process.StandardError.ReadToEnd();
            if (error.Length > 0) AppLog.Info($"schtasks: {error.Trim()}");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            AppLog.Error($"schtasks: {ex.Message}");
            return -1;
        }
    }

    public static bool Exists() => Run($"/Query /TN \"{TaskName}\"") == 0;

    public static bool Enable()
    {
        var exe = ProfileStore.ExePath;
        var command = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" {Flag}\" " +
                      "/SC ONLOGON /RU SYSTEM /RL HIGHEST /F";
        var result = Run(command);
        var ok = result == 0;
        ProfileStore.AppendLog(ok ? "Автозапуск включён" : "Не удалось включить автозапуск");
        return ok;
    }

    public static bool Disable()
    {
        var result = Run($"/Delete /TN \"{TaskName}\" /F");
        var ok = result == 0;
        ProfileStore.AppendLog(ok ? "Автозапуск выключен" : "Задача автозапуска не найдена");
        return ok;
    }

    public static bool Sync(bool enabled) =>
        enabled == Exists() ? true : (enabled ? Enable() : Disable());
}