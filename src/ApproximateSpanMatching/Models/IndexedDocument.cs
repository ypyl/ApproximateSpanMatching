namespace ApproximateSpanMatching.Models;

/// <summary>
/// An immutable, thread-safe indexed representation of a markdown document.
/// Built once from markdown text and reusable across multiple queries.
/// </summary>
public sealed class IndexedDocument
{
    /// <summary>The ordered sequence of word tokens in the document.</summary>
    public IReadOnlyList<Token> Tokens { get; }

    /// <summary>The NFC-normalized markdown text used as the source.</summary>
    public string OriginalText { get; }

    /// <summary>Whether this document was built in case-sensitive mode.</summary>
    public bool CaseSensitive { get; }

    /// <summary>
    /// Internal inverted index mapping unique tokens (lowercase in case-insensitive mode) to all their positions.
    /// </summary>
    private readonly Dictionary<string, List<int>> _invertedIndex;

    internal IndexedDocument(
        IReadOnlyList<Token> tokens,
        string originalText,
        bool caseSensitive,
        Dictionary<string, List<int>> invertedIndex)
    {
        Tokens = tokens;
        OriginalText = originalText;
        CaseSensitive = caseSensitive;
        _invertedIndex = invertedIndex;
    }

    /// <summary>
    /// Extracts the original text span for a given word position range.
    /// </summary>
    /// <param name="startWordIndex">Start word index, inclusive.</param>
    /// <param name="endWordIndex">End word index, exclusive.</param>
    /// <returns>The markdown text from Tokens[start].StartChar to Tokens[end-1].EndChar.</returns>
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
    /// Static factory: tokenizes markdown and builds an IndexedDocument in one call.
    /// </summary>
    /// <param name="markdown">The input markdown text.</param>
    /// <param name="caseSensitive">If true, token casing is preserved.</param>
    public static IndexedDocument FromMarkdown(string markdown, bool caseSensitive = false)
    {
        // NFC-normalize first so both the tokens' character offsets and OriginalText agree.
        var normalized = markdown.Normalize(System.Text.NormalizationForm.FormC);
        var tokens = Indexing.MarkdownTokenizer.Tokenize(normalized, caseSensitive);
        return Indexing.IndexedDocumentBuilder.Build(tokens, normalized, caseSensitive);
    }
}
