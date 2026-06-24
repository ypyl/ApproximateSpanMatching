using ApproximateSpanMatching.Models;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class IndexedDocumentTests
{
    [Fact]
    public void FromText_BuildsCorrectly()
    {
        var doc = IndexedDocument.FromText("The quick brown fox");
        Assert.Equal(4, doc.Tokens.Count);
        Assert.Equal("the", doc.Tokens[0].Text);
        Assert.Equal("The quick brown fox", doc.OriginalText);
        Assert.False(doc.CaseSensitive);
    }

    [Fact]
    public void FromText_CaseSensitive()
    {
        var doc = IndexedDocument.FromText("The Quick", caseSensitive: true);
        Assert.True(doc.CaseSensitive);
        Assert.Equal("The", doc.Tokens[0].Text);
        Assert.Equal("Quick", doc.Tokens[1].Text);
    }

    [Fact]
    public void GetPositions_ReturnsCorrectIndices()
    {
        var doc = IndexedDocument.FromText("the quick brown fox the lazy dog");
        var positions = doc.GetPositions("the");
        Assert.Equal(2, positions.Count);
        Assert.Equal(0, positions[0]);
        Assert.Equal(4, positions[1]);
    }

    [Fact]
    public void GetPositions_TokenNotInDoc_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var positions = doc.GetPositions("elephant");
        Assert.Empty(positions);
    }

    [Fact]
    public void GetSpan_ReturnsCorrectText()
    {
        var doc = IndexedDocument.FromText("The **quick** brown fox.");
        // Tokens: the(0), quick(1), brown(2), fox(3)
        // StartChar of token 1 to EndChar of token 3 = from 'q' onward
        var span = doc.GetSpan(1, 4);  // [1, 4) = quick, brown, fox
        Assert.StartsWith("quick", span);
        Assert.EndsWith("fox", span);
        Assert.Contains("brown", span);
    }

    [Fact]
    public void GetSpan_EmptyRange_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("The quick brown fox.");
        var span = doc.GetSpan(1, 1);
        Assert.Equal("", span);
    }

    [Fact]
    public void GetSpan_OutOfRange_Throws()
    {
        var doc = IndexedDocument.FromText("The quick brown fox.");
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetSpan(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetSpan(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetSpan(3, 1));
    }

    [Fact]
    public void EmptyDocument_ZeroTokens_EmptyIndex()
    {
        var doc = IndexedDocument.FromText("");
        Assert.Empty(doc.Tokens);
        Assert.Empty(doc.GetPositions("anything"));
        Assert.Equal("", doc.OriginalText);
    }

    [Fact]
    public void GetSpan_FullDocument()
    {
        var doc = IndexedDocument.FromText("The quick brown fox.");
        var span = doc.GetSpan(0, 4);
        Assert.Equal("The quick brown fox", span);
    }
}
