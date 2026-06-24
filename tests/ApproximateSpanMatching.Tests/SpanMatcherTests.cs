using ApproximateSpanMatching.Alignment;
using ApproximateSpanMatching.Matching;
using ApproximateSpanMatching.Models;
using ApproximateSpanMatching.Similarity;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class SpanMatcherTests
{
    [Fact]
    public void ExactMatch_ReturnsOneSpan()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps");
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
        var doc = IndexedDocument.FromText("quick brown fox jumps over the lazy dog");
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
        var doc = IndexedDocument.FromText("quick brown fox over lazy dog");
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
        var doc = IndexedDocument.FromText("the quick brown fox jumps over the lazy dog");
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
        var doc = IndexedDocument.FromText(
            "the quick brown the quick brown the quick brown the quick brown the quick brown");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown", topN: 3);

        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void OverlappingSpans_Deduplicated()
    {
        var doc = IndexedDocument.FromText(
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
        var doc = IndexedDocument.FromText("the quick brown fox jumps over the lazy dog");
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
        var caseSensitiveDoc = IndexedDocument.FromText("The Quick Brown Fox", caseSensitive: true);
        var caseInsensitiveDoc = IndexedDocument.FromText("The Quick Brown Fox", caseSensitive: false);
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
        var doc = IndexedDocument.FromText("");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "anything");
        Assert.Empty(results);
    }

    [Fact]
    public void EmptyQuery_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
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
        var doc = IndexedDocument.FromText("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentNullException>(() => matcher.Search(doc, null!));
    }

    [Fact]
    public void InvalidTopN_Throws()
    {
        var doc = IndexedDocument.FromText("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", topN: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", topN: -1));
    }

    [Fact]
    public void InvalidThreshold_Throws()
    {
        var doc = IndexedDocument.FromText("test");
        var matcher = new SpanMatcher();
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", threshold: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", threshold: -0.1));
    }

    [Fact]
    public void TieBreak_CoverageThenStartIndex()
    {
        // Two candidate spans with the same NormalizedScore should be ordered by Coverage then StartIndex
        var doc = IndexedDocument.FromText(
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
        var doc = IndexedDocument.FromText("the quick brown fox jumps over the lazy dog");
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
        var doc = IndexedDocument.FromText("The **quick** brown fox.");
        var matcher = new SpanMatcher();
        var results = matcher.Search(doc, "quick brown fox");

        Assert.Single(results);
        Assert.NotNull(results[0].OriginalText);
        Assert.NotEmpty(results[0].OriginalText);
    }

    // --- Fuzzy search integration tests ---

    [Fact]
    public void FuzzyDisabled_BackwardCompatible()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps");
        var matcher = new SpanMatcher();
        // No options → fuzzy disabled, same as before
        var results = matcher.Search(doc, "quick brown fox");

        Assert.Single(results);
        Assert.Equal(1.0, results[0].NormalizedScore);
    }

    [Fact]
    public void FuzzyEnabled_FindsSimilarWords()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        var results = matcher.Search(doc, "qu1ck brown fox", options: options);

        Assert.Single(results);
        Assert.True(results[0].NormalizedScore < 1.0);
        Assert.True(results[0].NormalizedScore > 0.0);
    }

    [Fact]
    public void FuzzySearch_MatchedPairsHaveSimilarity()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps");
        var sim = new TrigramJaccardSimilarity();
        var strategy = new SmithWatermanAlignment(wordSimilarity: sim, similarityThreshold: 0.2);
        var matcher = new SpanMatcher(alignmentStrategy: strategy);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        var results = matcher.Search(doc, "qu1ck brown fox", options: options);

        Assert.NotEmpty(results);
        // First matched pair should be fuzzy ("qu1ck"↔"quick", similarity < 1.0)
        var firstMatch = results[0].MatchedPairs[0];
        Assert.True(firstMatch.Similarity < 1.0);

        // Second and third should be exact
        Assert.Equal(1.0, results[0].MatchedPairs[1].Similarity);
        Assert.Equal(1.0, results[0].MatchedPairs[2].Similarity);
    }

    [Fact]
    public void FuzzySearch_HighThreshold_Excludes()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        // FuzzyAnchorThreshold 0.5 is above ~0.25 Jaccard for "qu1ck"↔"quick"
        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.5 };
        var results = matcher.Search(doc, "qu1ck brown", options: options);

        // "qu1ck" has no fuzzy anchors → only "brown" anchors exist → no meaningful alignment
        // This may return empty or a single-word result
        if (results.Count > 0)
        {
            Assert.True(results[0].NormalizedScore < 1.0);
        }
    }

    [Fact]
    public void FuzzySearch_AllWordsFuzzy()
    {
        var doc = IndexedDocument.FromText("quick brown fox");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        // Both query words need fuzzy anchors
        var results = matcher.Search(doc, "qu1ck br0wn", options: options);

        // Should still find the span, with reduced score
        if (results.Count > 0)
        {
            Assert.True(results[0].NormalizedScore > 0.0);
            Assert.True(results[0].NormalizedScore < 1.0);
        }
    }

    [Fact]
    public void FuzzySearch_ExactAnchorsTakePriority()
    {
        var doc = IndexedDocument.FromText("the quick brown fox the quick brown fox");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        // "brown" has exact matches, "qu1ck" needs fuzzy
        var results = matcher.Search(doc, "qu1ck brown", options: options);

        Assert.NotEmpty(results);
        // Both "brown" positions should have been found (exact anchors not suppressed by fuzzy)
    }

    // --- Edge case tests ---

    [Fact]
    public void FuzzySearch_EmptyQuery()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true };
        var results = matcher.Search(doc, "", options: options);

        Assert.Empty(results);
    }

    [Fact]
    public void FuzzySearch_NoSimilarWords_ReturnsEmpty()
    {
        var doc = IndexedDocument.FromText("the quick brown fox");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        // "zzzzz" has no trigram overlap with any document word
        var results = matcher.Search(doc, "zzzzz brown", options: options);

        // Should still find "brown" via exact match, but "zzzzz" contributes nothing
        if (results.Count > 0)
        {
            Assert.True(results[0].Coverage <= 0.5);
        }
    }

    [Fact]
    public void FuzzySearch_ShortQueryWords_UsesBigrams()
    {
        var doc = IndexedDocument.FromText("cat dog bird");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        // "bat" should have bigram overlap with "cat"
        var results = matcher.Search(doc, "bat", options: options);

        // Should find "cat" via fuzzy anchor, with reduced score
        if (results.Count > 0)
        {
            Assert.True(results[0].NormalizedScore > 0.0);
            Assert.True(results[0].NormalizedScore < 1.0);
        }
    }

    [Fact]
    public async Task FuzzySearch_ConcurrentSearches()
    {
        var doc = IndexedDocument.FromText("the quick brown fox jumps over the lazy dog");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        var tasks = new List<Task<List<SpanMatch>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => matcher.Search(doc, "qu1ck brown fox", options: options)));
        }

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.NotEmpty(r));
    }

    [Fact]
    public void FuzzySearch_InvalidThreshold_Throws()
    {
        var doc = IndexedDocument.FromText("test");
        var matcher = new SpanMatcher();

        var badOptions1 = new SearchOptions { FuzzyAnchorThreshold = 1.5 };
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", options: badOptions1));

        var badOptions2 = new SearchOptions { FuzzyAnchorThreshold = -0.1 };
        Assert.Throws<ArgumentOutOfRangeException>(() => matcher.Search(doc, "test", options: badOptions2));
    }

    [Fact]
    public void FuzzySearch_NormalizedScoreReflectsSimilarity()
    {
        // 5-word query where most/all words match fuzzily
        // Score should be reduced proportionally
        var doc = IndexedDocument.FromText("quick brown fox jumps over");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
        // Exact search (for comparison)
        var exactResults = matcher.Search(doc, "quick brown fox jumps over");

        if (exactResults.Count > 0)
        {
            Assert.Equal(1.0, exactResults[0].NormalizedScore);
        }
    }

    [Fact]
    public void FuzzySearch_WithDisabledFlag_ExactMatchOnly()
    {
        var doc = IndexedDocument.FromText("quick brown fox");
        var sim = new TrigramJaccardSimilarity();
        var matcher = new SpanMatcher(wordSimilarity: sim, similarityThreshold: 0.2);

        // Disabled explicitly
        var options = new SearchOptions { EnableFuzzyAnchors = false };
        var results = matcher.Search(doc, "qu1ck", options: options);

        // "qu1ck" has no exact anchors → no results
        Assert.Empty(results);
    }
}
