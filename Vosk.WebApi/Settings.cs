using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Vosk.WebApi;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed class Settings
{
    [Required]
    public string FFmpegApiUrl { get; set; } = null!;
    [Required]
    public string VoskWebSocketUrl { get; init; } = null!;
    [Required]
    public int ResultChunkSize { get; init; }
    [Required]
    public int WavSamplingRateHz { get; init; }
    [Required]
    public int WavBitRate { get; init; }

    /// <summary>
    /// Если включено, то любой WAV-файл будет конвертироваться в заданный формат.
    /// Иначе WAV будет сразу передаваться в VOSK.
    /// TODO: Передавать в запросе с каждым файлом.
    /// </summary>
    public bool ForceWavConversion { get; init; }
}