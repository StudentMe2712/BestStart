using System.Windows;
using System.Windows.Input;
using SelectCast.App.Interop;
using SelectCast.Core.Settings;

namespace SelectCast.App;

public partial class SettingsWindow : Window
{
    private readonly Func<uint, uint, bool> _applyHotkey;
    private readonly SettingsService _settings;
    private readonly AppSettings _current;

    private uint _modifiers;
    private uint _vk;

    /// <param name="applyHotkey">Re-registers the global hotkey; returns false if the OS rejects it (e.g. taken).</param>
    public SettingsWindow(AppSettings current, Func<uint, uint, bool> applyHotkey, SettingsService settings)
    {
        InitializeComponent();
        _current = current;
        _applyHotkey = applyHotkey;
        _settings = settings;
        _modifiers = current.HotkeyModifiers;
        _vk = current.HotkeyVk;
        HotkeyBox.Text = HotkeyCapture.Format(_modifiers, _vk);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        GlassChrome.Apply(this); // Win11 acrylic backdrop behind the frosted-glass panels
    }

    // Custom chrome has no system close box; ✕ closes the dialog without saving (like Отмена).
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        => HotkeyBox.Text = "Нажмите комбинацию…";

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true; // don't let the keystroke leak into the textbox or window
        if (HotkeyCapture.FromKeyEvent(e) is var combo && combo is not null)
        {
            (_modifiers, _vk) = combo.Value;
            HotkeyBox.Text = HotkeyCapture.Format(_modifiers, _vk);
            ErrorText.Visibility = Visibility.Collapsed;
        }
        else
        {
            HotkeyBox.Text = "Нажмите комбинацию… (нужен модификатор + клавиша)";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_applyHotkey(_modifiers, _vk))
        {
            _settings.Save(_current with { HotkeyModifiers = _modifiers, HotkeyVk = _vk });
            Close();
        }
        else
        {
            ErrorText.Text = "Эта комбинация занята другим приложением. Выберите другую.";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
