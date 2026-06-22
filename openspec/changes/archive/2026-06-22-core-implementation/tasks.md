## 1. Project Setup

- [x] 1.1 Create .NET solution (`ApproximateSpanMatching.sln`) and class library project (`ApproximateSpanMatching.csproj`) targeting .NET 10
- [x] 1.2 Configure NuGet package metadata: package ID `ApproximateSpanMatching`, description, tags (approximate-matching, passage-retrieval, text-search, span-matching), LICENSE file, package README (`README.md` mapped via `<PackageReadmeFile>`), repository URL + source link (`Microsoft.SourceLink.GitHub`), and deterministic/sourcetable build (`ContinuousIntegrationBuild`, `Deterministic`)
- [x] 1.3 Create project directory structure: `src/ApproximateSpanMatching/` with folders for Models, Indexing, Alignment, Matching
- [x] 1.4 Create test project `tests/ApproximateSpanMatching.Tests/ApproximateSpanMatching.Tests.csproj` targeting .NET 10, referencing the main library, with xUnit test framework; add `InternalsVisibleTo("ApproximateSpanMatching.Tests")` to the main library so tests can exercise the internal inverted-index `GetPositions(word)` lookup

## 2. Data Models

- [x] 2.1 Implement `Token` record/class with properties: `Text` (string), `StartChar` (int, inclusive), `EndChar` (int, exclusive — index just past last char); offsets are UTF-16 code units into the stored NFC-normalized string
- [x] 2.2 Implement `IndexedDocument` class with: `Tokens` (IReadOnlyList\<Token\>), `OriginalText` (string — the NFC-normalized markdown), `CaseSensitive` (bool, exposed for query tokenization consistency), `GetSpan(startWordIndex, endWordIndex)` public method for original text extraction using half-open interval `[start, end)` (`end` exclusive); inverted index is internal (accessed via `internal GetPositions(word)`) — only `SpanMatcher` needs it; handle empty-markdown construction (zero tokens, empty index)
- [x] 2.3 Implement `AlignmentResult` class with: `Score` (double), `MatchedPairs` (IReadOnlyList\<MatchedPair\>), `SpanStart` (int), `SpanEnd` (int)
- [x] 2.4 Implement `MatchedPair` record/struct with: `QueryIndex` (int), `DocIndex` (int)
- [x] 2.5 Implement `SpanMatch` result class with: `StartIndex` (int) and `EndIndex` (int) as absolute document word positions in half-open interval `[StartIndex, EndIndex)`, `NormalizedScore` (double, gap-aware = rawSW/queryWordCount), `Coverage` (double, = matchedQueryWordCount/queryWordCount), `OriginalText` (string), `MatchedPairs` (IReadOnlyList\<MatchedPair\>)

## 3. Document Indexing

- [x] 3.1 Implement `MarkdownTokenizer` with `Tokenize(string markdown, bool caseSensitive = false)`: first NFC-normalize the input (`string.Normalize(NormalizationForm.FormC)`), then extract word tokens via greedy leftmost match per the spec's token definition (word chars = Unicode letters L, decimal digits Nd, apostrophes U+0027/U+2019, hyphens/dashes U+002D and U+2010–U+2015, plus a single U+002E only between digits; soft hyphen U+00AD and all markdown/punctuation/symbols are delimiters); throw `ArgumentNullException` on null input; return empty list on empty string
- [x] 3.2 Implement `IndexedDocumentBuilder` that takes tokens and builds the inverted word-position index (Dictionary\<string, List\<int\>\> mapping each unique token to sorted position list)
- [x] 3.3 Add static factory method `IndexedDocument.FromMarkdown(string markdown, bool caseSensitive = false)` that tokenizes and builds the index in one call
- [x] 3.4 Ensure IndexedDocument is immutable after construction and thread-safe for concurrent reads

## 4. Alignment Strategy

