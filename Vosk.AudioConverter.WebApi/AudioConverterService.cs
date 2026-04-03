using NAudio.Wave;
using Vosk.Common;

namespace AudioConverter;

public sealed record AudioConverterOptions(int SamplingRateHz = 44100, int BitRate = 16, int Channels = 2);

public sealed class AudioConverterService
{
    public async Task<byte[]> ConvertToWav(IFormFile file, AudioFormat sourceFormat, AudioConverterOptions options)
    {
        Stream? mp3FileStream = null;
        string? wmaTempFilePath = null;
        try
        {
            Func<ValueTask<WaveStream>> getWaveStream = sourceFormat switch
            {
                // ReSharper disable once AccessToDisposedClosure
                AudioFormat.Mp3 => () =>
                {
                    mp3FileStream = file.OpenReadStream();
                    return ValueTask.FromResult<WaveStream>(new Mp3FileReader(mp3FileStream));
                },
                AudioFormat.Wma => async () =>
                {
                    wmaTempFilePath = Path.GetTempFileName() + ".wma";
                    await using var stream = new FileStream(wmaTempFilePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                    return new MediaFoundationReader(wmaTempFilePath);
                },
                _ => throw new ArgumentOutOfRangeException(nameof(sourceFormat))
            };

            await using var waveStream = await getWaveStream.Invoke();
            var targetFormat = new WaveFormat(options.SamplingRateHz, options.BitRate, options.Channels);

            await using var pcmWaveStream = WaveFormatConversionStream.CreatePcmStream(waveStream);
            await using var conversionStream = new WaveFormatConversionStream(targetFormat, pcmWaveStream);

            using var outputMemoryStream = new MemoryStream();
            await using var wavFileWriter = new WaveFileWriter(outputMemoryStream, conversionStream.WaveFormat);

            var conversionBuffer = new byte[65536];
            int bytesRead;

            while ((bytesRead = conversionStream.Read(conversionBuffer, 0, conversionBuffer.Length)) > 0)
            {
                wavFileWriter.Write(conversionBuffer, 0, bytesRead);
            }

            wavFileWriter.Flush();
            outputMemoryStream.Position = 0;

            return outputMemoryStream.ToArray();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: {e}. Message: {e.Message}. Stacktrace: {e.StackTrace}");
            return [];
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            mp3FileStream?.Dispose();

            if (File.Exists(wmaTempFilePath))
                File.Delete(wmaTempFilePath);
        }
    }
}