## Context

This is a greenfield .NET library (`ApproximateSpanMatching`) for approximate passage matching — locating the most similar span within a larger document based on word-level token similarity. The primary use case is searching OCR-extracted markdown documents (up to ~50 pages, ~20K words) where queries may be paraphrased, truncated, or contain slight variations from the source text.

Key constraints from exploration:
- Exact word matching only (no stemming, synonyms, or character-level edit distance)
- Word order is preserved (strict ordering initially)
- Gap penalties: tight clusters of matches score higher than scattered ones
- Multi-query: document is indexed once, queried many times
- NuGet package name: `ApproximateSpanMatching`

## Goals / Non-Goals

**Goals:**
- Provide `IndexedDocument` for one-time tokenization and inverted index construction from markdown
- Provide `SpanMatcher.Search()` returning top-N matching spans with scores, spans, and alignment traces
- Support pluggable alignment strategies via `IAlignmentStrategy` interface
- Default strict-order alignment using Smith-Waterman at the word level with affine gap penalties
- Operate efficiently for documents up to ~50 PDF pages (~20K words)
- Zero external dependencies for the core library

**Non-Goals:**
- Character-level fuzzy matching or edit distance
- Semantic/embedding-based similarity
- Stemming, lemmatization, or synonym expansion
- Streaming or incremental document updates
- Multi-document corpus search (single document at a time)
- Relaxed ordering (out of scope for initial release, but architecture supports adding it later)
- PDF parsing or OCR (the input is already markdown text)

## Decisions

### D1: Pipeline Architecture — Index → Cluster → Align → Rank

```
Markdown ──► Tokenize ──► IndexedDocument (built once)

Query ──► Find Anchors ──► Cluster ──► Align ──► Rank ──► Top-N Spans
              ▲                           │
              │                       [IAlignmentStrategy]
              │
         Inverted Index
```

**Rationale**: Separates concerns cleanly. The index is reusable. Clustering narrows the search space so alignment only runs on candidate regions (not the full document). Alignment is pluggable via the strategy interface. Ranking is a separate sorting/filtering step. Each step can be tested, tuned, and replaced independently.

**Alternatives considered**:
- Full Smith-Waterman over entire document: O(m×n), unnecessary for 20K-word docs with 5-50 word queries (~1M cells), but architecturally messier — no clean separation between candidate discovery and scoring.
- Sliding window with set similarity: Fast but no gap structure awareness, can't distinguish tight clusters from scattered matches.

### D2: Smith-Waterman at Word Level for Default Alignment

The default `IAlignmentStrategy` implementation uses Smith-Waterman local alignment adapted for word tokens:

```
Match score:     +1 per exact word match
Mismatch:        -∞ (words either match exactly or don't — no partial credit)
Gap open:        g (penalty for starting a gap in document or query)
Gap extend:      e (penalty for each additional word in the gap)
```

The DP recurrence:
```
H[i,j] = max(
    0,                                    // restart (local alignment)
    H[i-1, j-1] + match_score(i,j),       // match/mismatch
    H[i-1, j]   + gap_penalty(1),         // gap in query (skip doc word)
    H[i, j-1]   + gap_penalty(1)          // gap in doc (skip query word)
)
```

Where `gap_penalty(k) = g + k*e` (affine gap model with opening penalty distinct from extension).

**Rationale**: SW is the standard algorithm for local sequence alignment. At word level, it naturally handles missing words (gaps) and preserves ordering. Affine gaps distinguish "many small gaps" from "one big gap" — important for our "tight cluster" preference.

**Alternatives considered**:
- Density-based scoring (matches/span_length): Simpler but doesn't distinguish gap structure within the span. Two spans with same match count and same length get identical scores regardless of internal gap distribution.
- Gap-counting with fixed penalty: Loses the "gap open vs gap extend" distinction. Two 1-word gaps and one 2-word gap score identically when they shouldn't.

### D3: Seed-and-Cluster Heuristic Before Alignment

Before running the expensive alignment, candidate regions are identified:

1. **Anchoring**: For each word in the query, look up all positions in the inverted index. This produces (queryPos, docPos) pairs.
2. **Clustering**: Sort pairs by docPos. Group consecutive pairs where the gap between successive doc positions is ≤ threshold (generous, default: query length × 2). Each group forms a candidate region.
3. **Expansion**: Pad each candidate region by a small margin (e.g., ±query length) to ensure alignment has enough context.

**Rationale**: Reduces alignment cost from O(doc_length × query_length) to O(Σ region_size × query_length). For typical queries (5-50 words) in a 20K-word document, this is a 50-500x speedup. The clustering threshold is non-critical — it only affects performance, not correctness, because alignment within each region finds the optimal sub-span regardless of how the region was defined.

**Note on padded-region overlap**: Region expansion (±query length) can produce overlapping candidate regions. This is expected and safe: the alignment step finds the optimal sub-span within each region independently, and the ranking step's deduplication (D6) collapses any duplicate/overlapping results. No special merge step is needed.

