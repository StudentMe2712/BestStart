using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    private readonly IGuideExportService? _exportService;
    private readonly IGroqAIService? _groqAiService;
    private readonly IFileDialogService? _fileDialogService;
    private IProjectRepository? _repository;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _thumbnailsCts;

    [ObservableProperty]
    private bool _isAiBusy;

    [ObservableProperty]
    private string? _aiStatusMessage;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string? _exportStatusMessage;

    [ObservableProperty]
    private string _groqApiKey = string.Empty;

    [ObservableProperty]
    private string _selectedAiModel = "openai/gpt-oss-120b";

    public IReadOnlyList<string> AvailableAiModels { get; } = new[]
    {
        "openai/gpt-oss-120b",
        "qwen/qwen3.8-27b",
        "llama-3.3-70b-versatile"
    };

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

    [ObservableProperty]
    private int _virtualScreenOriginX = TryGetSystemMetric(SM_XVIRTUALSCREEN);

    [ObservableProperty]
    private int _virtualScreenOriginY = TryGetSystemMetric(SM_YVIRTUALSCREEN);

    // UI Visibilities
    public Visibility EmptyStateVisibility => (!HasSelectedStep || !HasSteps) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewLoadingVisibility => IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewErrorVisibility => IsPreviewError ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewImageVisibility => (PreviewImage != null && !IsPreviewLoading && !IsPreviewError) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PropertiesVisibility => HasSelectedStep ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HighlightVisibility => HasHighlight ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ClickPinVisibility => HasClickPin ? Visibility.Visible : Visibility.Collapsed;

    // Overlay Coordinates (offset by VirtualScreenOrigin for multi-monitor alignment)
    public double HighlightLeft => SelectedStep != null ? (SelectedStep.BoundingRectangle.X - VirtualScreenOriginX) : 0;
    public double HighlightTop => SelectedStep != null ? (SelectedStep.BoundingRectangle.Y - VirtualScreenOriginY) : 0;
    public double HighlightWidth => SelectedStep?.BoundingRectangle.Width ?? 0;
    public double HighlightHeight => SelectedStep?.BoundingRectangle.Height ?? 0;
    public bool HasHighlight => (ShowHighlightOverlay == true) && SelectedStep != null && SelectedStep.BoundingRectangle.Width > 0 && SelectedStep.BoundingRectangle.Height > 0;

    public double ClickPinLeft => SelectedStep != null ? (SelectedStep.ClickX - VirtualScreenOriginX) - 9 : -9;
    public double ClickPinTop => SelectedStep != null ? (SelectedStep.ClickY - VirtualScreenOriginY) - 9 : -9;
    public bool HasClickPin => (ShowHighlightOverlay == true) && SelectedStep != null && (SelectedStep.ClickX != 0 || SelectedStep.ClickY != 0);

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

    public EditorViewModel(
        IImageLoaderService imageLoader,
        IProjectRepository? repository = null,
        IGuideExportService? exportService = null,
        IGroqAIService? groqAiService = null,
        IFileDialogService? fileDialogService = null)
    {
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
        _repository = repository;
        _exportService = exportService;
        _groqAiService = groqAiService;
        _fileDialogService = fileDialogService;

        if (_groqAiService != null)
        {
            _groqApiKey = _groqAiService.StoredApiKey ?? string.Empty;
            _selectedAiModel = _groqAiService.SelectedModel ?? "openai/gpt-oss-120b";
        }

        if (_repository != null && !string.IsNullOrWhiteSpace(_repository.ProjectRootPath))
        {
            _projectPath = _repository.ProjectRootPath;
            _projectName = Path.GetFileName(_repository.ProjectRootPath);
        }
    }

    partial void OnGroqApiKeyChanged(string value)
    {
        if (_groqAiService != null)
        {
            _groqAiService.StoredApiKey = value;
        }
    }

    partial void OnSelectedAiModelChanged(string value)
    {
        if (_groqAiService != null)
        {
            _groqAiService.SelectedModel = value;
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

    partial void OnVirtualScreenOriginXChanged(int value)
    {
        NotifyOverlayChanged();
    }

    partial void OnVirtualScreenOriginYChanged(int value)
    {
        NotifyOverlayChanged();
    }

    public void RefreshVirtualScreenOrigin()
    {
        VirtualScreenOriginX = TryGetSystemMetric(SM_XVIRTUALSCREEN);
        VirtualScreenOriginY = TryGetSystemMetric(SM_YVIRTUALSCREEN);
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
        var defaultPath = !string.IsNullOrWhiteSpace(_repository?.ProjectRootPath)
            ? _repository.ProjectRootPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stepwise",
                "DefaultProject"
            );

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

    [RelayCommand]
    public async Task EnhanceStepAiAsync()
    {
        if (SelectedStep == null || _groqAiService == null) return;

        try
        {
            IsAiBusy = true;
            AiStatusMessage = "Генерация описания шага через Groq AI...";

            var result = await _groqAiService.EnhanceStepAsync(SelectedStep.Step, GroqApiKey, SelectedAiModel);
            if (result.Success)
            {
                CurrentStepTitle = result.Title;
                CurrentStepDescription = result.Description;
                AiStatusMessage = "Шаг успешно улучшен нейросетью!";
            }
            else
            {
                AiStatusMessage = $"Ошибка AI: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            AiStatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    public async Task EnhanceAllStepsAiAsync()
    {
        if (Steps.Count == 0 || _groqAiService == null) return;

        try
        {
            IsAiBusy = true;
            AiStatusMessage = $"Улучшение {Steps.Count} шагов через Groq AI...";

            var rawSteps = Steps.Select(s => s.Step).ToList();
            var enhanced = await _groqAiService.EnhanceAllStepsAsync(
                rawSteps,
                GroqApiKey,
                SelectedAiModel,
                new Progress<(int Current, int Total)>(p =>
                {
                    AiStatusMessage = $"Обработка шага {p.Current} из {p.Total}...";
                }));

            for (int i = 0; i < Math.Min(Steps.Count, enhanced.Count); i++)
            {
                var stepVm = Steps[i];
                if (enhanced[i].Title != null) stepVm.Title = enhanced[i].Title!;
                if (enhanced[i].Description != null) stepVm.Description = enhanced[i].Description!;
            }

            if (SelectedStep != null)
            {
                OnPropertyChanged(nameof(CurrentStepTitle));
                OnPropertyChanged(nameof(CurrentStepDescription));
                OnPropertyChanged(nameof(SelectedStepTitle));
            }

            AiStatusMessage = "Все шаги успешно улучшены нейросетью!";
        }
        catch (Exception ex)
        {
            AiStatusMessage = $"Ошибка пакетной обработки: {ex.Message}";
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportDocxAsync()
    {
        await ExportGuideInternalAsync("docx");
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        await ExportGuideInternalAsync("pdf");
    }

    [RelayCommand]
    public async Task ExportHtmlAsync()
    {
        await ExportGuideInternalAsync("html");
    }

    private async Task ExportGuideInternalAsync(string format)
    {
        if (Steps.Count == 0)
        {
            ExportStatusMessage = "Нет шагов для экспорта.";
            return;
        }

        if (_exportService == null || _fileDialogService == null)
        {
            ExportStatusMessage = "Сервис экспорта или диалога недоступен.";
            return;
        }

        try
        {
            IsExporting = true;
            ExportStatusMessage = $"Подготовка экспорта в {format.ToUpperInvariant()}...";

            var (filter, ext) = format.ToLowerInvariant() switch
            {
                "docx" => ("Документ Microsoft Word (*.docx)|*.docx|Все файлы (*.*)|*.*", "docx"),
                "pdf" => ("Документ PDF (*.pdf)|*.pdf|Все файлы (*.*)|*.*", "pdf"),
                "html" => ("Веб-документ HTML (*.html)|*.html|Все файлы (*.*)|*.*", "html"),
                _ => ("Все файлы (*.*)|*.*", format)
            };

            var defaultName = string.IsNullOrWhiteSpace(ProjectName) ? "Руководство" : ProjectName;
            var savePath = await _fileDialogService.ShowSaveFileDialogAsync(
                $"Сохранить руководство ({format.ToUpperInvariant()})",
                defaultName,
                ext,
                filter);

            if (string.IsNullOrWhiteSpace(savePath))
            {
                ExportStatusMessage = "Экспорт отменен пользователем.";
                return;
            }

            var project = new Project(
                Id: Guid.NewGuid(),
                Name: ProjectName,
                RootPath: ProjectPath,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                Description: "Экспортированное руководство Stepwise");
            var stepsList = Steps.Select(s => s.Step).ToList();

            switch (format.ToLowerInvariant())
            {
                case "docx":
                    await _exportService.ExportToDocxAsync(project, stepsList, savePath);
                    break;
                case "pdf":
                    await _exportService.ExportToPdfAsync(project, stepsList, savePath);
                    break;
                case "html":
                    await _exportService.ExportToHtmlAsync(project, stepsList, savePath);
                    break;
            }

            ExportStatusMessage = $"Успешно сохранено: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _previewCts?.Cancel(); } catch (ObjectDisposedException) { }
        _previewCts?.Dispose();
        _previewCts = null;

        try { _thumbnailsCts?.Cancel(); } catch (ObjectDisposedException) { }
        _thumbnailsCts?.Dispose();
        _thumbnailsCts = null;

        _repository?.Dispose();
        _repository = null;
    }
}
