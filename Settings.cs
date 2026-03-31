using System.ComponentModel.DataAnnotations;

namespace Vosk.WebApi;

public sealed class Settings
{
    [Required]
    public string VoskWebSocketUrl { get; init; } = null!;
    [Required]
    public int ResultChunkSize { get; init; }
    [Required]
    public int WavSamplingRateHz { get; init; }
    [Required]
    public int WavBitRate { get; init; }
}
