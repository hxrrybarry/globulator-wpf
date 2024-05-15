using System.IO;

namespace globulator;

internal class FileWrapper(string fileName, byte[] bytes)
{
    public string FileName { get; } = fileName;
    public byte[] Bytes { get; } = bytes;

    public static bool FileOrDirectoryExists(string path) => Directory.Exists(path) || File.Exists(path);
    public static bool FileIsImage(string path) => path.EndsWith("png") || path.EndsWith("jpg") || path.EndsWith("jpeg") || path.EndsWith("bmp") || path.EndsWith("gif");
}