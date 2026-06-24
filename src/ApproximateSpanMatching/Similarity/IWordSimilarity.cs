namespace ApproximateSpanMatching.Similarity;

/// <summary>
/// Pluggable word similarity function for fuzzy word matching.
/// Implementations compute a score ∈ [0, 1] where 1 = identical, 0 = completely different.
/// </summary>
public interface IWordSimilarity
{
    /// <summary>
    /// Returns similarity ∈ [0, 1] where 1 = identical, 0 = completely different.
    /// Must be symmetric: Similarity(a, b) == Similarity(b, a).
    /// Must be reflexive: Similarity(a, a) == 1.0.
    /// </summary>
    double Similarity(string a, string b);
}
