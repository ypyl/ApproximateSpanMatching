namespace ApproximateSpanMatching.Models;

/// <summary>
/// Result of a single alignment between query tokens and a document region.
/// </summary>
public sealed class AlignmentResult
{
    /// <summary>Raw Smith-Waterman alignment score before normalization. Never below 0.</summary>
    public double Score { get; }

    /// <summary>Ordered list of matched token pairs.</summary>
    public IReadOnlyList<MatchedPair> MatchedPairs { get; }

    /// <summary>Absolute start word index in the document (inclusive).</summary>
    public int SpanStart { get; }

    /// <summary>Absolute end word index in the document (exclusive).</summary>
    public int SpanEnd { get; }

    /// <summary>
    /// Creates a new AlignmentResult.
    /// </summary>
    /// <param name="score">Raw alignment score, never below 0.</param>
    /// <param name="matchedPairs">Ordered list of matched (queryIndex, docIndex) pairs.</param>
    /// <param name="spanStart">Absolute start word index in the document (inclusive).</param>
    /// <param name="spanEnd">Absolute end word index in the document (exclusive).</param>
    public AlignmentResult(double score, IReadOnlyList<MatchedPair> matchedPairs, int spanStart, int spanEnd)
    {
        Score = score;
        MatchedPairs = matchedPairs;
        SpanStart = spanStart;
        SpanEnd = spanEnd;
    }

    /// <summary>
    /// Creates an empty result for cases where no positive-scoring alignment exists.
    /// </summary>
    public static AlignmentResult Empty(int docStartIndex)
        => new(0.0, Array.Empty<MatchedPair>(), docStartIndex, docStartIndex);
}
