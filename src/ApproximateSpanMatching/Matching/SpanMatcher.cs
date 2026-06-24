namespace ApproximateSpanMatching.Matching;

using ApproximateSpanMatching.Alignment;
using ApproximateSpanMatching.Indexing;
using ApproximateSpanMatching.Models;

/// <summary>
/// Searches for approximate spans in an IndexedDocument matching a query string.
/// Stateless and safe for concurrent use. Reusable across multiple documents and queries.
/// </summary>
public class SpanMatcher
{
    private readonly IAlignmentStrategy _alignmentStrategy;

    /// <summary>
    /// Creates a SpanMatcher with the given alignment strategy.
    /// </summary>
    /// <param name="alignmentStrategy">The alignment strategy to use. Defaults to SmithWatermanAlignment with default gap penalties.</param>
    public SpanMatcher(IAlignmentStrategy? alignmentStrategy = null)
    {
        _alignmentStrategy = alignmentStrategy ?? new SmithWatermanAlignment();
    }

    /// <summary>
    /// Searches an indexed document for the top-N approximate matching spans for a query.
    /// </summary>
    /// <param name="doc">The indexed document to search. Must not be null.</param>
    /// <param name="query">The query string. Must not be null.</param>
    /// <param name="topN">Maximum number of results to return. Must be > 0.</param>
    /// <param name="threshold">Minimum NormalizedScore to include a result, in [0, 1].</param>
    /// <returns>List of top-N non-overlapping spans, ranked by NormalizedScore.</returns>
    public List<SpanMatch> Search(IndexedDocument doc, string query, int topN = 3, double threshold = 0.0)
    {
        // Argument validation
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (topN <= 0)
            throw new ArgumentOutOfRangeException(nameof(topN), "topN must be positive.");
        if (threshold < 0.0 || threshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "threshold must be in [0, 1].");

        // Tokenize the query in the same case-sensitivity mode as the document
        var queryTokens = WordTokenizer.Tokenize(query, doc.CaseSensitive);
        if (queryTokens.Count == 0)
            return new List<SpanMatch>();

        string[] queryWords = queryTokens.Select(t => t.Text).ToArray();
        int queryLength = queryWords.Length;

        // Step 1: Anchor finding
        var anchors = FindAnchors(doc, queryWords);
        if (anchors.Count == 0)
            return new List<SpanMatch>();

        // Step 2: Clustering into candidate regions
        int clusterThreshold = queryLength * 2;
        var regions = ClusterAnchors(anchors, clusterThreshold, queryLength, doc.Tokens.Count);
        if (regions.Count == 0)
            return new List<SpanMatch>();

        // Step 3: Align each region
        var alignmentResults = new List<(AlignmentResult Result, int RegionStart)>();
        foreach (var (regionStart, regionEnd) in regions)
        {
            var regionTokens = doc.Tokens
                .Skip(regionStart)
                .Take(regionEnd - regionStart)
                .Select(t => t.Text)
                .ToArray();

            var result = _alignmentStrategy.Align(queryWords, regionTokens, regionStart);

            // Only keep positive-scoring alignments
            if (result.Score > 0)
                alignmentResults.Add((result, regionStart));
        }

        if (alignmentResults.Count == 0)
            return new List<SpanMatch>();

        // Step 4: Score normalization and conversion to SpanMatch candidates
        var candidates = new List<SpanMatch>();
        foreach (var (alignment, _) in alignmentResults)
        {
            double matchedCount = alignment.MatchedPairs.Count;
            double coverage = matchedCount / queryLength;
            double normalizedScore = alignment.Score / queryLength;

            // Clamp to [0, 1]
            coverage = Math.Clamp(coverage, 0.0, 1.0);
            normalizedScore = Math.Clamp(normalizedScore, 0.0, 1.0);

            // Filter by threshold
            if (normalizedScore < threshold)
                continue;

            string originalText = doc.GetSpan(alignment.SpanStart, alignment.SpanEnd);

            candidates.Add(new SpanMatch(
                alignment.SpanStart,
                alignment.SpanEnd,
                normalizedScore,
                coverage,
                originalText,
                alignment.MatchedPairs));
        }

        if (candidates.Count == 0)
            return new List<SpanMatch>();

        // Step 5: Rank and deduplicate
        return RankAndDeduplicate(candidates, topN);
    }

