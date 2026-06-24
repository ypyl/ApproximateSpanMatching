## Context

The `ApproximateSpanMatching` library currently performs word-level span search using exact string equality for both anchor discovery (inverted index lookup) and alignment scoring (Smith-Waterman match reward). This is a deliberate design that keeps the algorithm fast and deterministic. However, for OCR-scanned documents and user-typed queries, exact matching is too brittle — a single-character discrepancy like `"qu1ck"` vs `"quick"` causes the word to become invisible to the entire pipeline.

This design extends the library with character-level fuzzy matching while maintaining the existing exact-match fast path as the default behavior.

**Constraints:**
- Zero external NuGet dependencies (pure .NET 10 class library)
- Backward compatible: all existing code, tests, and API contracts continue to work unchanged
- The `IAlignmentStrategy` pluggability remains the primary extension point for alignment behavior
- Performance budget: fuzzy-enabled searches should stay under the existing 200ms target for the 20K word / 50-word query reference case

## Goals / Non-Goals

**Goals:**
- Enable the system to discover and score spans when query words have minor character-level differences from document words (typos, OCR errors)
- Provide a pluggable word-similarity abstraction (`IWordSimilarity`) so users can swap similarity metrics
- Ship a default similarity implementation based on character trigram Jaccard that handles common single-character substitutions, insertions, and deletions
- Extend `MatchedPair` to carry similarity so consumers can distinguish exact from fuzzy matches
- Make fuzzy behavior opt-in at query time, with exact matching as the safe default

**Non-Goals:**
- Stemming, lemmatization, or morphological analysis (user confirmed this is not the use case)
- Phonetic matching (Soundex, Metaphone)
- Semantic/embedding-based word similarity
- Fuzzy matching that reorders query words (order preservation is preserved)
- Changing the overlap deduplication, ranking, or threshold filtering logic

## Decisions

### D1: Two-layer abstraction — `IWordSimilarity` consumed by `IAlignmentStrategy`

Rather than making `IAlignmentStrategy` directly fuzzy-aware, we introduce a separate `IWordSimilarity` interface:

```
IWordSimilarity               IAlignmentStrategy
┌──────────────┐              ┌───────────────────┐
│ Similarity() │◄─────────────│ Align()            │
│              │  consumed by │   uses similarity  │
│ (pluggable)  │              │   for match score  │
└──────────────┘              └───────────────────┘
```

**Rationale:** Separation of concerns. The alignment strategy (Smith-Waterman, or a future custom one) should not encode *how* word similarity is computed — only that it has a similarity function to work with. This lets users mix and match: Levenshtein with SW, trigram Jaccard with SW, or even plug a custom alignment algorithm that consumes the same similarity interface.

**Alternatives considered:**
- Inline the similarity function in `SmithWatermanAlignment`: Would couple fuzzy metric to alignment algorithm, preventing reuse
- Make `IAlignmentStrategy` generic over similarity: Over-engineering for a single-method interface

### D2: Character trigram Jaccard as the default similarity metric

The default `TrigramJaccardSimilarity` computes trigrams of `$word$` (word with start/end sentinels), then Jaccard coefficient = `|intersection| / |union|`. For words ≤ 4 characters, it uses bigrams instead of trigrams.

```
"quick" → {"$qu", "qui", "uic", "ick", "ck$"}     (5 trigrams)
"qu1ck" → {"$qu", "qu1", "u1c", "1ck", "ck$"}     (5 trigrams)
Jaccard = |{$qu, ck$}| / |{$qu, qui, uic, ick, ck$, qu1, u1c, 1ck}| = 2/8 = 0.25
```

**Rationale for Jaccard over edit distance:**
- Jaccard on n-grams is O(n + m) per pair (set intersection/union), vs O(n × m) for Levenshtein
- N-grams can be pre-computed and cached per word, making repeated comparisons cheap
- The same n-grams power the approximate index lookup (synergy with D3)
- For short words common in search queries (3-8 chars), Jaccard on padded trigrams is well-behaved

**The sentinel padding `$` is critical:** Without it, "quick" and "qu1ck" share zero trigrams (qui≠qu1, uic≠u1c, ick≠1ck). With padding, the shared prefix `$qu` and suffix `ck$` provide signal. This catches first-character and last-character errors which are the most common OCR mistakes.

