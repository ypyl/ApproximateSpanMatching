namespace ApproximateSpanMatching.Models;

/// <summary>
/// A matched pair of a query word and a document word.
/// </summary>
/// <param name="QueryIndex">Index of the word in the query tokens.</param>
/// <param name="DocIndex">Absolute index of the word in the document tokens.</param>
/// <param name="Similarity">Word similarity score ∈ [0, 1]. 1.0 for exact match, lower for fuzzy matches.</param>
public readonly record struct MatchedPair(int QueryIndex, int DocIndex, double Similarity = 1.0);
