using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>
    /// Gets or sets whether the window is permitted to close.
    /// Alt+F4 and other close signals are cancelled when this is false.
    /// </summary>
    public bool CanClose { get; set; }

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
    }

    /// <summary>
    /// Parameterless constructor for designer support or testability.
    /// </summary>
    public OverlayWindow() : this(null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Enforce coverage of the entire primary display
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        // Apply dimming opacity from settings
        double opacity = _settingsStore?.CurrentSettings?.DimmingOpacity ?? 0.60;
        opacity = Math.Clamp(opacity, 0.10, 0.95);
        byte alpha = (byte)(opacity * 255);
        DimmingRectangle.Fill = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
    }

    /// <summary>
    /// Safely closes the overlay window by setting CanClose to true before closing.
    /// </summary>
    public void SafeClose()
    {
        CanClose = true;
        Close();
    }

    /// <summary>
    /// Displays notification when the video library has no playable files.
    /// </summary>
    public void ShowEmptyLibraryNotice()
    {
        MascotContainer.Visibility = Visibility.Collapsed;
        EmptyLibraryNotice.Visibility = Visibility.Visible;
    }

    private void MascotContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _coordinator?.OnMascotClicked();
    }

    private void CloseNoticeButton_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.CompleteSession();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!CanClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
