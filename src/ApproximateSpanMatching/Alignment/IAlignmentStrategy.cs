namespace ApproximateSpanMatching.Alignment;

using ApproximateSpanMatching.Models;

/// <summary>
/// Pluggable alignment strategy for matching query tokens against a document region.
/// Implementations produce an AlignmentResult with the optimal score, span boundaries, and matched pairs.
/// </summary>
public interface IAlignmentStrategy
{
    /// <summary>
    /// Aligns query tokens against a document region.
    /// </summary>
    /// <param name="queryTokens">The query word tokens.</param>
    /// <param name="docRegionTokens">The document word tokens in this candidate region.</param>
    /// <param name="docStartIndex">The absolute word index in the document where docRegionTokens[0] starts.</param>
    /// <returns>An AlignmentResult with the optimal alignment.</returns>
    AlignmentResult Align(string[] queryTokens, string[] docRegionTokens, int docStartIndex);
}
