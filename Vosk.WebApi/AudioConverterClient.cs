using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Vosk.WebApi;

// Так как Nswag не дружит с AOT, пришлось модифицировать сгенерированный код

public sealed record AudioConverterOptions(int SamplingRateHz = 8000, int BitRate = 16, int Channels = 1);

public sealed class FileParameter
{
    public required Stream Data { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}

public sealed class ApiException : Exception
{
    public int StatusCode { get; private set; }

    public string? Response { get; private set; }

    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; private set; }

    public ApiException(string message, int statusCode, string? response, IReadOnlyDictionary<string, IEnumerable<string>> headers, Exception? innerException)
        : base(message + "\n\nStatus: " + statusCode + "\nResponse: \n" + ((response == null) ? "(null)" : response.Substring(0, response.Length >= 512 ? 512 : response.Length)), innerException)
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
        _baseUrl = settings.Value.AudioFileConverterUrl;
        if (!string.IsNullOrEmpty(_baseUrl) && !_baseUrl.EndsWith("/"))
            _baseUrl += '/';
    }

    public async Task<FileContentResult> ConvertToWavAsync(FileParameter file, AudioConverterOptions options, CancellationToken cancellationToken)
    {
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

        content.Add(new StringContent(options.SamplingRateHz.ToString()), nameof(options.SamplingRateHz));
        content.Add(new StringContent(options.BitRate.ToString()), nameof(options.BitRate));
        content.Add(new StringContent(options.Channels.ToString()), nameof(options.Channels));

        request.RequestUri = new Uri($"{_baseUrl}convert/to/wav", UriKind.RelativeOrAbsolute);
        request.Content = content;
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