**Alternatives considered**:
- Fixed sliding window over the whole document: Simpler code but no performance benefit from the index, and gaps larger than window size are missed.
- No clustering (directly align full doc): Conceptually simplest but wastes computation on vast irrelevant portions of the document.

### D4: Pluggable Alignment via IAlignmentStrategy

```csharp
public interface IAlignmentStrategy
{
    AlignmentResult Align(string[] queryTokens, string[] docTokens, int docStartIndex);
}
```

The `SpanMatcher` depends on `IAlignmentStrategy`, not a concrete implementation. The default is `SmithWatermanAlignment` with configurable gap penalties. Users can provide custom strategies.

**Rationale**: The user explicitly requested this for future flexibility (e.g., relaxed ordering). The interface is narrow — just the alignment step — so the rest of the pipeline (index, cluster, rank) is unaffected by swapping strategies.

**Alternatives considered**:
- Hard-coding SW and refactoring later: Cheaper now, but the interface boundary is natural and costs almost nothing to add upfront.
- Strategy selector with enum/config: Less flexible than an interface. Doesn't allow user-provided implementations.

### D5: Scoring — Two Normalized Metrics

Raw Smith-Waterman scores depend on query length and incorporate gap penalties. A single number cannot represent both "how many query words were found" and "how tightly they cluster". The result therefore exposes two normalized metrics, both in [0, 1]:

```
coverage        = matchedQueryWordCount / queryWordCount
normalizedScore = rawSmithWatermanScore / queryWordCount
```

- `coverage` = fraction of query words that matched anywhere in the span (ignores gaps). 1.0 = every query word found.
- `normalizedScore` = gap-aware quality score. 1.0 = every query word matched contiguously with no gaps; lower when matches are interrupted by gaps (affine penalties reduce the raw SW score).

**Rationale**: Thresholding and ranking use `normalizedScore` (so tight clusters outrank scattered ones — see "Score reflects gap penalties"). `coverage` is reported for user-facing interpretation ("6 of 7 words found") and diagnostics. The denominator is query length (not span/alignment length) because we measure how much of the query was located. Exposing a single metric would force a choice between gap-awareness and match-count semantics; reporting both preserves each property.

### D6: Top-N with Deduplication

After alignment, candidate spans may overlap. The ranking step:
1. Sort all alignments by `normalizedScore` descending; ties broken by `coverage` descending, then `StartIndex` ascending (deterministic).
2. Iterate, keeping a span if it doesn't overlap with any already-kept span by more than 50%. Overlap is measured against the **smaller** span: `|A ∩ B| / min(|A|, |B|) > 0.5` (word-count overlap, half-open intervals).
3. Drop alignments with `normalizedScore < threshold`.
4. Stop when N spans are collected or candidates are exhausted.

**Rationale**: Without deduplication, the same passage reported multiple times with slightly shifted boundaries is confusing. The 50%-of-smaller-span metric is robust to a short high-score span sitting inside a long low-score one (Jaccard would under-report overlap in that case). Deterministic tie-breaking makes results reproducible across runs.

## Risks / Trade-offs

- **Clustering threshold sensitivity**: If set too low, a real match scattered across the document may be split into fragments that individually score below threshold. Mitigation: Use a generous threshold (default: query length × 2), which only affects performance, not recall. And alignment within each region is optimal — fragmentation only costs you if none of the fragments individually score above threshold.
- **Affine gap parameter tuning**: Default values (g=-2, e=-1) work well for typical text but may need adjustment for very long or very short queries. Mitigation: Expose parameters on the strategy constructor with sensible defaults.
- **Tokenization edge cases**: Punctuation, numbers, hyphenated words, and markdown syntax all need consistent handling. Mitigation: Define a clear tokenization spec (specs/document-indexing) and test with real OCR artifacts.
- **Hyphen vs space asymmetry (known limitation)**: Because hyphens are word characters, a document containing `"state-of-the-art"` (one token) will not match a query `"state of the art"` (four tokens), and vice versa. OCR text also varies hyphen styles (U+002D vs en/em dashes vs soft hyphens). Mitigation: the tokenization spec normalizes the dash range (U+2010–U+2015) to word characters and treats the soft hyphen (U+00AD) as a delimiter, so the common dash variants match consistently. The hyphen-vs-space mismatch is accepted for the initial release; a future normalization strategy (e.g., an `ITokenNormalizer` that splits on hyphens, or treating hyphens as delimiters) can be added behind the existing `IAlignmentStrategy`/tokenizer seam without changing the pipeline.
- **No multi-document support**: Users with multiple documents must create multiple `IndexedDocument` instances. Acceptable for initial release given the "single document" use case.
- **Performance worst case**: The 200 ms budget (span-matching spec: "Performance at scale") covers the common case. A pathological document where every query word occurs hundreds of times throughout defeats clustering and may exceed the budget. Acceptable for v1; the clustering threshold can be tuned or a candidate cap added if real OCR corpora hit this.

## Open Questions

- **Gap penalty defaults**: Starting with g=-2, e=-1. These work well for typical text but may need adjustment for very long or very short queries. Tune with real OCR data once available.
