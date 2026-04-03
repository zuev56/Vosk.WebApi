namespace Vosk.Common;

[Flags]
public enum AudioFormat
{
    Unknown = 0b000,
    Mp3 = 0b001,
    Wav = 0b010,
    Wma = 0b100,

    Known = Mp3 | Wav | Wma
}