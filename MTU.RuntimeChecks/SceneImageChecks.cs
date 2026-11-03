using System.Buffers.Binary;
using System.IO.Compression;
using MTU.Core;

public static class SceneImageChecks
{
    public static void Run()
    {
        static void Check(byte[] bytes, int width, int height)
        {
            var info = SceneImageInfo.Read(bytes);
            if (info.Width != width || info.Height != height)
                throw new Exception("Incorrect scene dimensions");
        }
        static void Reject(byte[] bytes)
        {
            try
            {
                SceneImageInfo.Read(bytes);
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Invalid scene dimensions accepted");
        }
        Check(Png(4096, 4096), 4096, 4096);
        Check(Png(1, 4096), 1, 4096);
        Reject(Png(4097, 1));
        Reject(Png(1, 4097));
        Reject([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var bad = Png(1, 1);
        Array.Clear(bad, 16, 4);
        Reject(bad);
        BinaryPrimitives.WriteUInt32BigEndian(bad.AsSpan(16), uint.MaxValue);
        Reject(bad);
        var gif = Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"
        );
        Check(gif, 1, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), 4097);
        Reject(gif);
        // JPEG dimensions come from SOF, not an APP metadata payload or a fixed offset.
        byte[] jpeg =
        [
            0xff,
            0xd8,
            0xff,
            0xe1,
            0,
            8,
            69,
            120,
            105,
            102,
            0,
            0,
            0xff,
            0xc0,
            0,
            11,
            8,
            0,
            32,
            0,
            64,
            1,
            1,
            0x11,
            0,
            0xff,
            0xd9,
        ];
        Check(jpeg, 64, 32);
        jpeg[13] = 0xc2;
        Check(jpeg, 64, 32);
        Reject(jpeg[..20]);
        jpeg[19] = 0x10;
        jpeg[20] = 1;
        Reject(jpeg);
        byte[] vp8 = [0x10, 0, 0, 0x9d, 1, 0x2a, 64, 0, 32, 0];
        Check(Webp("VP8 ", vp8), 64, 32);
        var lossless = new byte[5];
        lossless[0] = 0x2f;
        BinaryPrimitives.WriteUInt32LittleEndian(lossless.AsSpan(1), 63u | 31u << 14);
        Check(Webp("VP8L", lossless), 64, 32);
        var extended = new byte[10];
        extended[4] = 0xff;
        extended[5] = 0x0f;
        extended[7] = 0xff;
        extended[8] = 0x0f;
        Check(Webp("VP8X", extended), 4096, 4096);
        extended[5] = 0x1f;
        Reject(Webp("VP8X", extended));
        var malformed = Webp("VP8L", lossless);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(16), uint.MaxValue);
        Reject(malformed);
        Console.WriteLine(
            "PNG, JPEG (baseline/progressive), GIF and WebP (lossy/lossless/extended) dimensions and malformed/boundary checks passed."
        );
    }

    public static byte[] Png(
        int width,
        int height,
        CompressionLevel compression = CompressionLevel.Fastest
    )
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        using var encoded = new MemoryStream();
        using (var zlib = new ZLibStream(encoded, compression, true))
        {
            var row = new byte[checked(width * 4 + 1)];
            for (var i = 0; i < height; i++)
                zlib.Write(row);
        }
        Chunk(output, "IDAT", encoded.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string kind, byte[] data)
    {
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, data.Length);
        output.Write(size);
        var type = System.Text.Encoding.ASCII.GetBytes(kind);
        output.Write(type);
        output.Write(data);
        uint crc = uint.MaxValue;
        foreach (var part in new[] { type, data })
        foreach (var value in part)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(size, crc ^ uint.MaxValue);
        output.Write(size);
    }

    private static byte[] Webp(string kind, byte[] payload)
    {
        var result = new byte[20 + payload.Length + (payload.Length & 1)];
        "RIFF"u8.CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), result.Length - 8);
        "WEBP"u8.CopyTo(result.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(kind).CopyTo(result, 12);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(16), payload.Length);
        payload.CopyTo(result, 20);
        return result;
    }
}
