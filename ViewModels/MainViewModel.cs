using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using SpecForge.Core;

namespace SpecForge.Avalonia.ViewModels;

/// <summary>
/// Состояние окна: список групп, видимые поля и три команды.
///
/// Отличия от WPF-версии ровно два:
///   * RelayCommand взят из CommunityToolkit.Mvvm - в самом Avalonia своего
///     ICommand нет, а в WPF-версии был свой RelayCommand на ICommand;
///   * MainViewModel подписан на изменения полей, чтобы счётчик «изменено N»
///     в заголовке обновлялся сразу при вводе, а не только при смене группы.
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProfileDocument _profile = ProfileStore.LoadProfile();
    private List<FieldSpec> _specs = new();
    private string _selectedGroup = string.Empty;
    private string _status = "Готово";
    private string _groupSubtitle = string.Empty;
    private HardwareTemplate? _selectedTemplate;
    private MemoryOption? _selectedMemory;

    public MainViewModel()
    {
        Reload();
        ApplyCommand = new RelayCommand(Apply);
        RevertCommand = new RelayCommand(Revert);
        RefreshCommand = new RelayCommand(Reload);
    }

    public ObservableCollection<string> Groups { get; } = new();

    /// <summary>Основные поля группы.</summary>
    public ObservableCollection<FieldViewModel> VisibleFields { get; } = new();

    /// <summary>
    /// Дублирующие ключи. Показываются свёрнутым списком: у видеокарты
    /// их обычно четыре, и по отдельности они только шумят.
    /// </summary>
    public ObservableCollection<FieldViewModel> SecondaryFields { get; } = new();

    /// <summary>Модели, доступные для подстановки в текущей группе.</summary>
    public ObservableCollection<HardwareTemplate> Templates { get; } = new();

    /// <summary>Объёмы памяти. Заполняется только для видеокарт.</summary>
    public ObservableCollection<MemoryOption> MemoryOptions { get; } = new();

    private ObservableCollection<FieldViewModel> All { get; } = new();

    public RelayCommand ApplyCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand RefreshCommand { get; }

    /// <summary>
    /// Выбор модели сразу заполняет поля - отдельная кнопка не нужна.
    /// </summary>
    public HardwareTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate == value) return;
            _selectedTemplate = value;
            OnPropertyChanged();
            if (value is not null) ApplyTemplate();
        }
    }

    public bool HasTemplates => Templates.Count > 0;

    public bool HasMemoryOptions => MemoryOptions.Count > 0;

    public bool HasSecondary => SecondaryFields.Count > 0;

    /// <summary>
    /// Объём памяти, выбранный отдельным списком. Если он не выбран,
    /// берётся память из выбранной модели.
    /// </summary>
    public MemoryOption? SelectedMemory
    {
        get => _selectedMemory;
        set
        {
            if (_selectedMemory == value) return;
            _selectedMemory = value;
            OnPropertyChanged();
            if (value is not null) ApplyMemory();
        }
    }

    public bool AutoStartEnabled
    {
        get => _profile.AutoStart;
        set { if (_profile.AutoStart == value) return; _profile.AutoStart = value; SaveProfile(); }
    }

    public bool AutoRevertEnabled
    {
        get => _profile.AutoRevert;
        set { if (_profile.AutoRevert == value) return; _profile.AutoRevert = value; SaveProfile(); }
    }

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup == value) return;
            _selectedGroup = value;
            OnPropertyChanged();
            RefreshVisible();
        }
    }

    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    public string GroupSubtitle { get => _groupSubtitle; private set { _groupSubtitle = value; OnPropertyChanged(); } }

    public int ChangedCount => All.Count(f => f.IsChanged);

    public void Reload()
    {
        _specs = ProfileEngine.Discover().ToList();

        All.Clear();
        foreach (var spec in _specs)
        {
            var vm = new FieldViewModel(spec, _profile[spec.Id]);
            vm.PropertyChanged += OnFieldChanged;
            All.Add(vm);
        }

        Groups.Clear();
        foreach (var group in _specs.Select(s => s.Group).Distinct())
            Groups.Add(group);

        if (Groups.Count > 0 && !Groups.Contains(_selectedGroup))
            SelectedGroup = Groups[0];

        OnPropertyChanged(nameof(Groups));

        // Один сигнал после полной пересборки. Раньше окно подписывалось на
        // CollectionChanged и перестраивало меню на каждое добавление -
        // при Apply это десятки перестроений панели подряд.
        GroupsChanged?.Invoke();

        RefreshVisible();
    }

    /// <summary>Список групп пересобран целиком.</summary>
    public event Action? GroupsChanged;

    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FieldViewModel.IsChanged) or null)
            OnPropertyChanged(nameof(ChangedCount));
    }

    private void RefreshVisible()
    {
        VisibleFields.Clear();
        SecondaryFields.Clear();
        foreach (var field in All.Where(f => f.Group == _selectedGroup))
        {
            if (field.Secondary) SecondaryFields.Add(field);
            else VisibleFields.Add(field);
        }

        Templates.Clear();
        foreach (var template in HardwareTemplates.ForGroup(_selectedGroup))
            Templates.Add(template);
        SelectedTemplate = null;

        MemoryOptions.Clear();
        if (HardwareTemplates.HasMemoryField(_selectedGroup))
        {
            foreach (var gb in HardwareTemplates.MemoryGb)
                MemoryOptions.Add(new MemoryOption(gb));
        }
        SelectedMemory = null;

        GroupSubtitle = Describe(_selectedGroup);
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(HasTemplates));
        OnPropertyChanged(nameof(HasMemoryOptions));
        OnPropertyChanged(nameof(HasSecondary));
    }

    private static string Describe(string group) => group switch
    {
        ProfileEngine.GroupGpu =>
            "Имя и объём памяти видеокарты. Ключевое поле - то, что читают Диспетчер устройств, WMI и Диспетчер задач.",
        ProfileEngine.GroupCpu =>
            "Имя процессора из HKLM\\HARDWARE. Пересобирается при загрузке - нужен автозапуск.",
        ProfileEngine.GroupBoard => "Из SMBIOS через ветку HKLM\\HARDWARE\\DESCRIPTION\\System\\BIOS.",
        ProfileEngine.GroupBios => "Производитель и версия прошивки.",
        ProfileEngine.GroupSystem => "Общие сведения о системе из SMBIOS.",
        _ => string.Empty
    };

    private void SaveProfile() => ProfileStore.SaveProfile(_profile);

    /// <summary>
    /// Раскладывает выбранную модель по полям группы.
    ///
    /// Поле заполняется, если имя его параметра в реестре есть в шаблоне.
    /// Благодаря этому у платы производитель попадает в «Производитель
    /// платы», а модель - в «Модель платы», а не обе строки в каждое поле.
    ///
    /// Объём памяти идёт в единственное числовое поле группы.
    ///
    /// В реестр ничего не пишется - это только заполнение полей.
    /// Запись происходит отдельно, по кнопке «Применить».
    /// </summary>
    private void ApplyTemplate()
    {
        if (SelectedTemplate is not { } template) return;

        // Явно выбранный объём важнее того, что прописано у модели.
        uint? memoryGb = SelectedMemory?.Gigabytes ?? template.MemoryGb;

        foreach (var field in All.Where(f => f.Group == _selectedGroup))
        {
            if (field.Kind == FieldKind.UInt64)
            {
                // В поле объём памяти вводится в гигабайтах - ровно так же,
                // как его подписывает заголовок карточки. В байты пересчёт
                // делается уже при записи в реестр.
                if (memoryGb is { } gb) field.Value = gb.ToString();
            }
            else if (template.Values.TryGetValue(field.RegistryName, out var value))
            {
                field.Value = value;
            }
        }

        Status = $"Подставлен шаблон «{template.Label}». Нажмите «Применить».";
    }

    /// <summary>Выбор объёма меняет только память, название не трогает.</summary>
    private void ApplyMemory()
    {
        if (SelectedMemory is not { } memory) return;

        foreach (var field in All.Where(f =>
                     f.Group == _selectedGroup && f.Kind == FieldKind.UInt64))
        {
            field.Value = memory.Gigabytes.ToString();
        }

        Status = $"Подставлен объём {memory}. Нажмите «Применить».";
    }

    private void Apply()
    {
        foreach (var field in All)
            _profile[field.Id] = field.Value;

        SaveProfile();

        AutoStart.Sync(_profile.AutoStart);

        var report = ProfileEngine.Apply(_profile, _specs);

        var snapshot = ProfileStore.LoadSnapshot();
        var saved = snapshot.Registry.Count + snapshot.Inf.Count;

        Status = report.AnyFailed
            ? $"Применено {report.Applied}, с ошибками {report.Failed}. Подробности - в логе."
            : $"Применено значений: {report.Applied}" +
              (saved > 0 ? $" · снимок оригинала: {saved}" : string.Empty);

        // Диспетчер задач берёт имя видеокарты из INF-файла драйвера, а тот
        // читается при загрузке. Без этого сообщения выглядит так, будто
        // ничего не применилось.
        if (report.NeedsReboot)
            Status += " · нужна ПЕРЕЗАГРУЗКА, иначе Диспетчер задач покажет старое имя";

        Reload();
    }

    private void Revert()
    {
        var report = ProfileEngine.Revert();
        _profile.Clear();
        SaveProfile();
        Status = report.Applied > 0
            ? $"Возвращено значений: {report.Applied}"
            : "Снимок оригинала пуст - возвращать нечего";
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
