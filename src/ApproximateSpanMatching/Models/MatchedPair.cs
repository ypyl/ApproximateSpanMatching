namespace ApproximateSpanMatching.Models;

/// <summary>
/// A matched pair of a query word and a document word.
/// </summary>
/// <param name="QueryIndex">Index of the word in the query tokens.</param>
/// <param name="DocIndex">Absolute index of the word in the document tokens.</param>
public readonly record struct MatchedPair(int QueryIndex, int DocIndex);
