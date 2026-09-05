using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.App.ViewModels;

/// <summary>
/// ViewModel уровня приложения / Shell (MainWindow).
/// Управляет жизненным циклом записи через IRecordingEngine и координирует передачу шагов в EditorViewModel.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IRecordingEngine _recordingEngine;
    private readonly EditorViewModel _editorViewModel;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _isDisposed;

    [ObservableProperty]
    private string _windowTitle = "Stepwise — Interactive Walkthrough Engine";

    [ObservableProperty]
    private string _statusMessage = "Готов к работе";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _currentView = "Editor";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    [NotifyPropertyChangedFor(nameof(CanResume))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(RecordingStateHexColor))]
    private RecordingSessionState _recordingState = RecordingSessionState.Idle;

    [ObservableProperty]
    private string _recordingStatusText = "Готов к записи";

    [ObservableProperty]
    private SolidColorBrush _recordingStateBrush = GetStatusBrush(RecordingSessionState.Idle);

    public bool CanStart => RecordingState == RecordingSessionState.Idle || RecordingState == RecordingSessionState.Completed;
    public bool CanPause => RecordingState == RecordingSessionState.Recording;
    public bool CanResume => RecordingState == RecordingSessionState.Paused;
    public bool CanStop => RecordingState == RecordingSessionState.Recording || RecordingState == RecordingSessionState.Paused;

    public string RecordingStateHexColor => RecordingState switch
    {
        RecordingSessionState.Idle => "#9CA3AF",
        RecordingSessionState.Recording => "#EF4444",
        RecordingSessionState.Paused => "#F59E0B",
        RecordingSessionState.Stopping => "#F59E0B",
        RecordingSessionState.Completed => "#10B981",
        RecordingSessionState.Failed => "#EF4444",
        _ => "#9CA3AF"
    };

    public MainViewModel(
        IRecordingEngine recordingEngine,
        EditorViewModel editorViewModel,
        DispatcherQueue? dispatcherQueue = null)
    {
        _recordingEngine = recordingEngine ?? throw new ArgumentNullException(nameof(recordingEngine));
        _editorViewModel = editorViewModel ?? throw new ArgumentNullException(nameof(editorViewModel));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        _recordingEngine.StateChanged += OnRecordingEngineStateChanged;
        _recordingEngine.StepRecorded += OnRecordingEngineStepRecorded;

        ApplyRecordingState(_recordingEngine.State);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartRecording()
    {
        try
        {
            _recordingEngine.StartRecording();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка запуска: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void PauseRecording()
    {
        try
        {
            _recordingEngine.PauseRecording();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка паузы: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void ResumeRecording()
    {
        try
        {
            _recordingEngine.ResumeRecording();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка возобновления: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopRecordingAsync()
    {
        try
        {
            await _recordingEngine.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка остановки: {ex.Message}";
        }
    }

    private void OnRecordingEngineStateChanged(object? sender, RecordingSessionState newState)
    {
        RunOnUIThread(() =>
        {
            ApplyRecordingState(newState);
        });
    }

    private void OnRecordingEngineStepRecorded(object? sender, Step step)
    {
        RunOnUIThread(() =>
        {
            _editorViewModel.AddStep(step);
        });
    }

    private void ApplyRecordingState(RecordingSessionState newState)
    {
        RecordingState = newState;
        RecordingStatusText = GetStatusText(newState);
        StatusMessage = RecordingStatusText;
        IsRecording = newState == RecordingSessionState.Recording;
        RecordingStateBrush = GetStatusBrush(newState);

        StartRecordingCommand.NotifyCanExecuteChanged();
        PauseRecordingCommand.NotifyCanExecuteChanged();
        ResumeRecordingCommand.NotifyCanExecuteChanged();
        StopRecordingCommand.NotifyCanExecuteChanged();
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

    public static string GetStatusText(RecordingSessionState state) => state switch
    {
        RecordingSessionState.Idle => "Готов к записи",
        RecordingSessionState.Recording => "Запись активна...",
        RecordingSessionState.Paused => "Пауза",
        RecordingSessionState.Stopping => "Завершение...",
        RecordingSessionState.Completed => "Запись завершена",
        RecordingSessionState.Failed => "Ошибка записи",
        _ => "Неизвестно"
    };

    public static SolidColorBrush GetStatusBrush(RecordingSessionState state)
    {
        try
        {
            return state switch
            {
                RecordingSessionState.Idle => new SolidColorBrush(ColorHelper.FromArgb(255, 156, 163, 175)),       // Gray (#9CA3AF)
                RecordingSessionState.Recording => new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)),    // Red (#EF4444)
                RecordingSessionState.Paused => new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11)),      // Yellow/Orange (#F59E0B)
                RecordingSessionState.Stopping => new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11)),    // Yellow/Orange (#F59E0B)
                RecordingSessionState.Completed => new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129)),   // Green (#10B981)
                RecordingSessionState.Failed => new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)),       // Red (#EF4444)
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

        _recordingEngine.StateChanged -= OnRecordingEngineStateChanged;
        _recordingEngine.StepRecorded -= OnRecordingEngineStepRecorded;
        _recordingEngine.Dispose();
    }
}
