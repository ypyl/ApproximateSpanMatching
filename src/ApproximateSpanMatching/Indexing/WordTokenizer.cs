namespace ApproximateSpanMatching.Indexing;

using System.Globalization;
using System.Text;
using ApproximateSpanMatching.Models;

/// <summary>
/// Tokenizes text into word tokens with character offsets.
/// Applies NFC normalization, then extracts tokens via greedy leftmost match.
/// </summary>
public static class WordTokenizer
{
    /// <summary>
    /// Tokenizes a text string into an ordered list of word tokens with character offsets.
    /// </summary>
    /// <param name="text">The input text string. Null throws ArgumentNullException.</param>
    /// <param name="caseSensitive">If false (default), tokens are lowercased.</param>
    /// <returns>Ordered list of tokens; empty if input is empty.</returns>
    public static List<Token> Tokenize(string text, bool caseSensitive = false)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        if (text.Length == 0)
            return new List<Token>();

        // Step 1: NFC normalization
        var normalized = text.Normalize(NormalizationForm.FormC);

        var tokens = new List<Token>();
        int i = 0;
        int len = normalized.Length;

        while (i < len)
        {
            // Skip delimiters
            if (!IsWordChar(normalized, i))
            {
                i++;
                continue;
            }

            // Start of a token — consume a maximal run of word characters
            int start = i;
            i++;

            // Greedy leftmost match: advance while current char is a word char
            // Special case: a period between digits is allowed in a token
            while (i < len)
            {
                char c = normalized[i];

                // Standard word char? Continue.
                if (IsWordChar(c))
                {
                    i++;
                    continue;
                }

                // Period between digits? Continue (inner period in numbers).
                if (c == '.')
                {
                    int prevIndex = i - 1;
                    if (i + 1 < len
                        && char.IsDigit(normalized[prevIndex])
                        && char.IsDigit(normalized[i + 1]))
                    {
                        i++; // consume the period
                        i++; // consume the next digit (will be consumed further by outer loop)
                        continue;
                    }
                }

                // Not a word char → end of token
                break;
            }

            // Extract token text from the stored (NFC) string
            string tokenText = normalized[start..i];

            // Optionally lowercase
            if (!caseSensitive)
                tokenText = tokenText.ToLowerInvariant();

            int endChar = i; // exclusive
            tokens.Add(new Token(tokenText, start, endChar));
        }

        return tokens;
    }

    /// <summary>
    /// Determines whether the character at the given index in the string is a word character.
    /// This is called during greedy expansion, and also used to check the first char of a token.
    /// </summary>
    private static bool IsWordChar(string s, int index)
    {
        if (index >= s.Length) return false;
        return IsWordChar(s[index]);
    }

    /// <summary>
    /// Determines whether a single char is a word character (caller handles the digit-internal-period case separately).
    /// </summary>
    internal static bool IsWordChar(char c)
    {
        // Unicode letter (any category L)
        if (char.IsLetter(c)) return true;

        // Decimal digit (category Nd)
        if (char.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber) return true;

        // Apostrophes: U+0027 (straight) and U+2019 (curly/right single quote)
        if (c == '\'' || c == '\u2019') return true;

        // Hyphens and dashes: U+002D and U+2010–U+2015
        if (c == '-' || (c >= '\u2010' && c <= '\u2015')) return true;

        return false;
    }
}
