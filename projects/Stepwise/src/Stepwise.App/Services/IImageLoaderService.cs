using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Stepwise.App.Services;

/// <summary>
/// Сервис асинхронной загрузки и кэширования изображений скриншотов с защитой от race condition
/// и гарантированным освобождением файловых дескрипторов (Разделы 12-13 specs/spec.md).
/// </summary>
public interface IImageLoaderService
{
    /// <summary>
    /// Загружает легковесную миниатюру скриншота с оптимизацией декодирования (по умолчанию 180px).
    /// </summary>
    Task<BitmapImage?> LoadThumbnailAsync(string? filePath, int decodePixelWidth = 180, CancellationToken ct = default);

    /// <summary>
    /// Загружает полноразмерный скриншот для центрального просмотрщика.
    /// </summary>
    Task<BitmapImage?> LoadPreviewAsync(string? filePath, CancellationToken ct = default);
}
