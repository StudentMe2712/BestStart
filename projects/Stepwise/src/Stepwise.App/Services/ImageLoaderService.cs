using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Stepwise.App.Services;

/// <summary>
/// Реализация IImageLoaderService строго в соответствии с Разделами 12-13 specs/spec.md.
/// - Неблокирующее чтение файла с немедленным освобождением дескриптора (Dispose stream).
/// - Декодирование BitmapImage на UI-потоке с контролем размера миниатюры (DecodePixelWidth).
/// - Защита от race conditions при быстром переключении шагов через CancellationToken.
/// - Безопасная обработка отсутствующих или поврежденных файлов.
/// - Безопасная запись через DataWriter c writer.DetachStream() во избежание закрытия IRandomAccessStream.
/// </summary>
public sealed class ImageLoaderService : IImageLoaderService
{
    private readonly DispatcherQueue? _dispatcherQueue;

    public ImageLoaderService(DispatcherQueue? dispatcherQueue = null)
    {
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
    }

    public Task<BitmapImage?> LoadThumbnailAsync(string? filePath, int decodePixelWidth = 180, CancellationToken ct = default)
    {
        return LoadImageInternalAsync(filePath, decodePixelWidth, ct);
    }

    public Task<BitmapImage?> LoadPreviewAsync(string? filePath, CancellationToken ct = default)
    {
        return LoadImageInternalAsync(filePath, decodePixelWidth: 0, ct);
    }

    private async Task<BitmapImage?> LoadImageInternalAsync(string? filePath, int decodePixelWidth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return null;
        }

        byte[] fileBytes;
        try
        {
            // Читаем байты скриншота в память и НЕМЕДЛЕННО закрываем FileStream
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fileStream.Length == 0)
            {
                return null;
            }

            fileBytes = new byte[fileStream.Length];
            await fileStream.ReadExactlyAsync(fileBytes, 0, (int)fileStream.Length, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageLoaderService] Не удалось прочитать файл '{filePath}': {ex.Message}");
            return null;
        }

        ct.ThrowIfCancellationRequested();

        // UI Thread Marshalling: создание и декодирование BitmapImage
        return await RunOnUIThreadAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var bitmap = new BitmapImage();
                if (decodePixelWidth > 0)
                {
                    bitmap.DecodePixelWidth = decodePixelWidth;
                }

                var ras = new InMemoryRandomAccessStream();
                try
                {
                    using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(fileBytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream(); // Обязательный DetachStream, чтобы writer.Dispose() не закрыл базовый ras
                    }

                    ras.Seek(0);
                    await bitmap.SetSourceAsync(ras);
                }
                finally
                {
                    // Освобождаем промежуточный поток в памяти
                    ras.Dispose();
                }

                return bitmap;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageLoaderService] Ошибка декодирования изображения '{filePath}': {ex.Message}");
                return null;
            }
        }).ConfigureAwait(false);
    }

    private Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> action)
    {
        if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var result = await action().ConfigureAwait(false);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue image task to UI thread DispatcherQueue."));
        }

        return tcs.Task;
    }
}
