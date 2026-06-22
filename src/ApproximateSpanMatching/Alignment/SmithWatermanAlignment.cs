namespace ApproximateSpanMatching.Alignment;

using ApproximateSpanMatching.Models;

/// <summary>
/// Default alignment strategy using Smith-Waterman local alignment adapted for exact word tokens
/// with affine gap penalties.
/// </summary>
public class SmithWatermanAlignment : IAlignmentStrategy
{
    /// <summary>Penalty for opening a new gap. Default: -2.0.</summary>
    public double GapOpenPenalty { get; }

    /// <summary>Penalty for extending an existing gap. Default: -1.0.</summary>
    public double GapExtendPenalty { get; }

    /// <summary>Reward for an exact word match. Fixed at +1.0.</summary>
    public const double MatchReward = 1.0;

    /// <summary>
    /// Creates a SmithWatermanAlignment with the given gap penalties.
    /// </summary>
    /// <param name="gapOpenPenalty">Penalty for opening a new gap. Must be &lt;= 0. Default: -2.0.</param>
    /// <param name="gapExtendPenalty">Penalty for extending an existing gap. Must be &lt;= 0. Default: -1.0.</param>
    public SmithWatermanAlignment(double gapOpenPenalty = -2.0, double gapExtendPenalty = -1.0)
    {
        if (gapOpenPenalty > 0 || gapExtendPenalty > 0)
            throw new ArgumentException("Gap penalties must be non-positive.");

        GapOpenPenalty = gapOpenPenalty;
        GapExtendPenalty = gapExtendPenalty;
    }

    /// <inheritdoc />
    public AlignmentResult Align(string[] queryTokens, string[] docRegionTokens, int docStartIndex)
    {
        if (queryTokens == null)
            throw new ArgumentNullException(nameof(queryTokens));
        if (docRegionTokens == null)
            throw new ArgumentNullException(nameof(docRegionTokens));

        int m = queryTokens.Length;
        int n = docRegionTokens.Length;

        // Edge case: empty query or empty region → no alignment possible
        if (m == 0 || n == 0)
            return AlignmentResult.Empty(docStartIndex);

        // DP matrices for Smith-Waterman with affine gaps
        // H[i,j] = best score ending at (i,j) (i query tokens, j doc tokens)
        // E[i,j] = best score ending with a gap in the query (alignment ends with doc token consumed but query token skipped)
        //           i.e., the last operation consumed a doc token (gap in query)
        // F[i,j] = best score ending with a gap in the document (alignment ends with query token consumed but doc token skipped)
        //           i.e., the last operation consumed a query token (gap in doc)

        double[,] H = new double[m + 1, n + 1];
        double[,] E = new double[m + 1, n + 1];
        double[,] F = new double[m + 1, n + 1];

        // Initialize with -inf for invalid gap-start boundaries
        double negInf = double.NegativeInfinity;

        for (int i = 0; i <= m; i++)
        {
            H[i, 0] = 0;
            E[i, 0] = negInf;   // can't end a gap in query at j=0
            F[i, 0] = negInf;   // can't end a gap in doc at j=0
        }
        for (int j = 0; j <= n; j++)
        {
            H[0, j] = 0;
            E[0, j] = negInf;   // can't end a gap in query at i=0
            F[0, j] = negInf;   // can't end a gap in doc at i=0
        }

        double bestScore = 0;
        int bestI = 0, bestJ = 0;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                // Gap in query (skip a document token — consume doc word J without matching to query word I)
                // E[i,j] = max(H[i, j-1] + gapOpen + gapExtend, E[i, j-1] + gapExtend)
                double openGapInQuery = H[i, j - 1] + GapOpenPenalty + GapExtendPenalty;
                double extendGapInQuery = E[i, j - 1] + GapExtendPenalty;
                E[i, j] = Math.Max(openGapInQuery, extendGapInQuery);

                // Gap in doc (skip a query token — consume query word I without matching to doc word J)
                // F[i,j] = max(H[i-1, j] + gapOpen + gapExtend, F[i-1, j] + gapExtend)
                double openGapInDoc = H[i - 1, j] + GapOpenPenalty + GapExtendPenalty;
                double extendGapInDoc = F[i - 1, j] + GapExtendPenalty;
                F[i, j] = Math.Max(openGapInDoc, extendGapInDoc);

                // Match (only if tokens are equal — no partial credit for mismatch)
                bool match = queryTokens[i - 1] == docRegionTokens[j - 1];
                double matchScore = match ? H[i - 1, j - 1] + MatchReward : negInf;

                // Best score at (i,j)
                H[i, j] = Math.Max(0, Math.Max(matchScore, Math.Max(E[i, j], F[i, j])));

                if (H[i, j] > bestScore)
                {
                    bestScore = H[i, j];
                    bestI = i;
                    bestJ = j;
                }
            }
        }

        // If no positive-scoring alignment found → return empty
        if (bestScore <= 0)
            return AlignmentResult.Empty(docStartIndex);

        // Backtrack from bestI, bestJ
        var matchedPairs = new List<MatchedPair>();
        int ci = bestI;
        int cj = bestJ;

        while (ci > 0 && cj > 0 && H[ci, cj] > 0)
        {
            // Check if current cell came from a match
            if (queryTokens[ci - 1] == docRegionTokens[cj - 1]
                && Math.Abs(H[ci, cj] - (H[ci - 1, cj - 1] + MatchReward)) < 1e-9)
            {
                matchedPairs.Add(new MatchedPair(ci - 1, docStartIndex + cj - 1));
                ci--;
                cj--;
                continue;
            }

            // Check if it came from E (gap in query — we skipped a doc word)
            if (Math.Abs(H[ci, cj] - E[ci, cj]) < 1e-9)
            {
                // Walk back through E cells until we find the one that opened the gap (from H)
                while (cj > 0)
                {
                    bool fromH = Math.Abs(E[ci, cj] - (H[ci, cj - 1] + GapOpenPenalty + GapExtendPenalty)) < 1e-9;
                    if (fromH)
                    {
                        cj--;  // move to the H cell that opened this gap
                        break;
                    }
                    // Otherwise this is an extend — keep walking back
                    cj--;
                }
                continue;
            }

            // Check if it came from F (gap in doc — we skipped a query word)
            if (Math.Abs(H[ci, cj] - F[ci, cj]) < 1e-9)
            {
                // Walk back through F cells until we find the one that opened the gap (from H)
                while (ci > 0)
                {
                    bool fromH = Math.Abs(F[ci, cj] - (H[ci - 1, cj] + GapOpenPenalty + GapExtendPenalty)) < 1e-9;
                    if (fromH)
                    {
                        ci--;  // move to the H cell that opened this gap
                        break;
                    }
                    // Otherwise this is an extend — keep walking back
                    ci--;
                }
                continue;
            }

            break;
        }

        // MatchedPairs were built in reverse order (from end to start); reverse them
        matchedPairs.Reverse();

        // Determine span boundaries from matched pairs
        if (matchedPairs.Count == 0)
            return AlignmentResult.Empty(docStartIndex);

        int spanStart = matchedPairs[0].DocIndex;
        int spanEnd = matchedPairs[^1].DocIndex + 1;  // exclusive

        return new AlignmentResult(bestScore, matchedPairs, spanStart, spanEnd);
    }
}
