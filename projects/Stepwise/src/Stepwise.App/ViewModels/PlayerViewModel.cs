using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Stepwise.App.Services;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;

namespace Stepwise.App.ViewModels;

/// <summary>
/// ViewModel для окна и страницы воспроизведения пошаговой интерактивной инструкции (Player).
/// Управляет привязкой данных к IPlayerEngine, навигацией по шагам, загрузкой скриншотов и UIA-телеметрией.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IPlayerEngine _playerEngine;
    private readonly IImageLoaderService _imageLoader;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly IOverlayService? _overlayService;
    private IProjectRepository? _repository;
    private string? _projectRootPath;
    private CancellationTokenSource? _previewCts;
    private bool _isDisposed;

    public string? ProjectRootPath
    {
        get => _projectRootPath;
        internal set => _projectRootPath = value;
    }

    public IOverlayService? OverlayService => _overlayService;

    internal CancellationTokenSource? ActivePreviewCts => _previewCts;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static int TryGetSystemMetric(int nIndex)
    {
        try
        {
            return GetSystemMetrics(nIndex);
        }
        catch
        {
            return 0;
        }
    }

    // --- Информационные свойства инструкции ---

    [ObservableProperty]
    private string _guideTitle = "Руководство";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuideDescriptionVisibility))]
    private string? _guideDescription;

    public Visibility GuideDescriptionVisibility =>
        !string.IsNullOrWhiteSpace(GuideDescription) ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepPositionText))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    private int _currentIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepPositionText))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    private int _totalSteps;

    public string StepPositionText =>
        TotalSteps > 0 && CurrentIndex >= 0
            ? $"Шаг {CurrentIndex + 1} из {TotalSteps}"
            : "Нет шагов";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStepTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentStepDescription))]
    [NotifyPropertyChangedFor(nameof(TargetElementName))]
    [NotifyPropertyChangedFor(nameof(TargetControlType))]
    [NotifyPropertyChangedFor(nameof(TargetAutomationId))]
    [NotifyPropertyChangedFor(nameof(TargetClassName))]
    [NotifyPropertyChangedFor(nameof(ProcessInfoText))]
    [NotifyPropertyChangedFor(nameof(TargetWindowTitle))]
    [NotifyPropertyChangedFor(nameof(BoundingBoxText))]
    [NotifyPropertyChangedFor(nameof(ClickPointText))]
    [NotifyPropertyChangedFor(nameof(HighlightLeft))]
    [NotifyPropertyChangedFor(nameof(HighlightTop))]
    [NotifyPropertyChangedFor(nameof(HighlightWidth))]
    [NotifyPropertyChangedFor(nameof(HighlightHeight))]
    [NotifyPropertyChangedFor(nameof(HasHighlight))]
    [NotifyPropertyChangedFor(nameof(HighlightVisibility))]
    [NotifyPropertyChangedFor(nameof(ClickPinLeft))]
    [NotifyPropertyChangedFor(nameof(ClickPinTop))]
    [NotifyPropertyChangedFor(nameof(HasClickPin))]
    [NotifyPropertyChangedFor(nameof(ClickPinVisibility))]
    private Step? _currentStep;

    public string CurrentStepTitle =>
        !string.IsNullOrWhiteSpace(CurrentStep?.Title)
            ? CurrentStep.Title
            : (CurrentStep != null ? $"Шаг {CurrentIndex + 1}" : "Без названия");

    public string CurrentStepDescription =>
        !string.IsNullOrWhiteSpace(CurrentStep?.Description)
            ? CurrentStep.Description
            : "Нет описания";

    // --- Состояние плеера ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerStateText))]
    [NotifyPropertyChangedFor(nameof(PlayerStateBrush))]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    [NotifyPropertyChangedFor(nameof(PlayPauseToolTip))]
    private PlayerState _playerState = PlayerState.Idle;

    public string PlayerStateText => GetStateText(PlayerState);

    public SolidColorBrush PlayerStateBrush => GetStateBrush(PlayerState);

    // --- UIA Телеметрия целевого элемента ---

    public string TargetElementName => CurrentStep?.TargetElement.Name ?? string.Empty;
    public string TargetControlType => CurrentStep?.TargetElement.ControlType ?? string.Empty;
    public string TargetAutomationId => CurrentStep?.TargetElement.AutomationId ?? string.Empty;
    public string TargetClassName => CurrentStep?.TargetElement.ClassName ?? string.Empty;

    public string ProcessInfoText => CurrentStep != null
        ? $"{CurrentStep.TargetElement.ProcessName} (PID: {CurrentStep.TargetElement.ProcessId})"
        : string.Empty;

    public string TargetWindowTitle => CurrentStep?.TargetElement.WindowTitle ?? string.Empty;

    public string BoundingBoxText => CurrentStep != null
        ? $"{CurrentStep.TargetElement.BoundingRectangle.X:F0}, {CurrentStep.TargetElement.BoundingRectangle.Y:F0} [{CurrentStep.TargetElement.BoundingRectangle.Width:F0}×{CurrentStep.TargetElement.BoundingRectangle.Height:F0}]"
        : string.Empty;

    public string ClickPointText => CurrentStep != null
        ? $"X: {CurrentStep.ClickX:F0}, Y: {CurrentStep.ClickY:F0}"
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetadataPanelVisibility))]
    private bool _isMetadataExpanded = false;

    public Visibility MetadataPanelVisibility =>
        IsMetadataExpanded ? Visibility.Visible : Visibility.Collapsed;

    // --- Рамка подсветки и Pin клика ---

    [ObservableProperty]
    private int _virtualScreenOriginX = TryGetSystemMetric(SM_XVIRTUALSCREEN);

    [ObservableProperty]
    private int _virtualScreenOriginY = TryGetSystemMetric(SM_YVIRTUALSCREEN);

    [ObservableProperty]
    private double _screenshotNaturalWidth = 1920;

    [ObservableProperty]
    private double _screenshotNaturalHeight = 1080;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHighlight))]
    [NotifyPropertyChangedFor(nameof(HighlightVisibility))]
    [NotifyPropertyChangedFor(nameof(HasClickPin))]
    [NotifyPropertyChangedFor(nameof(ClickPinVisibility))]
    private bool _showHighlightOverlay = false;

    partial void OnShowHighlightOverlayChanged(bool value)
    {
        try
        {
            if (value)
            {
                _overlayService?.Show();
                _overlayService?.UpdateTarget(CurrentStep);
            }
            else
            {
                _overlayService?.Hide();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay toggle sync error: {ex.Message}");
        }
    }

    public double HighlightLeft => CurrentStep != null ? (CurrentStep.TargetElement.BoundingRectangle.X - VirtualScreenOriginX) : 0;
    public double HighlightTop => CurrentStep != null ? (CurrentStep.TargetElement.BoundingRectangle.Y - VirtualScreenOriginY) : 0;
    public double HighlightWidth => CurrentStep?.TargetElement.BoundingRectangle.Width ?? 0;
    public double HighlightHeight => CurrentStep?.TargetElement.BoundingRectangle.Height ?? 0;

    public bool HasHighlight =>
        ShowHighlightOverlay &&
        CurrentStep != null &&
        CurrentStep.TargetElement.BoundingRectangle.Width > 0 &&
        CurrentStep.TargetElement.BoundingRectangle.Height > 0;

    public Visibility HighlightVisibility => HasHighlight ? Visibility.Visible : Visibility.Collapsed;

    public double ClickPinLeft => CurrentStep != null ? (CurrentStep.ClickX - VirtualScreenOriginX) - 9 : -9;
    public double ClickPinTop => CurrentStep != null ? (CurrentStep.ClickY - VirtualScreenOriginY) - 9 : -9;

    public bool HasClickPin =>
        ShowHighlightOverlay &&
        CurrentStep != null &&
        (CurrentStep.ClickX != 0 || CurrentStep.ClickY != 0);

    public Visibility ClickPinVisibility => HasClickPin ? Visibility.Visible : Visibility.Collapsed;

    // --- Превью скриншота ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewImageVisibility))]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewLoadingVisibility))]
    [NotifyPropertyChangedFor(nameof(PreviewImageVisibility))]
    private bool _isPreviewLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewErrorVisibility))]
    [NotifyPropertyChangedFor(nameof(PreviewImageVisibility))]
    private bool _isPreviewError;

    [ObservableProperty]
    private string? _previewErrorMessage;

    public Visibility EmptyStateVisibility => TotalSteps == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewLoadingVisibility => IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewErrorVisibility => IsPreviewError ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewImageVisibility =>
        (PreviewImage != null && !IsPreviewLoading && !IsPreviewError) ? Visibility.Visible : Visibility.Collapsed;

    // --- Состояния элементов навигации ---

    public bool CanNext => _playerEngine.CanNext;
    public bool CanPrevious => _playerEngine.CanPrevious;
    public bool CanFirst => _playerEngine.CanFirst;
    public bool CanLast => _playerEngine.CanLast;
    public bool CanRestart => _playerEngine.CanRestart;
    public bool CanPlay => _playerEngine.CanPlay;
    public bool CanPause => _playerEngine.CanPause;
    public bool CanPlayPause => _playerEngine.CanPlay || _playerEngine.CanPause;

    public string PlayPauseGlyph => PlayerState == PlayerState.Playing ? "\uE769" : "\uE768";
    public string PlayPauseToolTip => PlayerState == PlayerState.Playing ? "Пауза (Пробел)" : "Воспроизведение (Пробел)";

    /// <summary>
    /// Событие запроса закрытия окна плеера хост-компонентом.
    /// </summary>
    public event Action? RequestClose;

    public PlayerViewModel(
        IPlayerEngine playerEngine,
        IImageLoaderService imageLoader,
        IProjectRepository? repository = null,
        DispatcherQueue? dispatcherQueue = null,
        IOverlayService? overlayService = null)
    {
        _playerEngine = playerEngine ?? throw new ArgumentNullException(nameof(playerEngine));
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
        _repository = repository;
        _overlayService = overlayService;
        try
        {
            _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        }
        catch
        {
            _dispatcherQueue = null;
        }

        if (_repository != null && !string.IsNullOrWhiteSpace(_repository.ProjectRootPath))
        {
            _projectRootPath = _repository.ProjectRootPath;
            _guideTitle = Path.GetFileName(_repository.ProjectRootPath);
        }

        _playerEngine.StateChanged += OnPlayerEngineStateChanged;
        _playerEngine.StepChanged += OnPlayerEngineStepChanged;

        SyncFromEngine();
    }

    // --- Команды плеера ---

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next()
    {
        _playerEngine.Next();
    }

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private void Previous()
    {
        _playerEngine.Previous();
    }

    [RelayCommand(CanExecute = nameof(CanFirst))]
    private void First()
    {
        _playerEngine.First();
    }

    [RelayCommand(CanExecute = nameof(CanLast))]
    private void Last()
    {
        _playerEngine.Last();
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart()
    {
        _playerEngine.Restart();
    }

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private void PlayPause()
    {
        if (_playerEngine.State == PlayerState.Playing)
        {
            _playerEngine.Pause();
        }
        else
        {
            _playerEngine.Play();
        }
    }

    [RelayCommand]
    private void Close()
    {
        try
        {
            _overlayService?.Hide();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay hide on Close error: {ex.Message}");
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void RetryImageLoad()
    {
        _ = LoadPreviewForStepAsync(CurrentStep);
    }

    [RelayCommand]
    private void ToggleHighlightOverlay()
    {
        ShowHighlightOverlay = !ShowHighlightOverlay;
    }

    [RelayCommand]
    private void ToggleMetadata()
    {
        IsMetadataExpanded = !IsMetadataExpanded;
    }

    // --- Загрузка проекта и навигация ---

    public async Task InitializeAsync(string? projectPath = null)
    {
        try
        {
            var targetPath = !string.IsNullOrWhiteSpace(projectPath)
                ? projectPath
                : (!string.IsNullOrWhiteSpace(_repository?.ProjectRootPath)
                    ? _repository.ProjectRootPath
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Stepwise",
                        "DefaultProject"
                    ));

            var (project, steps, repo) = await Task.Run(() =>
            {
                if (_repository == null || _repository.ProjectRootPath != targetPath)
                {
                    _repository?.Dispose();
                    _repository = new ProjectRepository(targetPath);
                }

                var p = _repository.LoadProject();
                var s = _repository.LoadSteps();
                return (p, s, _repository);
            });

            _projectRootPath = targetPath;
            _repository = repo;

            RunOnUIThread(() =>
            {
                GuideTitle = project?.Name ?? (!string.IsNullOrWhiteSpace(targetPath) ? Path.GetFileName(targetPath) : "Руководство");
                if (string.IsNullOrWhiteSpace(GuideTitle))
                {
                    GuideTitle = "Руководство";
                }
                GuideDescription = project?.Description;

                var guideId = project?.Id ?? Guid.NewGuid();
                _playerEngine.LoadGuide(guideId, steps);

                SyncFromEngine();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Ошибка загрузки руководства: {ex.Message}");
            _playerEngine.Fail(ex.Message);
        }
    }

    public void RefreshVirtualScreenOrigin()
    {
        VirtualScreenOriginX = TryGetSystemMetric(SM_XVIRTUALSCREEN);
        VirtualScreenOriginY = TryGetSystemMetric(SM_YVIRTUALSCREEN);
        NotifyOverlayProperties();
    }

    private void OnPlayerEngineStateChanged(PlayerState oldState, PlayerState newState)
    {
        RunOnUIThread(() =>
        {
            PlayerState = newState;
            NotifyCommandsCanExecute();

            if (newState == PlayerState.Failed)
            {
                try
                {
                    _overlayService?.Hide();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay hide on Failed state error: {ex.Message}");
                }
            }
        });
    }

    private void OnPlayerEngineStepChanged(int newIndex, Step? newStep)
    {
        RunOnUIThread(() =>
        {
            CurrentIndex = newIndex;
            TotalSteps = _playerEngine.TotalSteps;
            CurrentStep = newStep;

            NotifyStepProperties();
            NotifyCommandsCanExecute();
            _ = LoadPreviewForStepAsync(newStep);

            try
            {
                _overlayService?.UpdateTarget(newStep);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay step update error: {ex.Message}");
            }
        });
    }

    private void SyncFromEngine()
    {
        CurrentIndex = _playerEngine.CurrentIndex;
        TotalSteps = _playerEngine.TotalSteps;
        CurrentStep = _playerEngine.CurrentStep;
        PlayerState = _playerEngine.State;

        NotifyStepProperties();
        NotifyCommandsCanExecute();
        _ = LoadPreviewForStepAsync(CurrentStep);

        try
        {
            _overlayService?.UpdateTarget(CurrentStep);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay initial sync error: {ex.Message}");
        }
    }

    private void NotifyStepProperties()
    {
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(TotalSteps));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(StepPositionText));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(TargetElementName));
        OnPropertyChanged(nameof(TargetControlType));
        OnPropertyChanged(nameof(TargetAutomationId));
        OnPropertyChanged(nameof(TargetClassName));
        OnPropertyChanged(nameof(ProcessInfoText));
        OnPropertyChanged(nameof(TargetWindowTitle));
        OnPropertyChanged(nameof(BoundingBoxText));
        OnPropertyChanged(nameof(ClickPointText));
        NotifyOverlayProperties();
    }

    private void NotifyOverlayProperties()
    {
        OnPropertyChanged(nameof(HighlightLeft));
        OnPropertyChanged(nameof(HighlightTop));
        OnPropertyChanged(nameof(HighlightWidth));
        OnPropertyChanged(nameof(HighlightHeight));
        OnPropertyChanged(nameof(HasHighlight));
        OnPropertyChanged(nameof(HighlightVisibility));

        OnPropertyChanged(nameof(ClickPinLeft));
        OnPropertyChanged(nameof(ClickPinTop));
        OnPropertyChanged(nameof(HasClickPin));
        OnPropertyChanged(nameof(ClickPinVisibility));
    }

    private void NotifyCommandsCanExecute()
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FirstCommand.NotifyCanExecuteChanged();
        LastCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(CanPrevious));
        OnPropertyChanged(nameof(CanFirst));
        OnPropertyChanged(nameof(CanLast));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanPlayPause));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(PlayPauseToolTip));
    }

    internal async Task LoadPreviewForStepAsync(Step? step)
    {
        try
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        if (step == null)
        {
            PreviewImage = null;
            IsPreviewLoading = false;
            IsPreviewError = false;
            PreviewErrorMessage = null;
            return;
        }

        try
        {
            IsPreviewLoading = true;
            IsPreviewError = false;
            PreviewErrorMessage = null;
            PreviewImage = null;

            var path = step.ScreenshotPath;
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(_projectRootPath))
            {
                path = Path.Combine(_projectRootPath, path);
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!ct.IsCancellationRequested && step == CurrentStep)
                {
                    IsPreviewLoading = false;
                    IsPreviewError = true;
                    PreviewErrorMessage = "Скриншот недоступен. Исходный файл не найден на диске.";
                }
                return;
            }

            var bitmap = await _imageLoader.LoadPreviewAsync(path, ct);
            if (ct.IsCancellationRequested || step != CurrentStep)
            {
                return;
            }

            if (bitmap == null)
            {
                if (!ct.IsCancellationRequested && step == CurrentStep)
                {
                    IsPreviewLoading = false;
                    IsPreviewError = true;
                    PreviewErrorMessage = "Не удалось декодировать скриншот. Файл поврежден или пуст.";
                }
            }
            else
            {
                if (!ct.IsCancellationRequested && step == CurrentStep)
                {
                    if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        ScreenshotNaturalWidth = bitmap.PixelWidth;
                        ScreenshotNaturalHeight = bitmap.PixelHeight;
                    }

                    PreviewImage = bitmap;
                    IsPreviewLoading = false;
                    IsPreviewError = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Отмена при быстром переключении шагов — штатное поведение
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested && step == CurrentStep)
            {
                IsPreviewLoading = false;
                IsPreviewError = true;
                PreviewErrorMessage = $"Ошибка загрузки: {ex.Message}";
            }
        }
    }

    private void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }

    public static string GetStateText(PlayerState state) => state switch
    {
        PlayerState.Idle => "Готов",
        PlayerState.Playing => "Воспроизведение",
        PlayerState.Paused => "Пауза",
        PlayerState.Completed => "Завершено",
        PlayerState.Failed => "Ошибка",
        _ => "Неизвестно"
    };

    public static SolidColorBrush GetStateBrush(PlayerState state)
    {
        try
        {
            return state switch
            {
                PlayerState.Idle => new SolidColorBrush(ColorHelper.FromArgb(255, 156, 163, 175)),       // Gray (#9CA3AF)
                PlayerState.Playing => new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129)),    // Green (#10B981)
                PlayerState.Paused => new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11)),      // Amber (#F59E0B)
                PlayerState.Completed => new SolidColorBrush(ColorHelper.FromArgb(255, 14, 165, 233)),   // Blue / Accent (#0EA5E9)
                PlayerState.Failed => new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)),       // Red (#EF4444)
                _ => new SolidColorBrush(ColorHelper.FromArgb(255, 156, 163, 175))
            };
        }
        catch
        {
            return null!;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _overlayService?.Hide();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerViewModel] Overlay hide on Dispose error: {ex.Message}");
        }

        _playerEngine.StateChanged -= OnPlayerEngineStateChanged;
        _playerEngine.StepChanged -= OnPlayerEngineStepChanged;

        try
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        _previewCts = null;

        _repository?.Dispose();
    }
}
