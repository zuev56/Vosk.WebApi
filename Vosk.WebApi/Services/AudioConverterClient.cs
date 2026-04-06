using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vosk.WebApi.Models;
using FileInfo = Vosk.WebApi.Models.FileInfo;

namespace Vosk.WebApi.Services;

public sealed record AudioConverterOptions(int SamplingRateHz = 8000, int BitRate = 16, int Channels = 1);

public sealed class ApiException : Exception
{
    public int StatusCode { get; private set; }

    public string? Response { get; private set; }

    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; private set; }

    public ApiException(string message, int statusCode, string? response, IReadOnlyDictionary<string, IEnumerable<string>> headers, Exception? innerException)
        : base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" + (response == null ? "(null)" : response[..(response.Length >= 512 ? 512 : response.Length)]), innerException)
    {
        StatusCode = statusCode;
        Response = response;
        Headers = headers;
    }

    public override string ToString() => $"HTTP Response: \n\n{Response}\n\n{base.ToString()}";
}

public sealed class AudioConverterClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;

    public AudioConverterClient(IHttpClientFactory httpClientFactory, IOptions<Settings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = settings.Value.FFmpegApiUrl;
        if (!string.IsNullOrEmpty(_baseUrl) && !_baseUrl.EndsWith("/"))
            _baseUrl += '/';
    }

    public async Task<FileContentResult> ConvertToWavAsync(FileInfo file, AudioConverterOptions options, CancellationToken cancellationToken)
    {
        // Example: curl -F "file=@input.mp3" -F "params=-ar 8000 -acodec pcm_s16le -ac 1" 127.0.0.1:3000/convert/audio/to/wav > output.wav

        using var httpClient = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage();

        var boundary = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent(boundary);
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);

        var fileContent = new StreamContent(file.Data);
        if (!string.IsNullOrEmpty(file.ContentType))
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        content.Add(fileContent, "file", file.FileName);

        var codec = options.BitRate switch
        {
            8 => "pcm_u8", // 8-bit unsigned PCM
            16 => "pcm_s16le", // 16-bit signed PCM (Little Endian)
            _ => throw new ArgumentOutOfRangeException(nameof(options.BitRate), options.BitRate, "BitRate must be either 8 or 16")
        };
        var parameters = $"-ar {options.SamplingRateHz} -acodec {codec} -ac {options.Channels}";

        content.Add(new StringContent(parameters), "params");
        request.Content = content;
        request.RequestUri = new Uri($"{_baseUrl}convert/audio/to/wav", UriKind.RelativeOrAbsolute);
        request.Method = HttpMethod.Post;
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("audio/wav"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // Только для исключений
        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var item in response.Headers)
            headers[item.Key] = item.Value;
        if (response.Content is { Headers: not null })
        {
            foreach (var item in response.Content.Headers)
                headers[item.Key] = item.Value;
        }

        var status = (int)response.StatusCode;
        switch (status)
        {
            case 200:
            {
                try
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    return new FileContentResult(fileBytes, response.Content.Headers?.ContentType?.MediaType ?? "application/octet-stream")
                    {
                        FileDownloadName = Path.ChangeExtension(file.FileName, ".wav")
                    };
                }
                catch (JsonException exception)
                {
                    var message = "Could not create FileContentResult from response.";
                    throw new ApiException(message, (int)response.StatusCode, string.Empty, headers, exception);
                }
            }
            case 400:
            {
                var responseText = await content.ReadAsStringAsync(cancellationToken);
                throw new ApiException("Bad Request", status, responseText, headers, null);
            }
            case 422:
            {
                var responseText = await content.ReadAsStringAsync(cancellationToken);
                throw new ApiException("Unprocessable Entity", status, responseText, headers, null);
            }
            default:
            {
                var responseData = await content.ReadAsStringAsync(cancellationToken);
                throw new ApiException("The HTTP status code of the response was not expected (" + status + ").", status,
                    responseData, headers, null);
            }
        }
    }
}