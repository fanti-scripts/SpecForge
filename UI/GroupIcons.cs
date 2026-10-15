using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using SpecForge.Core;

namespace SpecForge.Avalonia.UI;

/// <summary>
/// Иконки для пунктов навигации.
///
/// В наборе FASymbol нет символов для процессора и материнской платы -
/// там только общие глифы интерфейса. Поэтому для этих двух групп
/// нарисованы собственные контуры. Всё в сетке 24×24.
///
/// Чтобы заменить иконку, достаточно дописать случай в IconFor.
/// </summary>
public static class GroupIcons
{
    private static Geometry Path(string data) => Geometry.Parse(data);

    // Контур микросхемы: корпус 7..17 по X и 5..19 по Y, выводы по четырём сторонам.
    private static readonly Geometry Cpu = Path(
        "M7,5 h10 v14 h-10 z " +
        "M9,2 h1.6 v3 h-1.6 z M11.2,2 h1.6 v3 h-1.6 z M13.2,2 h1.6 v3 h-1.6 z " +
        "M2,9 h5 v1.6 h-5 z M2,11.2 h5 v1.6 h-5 z M2,13.2 h5 v1.6 h-5 z " +
        "M17,9 h5 v1.6 h-5 z M17,11.2 h5 v1.6 h-5 z M17,13.2 h5 v1.6 h-5 z " +
        "M9,19 h1.6 v3 h-1.6 z M11.2,19 h1.6 v3 h-1.6 z M13.2,19 h1.6 v3 h-1.6 z");

    // Плата: рамка с вырезом, внутри разъём, два слота памяти и чип.
    private static readonly Geometry Board = Path(
        "M3,4 h18 v16 h-18 z M5,6 v12 h14 v-12 z " +
        "M7,8 h4 v4 h-4 z " +
        "M13,8 h1.6 v8 h-1.6 z M15.4,8 h1.6 v8 h-1.6 z " +
        "M7,14 h4 v2 h-4 z");

    public static FAIconSource IconFor(string group) => group switch
    {
        ProfileEngine.GroupGpu    => new FASymbolIconSource { Symbol = FASymbol.Video },
        ProfileEngine.GroupCpu    => new FAPathIconSource { Data = Cpu },
        ProfileEngine.GroupBoard  => new FAPathIconSource { Data = Board },
        ProfileEngine.GroupBios   => new FASymbolIconSource { Symbol = FASymbol.Setting },
        ProfileEngine.GroupSystem => new FASymbolIconSource { Symbol = FASymbol.Home },
        _                         => new FASymbolIconSource { Symbol = FASymbol.AllApps }
    };
}
