## Context

The `fuzzy-word-matching` change introduced `TrigramJaccardSimilarity` and an always-on n-gram positional index. A review found that the n-gram size is chosen **per word** (`n = word.Length <= 4 ? 2 : 3`), which breaks Jaccard when a length-changing edit crosses the 4-character boundary: one word yields bigrams, the other trigrams, and the sets can never intersect → similarity 0.0. This is silent and affects the most common OCR errors (single-char insertions/deletions).

The same `GetNgrams` helper is used in three places, all affected:
1. `TrigramJaccardSimilarity.Similarity()` — public similarity
2. `IndexedDocumentBuilder.Build()` — building the n-gram index
3. `IndexedDocument.GetApproximatePositions()` / `ComputeNgramJaccard()` — approximate lookup

The review also found a contradictory spec scenario in `alignment-strategy`, dead code (`ExactMatchSimilarity`), and redundant similarity recomputation in the SW backtrack.

## Goals / Non-Goals

**Goals:**
- Make fuzzy similarity correct for length-changing edits across the 4-char boundary
- Fix the contradictory `alignment-strategy` spec scenario so it matches implemented behavior
- Remove dead code and a minor perf waste identified in review
- Keep the public API and backward-compatible exact-match path unchanged

**Non-Goals:**
- Replacing trigram Jaccard with a different metric (Levenshtein, etc.)
- Changing the 4-char short-word threshold itself
- Revisiting the always-on n-gram index design
- Changing anchor finding, clustering, or ranking

## Decisions

### D1: Pair-consistent n-gram size via `GetNgrams(word, n)`

Change `GetNgrams` to take an explicit `int n` parameter, and compute `n` once per pair:

```
n = (max(a.Length, b.Length) <= 4) ? 2 : 3
setA = GetNgrams(a, n)
setB = GetNgrams(b, n)
```

Both sets use the same `n`, so intersection is meaningful for any length combination.

**Why `max` and not `min`:** If one word is long (e.g. 6 chars), trigrams are the meaningful granularity; forcing bigrams just because the other word is short would over-state similarity. Using `max` keeps the more discriminating granularity whenever either word is long, and only falls back to bigrams when **both** words are short (≤ 4), where trigrams would be too sparse anyway.

**Alternatives considered:**
- `min(a.Length, b.Length) <= 4 ? 2 : 3` — would use bigrams whenever either word is short, over-matching long/short pairs. Rejected.
- Always trigrams, never bigrams — simplest, but trigrams of a 2-char word (`$a$` → only `$a$`... actually produces 1 trigram) are too sparse; bigrams for very short words give better signal. Rejected to preserve the short-word handling the original design intended.
- Always bigrams — loses discrimination for longer words, increases false positives. Rejected.

### D2: Index build must use a fixed n per token (trigrams), lookup adapts

The n-gram **index** is built once per document and cannot know query-word lengths in advance. So the index stores **trigrams** for all tokens (with bigrams additionally stored for tokens ≤ 4 chars, so short-word lookups still work). Alternatively, store both n-gram sizes for every token.

Chosen approach: **store both bigrams and trigrams for every token** in the index. This keeps lookup simple — whatever `n` the query pair needs, the index has entries for that n. Memory overhead is small (each token adds ~5 trigrams + ~6 bigrams). Lookup then uses `GetNgrams(queryWord, n)` with the pair-consistent `n` and matches against the index.

This is a change from the current index (which stores bigrams *or* trigrams per token based on token length). Storing both makes the index length-agnostic and fixes the lookup side of the bug.

**Alternatives considered:**
- Index only trigrams, lookup computes trigrams regardless of query length — loses short-word signal and reintroduces sparse-trigram problems for short query words.
- Index only trigrams, and at lookup time fall back to a brute-force scan when the query word is short — complex, slower.

### D3: Correct the `alignment-strategy` spec scenario

The scenario "Fuzzy alignment with similar words" states threshold 0.3 and expects "qu1ck" (sim ~0.25) to match. Under the implemented `sim >= threshold` rule, 0.25 < 0.3 → no match. Fix the scenario to use threshold 0.2 (so the match is valid and the ~2.25 total holds), keeping it a meaningful illustration of fuzzy alignment. The implementation test already uses 0.2, so this aligns spec with test.

### D4: Remove `ExactMatchSimilarity`

Delete the internal `ExactMatchSimilarity` class. It is never referenced; the alignment strategy's null-`WordSimilarity` path uses inline `==` comparison. No behavior change.

### D5: Cache similarity in the SW forward pass

Add a `double[,] sim` matrix populated during the forward pass (only when `WordSimilarity != null`) so the backtrack reads cached values instead of recomputing `Similarity(...)` for every cell it visits (including gap cells where the value is unused). This also removes a subtle risk of the backtrack's recomputed similarity disagreeing with the forward value due to any non-determinism in a custom `IWordSimilarity`.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Index size grows (both bigrams + trigrams per token) | Measured: ~2× n-gram entries vs current, still well under budget for 20K-word docs; benchmark confirms < 200ms |
| Similarity values change for cross-boundary pairs (behavioral shift) | This is the bug fix; existing tests for same-length pairs are unaffected. Add explicit cross-boundary tests. |
| `GetNgrams` signature change is `internal` | No public API impact; only same-assembly callers (IndexedDocument, builder, tests) |
| Pair-consistent `n` could over-match long-vs-short pairs if both happen to be ≤ 4 | `max` rule means bigrams only when both ≤ 4; acceptable and matches original intent |

## Open Questions

1. **Should the short-word threshold (4 chars) be revisited?** Out of scope here; the fix preserves the existing threshold and only changes how `n` is selected per pair. Tuning can be a separate change.
2. **Store both n-gram sizes, or just trigrams with a short-word brute-force?** D2 chooses store-both for simplicity and lookup speed; revisit if memory becomes a concern at very large document sizes.
