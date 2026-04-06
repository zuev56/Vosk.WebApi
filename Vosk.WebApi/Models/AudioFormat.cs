namespace Vosk.WebApi.Models;

[Flags]
public enum AudioFormat
{
    Unknown = 0b000,
    Mp3 = 0b0001,
    Wav = 0b0010,
    Wma = 0b0100,
    Ogg = 0b1000,

    Known = Mp3 | Wav | Wma | Ogg
}