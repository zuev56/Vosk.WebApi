using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Vosk.Common;
using Vosk.WebApi;

var builder = WebApplication.CreateSlimBuilder(args);

builder.ConfigureExternalAppConfiguration(args, Assembly.GetAssembly(typeof(Program))!);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// TODO: В случае с AOT не работает биндинг конфигурации, хотя всё делал по инструкции (.NET 10.0.5)
// var voskSection = builder.Configuration.GetSection(Settings.SectionName);
// builder.Services.Configure<Settings>(voskSection);
var settings = new Settings
{
    AudioFileConverterUrl = builder.Configuration.GetValue<string>($"{Settings.SectionName}:{nameof(Settings.AudioFileConverterUrl)}")!,
    WebSocketUrl = builder.Configuration.GetValue<string>($"{Settings.SectionName}:{nameof(Settings.WebSocketUrl)}")!,
    ResultChunkSize = builder.Configuration.GetValue<int>($"{Settings.SectionName}:{nameof(Settings.ResultChunkSize)}"),
    WavSamplingRateHz = builder.Configuration.GetValue<int>($"{Settings.SectionName}:{nameof(Settings.WavSamplingRateHz)}"),
    WavBitRate = builder.Configuration.GetValue<int>($"{Settings.SectionName}:{nameof(Settings.WavBitRate)}")
};
builder.Services.AddSingleton<IOptions<Settings>>(new OptionsWrapper<Settings>(settings));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<AudioConverterClient>();
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
        var validationResult = await RequestValidator.ValidateAsync(httpContext, AudioFormat.Wav|AudioFormat.Mp3, cancellationToken);
        if (!validationResult.Succeeded)
            return Results.BadRequest(validationResult.Error);

        var finalResult = await transcriptionService.TranscribeAsync(validationResult.File!, validationResult.Format!.Value, cancellationToken);

        var acceptHeader = httpContext.Request.Headers.Accept.ToString();
        return acceptHeader switch
        {
            _ when acceptHeader.Contains("application/json") => Results.Ok(
                JsonSerializer.Serialize(finalResult, AppJsonSerializerContext.Default.ListJsonElement)),
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

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(IFormFile))]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<JsonElement>))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext;