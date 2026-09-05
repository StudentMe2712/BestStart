using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Hooks;

namespace Stepwise.WindowsIntegration.Services;

/// <summary>
/// Агрегирующий сервис мониторинга глобального низкоуровневого ввода пользователя (мышь и клавиатура).
/// </summary>
public sealed class InputMonitoringService : IInputMonitoringService
{
    private readonly IMouseHookService _mouseHook;
    private readonly IKeyboardHookService _keyboardHook;
    private readonly bool _ownsHooks;
    private readonly object _syncLock = new();
    private bool _isDisposed;

    public event EventHandler<RawMouseEvent>? MouseEventReceived;
    public event EventHandler<RawKeyboardEvent>? KeyboardEventReceived;

    public bool IsRunning => _mouseHook.IsRunning || _keyboardHook.IsRunning;

    public InputMonitoringService()
        : this(new LowLevelMouseHookService(), new LowLevelKeyboardHookService(), ownsHooks: true)
    {
    }

    public InputMonitoringService(IMouseHookService mouseHook, IKeyboardHookService keyboardHook)
        : this(mouseHook, keyboardHook, ownsHooks: false)
    {
    }

    private InputMonitoringService(IMouseHookService mouseHook, IKeyboardHookService keyboardHook, bool ownsHooks)
    {
        _mouseHook = mouseHook ?? throw new ArgumentNullException(nameof(mouseHook));
        _keyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
        _ownsHooks = ownsHooks;

        _mouseHook.RawMouseEventReceived += OnMouseRawEvent;
        _keyboardHook.KeyboardEventReceived += OnKeyboardRawEvent;
    }

    public void Start()
    {
        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            bool mouseStarted = false;
            if (!_mouseHook.IsRunning)
            {
                _mouseHook.Start();
                mouseStarted = true;
            }

            try
            {
                if (!_keyboardHook.IsRunning)
                {
                    _keyboardHook.Start();
                }
            }
            catch
            {
                if (mouseStarted || _mouseHook.IsRunning)
                {
                    try
                    {
                        _mouseHook.Stop();
                    }
                    catch
                    {
                        // Игнорируем вторичную ошибку остановки при откате
                    }
                }
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_syncLock)
        {
            if (_mouseHook.IsRunning)
            {
                _mouseHook.Stop();
            }

            if (_keyboardHook.IsRunning)
            {
                _keyboardHook.Stop();
            }
        }
    }

    private void OnMouseRawEvent(object? sender, RawMouseEvent e)
    {
        MouseEventReceived?.Invoke(this, e);
    }

    private void OnKeyboardRawEvent(object? sender, RawKeyboardEvent e)
    {
        KeyboardEventReceived?.Invoke(this, e);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _mouseHook.RawMouseEventReceived -= OnMouseRawEvent;
            _keyboardHook.KeyboardEventReceived -= OnKeyboardRawEvent;

            Stop();

            if (_ownsHooks)
            {
                _mouseHook.Dispose();
                _keyboardHook.Dispose();
            }
        }
    }
}
