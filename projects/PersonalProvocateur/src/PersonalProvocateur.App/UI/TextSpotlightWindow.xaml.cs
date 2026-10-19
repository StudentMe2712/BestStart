using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// macOS Spotlight / Raycast-style floating card for text provocations and reminders (ADR-014).
/// </summary>
public partial class TextSpotlightWindow : Window
{
    private readonly Action? _onCompleted;
    private bool _isClosing;

    /// <summary>
    /// Initializes a new instance of <see cref="TextSpotlightWindow"/>.
    /// </summary>
    /// <param name="message">The reminder text to present.</param>
    /// <param name="onCompleted">Optional callback invoked when the user confirms with Done or Enter.</param>
    public TextSpotlightWindow(string message, Action? onCompleted = null)
    {
        InitializeComponent();

        _onCompleted = onCompleted;
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "Мотивационная задача" : message.Trim();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Parameterless constructor for designer and test support.
    /// </summary>
    public TextSpotlightWindow() : this("Пример напоминания", null)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlayEntranceAnimation();
        Activate();
        Focus();
    }

    private void PlayEntranceAnimation()
    {
        Opacity = 0.0;
        CardTranslate.Y = -12.0;

        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideIn = new DoubleAnimation(-12.0, 0.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fadeIn);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape or Key.Space)
        {
            e.Handled = true;
            Dismiss();
        }
    }

    private void Dismiss()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var slideOut = new DoubleAnimation(0.0, -12.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (_, _) =>
        {
            try
            {
                Close();
            }
            catch
            {
            }

            _onCompleted?.Invoke();
        };

        BeginAnimation(OpacityProperty, fadeOut);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
    }
}
