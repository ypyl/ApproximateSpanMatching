using ApproximateSpanMatching.Alignment;
using ApproximateSpanMatching.Models;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class SmithWatermanAlignmentTests
{
    [Fact]
    public void ExactMatch_NoGaps()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["a", "b", "c"], ["x", "a", "b", "c", "y"], 0);
        Assert.Equal(3.0, result.Score);
        Assert.Equal(3, result.MatchedPairs.Count);
        Assert.Equal(1, result.SpanStart);  // "a" at doc idx 1
        Assert.Equal(4, result.SpanEnd);    // exclusive
    }

    [Fact]
    public void ExactMatch_WithOffset()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["quick", "brown", "fox"], ["quick", "brown", "fox", "jumps"], 5);
        Assert.Equal(3.0, result.Score);
        Assert.Equal(3, result.MatchedPairs.Count);
        Assert.Equal(5, result.SpanStart);  // absolute
        Assert.Equal(8, result.SpanEnd);
        Assert.Contains(result.MatchedPairs, p => p.QueryIndex == 0 && p.DocIndex == 5);
        Assert.Contains(result.MatchedPairs, p => p.QueryIndex == 1 && p.DocIndex == 6);
        Assert.Contains(result.MatchedPairs, p => p.QueryIndex == 2 && p.DocIndex == 7);
    }

    [Fact]
    public void GapInDoc_OneMissingQueryWord()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["a", "z", "c"], ["a", "b", "c"], 0);
        // Best local alignment: either "a" (score 1) or "c" (score 1)
        // With g=-2, e=-1, a gap for "z" costs -3, so the full alignment is worse
        Assert.True(result.Score > 0);
        Assert.True(result.MatchedPairs.Count >= 1);
    }

    [Fact]
    public void TightClusterScoresHigherThanScattered()
    {
        var sw = new SmithWatermanAlignment();

        // Region 1: tight cluster
        var result1 = sw.Align(["a", "b", "c"], ["a", "b", "c"], 0);
        // Region 2: scattered with many gaps
        var result2 = sw.Align(["a", "b", "c"], ["a", "x", "x", "x", "x", "x", "b", "c"], 0);

        // Tight cluster should score higher (or scattered may reset to 0)
        Assert.True(result1.Score > result2.Score);
    }

    [Fact]
    public void EmptyQuery_ReturnsEmpty()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align([], ["a", "b"], 0);
        Assert.Equal(0.0, result.Score);
        Assert.Empty(result.MatchedPairs);
        Assert.Equal(0, result.SpanStart);
        Assert.Equal(0, result.SpanEnd);
    }

    [Fact]
    public void EmptyDocRegion_ReturnsEmpty()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["a", "b"], [], 5);
        Assert.Equal(0.0, result.Score);
        Assert.Empty(result.MatchedPairs);
        Assert.Equal(5, result.SpanStart);
        Assert.Equal(5, result.SpanEnd);
    }

    [Fact]
    public void NoPositiveAlignment_ReturnsScoreZero()
    {
        // Query word not matching anything → no local alignment > 0
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["x", "y", "z"], ["a", "b", "c"], 0);
        Assert.Equal(0.0, result.Score);
        Assert.Empty(result.MatchedPairs);
    }

    [Fact]
    public void NullQueryTokens_Throws()
    {
        var sw = new SmithWatermanAlignment();
        Assert.Throws<ArgumentNullException>(() => sw.Align(null!, ["a"], 0));
    }

    [Fact]
    public void NullDocRegionTokens_Throws()
    {
        var sw = new SmithWatermanAlignment();
        Assert.Throws<ArgumentNullException>(() => sw.Align(["a"], null!, 0));
    }

    [Fact]
    public void CustomGapPenalties_Applied()
    {
        var sw = new SmithWatermanAlignment(gapOpenPenalty: -0.5, gapExtendPenalty: -0.1);
        var result = sw.Align(["a", "b", "c"], ["a", "x", "b", "c"], 0);
        // 3 matches (+3) + 1 gap (-0.6) = 2.4, which beats 2.0 for just "b","c"
        Assert.Equal(2.4, result.Score, 6);
        Assert.Equal(3, result.MatchedPairs.Count);
    }

    [Fact]
    public void ScoreNeverBelowZero()
    {
        var sw = new SmithWatermanAlignment();
        // No matches at all → score 0
        var result = sw.Align(["x"], ["y"], 0);
        Assert.True(result.Score >= 0.0);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public void RepeatedWordsInQuery_MatchIndependentPositions()
    {
        // With mild gap penalties, both "the" tokens should be matchable across the gap
        var sw = new SmithWatermanAlignment(gapOpenPenalty: -0.5, gapExtendPenalty: -0.1);
        var result = sw.Align(["the", "quick", "the"], ["the", "quick", "brown", "the", "fox"], 0);
        Assert.True(result.Score >= 2.0);
        var matchedQueryIndices = result.MatchedPairs.Select(p => p.QueryIndex).ToList();
        Assert.Contains(0, matchedQueryIndices);
    }

    [Fact]
    public void MatchedPairsPreserveOrder()
    {
        var sw = new SmithWatermanAlignment();
        var result = sw.Align(["a", "b", "c"], ["x", "a", "b", "c", "y"], 0);
        Assert.True(result.MatchedPairs.Count >= 2);
        for (int i = 1; i < result.MatchedPairs.Count; i++)
        {
            Assert.True(result.MatchedPairs[i].QueryIndex > result.MatchedPairs[i - 1].QueryIndex);
            Assert.True(result.MatchedPairs[i].DocIndex > result.MatchedPairs[i - 1].DocIndex);
        }
    }
}
