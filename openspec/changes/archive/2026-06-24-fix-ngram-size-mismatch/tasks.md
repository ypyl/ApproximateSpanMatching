## 1. Fix n-gram size selection (pair-consistent)

- [x] 1.1 Change `TrigramJaccardSimilarity.GetNgrams` to accept an explicit `int n` parameter: `internal static HashSet<string> GetNgrams(string word, int n)`
- [x] 1.2 Update `TrigramJaccardSimilarity.Similarity` to compute `n = (max(a.Length, b.Length) <= 4) ? 2 : 3` once per pair and pass it to both `GetNgrams` calls
- [x] 1.3 Update `IndexedDocumentBuilder.Build()` to store **both** bigrams and trigrams for every token in the n-gram index (so lookups for either `n` succeed regardless of token length)
- [x] 1.4 Update `IndexedDocument.GetApproximatePositions` to compute pair-consistent `n` for the (query word, candidate doc word) pair and use `GetNgrams(queryWord, n)` for lookup; refine candidates with `ComputeNgramJaccard` using the same `n`
- [x] 1.5 Update `ComputeNgramJaccard` to use pair-consistent `n`

## 2. Update and add tests for the n-gram fix

- [x] 2.1 Add tests: `Similarity("word", "words")` (insertion across boundary) returns > 0.0, not 0.0
- [x] 2.2 Add tests: `Similarity("quick", "quik")` (deletion across boundary) returns > 0.0, not 0.0
- [x] 2.3 Add test: `Similarity("cats", "catch")` (4 vs 5) returns > 0.0
- [x] 2.4 Update existing `BigramFallback_ShortWord` and `BigramFallback_TotallyDifferent` tests to reflect pair-consistent `n` (both ≤ 4 → bigrams)
- [x] 2.5 Add n-gram index test: approximate lookup finds `quick` for query `quik` (deletion across boundary) at a low threshold
- [x] 2.6 Add n-gram index test: approximate lookup finds `words` for query `word` (insertion across boundary)

## 3. Fix alignment-strategy spec inconsistency

- [x] 3.1 Update `SmithWatermanAlignmentTests.FuzzyAlignment_SimilarWords` (and any other test referencing the scenario) to use threshold 0.2 explicitly, matching the corrected spec scenario (already uses 0.2 — verify and align assertion comments)
- [x] 3.2 Verify the fuzzy alignment test asserts the ~2.25 total score expectation holds with threshold 0.2

## 4. Remove dead code

- [x] 4.1 Delete the `ExactMatchSimilarity` internal class from `TrigramJaccardSimilarity.cs`
- [x] 4.2 Grep to confirm no references remain; build clean

## 5. Cache similarity in SW backtrack

- [x] 5.1 Add a `double[,] sim` matrix in `SmithWatermanAlignment.Align`, populated during the forward pass only when `WordSimilarity != null`
- [x] 5.2 Update the backtrack to read `sim[ci, cj]` instead of recomputing `WordSimilarity.Similarity(...)` at the top of each iteration
- [x] 5.3 Keep the null-`WordSimilarity` path unchanged (no sim matrix allocated)
- [x] 5.4 Verify all existing alignment tests still pass

## 6. Final verification

- [x] 6.1 Run full test suite — all tests pass
- [x] 6.2 Run benchmark test — verify still under 200ms with both bigrams+trigrams indexed
- [x] 6.3 Build Release configuration — no warnings
- [x] 6.4 Verify no new NuGet dependencies
