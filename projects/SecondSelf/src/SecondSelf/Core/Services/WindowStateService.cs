using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация сервиса сохранения и загрузки состояния окна виджета.
/// Координаты сохраняются в формате JSON (Settings/window_state.json).
/// </summary>
public sealed class WindowStateService : IWindowStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    public string SettingsFilePath { get; }

    public WindowStateService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsPath))
        {
            SettingsFilePath = Path.GetFullPath(customSettingsPath);
        }
        else
        {
            var envPath = Environment.GetEnvironmentVariable("SECONDSELF_WINDOW_STATE_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                SettingsFilePath = Path.GetFullPath(envPath);
            }
            else
            {
                SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "Settings", "window_state.json");
            }
        }
    }

    public async Task<WindowState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new WindowState();
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WindowState();
            }

            var state = JsonSerializer.Deserialize<WindowState>(json, JsonOptions);
            return state ?? new WindowState();
        }
        catch
        {
            // При ошибке чтения или битом JSON возвращаем безопасные настройки по умолчанию
            return new WindowState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveStateAsync(WindowState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tempFilePath = SettingsFilePath + ".tmp";

            await File.WriteAllTextAsync(tempFilePath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempFilePath, SettingsFilePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public (int X, int Y) CalculateWindowPosition(
        WindowState state,
        int windowWidth,
        int windowHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Ограничиваем нормализованные координаты диапазоном [0.0 .. 1.0]
        double normX = Math.Clamp(state.NormalizedX, 0.0, 1.0);
        double normY = Math.Clamp(state.NormalizedY, 0.0, 1.0);

        int targetX;
        if (workAreaWidth <= windowWidth)
        {
            targetX = workAreaX;
        }
        else
        {
            int maxOffsetX = workAreaWidth - windowWidth;
            targetX = workAreaX + (int)Math.Round(normX * maxOffsetX);
            targetX = Math.Clamp(targetX, workAreaX, workAreaX + maxOffsetX);
        }

        int targetY;
        if (workAreaHeight <= windowHeight)
        {
            targetY = workAreaY;
        }
        else
        {
            int maxOffsetY = workAreaHeight - windowHeight;
            targetY = workAreaY + (int)Math.Round(normY * maxOffsetY);
            targetY = Math.Clamp(targetY, workAreaY, workAreaY + maxOffsetY);
        }

        return (targetX, targetY);
    }

    public (double NormalizedX, double NormalizedY) CalculateNormalizedCoordinates(
        int currentX,
        int currentY,
        int windowWidth,
        int windowHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight)
    {
        double normX = 0.0;
        if (workAreaWidth > windowWidth)
        {
            int offset = currentX - workAreaX;
            int maxOffset = workAreaWidth - windowWidth;
            normX = Math.Clamp((double)offset / maxOffset, 0.0, 1.0);
        }

        double normY = 0.0;
        if (workAreaHeight > windowHeight)
        {
            int offset = currentY - workAreaY;
            int maxOffset = workAreaHeight - windowHeight;
            normY = Math.Clamp((double)offset / maxOffset, 0.0, 1.0);
        }

        return (normX, normY);
    }

    public WindowPosition EnsureSafePosition(WindowPosition pos, System.Collections.Generic.IReadOnlyList<Microsoft.UI.Windowing.DisplayArea> activeMonitors)
    {
        ArgumentNullException.ThrowIfNull(pos);
        if (activeMonitors == null || activeMonitors.Count == 0)
        {
            return pos;
        }

        var converted = new DisplayArea[activeMonitors.Count];
        for (int i = 0; i < activeMonitors.Count; i++)
        {
            converted[i] = DisplayArea.FromDisplayArea(activeMonitors[i]);
        }
        return EnsureSafePosition(pos, converted);
    }

    public WindowPosition EnsureSafePosition(WindowPosition pos, System.Collections.Generic.IReadOnlyList<DisplayArea> activeMonitors)
    {
        ArgumentNullException.ThrowIfNull(pos);
        if (activeMonitors == null || activeMonitors.Count == 0)
        {
            return pos;
        }

        int wLeft = pos.X;
        int wTop = pos.Y;
        int wRight = pos.X + pos.Width;
        int wBottom = pos.Y + pos.Height;

        bool intersectsAny = false;
        foreach (var monitor in activeMonitors)
        {
            int mLeft = monitor.WorkArea.X;
            int mTop = monitor.WorkArea.Y;
            int mRight = monitor.WorkArea.X + monitor.WorkArea.Width;
            int mBottom = monitor.WorkArea.Y + monitor.WorkArea.Height;

            if (wLeft < mRight && wRight > mLeft && wTop < mBottom && wBottom > mTop)
            {
                intersectsAny = true;
                break;
            }
        }

        if (intersectsAny)
        {
            return pos;
        }

        // Монитор отключен или виджет вне видимых экранов:
        // Перемещаем / зажимаем в границы основного монитора с безопасным отступом 24px
        DisplayArea? primary = null;
        for (int i = 0; i < activeMonitors.Count; i++)
        {
            if (activeMonitors[i].IsPrimary)
            {
                primary = activeMonitors[i];
                break;
            }
        }
        primary ??= activeMonitors[0];

        const int safeMargin = 24;
        int minX = primary.WorkArea.X + safeMargin;
        int maxX = primary.WorkArea.X + primary.WorkArea.Width - pos.Width - safeMargin;
        int minY = primary.WorkArea.Y + safeMargin;
        int maxY = primary.WorkArea.Y + primary.WorkArea.Height - pos.Height - safeMargin;

        if (maxX < minX) maxX = minX;
        if (maxY < minY) maxY = minY;

        int safeX = Math.Clamp(pos.X, minX, maxX);
        int safeY = Math.Clamp(pos.Y, minY, maxY);

        return new WindowPosition(safeX, safeY, pos.Width, pos.Height);
    }
}
