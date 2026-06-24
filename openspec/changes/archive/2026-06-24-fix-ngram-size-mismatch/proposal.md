## Why

The fuzzy matching feature shipped in `fuzzy-word-matching` has a correctness defect: `TrigramJaccardSimilarity.GetNgrams` selects the n-gram size **per word** (bigrams for ≤ 4 chars, trigrams otherwise). When a typo or OCR error changes a word's length across the 4-character boundary (e.g., `word` → `words`, `quick` → `quik`), the two words produce n-grams of **different sizes**, whose intersection is always empty — yielding similarity 0.0 and making the word invisible to fuzzy matching. This breaks exactly the insertion/deletion cases the feature was built to tolerate. A review also surfaced a contradictory spec scenario, dead code, and minor perf waste that should be cleaned up in the same pass.

## What Changes

- **Fix n-gram size selection (the headline bug):** n-gram size is determined **consistently for the pair**, not per word. Both words use the same `n`, so length-changing edits across the 4-char boundary no longer produce zero similarity.
- **Update `word-similarity` spec:** the "Short word uses bigrams" scenario language ("for words ≤ 4 chars") is replaced with pair-consistent language; add scenarios covering length-changing edits across the boundary.
- **Update `alignment-strategy` spec:** the "Fuzzy alignment with similar words" scenario is corrected — its stated threshold (0.3) and expected outcome (~0.25 contribution, total ~2.25) are contradictory under the implemented `sim >= threshold` rule; fix the threshold or the expectation so they agree.
- **Remove dead code:** delete the unused `ExactMatchSimilarity` internal class (the alignment strategy uses an inline exact-match path when `WordSimilarity` is null; this class is never instantiated).
- **Minor perf:** cache similarity values during the Smith-Waterman forward pass so the backtrack does not recompute `Similarity(...)` for every cell (including non-match cells).

## Capabilities

### New Capabilities

<!-- No new capabilities — this is a defect-fix to existing ones. -->

### Modified Capabilities

- `word-similarity`: `TrigramJaccardSimilarity` n-gram size selection changes from per-word to pair-consistent. Scenarios updated to reflect this and to cover cross-boundary length changes.
- `alignment-strategy`: the "Fuzzy alignment with similar words" scenario is corrected to remove the threshold/expectation contradiction. Backtrack similarity caching is an implementation detail (no spec change for that).

## Impact

- **Code:** `TrigramJaccardSimilarity.cs` (GetNgrams signature/usage), `IndexedDocument.cs` (uses GetNgrams for n-gram index build and approximate lookup), `SmithWatermanAlignment.cs` (remove dead class reference if any, add similarity cache in backtrack)
- **Specs:** `word-similarity/spec.md`, `alignment-strategy/spec.md`
- **No breaking API changes:** `GetNgrams` is `internal`; its signature may change without affecting public consumers. Similarity *values* change for cross-boundary pairs (a bug fix, not a contract break).
- **Tests:** update/add tests for cross-boundary pairs; update the alignment fuzzy test to match the corrected spec scenario
