namespace UniversalMediaPlayer.Persistence;

using System.Text.Json;
using System.Text.Json.Serialization;
using UniversalMediaPlayer.Core.Models;
using UniversalMediaPlayer.Core.Persistence;

public sealed class JsonShowPreferencesStore : IShowPreferencesStore, IDisposable
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public JsonShowPreferencesStore(string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filePath = Path.Combine(localAppData, "UniversalMediaPlayer", "config", "show_preferences.json");
        }
        else
        {
            _filePath = Path.GetFullPath(filePath);
        }
    }

    public async Task<ShowPreferences?> GetPreferencesAsync(string showId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showId);
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var preferences = await LoadInternalAsync(ct).ConfigureAwait(false);
            return preferences.TryGetValue(showId, out var pref) ? pref : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SavePreferencesAsync(ShowPreferences preferences, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferences.ShowId);
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await LoadInternalAsync(ct).ConfigureAwait(false);
            all[preferences.ShowId] = preferences;
            await SaveInternalAsync(all, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, ShowPreferences>> GetAllPreferencesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var preferences = await LoadInternalAsync(ct).ConfigureAwait(false);
            return preferences;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, ShowPreferences>> LoadInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, ShowPreferences>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            if (stream.Length == 0)
            {
                return new Dictionary<string, ShowPreferences>(StringComparer.OrdinalIgnoreCase);
            }

            var doc = await JsonSerializer.DeserializeAsync<ShowPreferencesDocument>(
                stream,
                SerializerOptions,
                ct).ConfigureAwait(false);

            var dict = new Dictionary<string, ShowPreferences>(StringComparer.OrdinalIgnoreCase);
            if (doc?.ShowPreferences != null)
            {
                foreach (var kvp in doc.ShowPreferences)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }

            return dict;
        }
        catch (JsonException)
        {
            return new Dictionary<string, ShowPreferences>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveInternalAsync(Dictionary<string, ShowPreferences> all, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        var doc = new ShowPreferencesDocument
        {
            Version = 1,
            ShowPreferences = all
        };

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, doc, SerializerOptions, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Move(tempPath, _filePath, overwrite: true);
                    break;
                }
                catch (Exception ex) when ((ex is UnauthorizedAccessException || ex is IOException) && attempt < 4)
                {
                    await Task.Delay(25, ct).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best effort cleanup of temp file
                }
            }
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lock.Dispose();
        }
    }

    private sealed class ShowPreferencesDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("showPreferences")]
        public Dictionary<string, ShowPreferences> ShowPreferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
