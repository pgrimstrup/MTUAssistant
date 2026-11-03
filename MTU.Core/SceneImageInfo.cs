using System.Buffers.Binary;

namespace MTU.Core;

public sealed record SceneImageInfo(int Width, int Height, string ContentType)
{
    public const int MaxDimension = 4096;

    // Read encoded canvas dimensions without allocating a decoded pixel buffer.
    public static SceneImageInfo Read(byte[] bytes)
    {
        var type = CharacterToken.ContentType(bytes);
        var (width, height) = type switch
        {
            "image/png" => Png(bytes),
            "image/jpeg" => Jpeg(bytes),
            "image/gif" => Gif(bytes),
            "image/webp" => Webp(bytes),
            _ => (0L, 0L),
        };
        if (width <= 0 || height <= 0)
            throw new ArgumentException(
                "Unable to read the image dimensions. Choose a PNG, JPEG, GIF or WebP image."
            );
        if (width > MaxDimension || height > MaxDimension)
            throw new ArgumentException(
                $"Scene images must be no larger than {MaxDimension} × {MaxDimension} pixels. This image is {width} × {height} pixels."
            );
        return new((int)width, (int)height, type!);
    }

    private static (long, long) Png(ReadOnlySpan<byte> data) =>
        data.Length >= 33
        && BinaryPrimitives.ReadUInt32BigEndian(data[8..]) == 13
        && data[12..16].SequenceEqual("IHDR"u8)
            ? (
                BinaryPrimitives.ReadUInt32BigEndian(data[16..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[20..])
            )
            : (0, 0);

    private static (long, long) Gif(ReadOnlySpan<byte> data) =>
        data.Length >= 13
            ? (
                BinaryPrimitives.ReadUInt16LittleEndian(data[6..]),
                BinaryPrimitives.ReadUInt16LittleEndian(data[8..])
            )
            : (0, 0);

    private static (long, long) Jpeg(ReadOnlySpan<byte> data)
    {
        var offset = 2;
        while (offset < data.Length)
        {
            if (data[offset++] != 0xff)
                return (0, 0);
            while (offset < data.Length && data[offset] == 0xff)
                offset++;
            if (offset >= data.Length)
                return (0, 0);
            var marker = data[offset++];
            if (marker is 0xd9 or 0xda or 0)
                return (0, 0);
            if (marker is 0x01 or >= 0xd0 and <= 0xd8)
                continue;
            if (data.Length - offset < 2)
                return (0, 0);
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            if (length < 2 || length > data.Length - offset)
                return (0, 0);
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
                return length >= 8
                    ? (
                        BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]),
                        BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 3)..])
                    )
                    : (0, 0);
            offset += length;
        }
        return (0, 0);
    }

    private static (long, long) Webp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
            return (0, 0);
        var end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (end > data.Length || end < 20)
            return (0, 0);
        var offset = 12;
        while (offset <= end - 8)
        {
            var kind = data.Slice(offset, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
            if (length > end - offset - 8)
                return (0, 0);
            var payload = data.Slice(offset + 8, (int)length);
            if (kind.SequenceEqual("VP8X"u8) && length >= 10)
                return (1 + UInt24(payload[4..]), 1 + UInt24(payload[7..]));
            if (
                kind.SequenceEqual("VP8 "u8)
                && length >= 10
                && (payload[0] & 1) == 0
                && payload[3..6].SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a })
            )
                return (
                    BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]) & 0x3fff,
                    BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]) & 0x3fff
                );
            if (kind.SequenceEqual("VP8L"u8) && length >= 5 && payload[0] == 0x2f)
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);
                if ((bits >> 29) != 0)
                    return (0, 0);
                return (1 + (bits & 0x3fff), 1 + ((bits >> 14) & 0x3fff));
            }
            var next = offset + 8L + length + (length & 1);
            if (next > end)
                return (0, 0);
            offset = (int)next;
        }
        return (0, 0);
    }

    private static int UInt24(ReadOnlySpan<byte> data) => data[0] | data[1] << 8 | data[2] << 16;
}
