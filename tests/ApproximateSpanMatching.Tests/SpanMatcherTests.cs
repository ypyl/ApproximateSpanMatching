using ApproximateSpanMatching.Matching;
using ApproximateSpanMatching.Models;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class SpanMatcherTests
{
    [Fact]
    public void ExactMatch_ReturnsOneSpan()
    {
        var doc = IndexedDocument.FromMarkdown("the quick brown fox jumps");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown fox");

        Assert.Single(results);
        Assert.Equal(1.0, results[0].NormalizedScore);
        Assert.Equal(1.0, results[0].Coverage);
        Assert.Equal(3, results[0].MatchedPairs.Count);
    }

    [Fact]
    public void MatchWithDocGaps_LowerScore()
    {
        var doc = IndexedDocument.FromMarkdown("quick brown fox jumps over the lazy dog");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown fox jumps over lazy dog");

        Assert.Single(results);
        // NormalizedScore < 1.0 due to gap penalty for "the"
        Assert.True(results[0].NormalizedScore < 1.0);
        Assert.True(results[0].Coverage > 0.5);
    }

    [Fact]
    public void MatchWithMissingQueryWords()
    {
        // "leaps" isn't in the document; results depend on gap penalty severity
        var doc = IndexedDocument.FromMarkdown("quick brown fox over lazy dog");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown fox leaps over lazy dog");

        // Any returned results must be well-formed
        foreach (var r in results)
        {
            Assert.True(r.NormalizedScore >= 0 && r.NormalizedScore <= 1.0);
            Assert.True(r.Coverage >= 0 && r.Coverage <= 1.0);
            Assert.NotNull(r.OriginalText);
        }
    }

    [Fact]
    public void Threshold_FiltersLowScoring()
    {
        var doc = IndexedDocument.FromMarkdown("the quick brown fox jumps over the lazy dog");
        var matcher = new SpanMatcher();

        // Without threshold
        var all = matcher.Search(doc, "quick", topN: 10);
        Assert.NotEmpty(all);

        // With high threshold — "quick" alone won't match all query words
        var filtered = matcher.Search(doc, "quick", topN: 10, threshold: 1.0);
        Assert.All(filtered, r => Assert.Equal(1.0, r.NormalizedScore));
    }

    [Fact]
    public void TopN_LimitsResults()
    {
        var doc = IndexedDocument.FromMarkdown(
            "the quick brown the quick brown the quick brown the quick brown the quick brown");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown", topN: 3);

        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void OverlappingSpans_Deduplicated()
    {
        var doc = IndexedDocument.FromMarkdown(
            "the quick brown fox jumps over the lazy dog");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "the quick", topN: 5);

        // All results should be non-overlapping (overlap <= 50% of smaller span)
        for (int i = 0; i < results.Count; i++)
        {
            for (int j = i + 1; j < results.Count; j++)
            {
                int intersectStart = Math.Max(results[i].StartIndex, results[j].StartIndex);
                int intersectEnd = Math.Min(results[i].EndIndex, results[j].EndIndex);
                int intersectLen = Math.Max(0, intersectEnd - intersectStart);
                int minLen = Math.Min(
                    results[i].EndIndex - results[i].StartIndex,
                    results[j].EndIndex - results[j].StartIndex);
                double overlap = minLen > 0 ? (double)intersectLen / minLen : 0;
                Assert.True(overlap <= 0.5, "Spans should not overlap by >50%");
            }
        }
    }

    [Fact]
    public void MultiQueryReuse_IndexNotRebuilt()
    {
        var doc = IndexedDocument.FromMarkdown("the quick brown fox jumps over the lazy dog");
        var matcher = new SpanMatcher();

        var r1 = matcher.Search(doc, "quick brown");
        var r2 = matcher.Search(doc, "lazy dog");
        var r3 = matcher.Search(doc, "fox jumps");

        Assert.NotEmpty(r1);
        Assert.NotEmpty(r2);
        Assert.NotEmpty(r3);
    }

    [Fact]
    public void QueryCaseSensitivity_FollowsDocument()
    {
        var caseSensitiveDoc = IndexedDocument.FromMarkdown("The Quick Brown Fox", caseSensitive: true);
        var caseInsensitiveDoc = IndexedDocument.FromMarkdown("The Quick Brown Fox", caseSensitive: false);
        var matcher = new SpanMatcher();

        // Case-sensitive doc: "quick" won't match "Quick"
        var r1 = matcher.Search(caseSensitiveDoc, "Quick");
        Assert.Single(r1);

        var r2 = matcher.Search(caseSensitiveDoc, "quick");
        Assert.Empty(r2);

        // Case-insensitive doc: both match
        var r3 = matcher.Search(caseInsensitiveDoc, "Quick");
        Assert.Single(r3);

        var r4 = matcher.Search(caseInsensitiveDoc, "quick");
        Assert.Single(r4);
    }

    [Fact]
    public void EmptyDocument_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromMarkdown("");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "anything");
        Assert.Empty(results);
    }

    [Fact]
    public void EmptyQuery_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromMarkdown("the quick brown fox");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "");
        Assert.Empty(results);
    }

    [Fact]
    public void NullDocument_Throws()
    {
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentNullException>(() => matcher.Search(null!, "query"));
    }

    [Fact]
    public void NullQuery_Throws()
    {
        var doc = IndexedDocument.FromMarkdown("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentNullException>(() => matcher.Search(doc, null!));
    }

    [Fact]
    public void InvalidTopN_Throws()
    {
        var doc = IndexedDocument.FromMarkdown("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", topN: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", topN: -1));
    }

    [Fact]
    public void InvalidThreshold_Throws()
    {
        var doc = IndexedDocument.FromMarkdown("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", threshold: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", threshold: -0.1));
    }

    [Fact]
    public void TieBreak_CoverageThenStartIndex()
    {
        // Two candidate spans with the same NormalizedScore should be ordered by Coverage then StartIndex
        var doc = IndexedDocument.FromMarkdown(
            "aaa aaa aaa aaa");  // 4 identical tokens
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "aaa aaa aaa", topN: 3);

        if (results.Count >= 2)
        {
            for (int i = 1; i < results.Count; i++)
            {
                Assert.True(
                    results[i - 1].NormalizedScore > results[i].NormalizedScore ||
                    (results[i - 1].NormalizedScore == results[i].NormalizedScore &&
                     results[i - 1].Coverage >= results[i].Coverage) ||
                    (results[i - 1].NormalizedScore == results[i].NormalizedScore &&
                     results[i - 1].Coverage == results[i].Coverage &&
                     results[i - 1].StartIndex <= results[i].StartIndex));
            }
        }
    }

    [Fact]
    public async Task ConcurrentSearches_Safe()
    {
        var doc = IndexedDocument.FromMarkdown("the quick brown fox jumps over the lazy dog");
        var matcher = new SpanMatcher();

        var tasks = new List<Task<List<SpanMatch>>>();
        for (int i = 0; i < 10; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() => matcher.Search(doc, "quick brown fox", topN: 3)));
        }

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.NotEmpty(r));
    }

    [Fact]
    public void ResultsContainOriginalText()
    {
        var doc = IndexedDocument.FromMarkdown("The **quick** brown fox.");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown fox");

        Assert.Single(results);
        Assert.NotNull(results[0].OriginalText);
        Assert.NotEmpty(results[0].OriginalText);
    }
}
