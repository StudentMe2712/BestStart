using System.Text;
using System.Windows;

namespace SelectCast.Core.Capture;

/// <summary>
/// Snapshot of the entire clipboard so it can be restored 1:1 after we hijack it
/// for a synthetic Ctrl+C. The clipboard is sacred: a naive "save the string, put the
/// string back" loses images/files, so we capture every raw format present.
/// Must be used on an STA thread (the WPF dispatcher thread is STA).
/// </summary>
public sealed class ClipboardSnapshot
{
    private readonly List<(string Format, object Data)> _data = new();
    private readonly bool _wasEmpty;

    private ClipboardSnapshot(bool wasEmpty) => _wasEmpty = wasEmpty;

    public static ClipboardSnapshot Capture(StringBuilder diag)
    {
        IDataObject? src = null;
        try
        {
            src = Clipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            diag.AppendLine("snapshot: GetDataObject failed — " + ex.Message);
        }

        if (src is null)
        {
            diag.AppendLine("snapshot: clipboard empty/unreadable");
            return new ClipboardSnapshot(wasEmpty: true);
        }

        var snap = new ClipboardSnapshot(wasEmpty: false);

        string[] formats;
        try
        {
            // autoConvert: false → exactly the formats physically on the clipboard,
            // so restoring them reproduces the original without synthesizing extras.
            formats = src.GetFormats(false);
        }
        catch
        {
            formats = Array.Empty<string>();
        }

        foreach (var fmt in formats)
        {
            try
            {
                var data = src.GetData(fmt, false);
                if (data is not null)
                    snap._data.Add((fmt, data));
            }
            catch (Exception ex)
            {
                diag.AppendLine($"snapshot: skip format '{fmt}' ({ex.GetType().Name})");
            }
        }

        diag.AppendLine($"snapshot: saved {snap._data.Count} format(s)");
        return snap;
    }

    public void Restore(StringBuilder diag)
    {
        if (_wasEmpty && _data.Count == 0)
        {
            try
            {
                Clipboard.Clear();
                diag.AppendLine("restore: cleared (clipboard was empty before)");
            }
            catch (Exception ex)
            {
                diag.AppendLine("restore: clear failed — " + ex.Message);
            }
            return;
        }

        if (_data.Count == 0)
        {
            // Snapshot failed but the clipboard was not empty: do nothing rather than
            // wipe the user's data.
            diag.AppendLine("restore: nothing captured, leaving clipboard untouched");
            return;
        }

        var obj = new DataObject();
        int restored = 0;
        foreach (var (fmt, data) in _data)
        {
            try
            {
                obj.SetData(fmt, data);
                restored++;
            }
            catch (Exception ex)
            {
                diag.AppendLine($"restore: skip format '{fmt}' ({ex.GetType().Name})");
            }
        }

        try
        {
            // copy: true → data persists on the clipboard after this process exits.
            Clipboard.SetDataObject(obj, true);
            diag.AppendLine($"restore: wrote {restored} format(s) back");
        }
        catch (Exception ex)
        {
            diag.AppendLine("restore: SetDataObject failed — " + ex.Message);
        }
    }
}
