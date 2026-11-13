using System.Buffers.Binary;
using System.Text;

namespace UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;

/// <summary>
/// Bakes Open Badges data into a PNG, per the Open Badges 2.0 Baking Specification: one uncompressed <c>iTXt</c>
/// chunk with the keyword <c>openbadges</c>. It goes straight before the first <c>IDAT</c> chunk; any
/// <c>openbadges</c> chunk already in the image is dropped, since only one may be present.
/// </summary>
public static class PngBadgeBaker
{
    public const string Keyword = "openbadges";

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static bool IsPng(ReadOnlySpan<byte> data) => data.Length >= Signature.Length && data[..Signature.Length].SequenceEqual(Signature);

    public static byte[] Bake(byte[] png, string text)
    {
        if (!IsPng(png))
        {
            throw new ArgumentException("Not a PNG image.", nameof(png));
        }

        using var output = new MemoryStream(png.Length + text.Length + 64);
        output.Write(Signature);

        var inserted = false;
        foreach (var chunk in ReadChunks(png))
        {
            if (!inserted && chunk.Type == "IDAT")
            {
                WriteChunk(output, "iTXt", BuildITxtData(Keyword, text));
                inserted = true;
            }

            if (chunk.Type == "iTXt" && IsOpenBadgesChunk(png.AsSpan(chunk.DataOffset, chunk.Length)))
            {
                continue;
            }

            output.Write(png, chunk.Offset, chunk.TotalLength);
        }

        if (!inserted)
        {
            throw new ArgumentException("The PNG has no IDAT chunk.", nameof(png));
        }

        return output.ToArray();
    }

    /// <summary>
    /// The iTXt data: keyword, null, compression flag 0, compression method 0, empty language tag, null, empty
    /// translated keyword, null, then the UTF-8 text.
    /// </summary>
    internal static byte[] BuildITxtData(string keyword, string text) =>
        [.. Encoding.Latin1.GetBytes(keyword), 0, 0, 0, 0, 0, .. Encoding.UTF8.GetBytes(text)];

    /// <summary>The chunks of a PNG, after the signature, in file order. Throws on a truncated file.</summary>
    internal static IEnumerable<PngChunk> ReadChunks(byte[] png)
    {
        var offset = Signature.Length;
        while (offset < png.Length)
        {
            if (offset + 12 > png.Length)
            {
                throw new ArgumentException("The PNG is truncated.", nameof(png));
            }

            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || offset + 12L + length > png.Length)
            {
                throw new ArgumentException("The PNG is truncated.", nameof(png));
            }

            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            yield return new PngChunk(type, offset, offset + 8, length);

            offset += 12 + length;
            if (type == "IEND")
            {
                yield break;
            }
        }
    }

    /// <summary>PNG CRC-32 (ISO 3309) over the chunk type and data.</summary>
    internal static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static bool IsOpenBadgesChunk(ReadOnlySpan<byte> data)
    {
        var keyword = Encoding.Latin1.GetBytes(Keyword);
        return data.Length > keyword.Length && data[..keyword.Length].SequenceEqual(keyword) && data[keyword.Length] == 0;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)data.Length);
        output.Write(header);

        byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
        output.Write(typeAndData);

        BinaryPrimitives.WriteUInt32BigEndian(header, Crc32(typeAndData));
        output.Write(header);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>One chunk: <see cref="Offset"/> is where its length field starts, <see cref="DataOffset"/> its data.</summary>
    internal readonly record struct PngChunk(string Type, int Offset, int DataOffset, int Length)
    {
        public int TotalLength => Length + 12;
    }
}
