using System.Diagnostics;
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
        ArgumentNullException.ThrowIfNull(target);

        if (_captureService == null || _repository == null)
        {
            return null;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return _captureService.Capture(
                    _repository.ProjectRootPath,
                    sequenceIndex,
                    target.BoundingRectangle,
                    target.WindowHandle
                );
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaptureCoordinator] Warning: Capture failed for step {sequenceIndex}: {ex.Message}");
            return null;
        }
    }
}
