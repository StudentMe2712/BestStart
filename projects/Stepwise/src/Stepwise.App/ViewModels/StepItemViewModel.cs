using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Stepwise.App.Services;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.App.ViewModels;

/// <summary>
/// ViewModel отдельного шага руководства для отображения в списке карточек и редактирования свойств.
/// </summary>
public sealed partial class StepItemViewModel : ObservableObject
{
    private readonly IProjectRepository? _repository;
    private readonly string _projectRoot;

    public Step Step { get; private set; }

    public Guid Id => Step.Id;
    public int SequenceIndex => Step.SequenceIndex;
    public int StepNumber => Step.SequenceIndex + 1;
    public ActionType Action => Step.Action;
    public DateTime Timestamp => Step.Timestamp;
    public ElementInfo TargetElement => Step.TargetElement;
    public string? RelativeScreenshotPath => Step.ScreenshotPath;
    public double ClickX => Step.ClickX;
    public double ClickY => Step.ClickY;
    public BoundingBox BoundingRectangle => Step.TargetElement.BoundingRectangle;

    public string FullScreenshotPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Step.ScreenshotPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(Step.ScreenshotPath))
            {
                return Step.ScreenshotPath;
            }

            return Path.Combine(_projectRoot, Step.ScreenshotPath);
        }
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private BitmapImage? _thumbnailImage;

    [ObservableProperty]
    private bool _isThumbnailLoading;

    [ObservableProperty]
    private bool _hasThumbnailError;

    public Visibility ThumbnailLoadingVisibility => IsThumbnailLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailErrorVisibility => HasThumbnailError ? Visibility.Visible : Visibility.Collapsed;

    public string ActionBadgeText => Step.Action switch
    {
        ActionType.LeftClick => "Left Click",
        ActionType.RightClick => "Right Click",
        ActionType.DoubleLeftClick => "Double Click",
        ActionType.MiddleClick => "Middle Click",
        ActionType.DragAndDrop => "Drag & Drop",
        ActionType.KeyPress => "Key Press",
        ActionType.TextInput => "Text Input",
        _ => Step.Action.ToString()
    };

    public string FormattedTimestamp => Step.Timestamp.ToString("HH:mm:ss");

    public string ElementSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TargetElement.Name))
            {
                return $"{TargetElement.ControlType}: \"{TargetElement.Name}\"";
            }
            if (!string.IsNullOrWhiteSpace(TargetElement.AutomationId))
            {
                return $"{TargetElement.ControlType} ({TargetElement.AutomationId})";
            }
            return TargetElement.ControlType;
        }
    }

    public StepItemViewModel(Step step, string projectRoot, IProjectRepository? repository = null)
    {
        Step = step ?? throw new ArgumentNullException(nameof(step));
        _projectRoot = projectRoot ?? string.Empty;
        _repository = repository;

        _title = step.Title ?? $"Шаг {step.SequenceIndex + 1}: {step.Action}";
        _description = step.Description ?? string.Empty;
    }

    partial void OnTitleChanged(string value)
    {
        Step = Step with { Title = value };
        PersistChanges();
    }

    partial void OnDescriptionChanged(string value)
    {
        Step = Step with { Description = value };
        PersistChanges();
    }

    partial void OnIsThumbnailLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ThumbnailLoadingVisibility));
    }

    partial void OnHasThumbnailErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(ThumbnailErrorVisibility));
    }

    private void PersistChanges()
    {
        var id = Id;
        var title = Title;
        var description = Description;
        var repo = _repository;
        if (repo == null) return;

        _ = Task.Run(() =>
        {
            try
            {
                repo.UpdateStepDetails(id, title, description);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StepItemViewModel] Ошибка сохранения изменений шага {id}: {ex.Message}");
            }
        });
    }

    public async Task LoadThumbnailAsync(IImageLoaderService imageLoader, CancellationToken ct = default)
    {
        if (ThumbnailImage != null || IsThumbnailLoading)
        {
            return;
        }

        var path = FullScreenshotPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            HasThumbnailError = true;
            return;
        }

        try
        {
            IsThumbnailLoading = true;
            HasThumbnailError = false;

            var bitmap = await imageLoader.LoadThumbnailAsync(path, decodePixelWidth: 180, ct).ConfigureAwait(true);
            if (!ct.IsCancellationRequested)
            {
                ThumbnailImage = bitmap;
                HasThumbnailError = (bitmap == null);
            }
        }
        catch (OperationCanceledException)
        {
            // Отмена операции при быстром скролле
        }
        catch (Exception)
        {
            HasThumbnailError = true;
        }
        finally
        {
            IsThumbnailLoading = false;
        }
    }
}
