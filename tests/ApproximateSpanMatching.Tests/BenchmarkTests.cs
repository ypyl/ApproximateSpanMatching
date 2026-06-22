using System.Diagnostics;
using ApproximateSpanMatching.Matching;
using ApproximateSpanMatching.Models;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class BenchmarkTests
{
    /// <summary>
    /// Performance budget: ~20K-word document with a 50-word query should return
    /// top-3 results in under 200 ms on release build (single-threaded).
    /// In debug builds this may be slower; the assertion is relaxed.
    /// </summary>
    [Fact]
    public void TwentyThousandWordDocument_FiftyWordQuery_UnderBudget()
    {
        // Build a ~20,000 word document
        var sb = new System.Text.StringBuilder();
        string[] wordPool = ["the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
            "cat", "runs", "fast", "slow", "red", "blue", "green", "yellow",
            "big", "small", "tall", "short", "heavy", "light", "day", "night"];

        var rng = new Random(42); // deterministic
        for (int i = 0; i < 20_000; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(wordPool[rng.Next(wordPool.Length)]);
        }

        string largeDocText = sb.ToString();
        var doc = IndexedDocument.FromMarkdown(largeDocText);

        // Build a 50-word query using a subset of words that exist in the doc
        sb.Clear();
        for (int i = 0; i < 50; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(wordPool[rng.Next(wordPool.Length / 2)]); // use common words
        }
        string query = sb.ToString();

        var matcher = new SpanMatcher();
        var sw = Stopwatch.StartNew();
        var results = matcher.Search(doc, query, topN: 3);
        sw.Stop();

        // Verify we got sensible results
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 3);

        // Performance assertion: in release mode, under 200ms
        // In debug mode, allow up to 2 seconds (debug builds are much slower)
#if DEBUG
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Debug benchmark took {sw.ElapsedMilliseconds}ms (expected < 2000ms)");
#else
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"Release benchmark took {sw.ElapsedMilliseconds}ms (expected < 200ms)");
#endif
    }

    /// <summary>
    /// Additional test: verifies the system handles a degenerate case where every
    /// query word appears many times (defeating clustering). This isn't expected
    /// to meet the 200ms budget, but it must not crash or hang.
    /// </summary>
    [Fact]
    public void DegenerateDocument_CompletesWithoutCrash()
    {
        // All words are the same → every query token has hundreds of anchors
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 5_000; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append("word");
        }

        var doc = IndexedDocument.FromMarkdown(sb.ToString());

        var matcher = new SpanMatcher();
        var sw = Stopwatch.StartNew();
        var results = matcher.Search(doc, "word word word word word");
        sw.Stop();

        // Should complete without error (may take longer)
        Assert.NotNull(results);
        // In debug mode, allow generous time
        Assert.True(sw.ElapsedMilliseconds < 30_000,
            $"Degenerate test took {sw.ElapsedMilliseconds}ms (should complete within 30s)");
    }
}
