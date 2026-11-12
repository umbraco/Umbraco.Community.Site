using FluentAssertions;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class SearchIndexTextTests
{
    [Fact]
    public void Removes_a_form_picker_guid_and_keeps_the_text()
    {
        SearchIndexText.Clean("5a4d3443-df6f-4670-87f4-e5eb2424d3b4 As an Umbraco Partner agency you get one free ticket")
            .Should().Be("As an Umbraco Partner agency you get one free ticket");
    }

    [Fact]
    public void Removes_json_from_data_only_editors()
    {
        SearchIndexText.Clean("""Schedule {"settings":{"startHour":6,"endHour":24},"days":[{"date":"2026-06-08","label":"08 Jun"}]} overview""")
            .Should().Be("Schedule overview");
    }

    [Fact]
    public void Removes_json_with_brackets_inside_strings()
    {
        SearchIndexText.Clean("""before [{"label":"a } tricky ] value","n":1}] after""")
            .Should().Be("before after");
    }

    [Fact]
    public void Removes_umbraco_references()
    {
        SearchIndexText.Clean("See umb://document/6753cbcee0e94bc4a8129502dd378956 for details")
            .Should().Be("See for details");
    }

    [Theory]
    [InlineData("Tickets [Early bird] are on sale")]
    [InlineData("Use {braces} in prose")]
    [InlineData("An unclosed [\"quote")]
    [InlineData("Version 17.2 and 2026-06-08")]
    public void Leaves_ordinary_text_alone(string text)
    {
        SearchIndexText.Clean(text).Should().Be(text);
    }

    [Fact]
    public void Returns_empty_when_only_data_remains()
    {
        SearchIndexText.Clean("""5f4df60d-60ca-4a47-a874-2642c687f289 {"a":1}""").Should().BeEmpty();
    }
}
