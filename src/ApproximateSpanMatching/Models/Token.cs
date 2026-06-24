namespace ApproximateSpanMatching.Models;

/// <summary>
/// A single word token extracted from text.
/// Offsets are UTF-16 code units into the stored NFC-normalized string.
/// </summary>
/// <param name="Text">The normalized word text.</param>
/// <param name="StartChar">Start character offset in the stored string (inclusive).</param>
/// <param name="EndChar">End character offset in the stored string (exclusive).</param>
public sealed record Token(string Text, int StartChar, int EndChar);
