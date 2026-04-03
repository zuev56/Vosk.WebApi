using System.Reflection;
using AudioConverter;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Vosk.Common;

var builder = WebApplication.CreateSlimBuilder(args);

builder.ConfigureExternalAppConfiguration(args, Assembly.GetAssembly(typeof(Program))!);

builder.Services.AddSingleton<AudioConverterService>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var voskApi = app.MapGroup("/convert");

voskApi.MapPost("/to/wav", async (
        HttpContext httpContext,
        [FromForm] IFormFile file,
        [FromForm] AudioConverterOptions options,
        [FromServices] AudioConverterService converter,
        CancellationToken cancellationToken) =>
    {
        var validationResult =
            await RequestValidator.ValidateAsync(httpContext, AudioFormat.Mp3 | AudioFormat.Wma, cancellationToken);
        if (!validationResult.Succeeded)
            return Results.BadRequest(validationResult.Error);

        var resultBytes = await converter.ConvertToWav(file, validationResult.Format!.Value, options);

        if (resultBytes.Length > 0)
            return Results.File(resultBytes, "audio/wav", Path.ChangeExtension(file.FileName, ".wav"));

        return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    })
    .DisableAntiforgery()
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<FileContentResult>(StatusCodes.Status200OK, "audio/wav")
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status422UnprocessableEntity)
    .WithName("ConvertToWav")
    .WithDescription("Converts an audio file to a WAV file.");

app.Run();