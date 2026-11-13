using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UmbracoCommunity.Web.Features.OpenBadges.Infrastructure;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.OpenBadges;

public class PngBadgeBakerTests
{
    private const string Assertion = "{\"id\":\"https://community.example/badges/assertions/abc.json\",\"note\":\"ünïcode\"}";

    [Fact]
    public void The_baked_image_is_still_a_png_that_decodes()
    {
        var baked = PngBadgeBaker.Bake(SamplePng(), Assertion);

        PngBadgeBaker.IsPng(baked).Should().BeTrue();
        using var image = Image.Load<Rgba32>(baked);
        image.Width.Should().Be(8);
        image.Height.Should().Be(8);
    }

    [Fact]
    public void One_openbadges_itxt_chunk_goes_straight_before_the_first_idat()
    {
        var baked = PngBadgeBaker.Bake(SamplePng(), Assertion);

        var types = PngBadgeBaker.ReadChunks(baked).Select(c => c.Type).ToList();
        var itxt = types.IndexOf("iTXt");
        itxt.Should().BeGreaterThan(0);
        types[itxt + 1].Should().Be("IDAT");
        types.IndexOf("IDAT").Should().Be(itxt + 1);
        OpenBadgesChunks(baked).Should().HaveCount(1);
    }

    [Fact]
    public void The_chunk_holds_the_keyword_uncompressed_and_the_text()
    {
        var baked = PngBadgeBaker.Bake(SamplePng(), Assertion);

        var data = OpenBadgesChunks(baked).Single();
        var keyword = Encoding.Latin1.GetBytes("openbadges");
        data[..keyword.Length].Should().Equal(keyword);
        data[keyword.Length].Should().Be(0);     // keyword terminator
        data[keyword.Length + 1].Should().Be(0); // compression flag: uncompressed
        data[keyword.Length + 2].Should().Be(0); // compression method
        data[keyword.Length + 3].Should().Be(0); // empty language tag
        data[keyword.Length + 4].Should().Be(0); // empty translated keyword
        Encoding.UTF8.GetString(data[(keyword.Length + 5)..]).Should().Be(Assertion);
    }

    [Fact]
    public void Every_chunk_has_a_valid_crc()
    {
        var baked = PngBadgeBaker.Bake(SamplePng(), Assertion);

        foreach (var chunk in PngBadgeBaker.ReadChunks(baked))
        {
            var stored = BinaryPrimitives.ReadUInt32BigEndian(baked.AsSpan(chunk.DataOffset + chunk.Length, 4));
            PngBadgeBaker.Crc32(baked.AsSpan(chunk.Offset + 4, chunk.Length + 4)).Should().Be(stored, $"chunk {chunk.Type}");
        }
    }

    [Fact]
    public void The_crc_matches_the_png_reference_value() =>
        PngBadgeBaker.Crc32(Encoding.ASCII.GetBytes("IEND")).Should().Be(0xAE426082u);

    [Fact]
    public void Baking_again_replaces_the_existing_badge_data()
    {
        var once = PngBadgeBaker.Bake(SamplePng(), "first");

        var twice = PngBadgeBaker.Bake(once, Assertion);

        var data = OpenBadgesChunks(twice).Should().ContainSingle().Subject;
        Encoding.UTF8.GetString(data).Should().EndWith(Assertion);
    }

    [Fact]
    public void Something_that_is_not_a_png_is_refused()
    {
        var act = () => PngBadgeBaker.Bake(Encoding.ASCII.GetBytes("GIF89a not a png"), Assertion);

        act.Should().Throw<ArgumentException>();
    }

    private static List<byte[]> OpenBadgesChunks(byte[] png) =>
        PngBadgeBaker.ReadChunks(png)
            .Where(c => c.Type == "iTXt")
            .Select(c => png.AsSpan(c.DataOffset, c.Length).ToArray())
            .Where(d => Encoding.Latin1.GetString(d).StartsWith("openbadges\0", StringComparison.Ordinal))
            .ToList();

    internal static byte[] SamplePng()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(27, 38, 79, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
