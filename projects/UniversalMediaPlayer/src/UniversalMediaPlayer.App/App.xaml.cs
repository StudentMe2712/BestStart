using Microsoft.UI.Xaml;

namespace UniversalMediaPlayer.App;

public partial class App : Application
{
    private Window? _window;
    private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalMediaPlayer", "startup.log");

    public App()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.WriteAllText(LogFile, $"[{DateTime.UtcNow:O}] App() constructor start\n");

            this.UnhandledException += (s, e) =>
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] App.UnhandledException: {e.Message}\n{e.Exception}\n");
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] AppDomain.UnhandledException: {e.ExceptionObject}\n");
            };

            InitializeComponent();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] App.InitializeComponent() completed\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION in App(): {ex}\n");
            throw;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] OnLaunched start\n");
            var mainWindow = new MainWindow();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] MainWindow created\n");
            _window = mainWindow;
            _window.Activate();
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] _window.Activate() completed\n");

            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            int tickCount = 0;
            t.Tick += async (s, e) =>
            {
                tickCount++;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                var isWin = Native.Win32.IsWindow(hwnd);
                var isVis = Native.Win32.IsWindowVisible(hwnd);
                Native.Win32.GetWindowRect(hwnd, out var rect);
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Window timer tick {tickCount}: PID={Environment.ProcessId}, hwnd=0x{hwnd:X}, IsWindow={isWin}, IsVisible={isVis}, Rect={rect.Left},{rect.Top} to {rect.Right},{rect.Bottom} ({width}x{height})\n");

                if (tickCount == 2 && _window.Content != null)
                {
                    try
                    {
                        var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
                        await rtb.RenderAsync(_window.Content);
                        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] RenderTargetBitmap successfully rendered Content: {rtb.PixelWidth}x{rtb.PixelHeight}\n");
                    }
                    catch (Exception renderEx)
                    {
                        File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] RenderTargetBitmap EXCEPTION: {renderEx}\n");
                    }
                }

                if (tickCount >= 5) t.Stop();
            };
            t.Start();

            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Length > 1 && File.Exists(cmdArgs[1]))
            {
                await mainWindow.OpenMediaFileAsync(cmdArgs[1]);
            }
            else if (!string.IsNullOrWhiteSpace(args.Arguments) && File.Exists(args.Arguments))
            {
                await mainWindow.OpenMediaFileAsync(args.Arguments);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION in OnLaunched: {ex}\n");
            throw;
        }
    }
}