**Alternatives considered:**
- Normalized Levenshtein (1 - editDist / maxLen): More precise for single substitutions, but O(n×m) per pair and no pre-computable index
- Jaro-Winkler: Designed for person names, biases toward common prefixes — less appropriate for general vocabulary
- Unpadded trigrams: Breaks for single-character edits (the common case)

### D3: Always-on n-gram positional index

The `IndexedDocument` builds a character n-gram positional index at construction time. This is not opt-in — it's always built, alongside the exact inverted index.

```
Document tokens: ["quick", "brown", "fox"] at positions [0, 1, 2]

Exact inverted index:
  "quick" → [0], "brown" → [1], "fox" → [2]

N-gram index (trigrams of $quick$, $brown$, $fox$):
  "$qu" → [0], "qui" → [0], "uic" → [0], "ick" → [0], "ck$" → [0]
  "$br" → [1], "bro" → [1], "row" → [1], "own" → [1], "wn$" → [1]
  "$fo" → [2], "fox" → [2], "ox$" → [2]
```

**Rationale for always-on:**
- Memory overhead is small: ~3-4× the number of tokens (each token contributes ~5 trigrams on average). For 20K tokens, that's ~80K dictionary entries — negligible (< 20 MB even with overhead)
- Keeps the API clean: no `enableFuzzy: true` at build time, no two code paths for document construction
- The index is only queried when fuzzy is enabled at search time; zero runtime cost when not used

**`GetApproximatePositions(word, threshold)`:**
1. Extract trigrams of `$word$`
2. For each trigram, collect position lists from the n-gram index
3. Group by position, count how many trigrams hit each position
4. For each candidate position, compute exact Jaccard similarity between query word and document token
5. Return positions where Jaccard ≥ threshold, sorted by similarity descending

**Alternatives considered:**
- BK-tree: Elegant O(log n) edit-distance queries, but construction cost is O(|vocab|²) in worst case and doesn't naturally support Jaccard (BK-tree requires a metric; Jaccard is a similarity, not a distance)
- Brute-force scan at query time: Works but O(|doc|) per fuzzy word — unacceptable for repeated queries
- Hybrid (bigram index for short words, trigram for long): Added complexity with marginal gain; the bigram fallback in the similarity function already handles short words

### D4: Anchor quality gating, not clustering weight

Fuzzy anchors carry no extra weight during clustering. The quality threshold is applied at anchor *creation* time: only anchors with Jaccard ≥ `fuzzyAnchorThreshold` (configurable, default 0.3) are created. Surviving anchors are treated identically to exact anchors for clustering.

```
FindAnchors():
  for each query word w:
    exact positions = doc.GetPositions(w)
    if exact positions exist:
      add (qi, p) as anchors     // exact, no quality tag needed
    else if fuzzy enabled:
      fuzzy positions = doc.GetApproximatePositions(w, fuzzyAnchorThreshold)
      add (qi, p) as anchors     // fuzzy, but pre-filtered by threshold
```

**Rationale:** A single low-confidence fuzzy anchor is weak, but multiple fuzzy anchors clustering in the same document region is a strong signal. If we penalized fuzzy anchors during clustering (e.g., increasing the cluster gap threshold for fuzzy-found positions), we would fragment those multi-anchor clusters. SW alignment naturally scores fuzzy-matched words lower, so the final `NormalizedScore` already reflects quality — no need to double-penalize.

**Alternatives considered:**
- Weighted clustering (fuzzy anchors expand regions less): Over-complicates the simple cluster algorithm; SW score already captures quality
- Separate "fuzzy confidence" field on anchors propagated through clustering: Unnecessary indirection — similarity is recomputed during alignment anyway

### D5: Similarity-weighted Smith-Waterman match scoring

The match score transitions from binary to continuous:

```
Before:  matchScore = (w1 == w2) ? H[i-1][j-1] + 1.0 : negInf
After:   sim = _wordSimilarity.Similarity(w1, w2)
         matchScore = (sim >= _similarityThreshold)
                      ? H[i-1][j-1] + sim * 1.0
                      : negInf
```

The `_similarityThreshold` (default 0.3) prevents SW from pairing completely unrelated words, which would create spurious low-scoring alignments. Below threshold, the pair is treated as a mismatch (negInf), same as today.

