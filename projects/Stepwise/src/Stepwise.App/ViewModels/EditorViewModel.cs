using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Stepwise.App.Services;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Storage.Repositories;

namespace Stepwise.App.ViewModels;

/// <summary>
/// ViewModel для 3-панельного редактора руководств (Phase 3 UI Contract).
/// Полностью управляет UI-состоянием шагов, превью и метаданных.
/// </summary>
public sealed partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly IImageLoaderService _imageLoader;
    private IProjectRepository? _repository;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _thumbnailsCts;

    public ObservableCollection<StepItemViewModel> Steps { get; } = new();

    [ObservableProperty]
    private StepItemViewModel? _selectedStep;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private bool _isPreviewLoading;

    [ObservableProperty]
    private bool _isPreviewError;

    [ObservableProperty]
    private string? _previewErrorMessage;

    [ObservableProperty]
    private string _projectName = "Stepwise Guide";

    [ObservableProperty]
    private string _projectPath = string.Empty;

    [ObservableProperty]
    private int _stepCount;

    [ObservableProperty]
    private bool _hasSteps;

    [ObservableProperty]
    private bool _hasSelectedStep;

    [ObservableProperty]
    private bool? _showHighlightOverlay = true;

    [ObservableProperty]
    private double _screenshotNaturalWidth = 1920;

    [ObservableProperty]
    private double _screenshotNaturalHeight = 1080;

    // UI Visibilities
    public Visibility EmptyStateVisibility => (!HasSelectedStep || !HasSteps) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewLoadingVisibility => IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewErrorVisibility => IsPreviewError ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewImageVisibility => (PreviewImage != null && !IsPreviewLoading && !IsPreviewError) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PropertiesVisibility => HasSelectedStep ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HighlightVisibility => HasHighlight ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ClickPinVisibility => HasClickPin ? Visibility.Visible : Visibility.Collapsed;

    // Overlay Coordinates
    public double HighlightLeft => SelectedStep?.BoundingRectangle.X ?? 0;
    public double HighlightTop => SelectedStep?.BoundingRectangle.Y ?? 0;
    public double HighlightWidth => SelectedStep?.BoundingRectangle.Width ?? 0;
    public double HighlightHeight => SelectedStep?.BoundingRectangle.Height ?? 0;
    public bool HasHighlight => (ShowHighlightOverlay == true) && SelectedStep != null && SelectedStep.BoundingRectangle.Width > 0 && SelectedStep.BoundingRectangle.Height > 0;

    public double ClickPinLeft => (SelectedStep?.ClickX ?? 0) - 9;
    public double ClickPinTop => (SelectedStep?.ClickY ?? 0) - 9;
    public bool HasClickPin => (ShowHighlightOverlay == true) && SelectedStep != null && (SelectedStep.ClickX > 0 || SelectedStep.ClickY > 0);

    // Selected Step Binding Properties
    public string SelectedStepTitle => SelectedStep?.Title ?? "Шаг не выбран";

    public string CurrentStepTitle
    {
        get => SelectedStep?.Title ?? string.Empty;
        set
        {
            if (SelectedStep != null && SelectedStep.Title != value)
            {
                SelectedStep.Title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedStepTitle));
            }
        }
    }

    public string CurrentStepDescription
    {
        get => SelectedStep?.Description ?? string.Empty;
        set
        {
            if (SelectedStep != null && SelectedStep.Description != value)
            {
                SelectedStep.Description = value;
                OnPropertyChanged();
            }
        }
    }

    // Telemetry display helpers
    public string TargetControlType => SelectedStep?.TargetElement.ControlType ?? string.Empty;
    public string TargetElementName => SelectedStep?.TargetElement.Name ?? string.Empty;
    public string TargetAutomationId => SelectedStep?.TargetElement.AutomationId ?? string.Empty;
    public string TargetClassName => SelectedStep?.TargetElement.ClassName ?? string.Empty;
    public string TargetWindowTitle => SelectedStep?.TargetElement.WindowTitle ?? string.Empty;

    public string ProcessInfoText => SelectedStep != null
        ? $"{SelectedStep.TargetElement.ProcessName} (PID: {SelectedStep.TargetElement.ProcessId})"
        : string.Empty;

    public string WindowHandleText => SelectedStep != null
        ? $"0x{SelectedStep.TargetElement.WindowHandle:X8}"
        : string.Empty;

    public string BoundingBoxText => SelectedStep != null
        ? $"{SelectedStep.BoundingRectangle.X:F0}, {SelectedStep.BoundingRectangle.Y:F0} [{SelectedStep.BoundingRectangle.Width:F0}×{SelectedStep.BoundingRectangle.Height:F0}]"
        : string.Empty;

    public string ClickPointText => SelectedStep != null
        ? $"X: {SelectedStep.ClickX:F0}, Y: {SelectedStep.ClickY:F0}"
        : string.Empty;

    public EditorViewModel(IImageLoaderService imageLoader, IProjectRepository? repository = null)
    {
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
        _repository = repository;
        if (_repository != null && !string.IsNullOrWhiteSpace(_repository.ProjectRootPath))
        {
            _projectPath = _repository.ProjectRootPath;
            _projectName = Path.GetFileName(_repository.ProjectRootPath);
        }
    }

    partial void OnSelectedStepChanged(StepItemViewModel? oldValue, StepItemViewModel? newValue)
    {
        // Отменяем предыдущую задачу загрузки скриншота без вызова Dispose() во избежание ObjectDisposedException в фоне
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();

        HasSelectedStep = newValue != null;

        NotifyOverlayChanged();
        NotifyTelemetryChanged();
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(PropertiesVisibility));
        OnPropertyChanged(nameof(SelectedStepTitle));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));

        if (newValue == null)
        {
            PreviewImage = null;
            IsPreviewLoading = false;
            IsPreviewError = false;
            PreviewErrorMessage = null;
            OnPropertyChanged(nameof(PreviewImageVisibility));
            OnPropertyChanged(nameof(PreviewLoadingVisibility));
            OnPropertyChanged(nameof(PreviewErrorVisibility));
            return;
        }

        _ = LoadPreviewForStepAsync(newValue, _previewCts.Token);
    }

    partial void OnPreviewImageChanged(BitmapImage? value)
    {
        OnPropertyChanged(nameof(PreviewImageVisibility));
    }

    partial void OnIsPreviewLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewLoadingVisibility));
        OnPropertyChanged(nameof(PreviewImageVisibility));
    }

    partial void OnIsPreviewErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewErrorVisibility));
        OnPropertyChanged(nameof(PreviewImageVisibility));
    }

    partial void OnHasStepsChanged(bool value)
    {
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    partial void OnHasSelectedStepChanged(bool value)
    {
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(PropertiesVisibility));
    }

    partial void OnShowHighlightOverlayChanged(bool? value)
    {
        NotifyOverlayChanged();
    }

    private void NotifyOverlayChanged()
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

    private void NotifyTelemetryChanged()
    {
        OnPropertyChanged(nameof(TargetControlType));
        OnPropertyChanged(nameof(TargetElementName));
        OnPropertyChanged(nameof(TargetAutomationId));
        OnPropertyChanged(nameof(TargetClassName));
        OnPropertyChanged(nameof(TargetWindowTitle));
        OnPropertyChanged(nameof(ProcessInfoText));
        OnPropertyChanged(nameof(WindowHandleText));
        OnPropertyChanged(nameof(BoundingBoxText));
        OnPropertyChanged(nameof(ClickPointText));
    }

    private async Task LoadPreviewForStepAsync(StepItemViewModel step, CancellationToken ct)
    {
        try
        {
            IsPreviewLoading = true;
            IsPreviewError = false;
            PreviewErrorMessage = null;
            PreviewImage = null;

            var path = step.FullScreenshotPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!ct.IsCancellationRequested)
                {
                    IsPreviewLoading = false;
                    IsPreviewError = true;
                    PreviewErrorMessage = "Скриншот недоступен. Исходный файл не найден на диске.";
                }
                return;
            }

            var bitmap = await _imageLoader.LoadPreviewAsync(path, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (bitmap == null)
            {
                IsPreviewLoading = false;
                IsPreviewError = true;
                PreviewErrorMessage = "Не удалось декодировать скриншот. Файл поврежден или пуст.";
            }
            else
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
        catch (OperationCanceledException)
        {
            // Отмена при быстром переключении шагов — штатное поведение
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                IsPreviewLoading = false;
                IsPreviewError = true;
                PreviewErrorMessage = $"Ошибка загрузки: {ex.Message}";
            }
        }
    }

    public async Task InitializeDefaultProjectAsync()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultPath = Path.Combine(appDataDir, "Stepwise", "DefaultProject");

        await LoadProjectAsync(defaultPath);
    }

    public async Task LoadProjectAsync(string projectRootPath)
    {
        try
        {
            _thumbnailsCts?.Cancel();
            _thumbnailsCts = new CancellationTokenSource();

            // Чтение SQLite строго в фоновом потоке Task.Run (Раздел 11 specs/spec.md)
            var (project, steps, repo) = await Task.Run(() =>
            {
                if (_repository == null || _repository.ProjectRootPath != projectRootPath)
                {
                    _repository?.Dispose();
                    _repository = new ProjectRepository(projectRootPath);
                }

                var p = _repository.LoadProject();
                var s = _repository.LoadSteps();
                return (p, s, _repository);
            });

            // Обновление UI коллекций строго на UI-потоке
            ProjectPath = projectRootPath;
            ProjectName = project?.Name ?? Path.GetFileName(projectRootPath);

            Steps.Clear();
            foreach (var step in steps)
            {
                var vm = new StepItemViewModel(step, projectRootPath, repo);
                Steps.Add(vm);
            }

            StepCount = Steps.Count;
            HasSteps = Steps.Count > 0;

            if (Steps.Count > 0)
            {
                SelectedStep = Steps[0];
                _ = LoadThumbnailsBackgroundAsync(_thumbnailsCts.Token);
            }
            else
            {
                SelectedStep = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditorViewModel] Ошибка загрузки проекта: {ex.Message}");
        }
    }

    private async Task LoadThumbnailsBackgroundAsync(CancellationToken ct = default)
    {
        foreach (var stepVm in Steps.ToList())
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await stepVm.LoadThumbnailAsync(_imageLoader, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Игнорируем ошибку единичной миниатюры
            }
        }
    }

    /// <summary>
    /// Добавляет вновь записанный шаг в сессию редактора (вызывается из UI-потока при получении события от IRecordingEngine).
    /// </summary>
    public void AddStep(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var vm = new StepItemViewModel(step, ProjectPath, _repository);
        Steps.Add(vm);
        StepCount = Steps.Count;
        HasSteps = Steps.Count > 0;

        if (SelectedStep == null)
        {
            SelectedStep = vm;
        }

        _ = vm.LoadThumbnailAsync(_imageLoader);
    }

    [RelayCommand]
    private void RetryLoadPreview()
    {
        if (SelectedStep != null)
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            _ = LoadPreviewForStepAsync(SelectedStep, _previewCts.Token);
        }
    }

    [RelayCommand]
    private void SelectNextStep()
    {
        if (SelectedStep == null || Steps.Count == 0) return;
        var idx = Steps.IndexOf(SelectedStep);
        if (idx < Steps.Count - 1)
        {
            SelectedStep = Steps[idx + 1];
        }
    }

    [RelayCommand]
    private void SelectPreviousStep()
    {
        if (SelectedStep == null || Steps.Count == 0) return;
        var idx = Steps.IndexOf(SelectedStep);
        if (idx > 0)
        {
            SelectedStep = Steps[idx - 1];
        }
    }

    [RelayCommand]
    private void DeleteStep()
    {
        if (SelectedStep == null) return;
        var idx = Steps.IndexOf(SelectedStep);
        var toRemove = SelectedStep;

        if (Steps.Count > 1)
        {
            SelectedStep = idx > 0 ? Steps[idx - 1] : Steps[idx + 1];
        }
        else
        {
            SelectedStep = null;
        }

        Steps.Remove(toRemove);
        StepCount = Steps.Count;
        HasSteps = Steps.Count > 0;
    }

    [RelayCommand]
    private void ToggleHighlightOverlay()
    {
        ShowHighlightOverlay = !ShowHighlightOverlay;
    }

    public void Dispose()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _thumbnailsCts?.Cancel();
        _thumbnailsCts?.Dispose();
        _repository?.Dispose();
    }
}
