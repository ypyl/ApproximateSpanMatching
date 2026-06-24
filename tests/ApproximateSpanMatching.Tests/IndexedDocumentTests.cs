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

    // --- N-gram index tests ---

    [Fact]
    public void GetApproximatePositions_FindsSimilarWord()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var results = doc.GetApproximatePositions("qu1ck", 0.2);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].Position);  // "quick" at position 1
        Assert.True(results[0].Similarity > 0.2);
        Assert.True(results[0].Similarity < 1.0);
    }

    [Fact]
    public void GetApproximatePositions_RespectsThreshold()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var results = doc.GetApproximatePositions("qu1ck", 0.5);

        // Jaccard for "qu1ck" vs "quick" is ~0.25, below 0.5
        Assert.Empty(results);
    }

    [Fact]
    public void GetApproximatePositions_NoMatch_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var results = doc.GetApproximatePositions("xyzzy", 0.2);

        Assert.Empty(results);
    }

    [Fact]
    public void GetApproximatePositions_MultipleCandidates_RankedBySimilarity()
    {
        var doc = IndexedDocument.FromText("cat bat car");
        var results = doc.GetApproximatePositions("cat", 0.2);

        Assert.NotEmpty(results);
        // "cat" at position 0 should be first (similarity 1.0)
        Assert.Equal(0, results[0].Position);
        Assert.Equal(1.0, results[0].Similarity);

        // Other candidates ("bat", "car") come after, sorted by similarity descending
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Similarity >= results[i].Similarity);
        }
    }

    [Fact]
    public void GetApproximatePositions_DuplicateTokens_MultiplePositions()
    {
        var doc = IndexedDocument.FromText("quick quick quick");
        var results = doc.GetApproximatePositions("qu1ck", 0.2);

        Assert.Equal(3, results.Count);
        // Positions 0, 1, 2 — all with same similarity
        Assert.All(results, r => Assert.True(r.Position >= 0 && r.Position <= 2));
    }

    [Fact]
    public void GetApproximatePositions_EmptyWord_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var results = doc.GetApproximatePositions("", 0.0);

        Assert.Empty(results);
    }

    [Fact]
    public void GetApproximatePositions_EmptyDocument_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("");
        var results = doc.GetApproximatePositions("quick", 0.2);

        Assert.Empty(results);
    }

    // --- Cross-boundary n-gram lookup tests (the headline bug fix) ---

    [Fact]
    public void GetApproximatePositions_CrossBoundary_Deletion()
    {
        // Query "quik" (4 chars) vs doc "quick" (5 chars): deletion across boundary
        // Previously this returned empty because the index/lookup used mismatched n-gram sizes.
        var doc = IndexedDocument.FromText("the quick brown fox");
        var results = doc.GetApproximatePositions("quik", 0.2);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].Position);  // "quick" at position 1
        Assert.True(results[0].Similarity > 0.2);
    }

    [Fact]
    public void GetApproximatePositions_CrossBoundary_Insertion()
    {
        // Query "word" (4 chars) vs doc "words" (5 chars): insertion across boundary
        var doc = IndexedDocument.FromText("the words of power");
        var results = doc.GetApproximatePositions("word", 0.2);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].Position);  // "words" at position 1
        Assert.True(results[0].Similarity > 0.2);
    }
}
