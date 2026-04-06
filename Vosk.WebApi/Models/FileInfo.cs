namespace Vosk.WebApi.Models;

public sealed class FileInfo
{
    public required Stream Data { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}