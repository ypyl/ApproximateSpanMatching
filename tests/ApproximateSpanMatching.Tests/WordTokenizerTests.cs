using ApproximateSpanMatching.Indexing;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class WordTokenizerTests
{
    [Fact]
    public void BasicText_Tokenizes()
    {
        var tokens = WordTokenizer.Tokenize("The **quick** brown fox.");
        Assert.Equal(4, tokens.Count);
        Assert.Equal("the", tokens[0].Text);
        Assert.Equal("quick", tokens[1].Text);
        Assert.Equal("brown", tokens[2].Text);
        Assert.Equal("fox", tokens[3].Text);

        // Verify character offsets exist and are ordered
        for (int i = 1; i < tokens.Count; i++)
            Assert.True(tokens[i].StartChar >= tokens[i - 1].EndChar);
    }

    [Fact]
    public void HyphenatedWords_AreOneToken()
    {
        var tokens = WordTokenizer.Tokenize("state-of-the-art solution");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("state-of-the-art", tokens[0].Text);
        Assert.Equal("solution", tokens[1].Text);
    }

    [Fact]
    public void EmDashRange_AreWordChars()
    {
        // EM DASH (U+2014) should be a word char
        var tokens = WordTokenizer.Tokenize("word\u2014word");
        Assert.Single(tokens);
        Assert.Equal("word\u2014word", tokens[0].Text);
    }

    [Fact]
    public void Contractions_AreOneToken()
    {
        var tokens = WordTokenizer.Tokenize("don't stop");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("don't", tokens[0].Text);
        Assert.Equal("stop", tokens[1].Text);
    }

    [Fact]
    public void CurlyApostrophe_WordChar()
    {
        // RIGHT SINGLE QUOTATION MARK (U+2019)
        var tokens = WordTokenizer.Tokenize("don\u2019t");
        Assert.Single(tokens);
        Assert.Equal("don\u2019t", tokens[0].Text);
    }

    [Fact]
    public void Numbers_DigitInternalPeriod()
    {
        var tokens = WordTokenizer.Tokenize("Section 3.2 covers details");
        Assert.Equal(4, tokens.Count);
        Assert.Equal("section", tokens[0].Text);
        Assert.Equal("3.2", tokens[1].Text);
        Assert.Equal("covers", tokens[2].Text);
        Assert.Equal("details", tokens[3].Text);
    }

    [Fact]
    public void MultiPeriodNumber_VersionString()
    {
        var tokens = WordTokenizer.Tokenize("version 1.0.0");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("version", tokens[0].Text);
        Assert.Equal("1.0.0", tokens[1].Text);
    }

    [Fact]
    public void TrailingPeriod_NotPartOfToken()
    {
        var tokens = WordTokenizer.Tokenize("end.");
        Assert.Single(tokens);
        Assert.Equal("end", tokens[0].Text);
    }

    [Fact]
    public void LeadingPeriod_NotPartOfToken()
    {
        var tokens = WordTokenizer.Tokenize(".5");
        Assert.Single(tokens);
        Assert.Equal("5", tokens[0].Text);
    }

    [Fact]
    public void PeriodBetweenLetters_Delimiter()
    {
        var tokens = WordTokenizer.Tokenize("U.S.A");
        Assert.Equal(3, tokens.Count);
        Assert.Equal("u", tokens[0].Text);
        Assert.Equal("s", tokens[1].Text);
        Assert.Equal("a", tokens[2].Text);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        var tokens = WordTokenizer.Tokenize("");
        Assert.Empty(tokens);
    }

    [Fact]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.Tokenize(null!));
    }

    [Fact]
    public void CaseInsensitive_Lowercases()
    {
        var tokens = WordTokenizer.Tokenize("The Quick Brown Fox");
        Assert.Equal(4, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(t.Text, t.Text.ToLowerInvariant()));
    }

    [Fact]
    public void CaseSensitive_PreservesCasing()
    {
        var tokens = WordTokenizer.Tokenize("The Quick Brown Fox", caseSensitive: true);
        Assert.Equal(4, tokens.Count);
        Assert.Equal("The", tokens[0].Text);
        Assert.Equal("Quick", tokens[1].Text);
        Assert.Equal("Brown", tokens[2].Text);
        Assert.Equal("Fox", tokens[3].Text);
    }

    [Fact]
    public void FormattingStructures_SplitOnSyntax()
    {
        var tokens = WordTokenizer.Tokenize("see `code` and [link](https://example.com/path) here");
        Assert.Contains(tokens, t => t.Text == "see");
        Assert.Contains(tokens, t => t.Text == "code");
        Assert.Contains(tokens, t => t.Text == "and");
        Assert.Contains(tokens, t => t.Text == "link");
        Assert.Contains(tokens, t => t.Text == "https");
        Assert.Contains(tokens, t => t.Text == "example");
        Assert.Contains(tokens, t => t.Text == "com");
        Assert.Contains(tokens, t => t.Text == "path");
        Assert.Contains(tokens, t => t.Text == "here");

        // Verify delimiters: backtick, brackets, parens, colon, slash
        Assert.DoesNotContain(tokens, t => t.Text.Contains('`'));
        Assert.DoesNotContain(tokens, t => t.Text.Contains('['));
        Assert.DoesNotContain(tokens, t => t.Text.Contains(']'));
        Assert.DoesNotContain(tokens, t => t.Text.Contains('('));
        Assert.DoesNotContain(tokens, t => t.Text.Contains(')'));
    }

    [Fact]
    public void SoftHyphen_ActsAsDelimiter()
    {
        var tokens = WordTokenizer.Tokenize("state\u00ADof affairs");
        Assert.Equal(3, tokens.Count);
        Assert.Equal("state", tokens[0].Text);
        Assert.Equal("of", tokens[1].Text);
        Assert.Equal("affairs", tokens[2].Text);
    }

    [Fact]
    public void NFCEquivalence_PrecomposedVsDecomposed()
    {
        // Precomposed é (U+00E9) vs decomposed é (U+0065 U+0301)
        var precomposed = WordTokenizer.Tokenize("caf\u00E9");
        var decomposed = WordTokenizer.Tokenize("caf\u0065\u0301");

        Assert.Equal(precomposed.Count, decomposed.Count);
        for (int i = 0; i < precomposed.Count; i++)
            Assert.Equal(precomposed[i].Text, decomposed[i].Text);
    }

    [Fact]
    public void OffsetsMatchStoredString()
    {
        var tokens = WordTokenizer.Tokenize("The quick fox");
        // Token "the": start=0, end=3 (exclusive)
        Assert.Equal(0, tokens[0].StartChar);
        Assert.Equal(3, tokens[0].EndChar);
        // Token "quick": start=4, end=9
        Assert.Equal(4, tokens[1].StartChar);
        Assert.Equal(9, tokens[1].EndChar);
    }
}
