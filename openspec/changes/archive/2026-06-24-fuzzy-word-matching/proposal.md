## Why

The current span matcher requires **exact word equality** to find and score matches. A query for `"qu1ck brown fox"` against a document containing `"quick brown fox"` returns zero results — the OCR-induced substitution of `l` → `1` breaks both anchor discovery (inverted index lookup) and alignment scoring (binary match/mismatch). Users searching against OCR-scanned documents or typing queries with minor typos need the system to tolerate character-level differences while still preferring exact matches.

## What Changes

- **New `IWordSimilarity` interface**: Pluggable abstraction for computing word-level similarity scores (∈ [0, 1]). Comes with a default `TrigramJaccardSimilarity` implementation using character trigrams with padding and bigram fallback for short words.
- **Character n-gram positional index**: Built into `IndexedDocument` at construction time (always-on), enabling approximate word lookup for anchor finding. Exact inverted index remains the primary fast path.
- **Fuzzy anchor finding**: `SpanMatcher` extends anchor discovery to use the n-gram index as a fallback when exact lookup returns empty. Anchors found via fuzzy lookup are subject to a configurable similarity threshold.
- **Similarity-weighted Smith-Waterman alignment**: `SmithWatermanAlignment` accepts an optional `IWordSimilarity`. Instead of binary +1/-∞ match scoring, match score = `similarity × 1.0`. A similarity threshold prevents spurious low-quality pairings.
- **`MatchedPair` extended with `Similarity`**: Each matched pair now carries the word-level similarity score, enabling consumers to distinguish exact from fuzzy matches.
- **Search-time fuzzy control**: `SpanMatcher.Search` gains an optional `SearchOptions` parameter with `EnableFuzzyAnchors` flag (default `false`, preserving backward compatibility). When enabled, both fuzzy anchor discovery and similarity-weighted alignment activate.
- **No breaking changes**: All new parameters are optional with defaults that preserve exact-match-only behavior. Existing code compiles and behaves identically.

## Capabilities

### New Capabilities

- `word-similarity`: Pluggable `IWordSimilarity` interface with a default character-trigram Jaccard implementation. Provides a `Similarity(a, b)` method returning [0, 1] for any two word strings, consumed by the alignment strategy and anchor finding.

### Modified Capabilities

- `alignment-strategy`: `SmithWatermanAlignment` extended to accept an optional `IWordSimilarity` and a similarity threshold. When a similarity function is provided, the match score transitions from binary (+1.0 for exact / -∞ for mismatch) to similarity-weighted (`sim * 1.0` for pairs meeting the threshold, -∞ otherwise).
- `document-indexing`: `IndexedDocument` extended to build a character n-gram positional index (trigrams of `$word$` with bigram fallback for ≤4-character words) alongside the existing exact inverted index. New internal method `GetApproximatePositions(word, threshold)` returns positions of words whose Jaccard similarity exceeds the threshold.
- `span-matching`: `SpanMatcher.Search` extended with optional `SearchOptions` parameter controlling fuzzy anchor discovery. Anchor finding extended to fall back to n-gram lookup for query words with no exact matches. `MatchedPair` extended with a `Similarity` field.

## Impact

- **Code**: `IAlignmentStrategy`, `SmithWatermanAlignment`, `IndexedDocument`, `IndexedDocumentBuilder`, `SpanMatcher`, `MatchedPair`, `SpanMatch`, `AlignmentResult`
- **New files**: `IWordSimilarity.cs`, `TrigramJaccardSimilarity.cs`, `SearchOptions.cs` (or nested config type)
- **No external dependencies**: Trigram computation and Jaccard similarity are implemented inline; no NuGet additions
- **Package**: Minor version bump for `ApproximateSpanMatching`
- **Tests**: New tests for `TrigramJaccardSimilarity`, fuzzy anchor finding, similarity-weighted alignment, and integration tests for the full fuzzy search pipeline
