using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using SpecForge.Avalonia.UI;
using SpecForge.Avalonia.ViewModels;

namespace SpecForge.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    /// <summary>
    /// Ставится, пока код сам меняет выделение в навигации, чтобы событие
    /// SelectionChanged не гонялось обратно во ViewModel.
    /// </summary>
    private bool _syncingNav;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _vm;

        _vm.GroupsChanged += RebuildNavItems;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        // FANavigationView сворачивает панель при выборе пункта
        // (ClosePaneIfNecessaryAfterItemIsClicked). Закрытие отменяется,
        // чтобы панель оставалась раскрытой.
        //
        // Раньше здесь ещё был обработчик, который возвращал IsPaneOpen в
        // true при любом закрытии. Он вступал в цикл с внутренней логикой
        // NavigationView и съедал целое ядро процессора, поэтому убран:
        // отмены одного события достаточно.
        Nav.PaneClosing += OnPaneClosing;

        RebuildNavItems();
    }

    private void OnPaneClosing(object? sender, FANavigationViewPaneClosingEventArgs e) => e.Cancel = true;

    // ------------------------------------------------------- навигация -----

    /// <summary>
    /// Пункты меню строятся в коде, а не биндятся на MenuItemsSource.
    /// Так у каждого пункта появляется своя иконка, и SelectedItem не
    /// приходится прогонять через строковое свойство во ViewModel -
    /// у FANavigationView это источник известных проблем с привязкой.
    /// </summary>
    private void RebuildNavItems()
    {
        _syncingNav = true;

        Nav.MenuItems.Clear();
        foreach (var group in _vm.Groups)
        {
            Nav.MenuItems.Add(new FANavigationViewItem
            {
                Content = group,
                Tag = group,
                IconSource = GroupIcons.IconFor(group)
            });
        }

        var first = Nav.MenuItems.OfType<FANavigationViewItem>().FirstOrDefault();

        // Выделение ставим только если оно действительно меняется:
        // присвоение того же значения всё равно гоняет разметку.
        if (!ReferenceEquals(Nav.SelectedItem, first))
            Nav.SelectedItem = first;

        if (first?.Tag is string tag)
            _vm.SelectedGroup = tag;

        _syncingNav = false;
    }

    private void SyncNavSelection(string group)
    {
        var item = Nav.MenuItems.OfType<FANavigationViewItem>()
            .FirstOrDefault(i => Equals(i.Tag, group));

        if (item is null || ReferenceEquals(Nav.SelectedItem, item))
            return;

        _syncingNav = true;
        Nav.SelectedItem = item;
        _syncingNav = false;
    }

    private void OnNavSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (_syncingNav) return;

        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string group)
            _vm.SelectedGroup = group;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedGroup))
            SyncNavSelection(_vm.SelectedGroup);
    }
}