- [x] 4.1 Define `IAlignmentStrategy` interface with method `AlignmentResult Align(string[] queryTokens, string[] docRegionTokens, int docStartIndex)`
- [x] 4.2 Implement `SmithWatermanAlignment` class: configurable `GapOpenPenalty` and `GapExtendPenalty` with defaults (-2.0, -1.0), building DP matrix with affine gap penalties and backtracking to produce matched pairs
- [x] 4.3 Handle edge cases: empty query or doc region (return empty result with score 0), query word not matching anything in region (gap skip), score never going below 0 (local alignment reset)

## 5. Span Matching Core

- [x] 5.1 Implement `SpanMatcher` class accepting optional `IAlignmentStrategy` (defaults to `SmithWatermanAlignment`)
- [x] 5.2 Implement anchor finding: tokenize the query string using the same `MarkdownTokenizer` in the **same `CaseSensitive` mode as the target `IndexedDocument`** (read `doc.CaseSensitive`), then for each query token look up positions from the inverted index via `GetPositions`, producing (queryPos, docPos) pairs; handle empty queries and zero-anchor cases (return empty results)
- [x] 5.3 Implement clustering: sort anchor pairs by docPos, group into candidate regions where gap between consecutive doc positions ≤ threshold (default: query length × 2), pad each region by ±query length
- [x] 5.4 Implement alignment invocation: for each candidate region, extract doc tokens slice and call `IAlignmentStrategy.Align(queryTokens, regionTokens, regionStartIndex)`
- [x] 5.5 Implement scoring: compute `Coverage = matchedQueryWordCount / queryWordCount` and `NormalizedScore = rawSmithWatermanScore / queryWordCount`, both clamped to [0, 1]; thresholding and ranking use `NormalizedScore`
- [x] 5.6 Implement ranking with deduplication: sort alignments by `NormalizedScore` descending, tie-break by `Coverage` descending then `StartIndex` ascending (deterministic); drop alignments with `NormalizedScore < threshold`; deduplicate by keeping a span only if its overlap with every already-kept span is ≤ 0.5, where `overlap = |A ∩ B| / min(|A|, |B|)` over half-open word-index intervals; take top N
- [x] 5.7 Implement `Search(IndexedDocument doc, string query, int topN = 3, double threshold = 0.0)` public API method that runs the full pipeline and returns `List<SpanMatch>`; validate arguments per spec (null `doc`/`query` → `ArgumentNullException`; `topN <= 0` → `ArgumentOutOfRangeException`; `threshold` outside `[0,1]` → `ArgumentOutOfRangeException`); ensure `SpanMatcher` is stateless and safe for concurrent `Search` calls

## 6. Integration and Polish

- [x] 6.1 Write unit tests for `MarkdownTokenizer` covering all spec scenarios: basic, hyphenated, contractions, numbers, empty, case sensitivity, markdown structures/URLs (`` `code` ``, `[t](url)`, `https://example.com`), soft hyphen as delimiter, and NFC equivalence (e.g. precomposed vs decomposed é yield the same token)
- [x] 6.2 Write unit tests for `IndexedDocument` covering index construction, internal position lookup (via `InternalsVisibleTo`), span extraction (half-open `[1,4)`), and empty-document construction
- [x] 6.3 Write unit tests for `SmithWatermanAlignment` covering exact match, gap penalties, scattered vs tight clusters, missing query words, empty inputs (query/region), no-positive-alignment reset (score 0, empty pairs), and null-argument throws
- [x] 6.4 Write unit tests for `SpanMatcher.Search()` covering exact match, match with gaps, match with missing query words, threshold filtering, top-N, deduplication (including the smaller-span overlap metric), tie-breaking (equal score → higher Coverage → earlier StartIndex), multi-query reuse, query case-sensitivity following document, empty document/query, null/invalid-argument throws, and concurrent `Search` on one `SpanMatcher` instance
- [x] 6.5 Add a benchmark/integration test asserting the performance budget: a ~20,000-word synthetic document with a 50-word query returns top-3 in under 200 ms (release build, single-threaded); document the degenerate-case carve-out
- [x] 6.6 Add XML documentation comments to all public types and methods
- [x] 6.7 Verify all spec scenarios pass end-to-end
