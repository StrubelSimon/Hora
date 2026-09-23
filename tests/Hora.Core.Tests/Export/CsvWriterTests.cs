using Hora.Core.Export;

namespace Hora.Core.Tests.Export;

public class CsvWriterTests
{
    [Fact]
    public void Escape_leaves_a_harmless_field_untouched()
    {
        Assert.Equal("HORA-12", CsvWriter.Escape("HORA-12"));
        Assert.Equal("8,00", CsvWriter.Escape("8,00"));
    }

    [Fact]
    public void Escape_turns_null_into_an_empty_field()
    {
        Assert.Equal(string.Empty, CsvWriter.Escape(null));
    }

    [Fact]
    public void Escape_quotes_a_field_containing_the_separator()
    {
        Assert.Equal("\"Rechnung; dann Versand\"", CsvWriter.Escape("Rechnung; dann Versand"));
    }

    [Fact]
    public void Escape_doubles_inner_quotation_marks_and_quotes_the_field()
    {
        Assert.Equal("\"Fehler in \"\"Suche\"\"\"", CsvWriter.Escape("Fehler in \"Suche\""));
    }

    [Fact]
    public void Escape_quotes_leading_spaces_so_the_indentation_survives_the_import()
    {
        Assert.Equal("\"  HORA-12\"", CsvWriter.Escape("  HORA-12"));
    }

    [Fact]
    public void Escape_quotes_trailing_spaces()
    {
        Assert.Equal("\"HORA-12 \"", CsvWriter.Escape("HORA-12 "));
    }

    [Fact]
    public void Escape_quotes_a_field_containing_a_line_break()
    {
        Assert.Equal("\"erste\nzweite\"", CsvWriter.Escape("erste\nzweite"));
        Assert.Equal("\"erste\rzweite\"", CsvWriter.Escape("erste\rzweite"));
    }

    [Fact]
    public void WriteRow_joins_the_fields_with_semicolons_and_ends_the_line_with_crlf()
    {
        var writer = new CsvWriter();

        writer.WriteRow("01.09.2026", "Di", null, null, "8,00");

        Assert.Equal("01.09.2026;Di;;;8,00\r\n", writer.Build());
    }

    [Fact]
    public void WriteRow_escapes_every_field_it_writes()
    {
        var writer = new CsvWriter();

        writer.WriteRow("  HORA-12", "Rechnung; dann Versand");

        Assert.Equal("\"  HORA-12\";\"Rechnung; dann Versand\"\r\n", writer.Build());
    }

    [Fact]
    public void WriteBlankRow_produces_an_empty_line()
    {
        var writer = new CsvWriter();

        writer.WriteRow("Kopf");
        writer.WriteBlankRow();
        writer.WriteRow("Daten");

        Assert.Equal("Kopf\r\n\r\nDaten\r\n", writer.Build());
    }
}
