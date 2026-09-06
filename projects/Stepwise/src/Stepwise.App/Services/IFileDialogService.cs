using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Stepwise.App.Services;

/// <summary>
/// Сервис открытия системных диалоговых окон выбора и сохранения файлов.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Показывает системный диалог сохранения файла и возвращает выбранный путь или null в случае отмены.
    /// </summary>
    Task<string?> ShowSaveFileDialogAsync(
        string title,
        string defaultFileName,
        string defaultExtension,
        string filter,
        nint ownerHwnd = 0);
}

/// <summary>
/// Высоконадежная реализация IFileDialogService на базе Win32 GetSaveFileNameW (comdlg32.dll).
/// Гарантирует корректную работу в любых средах Windows без ограничений WinRT shell-брокеров.
/// </summary>
public sealed class NativeFileDialogService : IFileDialogService
{
    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_EXPLORER = 0x00080000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAMEW
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpstrFilter;
        public nint lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref OPENFILENAMEW ofn);

    public Task<string?> ShowSaveFileDialogAsync(
        string title,
        string defaultFileName,
        string defaultExtension,
        string filter,
        nint ownerHwnd = 0)
    {
        return Task.Run(() =>
        {
            const int maxPathChars = 2048;
            var fileBufferPtr = Marshal.AllocHGlobal(maxPathChars * sizeof(char));
            try
            {
                var initialChars = new char[maxPathChars];
                var nameWithoutExt = Path.GetFileNameWithoutExtension(defaultFileName);
                var ext = defaultExtension.TrimStart('.');
                var initialName = $"{nameWithoutExt}.{ext}";
                initialName.CopyTo(0, initialChars, 0, Math.Min(initialName.Length, maxPathChars - 1));
                Marshal.Copy(initialChars, 0, fileBufferPtr, maxPathChars);

                // Нормализация фильтра для Win32 (разделитель \0, заканчивается двойным \0)
                var formattedFilter = filter.Replace('|', '\0') + "\0\0";

                var initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                var ofn = new OPENFILENAMEW
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAMEW>(),
                    hwndOwner = ownerHwnd,
                    lpstrFilter = formattedFilter,
                    lpstrFile = fileBufferPtr,
                    nMaxFile = maxPathChars,
                    lpstrInitialDir = Directory.Exists(initialDir) ? initialDir : null,
                    lpstrTitle = title,
                    Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_EXPLORER,
                    lpstrDefExt = ext
                };

                if (GetSaveFileNameW(ref ofn))
                {
                    var result = Marshal.PtrToStringUni(fileBufferPtr);
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        return result;
                    }
                }

                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(fileBufferPtr);
            }
        });
    }
}