    /// <summary>
    /// Finds anchor pairs: for each query token, looks up all document positions via the inverted index.
    /// </summary>
    private static List<(int QueryPos, int DocPos)> FindAnchors(IndexedDocument doc, string[] queryWords)
    {
        var anchors = new List<(int QueryPos, int DocPos)>();

        for (int qi = 0; qi < queryWords.Length; qi++)
        {
            var positions = doc.GetPositions(queryWords[qi]);
            foreach (int docPos in positions)
            {
                anchors.Add((qi, docPos));
            }
        }

        return anchors;
    }

    /// <summary>
    /// Clusters anchor pairs into candidate document regions.
    /// Sorts by document position, groups consecutive anchors where gap ≤ threshold, pads each region.
    /// </summary>
    private static List<(int Start, int End)> ClusterAnchors(
        List<(int QueryPos, int DocPos)> anchors,
        int clusterThreshold,
        int queryLength,
        int docTokenCount)
    {
        // Sort by document position, then query position
        anchors.Sort((a, b) =>
        {
            int cmp = a.DocPos.CompareTo(b.DocPos);
            if (cmp != 0) return cmp;
            return a.QueryPos.CompareTo(b.QueryPos);
        });

        var regions = new List<(int Start, int End)>();
        int i = 0;

        while (i < anchors.Count)
        {
            int regionStartDoc = anchors[i].DocPos;
            int regionEndDoc = anchors[i].DocPos + 1;  // inclusive end for building, convert later

            i++;
            while (i < anchors.Count)
            {
                int gap = anchors[i].DocPos - regionEndDoc;
                if (gap <= clusterThreshold)
                {
                    regionEndDoc = anchors[i].DocPos + 1;
                    i++;
                }
                else
                {
                    break;
                }
            }

            // Expand region by ±queryLength, clamped to document bounds
            int paddedStart = Math.Max(0, regionStartDoc - queryLength);
            int paddedEnd = Math.Min(docTokenCount, regionEndDoc + queryLength);

            // Deduplicate against already-added regions: if this region overlaps significantly
            // with the previous one, merge instead of duplicating
            if (regions.Count > 0)
            {
                var last = regions[^1];
                if (paddedStart < last.End)  // overlap
                {
                    // Merge: extend the last region
                    regions[^1] = (last.Start, Math.Max(last.End, paddedEnd));
                    continue;
                }
            }

            regions.Add((paddedStart, paddedEnd));
        }

        return regions;
    }

    /// <summary>
    /// Ranks candidate spans by NormalizedScore descending, then Coverage descending, then StartIndex ascending.
    /// Deduplicates by keeping a span only if overlap with every already-kept span is ≤ 0.5
    /// (overlap measured as |intersection| / min(|A|, |B|) over half-open word intervals).
    /// Takes at most topN results.
    /// </summary>
    private static List<SpanMatch> RankAndDeduplicate(List<SpanMatch> candidates, int topN)
    {
        // Sort: descending NormalizedScore, descending Coverage, ascending StartIndex
        candidates.Sort((a, b) =>
        {
            int cmp = b.NormalizedScore.CompareTo(a.NormalizedScore);
            if (cmp != 0) return cmp;
            cmp = b.Coverage.CompareTo(a.Coverage);
            if (cmp != 0) return cmp;
            return a.StartIndex.CompareTo(b.StartIndex);
        });

        var kept = new List<SpanMatch>();

        foreach (var candidate in candidates)
        {
            bool overlaps = false;
            int candLen = candidate.EndIndex - candidate.StartIndex;

            foreach (var existing in kept)
            {
                // Compute intersection
                int intersectStart = Math.Max(candidate.StartIndex, existing.StartIndex);
                int intersectEnd = Math.Min(candidate.EndIndex, existing.EndIndex);
                int intersectLen = Math.Max(0, intersectEnd - intersectStart);

                int minLen = Math.Min(candLen, existing.EndIndex - existing.StartIndex);
                if (minLen == 0)
                    continue;

                double overlapRatio = (double)intersectLen / minLen;
                if (overlapRatio > 0.5)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                kept.Add(candidate);
                if (kept.Count >= topN)
                    break;
            }
        }

        return kept;
    }
}
