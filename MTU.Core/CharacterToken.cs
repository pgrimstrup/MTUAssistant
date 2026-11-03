namespace MTU.Core;

public static class CharacterToken
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static string? ContentType(byte[]? bytes)
    {
        if (bytes is null)
            return null;
        ReadOnlySpan<byte> data = bytes;
        if (data.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return "image/png";
        if (data.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
            return "image/jpeg";
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
            return "image/gif";
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    public static string? DataUri(byte[]? bytes) =>
        ContentType(bytes) is { } type
            ? $"data:{type};base64,{Convert.ToBase64String(bytes!)}"
            : null;
}
