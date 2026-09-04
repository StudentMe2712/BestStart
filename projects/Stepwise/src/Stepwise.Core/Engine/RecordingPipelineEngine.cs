using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Core.Engine;

/// <summary>
/// Оркестратор полного конвейера записи действий пользователя:
/// Hook (клик) -> UIA (инспекция элемента) -> Capture (скриншот) -> Storage (SQLite) -> StepRecorded.
/// </summary>
public sealed class RecordingPipelineEngine : IRecordingEngine
{
    private readonly IMouseHookService _mouseHookService;
    private readonly IUIAutomationService _uiaService;
    private readonly IScreenCaptureService? _captureService;
    private readonly IProjectRepository? _repository;

    private int _sequenceIndex;
    private bool _isRecording;
    private bool _isDisposed;

    public event EventHandler<Step>? StepRecorded;

    public bool IsRecording => _isRecording;

    public RecordingPipelineEngine(
        IMouseHookService mouseHookService,
        IUIAutomationService uiaService,
        IScreenCaptureService? captureService = null,
        IProjectRepository? repository = null)
    {
        _mouseHookService = mouseHookService ?? throw new ArgumentNullException(nameof(mouseHookService));
        _uiaService = uiaService ?? throw new ArgumentNullException(nameof(uiaService));
        _captureService = captureService;
        _repository = repository;
    }

    public void StartRecording()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isRecording)
        {
            return;
        }

        _sequenceIndex = 0;
        _mouseHookService.MouseClicked += OnMouseClicked;
        _mouseHookService.Start();
        _isRecording = true;
    }

    public void StopRecording()
    {
        if (!_isRecording)
        {
            return;
        }

        _mouseHookService.MouseClicked -= OnMouseClicked;
        _mouseHookService.Stop();
        _isRecording = false;
    }

    private void OnMouseClicked(object? sender, MouseClickEvent e)
    {
        if (!_isRecording)
        {
            return;
        }

        // 1. Инспектируем UI-элемент в точке клика через UI Automation
        var element = _uiaService.InspectElementAt(e.X, e.Y);

        int index = Interlocked.Increment(ref _sequenceIndex);
        string title = GenerateStepTitle(e.Action, element);

        // 2. Выполняем захват экрана и сохранение скриншота в assets/screenshots/
        string? screenshotRelPath = null;
        if (_captureService != null && _repository != null)
        {
            screenshotRelPath = _captureService.Capture(
                _repository.ProjectRootPath,
                index,
                element.BoundingRectangle,
                element.WindowHandle
            );
        }

        var metadata = new Dictionary<string, string>
        {
            ["ProcessName"] = element.ProcessName,
            ["ProcessId"] = element.ProcessId.ToString(),
            ["WindowTitle"] = element.WindowTitle,
            ["ControlType"] = element.ControlType,
            ["AutomationId"] = element.AutomationId,
            ["ClassName"] = element.ClassName
        };

        var step = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: index,
            Timestamp: e.Timestamp,
            Action: e.Action,
            ClickX: e.X,
            ClickY: e.Y,
            TargetElement: element,
            ScreenshotPath: screenshotRelPath,
            Title: title,
            Description: $"Выполнено действие {e.Action} на элементе '{element.Name}' ({element.ControlType}) в приложении {element.ProcessName}.",
            Metadata: metadata
        );

        // 3. Сохраняем шаг в локальную базу данных проекта SQLite
        _repository?.SaveStep(step);

        // 4. Оповещаем подписчиков о новом шаге
        StepRecorded?.Invoke(this, step);
    }

    private static string GenerateStepTitle(ActionType action, ElementInfo element)
    {
        string actionVerb = action switch
        {
            ActionType.LeftClick => "Нажмите",
            ActionType.RightClick => "Нажмите правой кнопкой на",
            ActionType.DoubleLeftClick => "Дважды нажмите на",
            ActionType.MiddleClick => "Нажмите средней кнопкой на",
            _ => "Взаимодействуйте с"
        };

        if (!string.IsNullOrWhiteSpace(element.Name))
        {
            return $"{actionVerb} \"{element.Name}\" ({element.ControlType})";
        }

        if (!string.IsNullOrWhiteSpace(element.AutomationId))
        {
            return $"{actionVerb} элемент [{element.AutomationId}] ({element.ControlType})";
        }

        if (!string.IsNullOrWhiteSpace(element.WindowTitle))
        {
            return $"{actionVerb} область окна \"{element.WindowTitle}\"";
        }

        return $"{actionVerb} элемент ({element.ControlType}) в {element.ProcessName}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopRecording();
        _mouseHookService.Dispose();
        _repository?.Dispose();
    }
}
