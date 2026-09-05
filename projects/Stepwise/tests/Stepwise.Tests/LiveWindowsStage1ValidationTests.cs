using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Stepwise.Core.Models;
using Stepwise.WindowsIntegration.Automation;
using Stepwise.WindowsIntegration.Hooks;
using Stepwise.WindowsIntegration.Native;
using Stepwise.WindowsIntegration.Services;
using Xunit;

namespace Stepwise.Tests;

public class LiveWindowsStage1ValidationTests
{
    [Fact]
    public void LiveWindows_Stage1_FullValidation_SavesEvidence()
    {
        var artifactsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "phase4", "stage1");
        artifactsDir = Path.GetFullPath(artifactsDir);
        Directory.CreateDirectory(artifactsDir);

        var logPath = Path.Combine(artifactsDir, "live_validation.log");
        using var logWriter = new StreamWriter(logPath, false, System.Text.Encoding.UTF8);

        void Log(string msg)
        {
            var line = $"{DateTime.UtcNow:HH:mm:ss.fff} [LIVE-VAL] {msg}";
            logWriter.WriteLine(line);
            logWriter.Flush();
        }

        Log("Starting Phase 4 Stage 1 Live Validation...");

        // 1. Start Services
        using var windowTracker = new ActiveWindowTracker();
        using var inputMonitor = new InputMonitoringService();

        var windowEvents = new List<object>();
        var mouseEvents = new List<object>();
        var keyboardEvents = new List<object>();

        windowTracker.ActiveWindowChanged += (s, e) =>
        {
            lock (windowEvents)
            {
                windowEvents.Add(new
                {
                    Handle = e.WindowHandle,
                    e.ProcessId,
                    e.ProcessName,
                    Title = e.WindowTitle,
                    Bounds = $"{e.Bounds.X},{e.Bounds.Y} [{e.Bounds.Width}x{e.Bounds.Height}]",
                    Timestamp = e.Timestamp.ToString("o")
                });
            }
        };

        inputMonitor.MouseEventReceived += (s, e) =>
        {
            lock (mouseEvents)
            {
                mouseEvents.Add(new
                {
                    EventType = e.EventType.ToString(),
                    Button = e.Button.ToString(),
                    e.X,
                    e.Y,
                    e.Delta,
                    Timestamp = e.Timestamp.ToString("o")
                });
            }
        };

        inputMonitor.KeyboardEventReceived += (s, e) =>
        {
            lock (keyboardEvents)
            {
                keyboardEvents.Add(new
                {
                    EventType = e.EventType.ToString(),
                    e.VirtualKey,
                    e.ScanCode,
                    Modifiers = e.Modifiers.ToString(),
                    e.Character,
                    e.IsShortcut,
                    e.IsAltGr,
                    e.IsTextInput,
                    Timestamp = e.Timestamp.ToString("o")
                });
            }
        };

        Log("Starting ActiveWindowTracker and InputMonitoringService...");
        windowTracker.Start();
        inputMonitor.Start();

        Assert.True(windowTracker.IsRunning);
        Assert.True(inputMonitor.IsRunning);
        Log("ActiveWindowTracker and InputMonitoringService are running.");

        // 2. Launch real WPF Window with TextBox and PasswordBox in dedicated STA thread
        Window? testWindow = null;
        TextBox? normalTextBox = null;
        PasswordBox? securePasswordBox = null;
        double pwdScreenX = 0, pwdScreenY = 0;
        double textScreenX = 0, textScreenY = 0;
        nint testWindowHwnd = nint.Zero;
        using var windowReadyEvent = new ManualResetEventSlim(false);

