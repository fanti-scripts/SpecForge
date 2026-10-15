using Avalonia;
using SpecForge.Core;

namespace SpecForge.Avalonia;

internal static class Program
{
    /// <summary>
    /// Точка входа. Здесь же обрабатывается тихий прогон по задаче автозапуска:
    /// он должен применить профиль и выйти, ни разу не создав окно.
    /// В WPF-версии это было в App.OnStartup; в Avalonia запуск идёт
    /// через Main, потому что жизненный цикл создаётся вручную.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase)))
            return ProfileEngine.ApplySilent();

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Та же обработка, что была в WPF: пишем в лог и молча отдаём код 1.
            // MessageBox здесь не нужен - если окно не создалось, его нечем рисовать.
            ProfileStore.AppendLog("FATAL: " + ex);

            try
            {
                File.WriteAllText(
                    Path.Combine(ProfileStore.DataDir, "crash.log"),
                    ex.ToString());
            }
            catch
            {
                // если даже лог не пишется - просто падаем
            }

            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
