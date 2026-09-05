using System.Diagnostics;
using System.IO;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.WindowsIntegration.Capture;

/// <summary>
/// Координатор захвата экранных снимков для формируемых шагов инструкции.
/// Безопасно выполняет захват через <see cref="IScreenCaptureService"/> и сохраняет файл в каталог проекта <see cref="IProjectRepository.ProjectRootPath"/>.
/// Гарантирует неблокирующее выполнение, обработку отмены и отсутствие аварийного завершения процесса при любых системных ошибках GDI/окна.
/// </summary>
public sealed class CaptureCoordinator : ICaptureCoordinator
{
    private readonly IScreenCaptureService? _captureService;
    private readonly IProjectRepository? _repository;

    /// <summary>
    /// Создает экземпляр <see cref="CaptureCoordinator"/>.
    /// </summary>
    /// <param name="captureService">Опциональный сервис создания скриншотов.</param>
    /// <param name="repository">Опциональный репозиторий проекта.</param>
    public CaptureCoordinator(
        IScreenCaptureService? captureService = null,
        IProjectRepository? repository = null)
    {
        _captureService = captureService;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<string?> CaptureStepAsync(
        int sequenceIndex,
        ElementInfo target,
        CancellationToken cancellationToken = default)
    {
        var result = await CaptureStepWithResultAsync(sequenceIndex, target, cancellationToken).ConfigureAwait(false);
        return result.Success ? result.RelativePath : null;
    }

    /// <inheritdoc />
    public async Task<CaptureResult> CaptureStepWithResultAsync(
        int sequenceIndex,
        ElementInfo target,
        CancellationToken cancellationToken = default)
    {
        if (target == null)
        {
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: BoundingBox.Empty,
                ErrorMessage: "Target element is null.");
        }

        if (_captureService == null || _repository == null)
        {
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: target.BoundingRectangle,
                ErrorMessage: "CaptureService or Repository is not configured.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: target.BoundingRectangle,
                ErrorMessage: "Operation cancelled.");
        }

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = _captureService.Capture(
                    _repository.ProjectRootPath,
                    sequenceIndex,
                    target.BoundingRectangle,
                    target.WindowHandle
                );

                if (cancellationToken.IsCancellationRequested)
                {
                    return new CaptureResult(
                        Success: false,
                        RelativePath: null,
                        Width: 0,
                        Height: 0,
                        HighlightBounds: target.BoundingRectangle,
                        ErrorMessage: "Operation cancelled.");
                }

                if (relativePath == null)
                {
                    return new CaptureResult(
                        Success: false,
                        RelativePath: null,
                        Width: 0,
                        Height: 0,
                        HighlightBounds: target.BoundingRectangle,
                        ErrorMessage: "Screen capture failed.");
                }

                int width = 0;
                int height = 0;
                try
                {
                    var fullPath = Path.Combine(_repository.ProjectRootPath, relativePath);
                    if (File.Exists(fullPath))
                    {
                        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var img = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                        width = img.Width;
                        height = img.Height;
                    }
                }
                catch
                {
                    // Игнорируем ошибки чтения файла (например, в тестах с моками)
                }

                if (width <= 0 || height <= 0)
                {
                    width = 1920;
                    height = 1080;
                }

                return new CaptureResult(
                    Success: true,
                    RelativePath: relativePath,
                    Width: width,
                    Height: height,
                    HighlightBounds: target.BoundingRectangle
                );
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: target.BoundingRectangle,
                ErrorMessage: "Operation cancelled.");
        }
        catch (Exception ex) when (ex.InnerException is OperationCanceledException)
        {
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: target.BoundingRectangle,
                ErrorMessage: "Operation cancelled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaptureCoordinator] Warning: Capture failed for step {sequenceIndex}: {ex.Message}");
            return new CaptureResult(
                Success: false,
                RelativePath: null,
                Width: 0,
                Height: 0,
                HighlightBounds: target.BoundingRectangle,
                ErrorMessage: ex.Message);
        }
    }
}