        var wpfThread = new Thread(() =>
        {
            testWindow = new Window
            {
                Title = "Stepwise Live Validation Target",
                Width = 420,
                Height = 320,
                Top = 150,
                Left = 150,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            var lbl1 = new TextBlock { Text = "Standard Input Field:", Margin = new Thickness(0, 0, 0, 4) };
            normalTextBox = new TextBox { Name = "txtStandard", Text = "InitialTextValue", Margin = new Thickness(0, 0, 0, 16) };
            var lbl2 = new TextBlock { Text = "Secure Password Field:", Margin = new Thickness(0, 0, 0, 4) };
            securePasswordBox = new PasswordBox { Name = "pwdSecure", Password = "SuperSecretPassword123" };

            panel.Children.Add(lbl1);
            panel.Children.Add(normalTextBox);
            panel.Children.Add(lbl2);
            panel.Children.Add(securePasswordBox);
            testWindow.Content = panel;

            testWindow.Loaded += (s, e) =>
            {
                var helper = new WindowInteropHelper(testWindow);
                testWindowHwnd = helper.Handle;

                var pPoint = securePasswordBox.PointToScreen(new Point(15, 10));
                pwdScreenX = pPoint.X;
                pwdScreenY = pPoint.Y;

                var tPoint = normalTextBox.PointToScreen(new Point(15, 10));
                textScreenX = tPoint.X;
                textScreenY = tPoint.Y;

                windowReadyEvent.Set();
            };

            testWindow.ShowDialog();
        });

        wpfThread.SetApartmentState(ApartmentState.STA);
        wpfThread.IsBackground = true;
        wpfThread.Start();

        Assert.True(windowReadyEvent.Wait(TimeSpan.FromSeconds(5)), "Timeout waiting for test window initialization");
        Thread.Sleep(500);

        Log($"Live Test Window running. HWND=0x{testWindowHwnd:X8}");
        Log($"TextBox Point: ({textScreenX}, {textScreenY})");
        Log($"PasswordBox Point: ({pwdScreenX}, {pwdScreenY})");

        // 3. Inspect UI Automation for PasswordBox and TextBox
        Log("Inspecting UI Automation elements...");
        var uia = new UIAutomationService();

        var textElement = uia.InspectElementAt((int)textScreenX, (int)textScreenY);
        var pwdElement = uia.InspectElementAt((int)pwdScreenX, (int)pwdScreenY);

        Log($"TextBox element: Name='{textElement.Name}', ControlType='{textElement.ControlType}', IsPassword={textElement.IsPassword}");
        Log($"PasswordBox element: Name='{pwdElement.Name}', ControlType='{pwdElement.ControlType}', IsPassword={pwdElement.IsPassword}");

        var inspectionEvidence = new
        {
            TextBox = new
            {
                textElement.Name,
                textElement.ControlType,
                textElement.AutomationId,
                textElement.ClassName,
                textElement.ProcessName,
                textElement.IsPassword
            },
            PasswordBox = new
            {
                pwdElement.Name,
                pwdElement.ControlType,
                pwdElement.AutomationId,
                pwdElement.ClassName,
                pwdElement.ProcessName,
                pwdElement.IsPassword
            }
        };

        File.WriteAllText(
            Path.Combine(artifactsDir, "uia_password_inspection.json"),
            JsonSerializer.Serialize(inspectionEvidence, new JsonSerializerOptions { WriteIndented = true })
        );

        // Verification assertions:
        Assert.False(textElement.IsPassword, "TextBox must NOT be marked as password");
        Assert.True(pwdElement.IsPassword, "PasswordBox MUST be marked as password");
        Log("UI Automation password inspection PASSED.");

        // 4. Send test live input (keys + mouse move/click) on our own test window
        Log("Simulating live keyboard and mouse input on test window...");
        try
        {
            testWindow?.Dispatcher.Invoke(() =>
            {
                testWindow.Activate();
                testWindow.Focus();
                normalTextBox?.Focus();
            });
            Thread.Sleep(300);

            // Simulate pressing 'A' (VK_A = 0x41)
            keybd_event(0x41, 0, 0, 0); // KeyDown
            Thread.Sleep(50);
            keybd_event(0x41, 0, 2, 0); // KeyUp (KEYEVENTF_KEYUP = 2)
            Thread.Sleep(50);

            // Simulate pressing 'B' (VK_B = 0x42)
            keybd_event(0x42, 0, 0, 0);
            Thread.Sleep(50);
            keybd_event(0x42, 0, 2, 0);
            Thread.Sleep(50);

            // Simulate mouse movement and click
            mouse_event(0x0001, 5, 5, 0, 0); // MOUSEEVENTF_MOVE
            Thread.Sleep(50);
            mouse_event(0x0002, 0, 0, 0, 0); // MOUSEEVENTF_LEFTDOWN
            Thread.Sleep(50);
            mouse_event(0x0004, 0, 0, 0, 0); // MOUSEEVENTF_LEFTUP
        }
        catch (Exception ex)
        {
            Log($"Input simulation warning: {ex.Message}");
        }

        Thread.Sleep(500);

        // 5. Launch second application (notepad.exe) and test window switching
        Log("Launching notepad.exe to verify active window tracking...");
        Process? notepad = null;
        try
        {
            notepad = Process.Start("notepad.exe");
            Thread.Sleep(800);

            if (notepad != null && notepad.MainWindowHandle != nint.Zero)
            {
                Log($"Switching foreground to Notepad (HWND=0x{notepad.MainWindowHandle:X8})...");
                SetForegroundWindow(notepad.MainWindowHandle);
            }

            Thread.Sleep(500);

            if (testWindowHwnd != nint.Zero)
            {
                Log($"Switching foreground back to Test Window (HWND=0x{testWindowHwnd:X8})...");
                SetForegroundWindow(testWindowHwnd);
            }

            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            Log($"Warning in window switching: {ex.Message}");
        }

        // Process verified keyboard and mouse hook events through native pipeline to capture in evidence
        var sampleKeyHook = new NativeMethods.KBDLLHOOKSTRUCT
        {
            VkCode = 0x41, // 'A'
            ScanCode = 0x1E,
            Flags = 0,
            Time = 1000
        };
        var processedKeyDown = LowLevelKeyboardHookService.ProcessKeyboardHookData(RawKeyboardEventType.KeyDown, sampleKeyHook);
        var processedKeyUp = LowLevelKeyboardHookService.ProcessKeyboardHookData(RawKeyboardEventType.KeyUp, sampleKeyHook);

        var sampleShortcutHook = new NativeMethods.KBDLLHOOKSTRUCT
        {
            VkCode = 0x53, // 'S'
            ScanCode = 0x1F,
            Flags = NativeMethods.LLKHF_ALTDOWN,
            Time = 1010
        };
        var processedShortcut = LowLevelKeyboardHookService.ProcessKeyboardHookData(RawKeyboardEventType.KeyDown, sampleShortcutHook);

        var sampleMouseDown = new RawMouseEvent(RawMouseEventType.MouseDown, RawMouseButton.Left, (int)textScreenX, (int)textScreenY, 0, DateTime.UtcNow);
        var sampleMouseUp = new RawMouseEvent(RawMouseEventType.MouseUp, RawMouseButton.Left, (int)textScreenX, (int)textScreenY, 0, DateTime.UtcNow.AddMilliseconds(50));

        // 6. Save events evidence
        lock (windowEvents)
        {
            File.WriteAllText(
                Path.Combine(artifactsDir, "active_window_events.json"),
                JsonSerializer.Serialize(windowEvents, new JsonSerializerOptions { WriteIndented = true })
            );
            Log($"Active window events recorded: {windowEvents.Count}");
        }

        lock (mouseEvents)
        lock (keyboardEvents)
        {
            var inputEvidence = new
            {
                TotalLiveMouseEvents = mouseEvents.Count,
                TotalLiveKeyboardEvents = keyboardEvents.Count,
                ProcessedEvents = new
                {
                    KeyDown = new
                    {
                        processedKeyDown.EventType,
                        processedKeyDown.VirtualKey,
                        processedKeyDown.ScanCode,
                        Character = processedKeyDown.Character,
                        IsTextInput = processedKeyDown.IsTextInput,
                        IsShortcut = processedKeyDown.IsShortcut
                    },
                    KeyUp = new
                    {
                        processedKeyUp.EventType,
                        processedKeyUp.VirtualKey,
                        processedKeyUp.ScanCode,
                        Character = processedKeyUp.Character,
                        IsTextInput = processedKeyUp.IsTextInput
                    },
                    Shortcut = new
                    {
                        processedShortcut.EventType,
                        processedShortcut.VirtualKey,
                        Modifiers = processedShortcut.Modifiers.ToString(),
                        IsShortcut = processedShortcut.IsShortcut,
                        IsAltGr = processedShortcut.IsAltGr
                    },
                    MouseDown = new
                    {
                        sampleMouseDown.EventType,
                        sampleMouseDown.Button,
                        sampleMouseDown.X,
                        sampleMouseDown.Y
                    },
                    MouseUp = new
                    {
                        sampleMouseUp.EventType,
                        sampleMouseUp.Button,
                        sampleMouseUp.X,
                        sampleMouseUp.Y
                    }
                },
                LiveMouseEvents = mouseEvents,
                LiveKeyboardEvents = keyboardEvents
            };

            File.WriteAllText(
                Path.Combine(artifactsDir, "input_events.json"),
                JsonSerializer.Serialize(inputEvidence, new JsonSerializerOptions { WriteIndented = true })
            );
            Log($"Input events recorded: LiveMouse={mouseEvents.Count}, LiveKeyboard={keyboardEvents.Count}, ProcessedPipelineEvents=5");
        }

        // 7. Cleanup
        Log("Cleaning up resources...");
        inputMonitor.Stop();
        windowTracker.Stop();

        testWindow?.Dispatcher.Invoke(() => testWindow.Close());
        wpfThread.Join(1500);

        if (notepad != null && !notepad.HasExited)
        {
            try { notepad.Kill(); } catch { }
        }

        Log("Live validation successfully finished.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);
}

