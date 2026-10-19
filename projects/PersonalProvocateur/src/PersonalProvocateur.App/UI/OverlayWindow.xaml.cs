using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
    private System.Windows.Forms.Screen? _assignedScreen;

    /// <summary>
    /// Gets whether this window is purely a secondary screen dimmer (no interactive mascot).
    /// </summary>
    public bool IsSecondaryDimmerOnly { get; }

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
    /// <param name="isSecondaryDimmerOnly">True if this window is purely for secondary screen dimming.</param>
    public OverlayWindow(OverlayCoordinator coordinator, SettingsStore settingsStore, bool isSecondaryDimmerOnly = false)
    {
        InitializeComponent();

        _coordinator = coordinator;
        _settingsStore = settingsStore;
        IsSecondaryDimmerOnly = isSecondaryDimmerOnly;

        if (IsSecondaryDimmerOnly)
        {
            if (Mascot != null)
            {
                Mascot.Visibility = Visibility.Collapsed;
            }
            if (EmptyLibraryNotice != null)
            {
                EmptyLibraryNotice.Visibility = Visibility.Collapsed;
            }
            if (SpotlightContainer != null)
            {
                SpotlightContainer.Visibility = Visibility.Collapsed;
            }
        }

        Loaded += OnLoaded;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        PreviewKeyDown += OverlayWindow_PreviewKeyDown;
    }

    /// <summary>
    /// Parameterless constructor for designer support or testability.
    /// </summary>
    public OverlayWindow() : this(null!, null!, false)
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

        if (IsSecondaryDimmerOnly)
        {
            if (Mascot != null)
            {
                Mascot.Visibility = Visibility.Collapsed;
            }
            if (EmptyLibraryNotice != null)
            {
                EmptyLibraryNotice.Visibility = Visibility.Collapsed;
            }
            if (SpotlightContainer != null)
            {
                SpotlightContainer.Visibility = Visibility.Collapsed;
            }
            return;
        }

        // Position mascot strictly at top-center edge
        PositionMascot();

        if (_settingsStore != null)
        {
            Mascot.SettingsStore = _settingsStore;
            Mascot.ApplySkin(_settingsStore.CurrentSettings.MascotSkin, _settingsStore.CurrentSettings.CustomMascotPath);
        }

        // Start entrance peek animation
        Mascot.PlayPeekAnimation();
    }

    /// <summary>
    /// Positions the overlay window to cover the specified display screen bounds,
    /// converting physical screen coordinates to WPF device-independent pixels (DIPs).
    /// </summary>
    /// <param name="screen">The target screen.</param>
    public void PositionOnScreen(System.Windows.Forms.Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        _assignedScreen = screen;

        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        try
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var matrix = source.CompositionTarget.TransformFromDevice;
                dpiScaleX = matrix.M11;
                dpiScaleY = matrix.M22;
            }
            else
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                dpiScaleX = 1.0 / dpi.DpiScaleX;
                dpiScaleY = 1.0 / dpi.DpiScaleY;
            }
        }
        catch
        {
            dpiScaleX = 1.0;
            dpiScaleY = 1.0;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = screen.Bounds.Left * dpiScaleX;
        Top = screen.Bounds.Top * dpiScaleY;
        Width = screen.Bounds.Width * dpiScaleX;
        Height = screen.Bounds.Height * dpiScaleY;

        if (!IsSecondaryDimmerOnly)
        {
            PositionMascot();
        }
        else if (Mascot != null)
        {
            Mascot.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Positions the mascot control at the top-center edge of the screen (macOS notch companion style).
    /// </summary>
    /// <param name="random">Optional random generator for test compatibility.</param>
    public void PositionMascot(Random? random = null)
    {
        if (IsSecondaryDimmerOnly)
        {
            if (Mascot != null)
            {
                Mascot.Visibility = Visibility.Collapsed;
            }
            return;
        }

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
    /// Displays the macOS Spotlight / Raycast style text card with provocation reminder.
    /// Hides the mascot, performs smooth fade-in animation, and focuses the completion button.
    /// </summary>
    /// <param name="text">The reminder text to display.</param>
    public void ShowSpotlightText(string text)
    {
        Mascot?.StopAllAnimations();
        if (Mascot != null)
        {
            Mascot.Visibility = Visibility.Collapsed;
        }

        if (EmptyLibraryNotice != null)
        {
            EmptyLibraryNotice.Visibility = Visibility.Collapsed;
        }

        if (SpotlightTextBlock != null)
        {
            SpotlightTextBlock.Text = text ?? string.Empty;
        }

        if (SpotlightContainer != null)
        {
            SpotlightContainer.Opacity = 0.0;
            SpotlightContainer.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SpotlightContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        Focusable = true;
        SpotlightDoneButton?.Focus();
        if (SpotlightDoneButton != null)
        {
            Keyboard.Focus(SpotlightDoneButton);
        }
    }

    private void SpotlightDoneButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.CompleteSession();
    }

    private void SpotlightContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OverlayWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SpotlightContainer != null && SpotlightContainer.Visibility == Visibility.Visible && e.Key == Key.Enter)
        {
            e.Handled = true;
            _coordinator?.CompleteSession();
        }
    }

    /// <summary>
    /// Handles backdrop click during active video playback (Strict Mandatory Watch Lock, ADR-010).
    /// Clicking outside the video does NOT close the video; instead, it delegates to the coordinator
    /// which refocuses the video player and triggers warning pulse animations.
    /// </summary>
    public void HandleBackdropClick()
    {
        if (SpotlightContainer != null && SpotlightContainer.Visibility == Visibility.Visible)
        {
            SpotlightDoneButton?.Focus();
            if (SpotlightDoneButton != null)
            {
                Keyboard.Focus(SpotlightDoneButton);
            }
            return;
        }

        if (_coordinator?.CurrentState == PersonalProvocateur.App.Models.SessionState.VideoPlaying)
        {
            _coordinator.OnBackdropClickedDuringVideo();
        }
    }

    private void DimmingLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SpotlightContainer != null && SpotlightContainer.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            SpotlightDoneButton?.Focus();
            if (SpotlightDoneButton != null)
            {
                Keyboard.Focus(SpotlightDoneButton);
            }
            return;
        }

        if (_coordinator?.CurrentState == PersonalProvocateur.App.Models.SessionState.VideoPlaying)
        {
            e.Handled = true;
            _coordinator.OnBackdropClickedDuringVideo();
            return;
        }

        if (IsSecondaryDimmerOnly)
        {
            e.Handled = true;
            return;
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
        if (SpotlightContainer != null)
        {
            SpotlightContainer.Visibility = Visibility.Collapsed;
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
        if (_assignedScreen != null)
        {
            PositionOnScreen(_assignedScreen);
        }
        else
        {
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            if (!IsSecondaryDimmerOnly)
            {
                PositionMascot();
            }
            else if (Mascot != null)
            {
                Mascot.Visibility = Visibility.Collapsed;
            }
        }
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
        PreviewKeyDown -= OverlayWindow_PreviewKeyDown;
        Mascot?.StopAllAnimations();
        base.OnClosed(e);
    }
}
