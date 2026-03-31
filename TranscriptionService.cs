using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NAudio.Wave;

namespace Vosk.WebApi;

public sealed class TranscriptionService
{
    private readonly Settings _settings;

    public TranscriptionService(IOptions<Settings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<List<JsonElement>> TranscribeWav(Stream stream, CancellationToken cancellationToken)
    {
        // TODO: Если характеристики wav не соответствуют настройкам, надо сначала выполнить преобразование!

        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_settings.VoskWebSocketUrl), cancellationToken);

        var data = new byte[8000];
        var results = new List<JsonElement>();
        while (true)
        {
            var count = await stream.ReadAsync(data.AsMemory(0, 8000), cancellationToken);
            if (count == 0)
                break;

            await ws.SendAsync(new ArraySegment<byte>(data, 0, count), WebSocketMessageType.Binary, true, cancellationToken);

            var part = await ReceiveResult(ws, cancellationToken);
            if (part.GetString("result") != null)
                results.Add(part);
        }

        await ws.SendAsync(new ArraySegment<byte>("{\"eof\" : 1}"u8.ToArray()), WebSocketMessageType.Text, true, cancellationToken);

        results.Add(await ReceiveResult(ws, cancellationToken));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", cancellationToken);
        return results;
    }

    private async Task<JsonElement> ReceiveResult(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var result = new byte[_settings.ResultChunkSize];
        var webSocketReceiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(result), cancellationToken);

        var jsonStr = Encoding.UTF8.GetString(result, 0, webSocketReceiveResult.Count);

        return JsonSerializer.Deserialize<JsonElement>(jsonStr);
    }

    public async Task<List<JsonElement>> TranscribeMp3(Stream mp3Stream, CancellationToken cancellationToken)
    {
        try
        {
            await using Mp3FileReader mp3FileReader = new Mp3FileReader(mp3Stream);
            await using WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3FileReader);

            var targetFormat = new WaveFormat(_settings.WavSamplingRateHz, _settings.WavBitRate, channels: 1);

            await using var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3FileReader);
            await using var conversionStream = new WaveFormatConversionStream(targetFormat, pcmStream);

            var outputMemoryStream = new MemoryStream();
            await using var wavFileWriter = new WaveFileWriter(outputMemoryStream, conversionStream.WaveFormat);

            var conversionBuffer = new byte[65536];
            int bytesRead;

            while ((bytesRead = conversionStream.Read(conversionBuffer, 0, conversionBuffer.Length)) > 0)
            {
                wavFileWriter.Write(conversionBuffer, 0, bytesRead);
            }

            wavFileWriter.Flush();
            outputMemoryStream.Position = 0;

            return await TranscribeWav(outputMemoryStream, cancellationToken);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Ошибка конвертации", e);
        }
    }
}