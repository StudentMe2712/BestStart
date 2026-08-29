using System.IO;

namespace DropZone.Models;

public class FileItem
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }

    public FileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(FileName))
        {
            FileName = filePath;
        }

        string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            Extension = Directory.Exists(filePath) ? "DIR" : "FILE";
        }
        else
        {
            Extension = ext;
        }
    }
}
