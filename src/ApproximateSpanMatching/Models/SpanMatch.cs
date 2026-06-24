namespace ApproximateSpanMatching.Models;

/// <summary>
/// A ranked result from a span search: a matching passage in the document with scores and alignment details.
/// </summary>
public sealed class SpanMatch
{
    /// <summary>Absolute start word index in the document (inclusive), half-open interval [StartIndex, EndIndex).</summary>
    public int StartIndex { get; }

    /// <summary>Absolute end word index in the document (exclusive), half-open interval [StartIndex, EndIndex).</summary>
    public int EndIndex { get; }

    /// <summary>Gap-aware quality score = rawSmithWatermanScore / queryWordCount, range [0, 1].</summary>
    public double NormalizedScore { get; }

    /// <summary>Fraction of query words matched = matchedQueryWordCount / queryWordCount, range [0, 1].</summary>
    public double Coverage { get; }

    /// <summary>Original text extracted from the document for [StartIndex, EndIndex).</summary>
    public string OriginalText { get; }

    /// <summary>Alignment trace: list of (queryWordIndex, docWordIndex) pairs showing which words matched.</summary>
    public IReadOnlyList<MatchedPair> MatchedPairs { get; }

    /// <summary>
    /// Creates a new SpanMatch result.
    /// </summary>
    public SpanMatch(
        int startIndex,
        int endIndex,
        double normalizedScore,
        double coverage,
        string originalText,
        IReadOnlyList<MatchedPair> matchedPairs)
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
        NormalizedScore = normalizedScore;
        Coverage = coverage;
        OriginalText = originalText;
        MatchedPairs = matchedPairs;
    }
}
