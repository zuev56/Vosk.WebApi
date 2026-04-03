using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Vosk.Common;

namespace Vosk.WebApi;

public sealed class TranscriptionService
{
    private readonly AudioConverterClient _converter;
    private readonly Settings _settings;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(AudioConverterClient converter, IOptions<Settings> settings, ILogger<TranscriptionService> logger)
    {
        _converter = converter;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<List<JsonElement>> TranscribeAsync(
        IFormFile file,
        AudioFormat sourceFormat,
        CancellationToken cancellationToken)
    {
        Stream? wavFileStream = null;
        try
        {
            if (sourceFormat == AudioFormat.Wav)
            {
                // TODO: Если характеристики wav не соответствуют настройкам, надо сначала выполнить преобразование!
                wavFileStream = file.OpenReadStream();
            }
            else
            {
                var sw = Stopwatch.StartNew();
                var wavOptions = new AudioConverterOptions(_settings.WavSamplingRateHz, _settings.WavBitRate, Channels: 1);
                var sourceFileParameter = new FileParameter
                {
                    Data = file.OpenReadStream(),
                    ContentType = file.ContentType,
                    FileName = file.FileName
                };
                var convertedFileResult = await _converter.ConvertToWavAsync(sourceFileParameter, wavOptions , cancellationToken);
                _logger.LogInformation($"Conversion to wave {sw.ElapsedMilliseconds}ms. Source file size: {sourceFileParameter.Data.Length} bytes, name: \"{file.FileName}\"");
                wavFileStream = new MemoryStream(convertedFileResult.FileContents);
            }

            var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(_settings.WebSocketUrl), cancellationToken);

            var data = new byte[8000];
            var results = new List<JsonElement>();
            while (true)
            {
                var count = await wavFileStream.ReadAsync(data.AsMemory(0, 8000), cancellationToken);
                if (count == 0)
                    break;

                await webSocket.SendAsync(new ArraySegment<byte>(data, 0, count), WebSocketMessageType.Binary, true, cancellationToken);

                var part = await ReceiveResult(webSocket, cancellationToken);
                if (part.GetString("result") != null)
                    results.Add(part);
            }

            await webSocket.SendAsync(new ArraySegment<byte>("{\"eof\" : 1}"u8.ToArray()), WebSocketMessageType.Text, true, cancellationToken);

            results.Add(await ReceiveResult(webSocket, cancellationToken));

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", cancellationToken);

            return results;
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            wavFileStream?.Dispose();
        }
    }

    private async Task<JsonElement> ReceiveResult(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var result = new byte[_settings.ResultChunkSize];
        var webSocketReceiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(result), cancellationToken);

        var jsonStr = Encoding.UTF8.GetString(result, 0, webSocketReceiveResult.Count);

        return JsonSerializer.Deserialize(jsonStr, AppJsonSerializerContext.Default.JsonElement);
    }
}