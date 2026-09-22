using Hora.App.Services;

namespace Hora.App.Tests;

public class InitialsFormatterTests
{
    [Fact]
    public void Two_words_yield_first_letter_of_each()
    {
        Assert.Equal("SS", InitialsFormatter.From("Simon Strubel"));
    }

    [Fact]
    public void Single_word_yields_one_letter()
    {
        Assert.Equal("S", InitialsFormatter.From("Simon"));
    }

    [Fact]
    public void Three_lowercase_words_yield_two_uppercase_letters()
    {
        var result = InitialsFormatter.From("simon strubel müller");

        Assert.Equal(2, result.Length);
        Assert.Equal(result, result.ToUpperInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_only_yields_placeholder(string displayName)
    {
        Assert.Equal("?", InitialsFormatter.From(displayName));
    }
}
