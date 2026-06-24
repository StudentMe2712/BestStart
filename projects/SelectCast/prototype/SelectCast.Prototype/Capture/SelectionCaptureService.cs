using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using SelectCast.Prototype.Interop;

namespace SelectCast.Prototype.Capture;

/// <summary>
/// Captures the current selection of the foreground window: snapshot clipboard →
/// synthetic Ctrl+C → wait for the clipboard to actually change → read text →
/// restore the original clipboard (always, even on error).
/// Run on the WPF dispatcher (STA) thread; the awaits resume there too.
/// </summary>
public sealed class SelectionCaptureService
{
    public async Task<CaptureResult> CaptureAsync(int timeoutMs = 700, int pollIntervalMs = 15)
    {
        var diag = new StringBuilder();
        ClipboardSnapshot? snapshot = null;
        var sw = new Stopwatch();
        try
        {
            snapshot = ClipboardSnapshot.Capture(diag);

            uint baseline = NativeMethods.GetClipboardSequenceNumber();
            diag.AppendLine($"baseline clipboard seq = {baseline}");

            SendCopyKeystroke(diag);

            sw.Start();
            bool changed = false;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(pollIntervalMs).ConfigureAwait(true);
                if (NativeMethods.GetClipboardSequenceNumber() != baseline)
                {
                    changed = true;
                    break;
                }
            }
            sw.Stop();

            if (!changed)
            {
                diag.AppendLine($"FALLBACK: clipboard unchanged within {timeoutMs} ms");
                return new CaptureResult(CaptureStatus.NoSelection, null, sw.ElapsedMilliseconds, diag.ToString());
            }

            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                {
                    diag.AppendLine("clipboard changed, text empty → treat as non-text");
                    return new CaptureResult(CaptureStatus.NonText, null, sw.ElapsedMilliseconds, diag.ToString());
                }

                diag.AppendLine($"captured {text.Length} char(s) in {sw.ElapsedMilliseconds} ms");
                return new CaptureResult(CaptureStatus.Captured, text, sw.ElapsedMilliseconds, diag.ToString());
            }

            diag.AppendLine("clipboard changed but holds no text (image/files selected)");
            return new CaptureResult(CaptureStatus.NonText, null, sw.ElapsedMilliseconds, diag.ToString());
        }
        catch (Exception ex)
        {
            diag.AppendLine("EXCEPTION: " + ex);
            return new CaptureResult(CaptureStatus.Error, null, sw.ElapsedMilliseconds, diag.ToString());
        }
        finally
        {
            // Clipboard is sacred — restore no matter what happened above.
            try
            {
                snapshot?.Restore(diag);
            }
            catch (Exception ex)
            {
                diag.AppendLine("RESTORE FAILED: " + ex.Message);
            }
        }
    }

    private static void SendCopyKeystroke(StringBuilder diag)
    {
        var inputs = new List<NativeMethods.INPUT>(8);

        void Key(ushort vk, bool up) => inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = up ? NativeMethods.KEYEVENTF_KEYUP : 0,
                },
            },
        });

        // The hotkey is Ctrl+Alt+C, so at this instant the user physically holds Ctrl+Alt.
        // Release Alt/Shift/Win first, otherwise the target receives Ctrl+Alt+C, not a copy.
        if (NativeMethods.IsKeyDown(NativeMethods.VK_MENU)) { Key(NativeMethods.VK_MENU, true); diag.AppendLine("release ALT"); }
        if (NativeMethods.IsKeyDown(NativeMethods.VK_SHIFT)) { Key(NativeMethods.VK_SHIFT, true); diag.AppendLine("release SHIFT"); }
        if (NativeMethods.IsKeyDown(NativeMethods.VK_LWIN)) { Key(NativeMethods.VK_LWIN, true); }
        if (NativeMethods.IsKeyDown(NativeMethods.VK_RWIN)) { Key(NativeMethods.VK_RWIN, true); }

        // Ctrl down, C down, C up, Ctrl up.
        Key(NativeMethods.VK_CONTROL, false);
        Key(NativeMethods.VK_C, false);
        Key(NativeMethods.VK_C, true);
        Key(NativeMethods.VK_CONTROL, true);

        var arr = inputs.ToArray();
        uint sent = NativeMethods.SendInput((uint)arr.Length, arr, Marshal.SizeOf<NativeMethods.INPUT>());
        diag.AppendLine($"SendInput injected {sent}/{arr.Length} key event(s)");
    }
}