**Rationale for sim × 1.0 (not a separate penalty):**
- An exact match: sim = 1.0 → score contribution = 1.0 (same as today)
- A fuzzy match: sim = 0.6 → score contribution = 0.6 (partial credit)
- This naturally makes fuzzy spans score lower than exact spans, which matches user expectation
- The normalized score still works: `rawScore / queryLength` — a 5-word query where all words match at 0.6 gives 3.0/5 = 0.6

**Alternatives considered:**
- Fixed substitution penalty (e.g., -0.5 for any non-exact): "quick"↔"qu1ck" and "quick"↔"elephant" get the same penalty — too coarse
- Similarity as a separate reward term: Adds unnecessary complexity when sim × reward is mathematically equivalent

### D6: `MatchedPair` extended with `Similarity`

```
Before: public readonly record struct MatchedPair(int QueryIndex, int DocIndex);
After:  public readonly record struct MatchedPair(int QueryIndex, int DocIndex, double Similarity);
```

A value of 1.0 means exact match; < 1.0 means fuzzy match. This is a **source-compatible** change for most consumers — the record struct's generated `ToString()`, `Equals()`, and deconstruction change, but existing code that only reads `.QueryIndex` and `.DocIndex` continues to compile.

**Rationale:** Consumers of the match trace (e.g., highlighting UIs, debugging tools) need to know which matches were exact and which were fuzzy-approximated. Carrying the similarity score on the pair itself is more natural than a separate lookup table.

### D7: Search-time fuzzy control via `SearchOptions`

```csharp
public class SearchOptions
{
    public bool EnableFuzzyAnchors { get; set; } = false;
    public double FuzzyAnchorThreshold { get; set; } = 0.3;
}
```

The existing `Search(doc, query, topN, threshold)` signature gains an optional `SearchOptions? options = null` parameter (placed last). When `null` or `EnableFuzzyAnchors = false`, behavior is identical to today.

**Rationale for search-time control (not strategy-time):**
- Anchor finding and alignment are separate pipeline stages; fuzzy anchor discovery is not a property of the alignment strategy
- A user may want to run both exact and fuzzy searches against the same matcher instance
- `IWordSimilarity` lives on the strategy (it defines "how similar") while `EnableFuzzyAnchors` lives on the search options (it defines "whether to look for fuzzy anchors")

**Alternative considered:**
- All config on `IAlignmentStrategy`: Simpler but conflates two separate concerns; would require creating two strategy instances for exact vs fuzzy searches

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| **False positives**: Fuzzy anchors may pull in unrelated document regions, producing spurious results | Anchor similarity threshold (default 0.3) gates fuzzy anchor creation; SW similarity threshold independently gates alignment pairings; both defaults are configurable |
| **Performance regression**: N-gram index increases memory and build time | Always-on but small (~4× token count); build time measured and bounded; fuzzy search adds n-gram lookup + per-candidate Jaccard computation, but candidate count is typically low |
| **`MatchedPair` breaking change**: Adding `Similarity` changes the record struct's layout | Source-compatible for property access; binary breaking for serialization — acceptable in a minor version bump |
| **Trigram Jaccard blind spots**: Very short words (1-2 chars) produce few/zero trigrams, making fuzzy matching unreliable for them | These words are already handled by exact matching; fuzzy operates on ≥3 char words where trigrams are meaningful. The bigram fallback for ≤4 char words helps |
| **Exploding candidate regions**: If a common word like "the" has fuzzy matches throughout the document | Common words have many *exact* anchors already — fuzzy fallback is only triggered when exact lookup returns *empty*. "the" will always have exact matches, so fuzzy fallback never activates for it |

## Open Questions

1. **Should fuzzy anchors also be created when exact anchors *do* exist?** Currently, the design only creates fuzzy anchors as a fallback when exact lookup returns empty. There's an argument for always creating both: a fuzzy anchor for "qu1ck" at the position of "quick" might improve the alignment score even when "quick" also has exact matches for other query words. However, this doubles anchor counts for all exact-matched words. Decision deferred — start with fallback-only, measure, and revisit.

2. **What's the right default for `FuzzyAnchorThreshold`?** 0.3 is a starting point based on trigram Jaccard behavior: 1-char substitution in a 5-char word gives ~0.25 (2/8). We may need to tune this after real-world testing.

3. **Should `SearchOptions` also carry the alignment similarity threshold?** Currently it's on the strategy constructor. There's a case for putting it on `SearchOptions` too (per-query control). For now, keep it on the strategy since it's an alignment concern, not a search concern.
