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
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] cmdArgs length={cmdArgs.Length}: [{string.Join(", ", cmdArgs)}], args.Arguments='{args.Arguments}'\n");

            string? targetFile = null;
            if (cmdArgs.Length > 1)
            {
                for (int i = 1; i < cmdArgs.Length; i++)
                {
                    var candidate = cmdArgs[i].Trim('"', '\'');
                    if (File.Exists(candidate))
                    {
                        targetFile = candidate;
                        break;
                    }
                }
            }
            if (targetFile == null && !string.IsNullOrWhiteSpace(args.Arguments))
            {
                var candidate = args.Arguments.Trim('"', '\'');
                if (File.Exists(candidate))
                {
                    targetFile = candidate;
                }
            }

            if (!string.IsNullOrEmpty(targetFile))
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] Opening media file from arguments: {targetFile}\n");
                await mainWindow.OpenMediaFileAsync(targetFile);
            }

            if (cmdArgs.Any(a => a.Equals("--verify-ui", StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(args.Arguments) && args.Arguments.Contains("--verify-ui", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] --verify-ui detected, launching verification suite task\n");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    mainWindow.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await mainWindow.RunUiVerificationSuiteAsync();
                        }
                        catch (Exception suiteEx)
                        {
                            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] UI Verification Suite EXCEPTION: {suiteEx}\n");
                        }
                        finally
                        {
                            await Task.Delay(1000);
                            mainWindow.Close();
                        }
                    });
                });
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EXCEPTION in OnLaunched: {ex}\n");
            throw;
        }
    }
}
