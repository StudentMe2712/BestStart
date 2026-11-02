using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecondSelf.Core.Models;
using SecondSelf.Core.Services;

namespace SecondSelf.Core.ViewModels;

/// <summary>
/// ViewModel пятого раздела Dashboard — Settings (Настройки приложения).
/// Управляет локальной библиотекой видео Daily Reset, настройками весов формулы прогресса (10%..50%),
/// параметрами закрепления и прозрачности виджета, автозапуском Windows и резервным копированием SQLite.
/// </summary>
public partial class SettingsTabViewModel : ObservableObject
{
    private readonly IVideoLibraryService _videoLibraryService;
    private readonly IFisherYatesShuffleService _shuffleService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAutostartService? _autostartService;
    private readonly IBackupService _backupService;

    // --- Video Library ---
    public ObservableCollection<VideoLibraryItemViewModel> Videos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalVideosText))]
    private int _totalVideosCount;

    public string TotalVideosText => $"Всего видеофайлов в ротации: {TotalVideosCount}";

    // --- Progress Weights ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DailyWeightText))]
    private double _dailyWeight = 30.0; // в процентах (10..50)

    public string DailyWeightText => $"Ритуалы дня: {DailyWeight:F0}%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeeklyWeightText))]
    private double _weeklyWeight = 70.0; // в процентах (100 - dailyWeight)

    public string WeeklyWeightText => $"Цели недели: {WeeklyWeight:F0}%";

    // --- Widget Settings ---
    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private bool _autoCollapse = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlassOpacityText))]
    private double _glassOpacity = 85.0; // в процентах (30..80)

    public string GlassOpacityText => $"Прозрачность тёплого стекла: {GlassOpacity:F0}%";

    [ObservableProperty]
    private string _preferredMonitor = "Primary";

    // --- System & Autostart ---
    [ObservableProperty]
    private bool _isAutostartEnabled;

    // --- Backups ---
    public ObservableCollection<BackupFileInfo> Backups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastBackupDisplayText))]
    private string? _lastBackupTime;

    public string LastBackupDisplayText => $"Последний бэкап: {LastBackupTime ?? "Нет"}";

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsTabViewModel(
        IVideoLibraryService videoLibraryService,
        IFisherYatesShuffleService shuffleService,
        IAppSettingsService appSettingsService,
        IBackupService backupService,
        IAutostartService? autostartService = null)
    {
        _videoLibraryService = videoLibraryService ?? throw new ArgumentNullException(nameof(videoLibraryService));
        _shuffleService = shuffleService ?? throw new ArgumentNullException(nameof(shuffleService));
        _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _autostartService = autostartService;
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        // 1. Настройки приложения
        var settings = await _appSettingsService.GetSettingsAsync();
        DailyWeight = Math.Round(settings.DailyWeight * 100.0, 1);
        WeeklyWeight = Math.Round(settings.WeeklyWeight * 100.0, 1);
        AlwaysOnTop = settings.AlwaysOnTop;
        AutoCollapse = settings.AutoCollapse;
        GlassOpacity = Math.Round(settings.GlassOpacity * 100.0, 1);
        PreferredMonitor = settings.PreferredMonitor;

        // 2. Библиотека видео
        await ReloadVideosAsync();

        // 3. Автозапуск
        if (_autostartService != null)
        {
            IsAutostartEnabled = _autostartService.IsAutostartEnabled();
        }

        // 4. Бэкапы
        await ReloadBackupsAsync();
    }

    public async Task ReloadVideosAsync()
    {
        var items = await _videoLibraryService.GetAllVideosAsync();
        Videos.Clear();
        foreach (var item in items)
        {
            Videos.Add(new VideoLibraryItemViewModel(item));
        }
        TotalVideosCount = Videos.Count;
    }

    public async Task ReloadBackupsAsync()
    {
        var backupFiles = await _backupService.GetBackupsAsync();
        Backups.Clear();
        foreach (var b in backupFiles)
        {
            Backups.Add(b);
        }

        if (Backups.Count > 0)
        {
            LastBackupTime = Backups[0].CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            LastBackupTime = "Нет резервных копий";
        }
    }

    [RelayCommand]
    public async Task RescanLibraryAsync()
    {
        StatusMessage = "Сканирование видеотеки...";
        await _videoLibraryService.ScanLibraryAsync();
        await ReloadVideosAsync();
        StatusMessage = $"Сканирование завершено: найдено {TotalVideosCount} видео";
    }

    [RelayCommand]
    public async Task ShuffleQueueAsync()
    {
        await _shuffleService.ResetQueueAsync();
        StatusMessage = "Очередь воспроизведения перетасована";
    }

    [RelayCommand]
    public async Task AddVideoFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            StatusMessage = "Указанный видеофайл не найден на диске";
            return;
        }

        var destinationFolder = _videoLibraryService.VideosDirectory;
        Directory.CreateDirectory(destinationFolder);

        var destPath = Path.Combine(destinationFolder, Path.GetFileName(filePath));
        if (!string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(filePath, destPath, overwrite: true);
        }

        await RescanLibraryAsync();
    }

    [RelayCommand]
    public async Task UpdateDailyWeightAsync(double newDailyPct)
    {
        double clampedPct = Math.Clamp(newDailyPct, 10.0, 50.0);
        DailyWeight = clampedPct;
        WeeklyWeight = 100.0 - clampedPct;

        await _appSettingsService.UpdateWeightsAsync(clampedPct / 100.0);
        StatusMessage = $"Веса обновлены: {DailyWeight:F0}% Daily / {WeeklyWeight:F0}% Weekly";
    }

    [RelayCommand]
    public async Task SaveWidgetSettingsAsync()
    {
        var settings = await _appSettingsService.GetSettingsAsync();
        settings.AlwaysOnTop = AlwaysOnTop;
        settings.AutoCollapse = AutoCollapse;
        settings.GlassOpacity = Math.Clamp(GlassOpacity / 100.0, 0.30, 0.85);
        settings.PreferredMonitor = PreferredMonitor;

        await _appSettingsService.SaveSettingsAsync(settings);
        StatusMessage = "Настройки виджета сохранены";
    }

    [RelayCommand]
    public void ToggleAutostart(bool enable)
    {
        if (_autostartService == null) return;
        _autostartService.SetAutostart(enable);
        IsAutostartEnabled = _autostartService.IsAutostartEnabled();
        StatusMessage = IsAutostartEnabled ? "Автозапуск включен" : "Автозапуск отключен";
    }

    [RelayCommand]
    public async Task CreateBackupNowAsync()
    {
        StatusMessage = "Создание резервной копии базы данных...";
        var backupPath = await _backupService.CreateBackupAsync();
        await ReloadBackupsAsync();
        StatusMessage = $"Бэкап создан: {Path.GetFileName(backupPath)}";
    }

    [RelayCommand]
    public async Task RestoreBackupAsync(string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        StatusMessage = $"Восстановление базы из {Path.GetFileName(backupPath)}...";
        await _backupService.RestoreBackupAsync(backupPath);
        StatusMessage = "База данных успешно восстановлена!";
    }
}
