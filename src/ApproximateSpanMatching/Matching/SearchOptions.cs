namespace ApproximateSpanMatching.Matching;

/// <summary>
/// Options controlling fuzzy search behavior in SpanMatcher.Search.
/// </summary>
public class SearchOptions
{
    /// <summary>
    /// Enable fuzzy anchor discovery for query words with no exact matches.
    /// When false (default), only exact inverted-index lookup is used.
    /// </summary>
    public bool EnableFuzzyAnchors { get; set; } = false;

    /// <summary>
    /// Minimum Jaccard similarity for a fuzzy anchor candidate, in [0, 1].
    /// Only used when <see cref="EnableFuzzyAnchors"/> is true.
    /// Default: 0.3.
    /// </summary>
    public double FuzzyAnchorThreshold { get; set; } = 0.3;
}
