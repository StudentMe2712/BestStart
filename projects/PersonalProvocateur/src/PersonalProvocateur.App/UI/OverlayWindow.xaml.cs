using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Full-screen transparent overlay window hosting the screen dimming and interactive mascot.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly OverlayCoordinator? _coordinator;
    private readonly SettingsStore? _settingsStore;
    private bool _isClickHandled;

    /// <summary>
    /// Gets or sets whether the window is permitted to close.
    /// Alt+F4 and other close signals are cancelled when this is false.
    /// </summary>
    public bool CanClose { get; set; }

    /// <summary>
    /// Gets whether a mascot click has already been handled to prevent double-clicking.
    /// </summary>
    public bool IsClickHandled => _isClickHandled;

    /// <summary>
    /// Initializes a new instance of <see cref="OverlayWindow"/>.
    /// </summary>
    /// <param name="coordinator">The overlay coordinator orchestrating sessions.</param>
    /// <param name="settingsStore">Application settings store for reading dimming opacity.</param>
    public OverlayWindow(OverlayCoordinator coordinator, SettingsStore settingsStore)
    {
        InitializeComponent();

        _coordinator = coordinator;
        _settingsStore = settingsStore;

        Loaded += OnLoaded;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Parameterless constructor for designer support or testability.
    /// </summary>
    public OverlayWindow() : this(null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateOverlayBounds();

        // Apply dimming opacity from settings with smooth 350ms FadeIn animation (DoubleAnimation)
        double targetOpacity = _settingsStore?.CurrentSettings?.DimmingOpacity ?? 0.60;
        targetOpacity = Math.Clamp(targetOpacity, 0.10, 0.95);
        DimmingRectangle.Fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        DimmingRectangle.Opacity = 0.0;

        var fadeInAnimation = new DoubleAnimation
        {
            From = 0.0,
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(350),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        DimmingRectangle.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);

        // Reset click handling flag
        _isClickHandled = false;

        // Position mascot strictly at top-center edge
        PositionMascot();

        // Start entrance peek animation
        Mascot.PlayPeekAnimation();
    }

    /// <summary>
    /// Positions the mascot control at the top-center edge of the screen (macOS notch companion style).
    /// </summary>
    /// <param name="random">Optional random generator for test compatibility.</param>
    public void PositionMascot(Random? random = null)
    {
        if (Mascot != null)
        {
            Mascot.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            Mascot.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Mascot.Margin = new Thickness(0);
        }
    }

    /// <summary>
    /// Calculates the top-center safe spawn position for the mascot on screen.
    /// </summary>
    /// <param name="screenWidth">Width of the target display.</param>
    /// <param name="screenHeight">Height of the target display.</param>
    /// <param name="mascotWidth">Width of the mascot control (default 260).</param>
    /// <param name="mascotHeight">Height of the mascot control (default 260).</param>
    /// <param name="random">Optional random generator for deterministic testing.</param>
    /// <returns>A tuple with safe (Left, Top) coordinates centered at the top.</returns>
    public static (double Left, double Top) CalculateSafeMascotPosition(
        double screenWidth,
        double screenHeight,
        double mascotWidth = 260,
        double mascotHeight = 260,
        Random? random = null)
    {
        double left = Math.Max(0.0, (screenWidth - mascotWidth) / 2.0);
        double top = 0.0;
        return (left, top);
    }

    /// <summary>
    /// Handles backdrop click during active video playback (Mandatory Watch Lock).
    /// Clicking outside the video does NOT close the video; instead, it returns focus
    /// to the video player and flashes the mandatory watch alert notice.
    /// </summary>
    public void HandleBackdropClick()
    {
        if (_coordinator?.CurrentState == PersonalProvocateur.App.Models.SessionState.VideoPlaying)
        {
            _coordinator.ActiveVideoWindow?.Activate();
            _coordinator.ActiveVideoWindow?.FlashMandatoryWatchNotice();
        }
    }

    private void DimmingLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_coordinator?.CurrentState == PersonalProvocateur.App.Models.SessionState.VideoPlaying)
        {
            e.Handled = true;
            _coordinator.ActiveVideoWindow?.Activate();
            _coordinator.ActiveVideoWindow?.FlashMandatoryWatchNotice();
        }
    }

    /// <summary>
    /// Handles user click on the mascot control with debounce protection.
    /// </summary>
    public void HandleMascotClick()
    {
        if (_isClickHandled)
        {
            return;
        }

        _isClickHandled = true;
        Mascot.PlayClickAnimation(() => _coordinator?.OnMascotClicked());
    }

    private void Mascot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        HandleMascotClick();
    }

    /// <summary>
    /// Safely closes the overlay window by setting CanClose to true before closing.
    /// Stops all running mascot animations and timers.
    /// </summary>
    public void SafeClose()
    {
        CanClose = true;
        Mascot?.StopAllAnimations();
        Close();
    }

    /// <summary>
    /// Displays notification when the video library has no playable files.
    /// </summary>
    public void ShowEmptyLibraryNotice()
    {
        Mascot?.StopAllAnimations();
        if (Mascot != null)
        {
            Mascot.Visibility = Visibility.Collapsed;
        }
        EmptyLibraryNotice.Visibility = Visibility.Visible;
    }

    private void CloseNoticeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator != null)
        {
            _coordinator.CompleteSession();
        }
        else
        {
            SafeClose();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(UpdateOverlayBounds);
        }
        else
        {
            UpdateOverlayBounds();
        }
    }

    private void UpdateOverlayBounds()
    {
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        PositionMascot();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!CanClose)
        {
            e.Cancel = true;
            return;
        }

        Mascot?.StopAllAnimations();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Mascot?.StopAllAnimations();
        base.OnClosed(e);
    }
}
