using ApproximateSpanMatching.Similarity;
using Xunit;

namespace ApproximateSpanMatching.Tests;

public class TrigramJaccardSimilarityTests
{
    private readonly TrigramJaccardSimilarity _sim = new();

    [Fact]
    public void ExactMatch_Returns1()
    {
        Assert.Equal(1.0, _sim.Similarity("quick", "quick"));
    }

    [Fact]
    public void SingleCharSubstitution()
    {
        // "quick" vs "qu1ck" — 1 char difference
        // Trigrams of $quick$: $qu, qui, uic, ick, ck$ (5)
        // Trigrams of $qu1ck$: $qu, qu1, u1c, 1ck, ck$ (5)
        // Intersection: $qu, ck$ (2), Union: 8 → 2/8 = 0.25
        double sim = _sim.Similarity("quick", "qu1ck");
        Assert.Equal(0.25, sim, 6);
    }

    [Fact]
    public void CompletelyDifferent_Returns0()
    {
        double sim = _sim.Similarity("elephant", "quick");
        Assert.Equal(0.0, sim);
    }

    [Fact]
    public void EmptyStrings()
    {
        Assert.Equal(1.0, _sim.Similarity("", ""));
        Assert.Equal(0.0, _sim.Similarity("", "word"));
        Assert.Equal(0.0, _sim.Similarity("word", ""));
    }

    [Fact]
    public void Symmetry()
    {
        double ab = _sim.Similarity("quick", "qu1ck");
        double ba = _sim.Similarity("qu1ck", "quick");
        Assert.Equal(ab, ba);

        ab = _sim.Similarity("cat", "dog");
        ba = _sim.Similarity("dog", "cat");
        Assert.Equal(ab, ba);
    }

    [Fact]
    public void BigramFallback_ShortWord()
    {
        // "cat" vs "bat": both ≤ 4 chars → bigrams
        // Bigrams of $cat$: $c, ca, at, t$ (4)
        // Bigrams of $bat$: $b, ba, at, t$ (4)
        // Intersection: at, t$ (2), Union: 6 → 2/6 ≈ 0.333
        double sim = _sim.Similarity("cat", "bat");
        Assert.True(sim > 0.0);
        Assert.True(sim < 1.0);
        // Exact value depends on bigram extraction
        Assert.Equal(2.0 / 6.0, sim, 6);
    }

    [Fact]
    public void BigramFallback_IdenticalShortWord()
    {
        double sim = _sim.Similarity("cat", "cat");
        Assert.Equal(1.0, sim);
    }

    [Fact]
    public void BigramFallback_TotallyDifferent()
    {
        double sim = _sim.Similarity("cat", "dog");
        // Bigrams of $cat$: $c, ca, at, t$
        // Bigrams of $dog$: $d, do, og, g$
        // Intersection: 0, Union: 8 → 0.0
        Assert.Equal(0.0, sim);
    }

    // --- Cross-boundary length-changing edit tests (the headline bug fix) ---

    [Fact]
    public void CrossBoundary_Insertion_WordToWords()
    {
        // Lengths 4 and 5 → max > 4 → trigrams for both (pair-consistent)
        // Previously: "word" used bigrams, "words" used trigrams → 0.0 (bug)
        double sim = _sim.Similarity("word", "words");
        Assert.True(sim > 0.0, "Insertion across 4-char boundary must not yield 0.0");
        Assert.True(sim < 1.0);
    }

    [Fact]
    public void CrossBoundary_Deletion_QuickToQuik()
    {
        // Lengths 5 and 4 → max > 4 → trigrams for both (pair-consistent)
        // Previously: "quick" used trigrams, "quik" used bigrams → 0.0 (bug)
        double sim = _sim.Similarity("quick", "quik");
        Assert.True(sim > 0.0, "Deletion across 4-char boundary must not yield 0.0");
        Assert.True(sim < 1.0);
    }

    [Fact]
    public void CrossBoundary_CatsToCatch()
    {
        // Lengths 4 and 5 → trigrams for both
        double sim = _sim.Similarity("cats", "catch");
        Assert.True(sim > 0.0, "4-vs-5 pair must not yield 0.0");
        Assert.True(sim < 1.0);
    }

    [Fact]
    public void SingleCharWord()
    {
        // Single-char word: bigram fallback applies
        // $a$ → $a, a$ (2 bigrams)
        // $b$ → $b, b$ (2 bigrams)
        // Intersection: 0 → 0.0
        double sim = _sim.Similarity("a", "b");
        Assert.Equal(0.0, sim);

        sim = _sim.Similarity("a", "a");
        Assert.Equal(1.0, sim);
    }

    [Fact]
    public void CaseSensitive_Different()
    {
        // TrigramJaccardSimilarity is case-sensitive
        double sim = _sim.Similarity("Quick", "quick");
        // Trigrams of $Quick$: $Qu, Qui, uic, ick, ck$ 
        // Trigrams of $quick$: $qu, qui, uic, ick, ck$
        // Intersection: uic, ick, ck$ (3), Union: 7 → 3/7 ≈ 0.429
        Assert.True(sim < 1.0, "Case difference should reduce similarity");
        Assert.True(sim > 0.0);
    }

    [Fact]
    public void GetNgrams_TrigramsForLongWord()
    {
        var ngrams = TrigramJaccardSimilarity.GetNgrams("quick", 3);
        Assert.Equal(5, ngrams.Count);
        Assert.Contains("$qu", ngrams);
        Assert.Contains("qui", ngrams);
        Assert.Contains("uic", ngrams);
        Assert.Contains("ick", ngrams);
        Assert.Contains("ck$", ngrams);
    }

    [Fact]
    public void GetNgrams_BigramsForShortWord()
    {
        var ngrams = TrigramJaccardSimilarity.GetNgrams("cat", 2);
        Assert.Equal(4, ngrams.Count);
        Assert.Contains("$c", ngrams);
        Assert.Contains("ca", ngrams);
        Assert.Contains("at", ngrams);
        Assert.Contains("t$", ngrams);
    }
}
