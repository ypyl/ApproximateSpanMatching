namespace ApproximateSpanMatching.Indexing;

using ApproximateSpanMatching.Models;

/// <summary>
/// Builds an IndexedDocument from tokenized output.
/// </summary>
public static class IndexedDocumentBuilder
{
    /// <summary>
    /// Builds an inverted word-position index from a list of tokens and creates an IndexedDocument.
    /// </summary>
    public static IndexedDocument Build(IReadOnlyList<Token> tokens, string originalText, bool caseSensitive = false)
    {
        var invertedIndex = new Dictionary<string, List<int>>(
            StringComparer.Ordinal);  // tokenizer already normalizes case, so Ordinal is sufficient

        for (int i = 0; i < tokens.Count; i++)
        {
            var word = tokens[i].Text;

            if (!invertedIndex.TryGetValue(word, out var positions))
            {
                positions = new List<int>();
                invertedIndex[word] = positions;
            }
            positions.Add(i);
        }

        return new IndexedDocument(tokens, originalText, caseSensitive, invertedIndex);
    }
}
