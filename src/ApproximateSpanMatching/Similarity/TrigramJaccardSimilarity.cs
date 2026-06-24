namespace ApproximateSpanMatching.Similarity;

/// <summary>
/// Default word similarity implementation using character n-gram Jaccard coefficient.
/// Computes n-grams of "$word$" (padded with sentinels). The n-gram size is chosen
/// consistently for the pair: bigrams when both words are ≤ 4 characters, trigrams otherwise.
/// Similarity = |intersection| / |union|.
/// </summary>
public class TrigramJaccardSimilarity : IWordSimilarity
{
    /// <summary>
    /// Computes the Jaccard similarity between two words using character n-grams.
    /// </summary>
    /// <param name="a">First word (case-sensitive comparison; caller normalizes case).</param>
    /// <param name="b">Second word (case-sensitive comparison; caller normalizes case).</param>
    /// <returns>Similarity ∈ [0, 1].</returns>
    public double Similarity(string a, string b)
    {
        // Both empty → identical
        if (a.Length == 0 && b.Length == 0)
            return 1.0;

        // One empty, one not → no similarity
        if (a.Length == 0 || b.Length == 0)
            return 0.0;

        // Pair-consistent n-gram size: bigrams only when both words are short,
        // trigrams otherwise. Using max() ensures length-changing edits across the
        // 4-char boundary still produce comparable n-gram sets (same n for both).
        int n = (Math.Max(a.Length, b.Length) <= 4) ? 2 : 3;

        var setA = GetNgrams(a, n);
        var setB = GetNgrams(b, n);

        if (setA.Count == 0 && setB.Count == 0)
            return 1.0;  // both produce no n-grams (e.g., single-char words)
        if (setA.Count == 0 || setB.Count == 0)
            return 0.0;

        int intersection = 0;
        foreach (var ngram in setA)
        {
            if (setB.Contains(ngram))
                intersection++;
        }

        int union = setA.Count + setB.Count - intersection;
        return (double)intersection / union;
    }

    /// <summary>
    /// Extracts character n-grams of size <paramref name="n"/> from a word padded
    /// with "$" sentinels. The caller chooses <paramref name="n"/> so that both
    /// words in a comparison use the same n-gram size.
    /// </summary>
    internal static HashSet<string> GetNgrams(string word, int n)
    {
        string padded = "$" + word + "$";
        var ngrams = new HashSet<string>();
        for (int i = 0; i <= padded.Length - n; i++)
        {
            ngrams.Add(padded.Substring(i, n));
        }

        return ngrams;
    }
}
