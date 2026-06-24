## 1. Word Similarity Foundation

- [x] 1.1 Create `IWordSimilarity` interface with `double Similarity(string a, string b)` method in `Models/` or new `Similarity/` namespace
- [x] 1.2 Implement `TrigramJaccardSimilarity` class: compute padded trigrams (`$word$`), Jaccard coefficient, bigram fallback for words ≤ 4 chars
- [x] 1.3 Add `ExactMatchSimilarity` internal helper that returns 1.0 when `a == b`, 0.0 otherwise (used as fallback when no `IWordSimilarity` is provided)
- [x] 1.4 Write unit tests for `TrigramJaccardSimilarity`: exact match (1.0), single-char substitution (~0.25), completely different (0.0), empty strings, symmetry, bigram fallback for short words

## 2. MatchedPair Model Extension

- [x] 2.1 Add `double Similarity` parameter to `MatchedPair` record struct (third positional parameter, default `1.0`)
- [x] 2.2 Update all existing `new MatchedPair(i, j)` calls to `new MatchedPair(i, j, 1.0)` — check `SmithWatermanAlignment` backtrack and all test files
- [x] 2.3 Verify all existing tests still pass after the change

## 3. Smith-Waterman Alignment Extension

- [x] 3.1 Add constructor parameters to `SmithWatermanAlignment`: `IWordSimilarity? wordSimilarity = null`, `double similarityThreshold = 0.3`
- [x] 3.2 Add validation: `similarityThreshold` must be in [0.0, 1.0]
- [x] 3.3 Modify DP match scoring: when `_wordSimilarity` is non-null, compute `sim = _wordSimilarity.Similarity(w1, w2)`, use `sim * MatchReward` if `sim >= _similarityThreshold`, else `negInf`
- [x] 3.4 Update backtrack to carry similarity into `MatchedPair`: when matching, record the computed similarity
- [x] 3.5 Ensure null `wordSimilarity` path is unchanged (binary exact match, backward compat)
- [x] 3.6 Write/update unit tests: fuzzy alignment with similar words, below-threshold treated as mismatch, backward compatibility with null similarity

## 4. N-Gram Index in IndexedDocument

- [x] 4.1 Add `Dictionary<string, List<int>> _ngramIndex` field to `IndexedDocument`
- [x] 4.2 Implement `BuildNgramIndex(IReadOnlyList<Token> tokens)` static method: for each token, compute padded trigrams (bigrams for ≤ 4 chars), add position to each n-gram's list
- [x] 4.3 Implement `internal GetApproximatePositions(string word, double threshold)` method: compute query n-grams, gather candidate positions, compute Jaccard per candidate, filter by threshold, return sorted by similarity
- [x] 4.4 Update `IndexedDocument` internal constructor to accept n-gram index dictionary
- [x] 4.5 Update `IndexedDocumentBuilder.Build()` to build n-gram index alongside exact index
- [x] 4.6 Write unit tests: approximate lookup finds similar word, respects threshold, empty result for no overlap, multiple candidates ranked by similarity, duplicate tokens at different positions

## 5. SearchOptions and SpanMatcher Extension

- [x] 5.1 Create `SearchOptions` class with `EnableFuzzyAnchors` (bool, default false) and `FuzzyAnchorThreshold` (double, default 0.3, validated to [0, 1])
- [x] 5.2 Add optional `SearchOptions? options = null` parameter to `SpanMatcher.Search()` (after `threshold`, before end)
- [x] 5.3 Validate `options.FuzzyAnchorThreshold` in [0, 1] if options is non-null
- [x] 5.4 Modify `FindAnchors()`: accept `SearchOptions`, for each query word with zero exact matches, call `GetApproximatePositions` if fuzzy enabled and threshold > 0
- [x] 5.5 Update `SpanMatcher` constructor to optionally accept `IWordSimilarity` and pass to alignment strategy (or delegate to strategy's own config)

## 6. Integration Tests

- [x] 6.1 Test exact-match search with fuzzy disabled (options null or EnableFuzzyAnchors=false) returns same results as before
- [x] 6.2 Test fuzzy search: "qu1ck brown fox" against document with "quick brown fox" returns span with NormalizedScore < 1.0
- [x] 6.3 Test fuzzy search with MatchedPair similarity values: verify exact matches have 1.0, fuzzy matches have < 1.0
- [x] 6.4 Test fuzzy threshold filtering: query word with no matches above FuzzyAnchorThreshold returns empty
- [x] 6.5 Test all-fuzzy query: every query word requires fuzzy anchor discovery
- [x] 6.6 Test fuzzy anchors don't interfere with exact anchors when both are present
- [x] 6.7 Test backward compat: all existing SpanMatcherTests pass unmodified (no SearchOptions passed)

## 7. Edge Cases and Validation

- [x] 7.1 Test empty query with fuzzy enabled returns empty list
- [x] 7.2 Test document with no n-gram matches (e.g., query in different character set) returns empty
- [x] 7.3 Test very short query words (1-2 chars) with fuzzy — they should use bigram fallback
- [x] 7.4 Test concurrent searches with fuzzy enabled (SpanMatcher reentrancy)
- [x] 7.5 Test `SearchOptions` invalid thresholds throw `ArgumentOutOfRangeException`
- [x] 7.6 Verify `NormalizedScore` with fuzzy matches: a 5-word query where all words match fuzzily at ~0.6 gives score ~3.0/5 = 0.6

## 8. Final Verification

- [x] 8.1 Run full existing test suite — all tests pass
- [x] 8.2 Run benchmark test for 20K-word doc with 50-word fuzzy query — verify stays under 200ms
- [x] 8.3 Verify no new NuGet dependencies introduced
- [x] 8.4 Build Release configuration — no warnings
