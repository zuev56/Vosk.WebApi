using Vosk.WebApi.Models;

namespace Vosk.WebApi.Services;

public sealed record ValidationResult(bool Succeeded, IFormFile? File = null, AudioFormat? Format = AudioFormat.Unknown, string? Error = null);

public static class RequestValidator
{
    public static async Task<ValidationResult> ValidateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
            return new ValidationResult(Succeeded: false, Error: "Expected multipart/form-data content type");

        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException ex)
            when (ex.Message.Contains("Missing content-type boundary"))
        {
            return new ValidationResult(Succeeded: false, Error: "Invalid request format: missing boundary in Content-Type header for multipart/form-data");
        }

        var file = form.Files.FirstOrDefault();

        if (file == null || file.Length == 0)
            return new ValidationResult(Succeeded: false, file, Error: "No audio file provided.");

        var format = file.ContentType.ToLower() switch
        {
            "audio/wav" or "audio/x-wav" => AudioFormat.Wav,
            "audio/mpeg" or "audio/mp3" => AudioFormat.Mp3,
            "audio/x-ms-wma" => AudioFormat.Wma,
            "audio/ogg" or "audio/oga" or "application/ogg" => AudioFormat.Ogg,
            _ => AudioFormat.Unknown
        };

        if (format == AudioFormat.Unknown || (format & ~AudioFormat.Known) == 0)
            return new ValidationResult(Succeeded: true, file, format);

        return new ValidationResult(Succeeded: false, file, format, Error: $"Unexpected audio format: {file.ContentType}");
    }
}