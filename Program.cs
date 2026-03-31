using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Vosk.WebApi;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOptions<Settings>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var voskApi = app.MapGroup("/vosk");

voskApi.MapPost("/transcribe", async (HttpContext httpContext, [FromServices] TranscriptionService transcriptionService, CancellationToken cancellationToken) =>
    {
        if (!httpContext.Request.HasFormContentType)
            return Results.BadRequest("Expected multipart/form-data content type");

        var form = await httpContext.Request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();

        if (file == null || file.Length == 0)
            return Results.BadRequest("No audio file provided.");

        var inputFormat = file.ContentType.ToLower() switch
        {
            "audio/wav" or "audio/x-wav" => AudioFormat.Wav,
            "audio/mpeg" or "audio/mp3" => AudioFormat.Mp3,
            _ => AudioFormat.Unsupported
        };

        if (inputFormat == AudioFormat.Unsupported)
            Results.BadRequest($"Unsupported audio format: {file.ContentType}");

        await using var stream = file.OpenReadStream();

        var finalResult = inputFormat switch
        {
            AudioFormat.Wav => await transcriptionService.TranscribeWav(stream, cancellationToken),
            AudioFormat.Mp3 => await transcriptionService.TranscribeMp3(stream, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(inputFormat))
        };

        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        return acceptHeader switch
        {
            _ when acceptHeader.Contains("application/json") => Results.Ok(finalResult),
            _ when acceptHeader.Contains("text/plain") => Results.Text(
                string.Join(Environment.NewLine, (IEnumerable<string>)finalResult.Select(r => r.GetString("text")))),
            _ => Results.Ok(finalResult)
        };
    })
    .DisableAntiforgery()
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .Produces<object>(StatusCodes.Status200OK, "application/json")
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("TranscribeAudio")
    .WithDescription("Available \"Accept\" headers are \"text/plain\", \"application/json\"");

app.Run();

internal enum AudioFormat { Unsupported, Mp3, Wav }