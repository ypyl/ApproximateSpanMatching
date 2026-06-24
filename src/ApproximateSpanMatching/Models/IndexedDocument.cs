namespace ApproximateSpanMatching.Models;

using ApproximateSpanMatching.Similarity;

/// <summary>
/// An immutable, thread-safe indexed representation of a text document.
/// Built once from text and reusable across multiple queries.
/// </summary>
public sealed class IndexedDocument
{
    /// <summary>The ordered sequence of word tokens in the document.</summary>
    public IReadOnlyList<Token> Tokens { get; }

    /// <summary>The NFC-normalized text used as the source.</summary>
    public string OriginalText { get; }

    /// <summary>Whether this document was built in case-sensitive mode.</summary>
    public bool CaseSensitive { get; }

    /// <summary>
    /// Internal inverted index mapping unique tokens (lowercase in case-insensitive mode) to all their positions.
    /// </summary>
    private readonly Dictionary<string, List<int>> _invertedIndex;

    /// <summary>
    /// Character n-gram positional index for approximate word lookup.
    /// Maps each n-gram (trigram of $word$ or bigram for short words) to document positions.
    /// </summary>
    private readonly Dictionary<string, List<int>> _ngramIndex;

    internal IndexedDocument(
        IReadOnlyList<Token> tokens,
        string originalText,
        bool caseSensitive,
        Dictionary<string, List<int>> invertedIndex,
        Dictionary<string, List<int>> ngramIndex)
    {
        Tokens = tokens;
        OriginalText = originalText;
        CaseSensitive = caseSensitive;
        _invertedIndex = invertedIndex;
        _ngramIndex = ngramIndex;
    }

    /// <summary>
    /// Extracts the original text span for a given word position range.
    /// </summary>
    /// <param name="startWordIndex">Start word index, inclusive.</param>
    /// <param name="endWordIndex">End word index, exclusive.</param>
    /// <returns>The text from Tokens[start].StartChar to Tokens[end-1].EndChar.</returns>
    public string GetSpan(int startWordIndex, int endWordIndex)
    {
        if (startWordIndex < 0 || startWordIndex >= Tokens.Count)
            throw new ArgumentOutOfRangeException(nameof(startWordIndex));
        if (endWordIndex < startWordIndex || endWordIndex > Tokens.Count)
            throw new ArgumentOutOfRangeException(nameof(endWordIndex));

        if (startWordIndex == endWordIndex)
            return string.Empty;

        var start = Tokens[startWordIndex].StartChar;
        var end = Tokens[endWordIndex - 1].EndChar;
        return OriginalText[start..end];
    }

    /// <summary>
    /// Looks up all word positions for a given token. Returns an empty list if not found.
    /// Internal — only SpanMatcher and tests need access to the index.
    /// </summary>
    internal IReadOnlyList<int> GetPositions(string word)
    {
        return _invertedIndex.TryGetValue(word, out var positions)
            ? positions
            : Array.Empty<int>();
    }

    /// <summary>
    /// Finds document positions of words whose character n-gram Jaccard similarity
    /// to the given word meets or exceeds the threshold.
    /// Returns positions ordered by similarity descending.
    /// Internal — used by SpanMatcher for fuzzy anchor discovery.
    /// </summary>
    internal List<(int Position, double Similarity)> GetApproximatePositions(string word, double threshold)
    {
        if (string.IsNullOrEmpty(word) || _ngramIndex.Count == 0)
            return new List<(int, double)>();

        // Gather candidate positions using BOTH bigram and trigram n-grams of the query
        // word. Because the index stores both sizes per token, and the candidate doc word's
        // length is unknown until we inspect it, querying both sizes ensures we don't miss
        // candidates whose pair-consistent n differs from the query word's own length class.
        var candidatePositions = new HashSet<int>();
        foreach (var ngram in TrigramJaccardSimilarity.GetNgrams(word, 2))
        {
            if (_ngramIndex.TryGetValue(ngram, out var positions))
                foreach (int pos in positions) candidatePositions.Add(pos);
        }
        foreach (var ngram in TrigramJaccardSimilarity.GetNgrams(word, 3))
        {
            if (_ngramIndex.TryGetValue(ngram, out var positions))
                foreach (int pos in positions) candidatePositions.Add(pos);
        }

        if (candidatePositions.Count == 0)
            return new List<(int, double)>();

        // For each candidate, compute pair-consistent Jaccard similarity and filter by threshold
        var results = new List<(int Position, double Similarity)>();
        foreach (int pos in candidatePositions)
        {
            var docWord = Tokens[pos].Text;
            double similarity = ComputeNgramJaccard(word, docWord);
            if (similarity >= threshold)
            {
                results.Add((pos, similarity));
            }
        }

        // Sort by similarity descending
        results.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));
        return results;
    }

    private static double ComputeNgramJaccard(string a, string b)
    {
        // Pair-consistent n-gram size (must match TrigramJaccardSimilarity.Similarity)
        int n = (Math.Max(a.Length, b.Length) <= 4) ? 2 : 3;
        var setA = TrigramJaccardSimilarity.GetNgrams(a, n);
        var setB = TrigramJaccardSimilarity.GetNgrams(b, n);

        if (setA.Count == 0 && setB.Count == 0)
            return 1.0;
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
    /// Static factory: tokenizes text and builds an IndexedDocument in one call.
    /// </summary>
    /// <param name="text">The input text.</param>
    /// <param name="caseSensitive">If true, token casing is preserved.</param>
    public static IndexedDocument FromText(string text, bool caseSensitive = false)
    {
        // NFC-normalize first so both the tokens' character offsets and OriginalText agree.
        var normalized = text.Normalize(System.Text.NormalizationForm.FormC);
        var tokens = Indexing.WordTokenizer.Tokenize(normalized, caseSensitive);
        return Indexing.IndexedDocumentBuilder.Build(tokens, normalized, caseSensitive);
    }
}
