namespace ApproximateSpanMatching.Indexing;

using ApproximateSpanMatching.Models;
using ApproximateSpanMatching.Similarity;

/// <summary>
/// Builds an IndexedDocument from tokenized output.
/// </summary>
public static class IndexedDocumentBuilder
{
    /// <summary>
    /// Builds an inverted word-position index and character n-gram index from a list of tokens and creates an IndexedDocument.
    /// </summary>
    public static IndexedDocument Build(IReadOnlyList<Token> tokens, string originalText, bool caseSensitive = false)
    {
        var invertedIndex = new Dictionary<string, List<int>>(
            StringComparer.Ordinal);  // tokenizer already normalizes case, so Ordinal is sufficient

        var ngramIndex = new Dictionary<string, List<int>>(
            StringComparer.Ordinal);

        for (int i = 0; i < tokens.Count; i++)
        {
            var word = tokens[i].Text;

            // Exact inverted index
            if (!invertedIndex.TryGetValue(word, out var positions))
            {
                positions = new List<int>();
                invertedIndex[word] = positions;
            }
            positions.Add(i);

            // Character n-gram index — store BOTH bigrams and trigrams for every token
            // so that lookups for either n-gram size succeed regardless of token length.
            // This makes the index length-agnostic and supports pair-consistent n selection
            // at query time (see IndexedDocument.GetApproximatePositions).
            AddNgramPositions(ngramIndex, TrigramJaccardSimilarity.GetNgrams(word, 2), i);
            AddNgramPositions(ngramIndex, TrigramJaccardSimilarity.GetNgrams(word, 3), i);
        }

        return new IndexedDocument(tokens, originalText, caseSensitive, invertedIndex, ngramIndex);
    }

    private static void AddNgramPositions(Dictionary<string, List<int>> ngramIndex, HashSet<string> ngrams, int position)
    {
        foreach (var ngram in ngrams)
        {
            if (!ngramIndex.TryGetValue(ngram, out var ngramPositions))
            {
                ngramPositions = new List<int>();
                ngramIndex[ngram] = ngramPositions;
            }
            ngramPositions.Add(position);
        }
    }
}
