using System.Text.Json;
using Hora.Core.Jira;

namespace Hora.Core.Tests.Jira;

public class AdfTextExtractorTests
{
    private static JsonElement? Adf(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void Extract_returns_empty_string_for_null()
    {
        Assert.Equal(string.Empty, AdfTextExtractor.Extract(null));
    }

    [Fact]
    public void Extract_returns_plain_string_for_legacy_string_comment()
    {
        Assert.Equal("Alter Kommentar", AdfTextExtractor.Extract(Adf("\"Alter Kommentar\"")));
    }

    [Fact]
    public void Extract_returns_empty_string_for_empty_document()
    {
        var document = Adf("""{"type":"doc","version":1,"content":[]}""");

        Assert.Equal(string.Empty, AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_reads_single_paragraph()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[{"type":"text","text":"I did some work here."}]}
            ]}
            """);

        Assert.Equal("I did some work here.", AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_joins_paragraphs_with_newline()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[{"type":"text","text":"Erster Absatz"}]},
              {"type":"paragraph","content":[{"type":"text","text":"Zweiter Absatz"}]}
            ]}
            """);

        Assert.Equal($"Erster Absatz{Environment.NewLine}Zweiter Absatz", AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_concatenates_marked_text_within_a_paragraph()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[
                {"type":"text","text":"Hallo "},
                {"type":"text","text":"Welt","marks":[{"type":"strong"}]}
              ]}
            ]}
            """);

        Assert.Equal("Hallo Welt", AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_reads_bullet_list_items_as_separate_lines()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"bulletList","content":[
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Punkt 1"}]}]},
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Punkt 2"}]}]}
              ]}
            ]}
            """);

        Assert.Equal($"Punkt 1{Environment.NewLine}Punkt 2", AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_includes_mention_text()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[
                {"type":"text","text":"Abgestimmt mit "},
                {"type":"mention","attrs":{"id":"123","text":"@Simon"}}
              ]}
            ]}
            """);

        Assert.Equal("Abgestimmt mit @Simon", AdfTextExtractor.Extract(document));
    }

    [Fact]
    public void Extract_treats_hard_break_as_line_break()
    {
        var document = Adf("""
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[
                {"type":"text","text":"Zeile 1"},
                {"type":"hardBreak"},
                {"type":"text","text":"Zeile 2"}
              ]}
            ]}
            """);

        Assert.Equal($"Zeile 1{Environment.NewLine}Zeile 2", AdfTextExtractor.Extract(document));
    }
}
