## ADDED Requirements

### Requirement: Find top-N matching spans

The system SHALL accept a query string, an IndexedDocument, and parameters (topN, threshold) and return up to topN non-overlapping spans ranked by `NormalizedScore` (gap-aware), where each returned span has `NormalizedScore >= threshold`.

A span SHALL include:
- `StartIndex` and `EndIndex`: absolute word positions in the document, half-open interval `[StartIndex, EndIndex)` (`EndIndex` exclusive)
- `NormalizedScore` (double, 0.0–1.0): gap-aware quality score = `rawSmithWatermanScore / queryWordCount` (see design D5)
- `Coverage` (double, 0.0–1.0): fraction of query words matched = `matchedQueryWordCount / queryWordCount`
- `OriginalText`: original markdown text extracted from the document for `[StartIndex, EndIndex)`
- `MatchedPairs`: alignment trace — list of `(queryWordIndex, docWordIndex)` pairs showing which words matched

#### Scenario: Exact match

- **WHEN** searching for `"quick brown fox"` in a document containing `"the quick brown fox jumps"`
- **THEN** exactly one span is returned starting at "quick" and ending at "fox" with `NormalizedScore` 1.0 and `Coverage` 1.0

#### Scenario: Match with gaps in document

- **WHEN** searching for `"quick brown fox jumps over lazy dog"` in a document containing `"quick brown fox jumps over the lazy dog"`
- **THEN** one span is returned covering the matched region
- **AND** `Coverage` may be < 1.0 with default gap penalties (g=-2, e=-1), because the 1-word gap for "the" costs -3 (more than a match's +1), so SW may prefer a shorter gap-free sub-alignment (e.g. "quick brown fox jumps over" = 5/7 ≈ 0.71) over the full 7-word alignment with gap (score 4)
- **AND** `NormalizedScore` is < 1.0
- **NOTE**: With milder gap penalties (where a single gap costs less than a match), the full alignment wins and `Coverage` approaches 1.0

#### Scenario: Match with missing query words

- **WHEN** searching for `"quick brown fox leaps over lazy dog"` (where "leaps" is not in the document)
- **THEN** a span is returned matching the available words (if any sub-alignment scores positively)
- **AND** `Coverage` depends on the SW local optimum under the configured gap penalties: with default penalties (g=-2, e=-1), the best alignment may be a tight gap-free cluster (e.g. "quick brown fox" → Coverage 3/7 ≈ 0.43) rather than spanning the gap to reach all 6 available words (6/7 ≈ 0.86)
- **AND** `NormalizedScore` is lower than `Coverage` when gaps are traversed; with milder penalties, `Coverage` approaches 6/7

#### Scenario: No match above threshold

- **WHEN** searching for `"elephant in the room"` in a document with no matching words
- **THEN** an empty result list is returned

#### Scenario: Top-3 results

- **WHEN** searching for `"quick brown"` in a document where those words appear in 5 separate locations
- **THEN** at most 3 non-overlapping spans are returned, ranked by score

#### Scenario: Overlapping spans deduplicated

- **WHEN** multiple candidate alignments overlap by more than 50%
- **THEN** only the highest-scoring one is included in results

### Requirement: Overlap metric and tie-breaking for ranking

Deduplication overlap between two spans `A = [A.StartIndex, A.EndIndex)` and `B = [B.StartIndex, B.EndIndex)` is measured as the fraction of the **smaller** span covered by the intersection: `overlap = |A ∩ B| / min(|A|, |B|)`, where `|S| = S.EndIndex - S.StartIndex` (word count). If `overlap > 0.5`, the lower-scoring span is dropped; the higher-scoring span is kept.

When two candidate spans have equal `NormalizedScore`, the tie is broken by descending `Coverage`, then by earlier `StartIndex` (smaller word position first). This ordering is deterministic.

#### Scenario: Overlap measured against smaller span

- **WHEN** span A covers word positions `[10, 20)` (10 words) and span B covers `[15, 40)` (25 words)
- **AND** their intersection is `[15, 20)` (5 words)
- **THEN** the overlap is `5 / min(10, 25) = 5/10 = 0.5`, which is not `> 0.5`, so both spans are kept

#### Scenario: Equal-score tie-break

- **WHEN** two non-overlapping candidate spans have identical `NormalizedScore`
- **THEN** the one with higher `Coverage` ranks first
- **AND** if `Coverage` is also equal, the one with the smaller `StartIndex` ranks first

### Requirement: Tokenize query string for matching

The system SHALL tokenize the query string using exactly the same `MarkdownTokenizer` and tokenization rules as document indexing (see document-indexing spec: "Tokenize markdown into word tokens" — NFC normalization, then word characters = Unicode letters, digits, apostrophes, hyphens/dashes, and digit-internal periods). This ensures token consistency between query and document for exact word matching.

The query SHALL be tokenized in the **same case-sensitivity mode** as the target `IndexedDocument`. The `IndexedDocument` SHALL expose its `CaseSensitive` mode so that `SpanMatcher.Search` can tokenize the query consistently; mismatched modes would otherwise silently break matching.

#### Scenario: Query tokenized same as document

- **WHEN** searching for `"The **quick** brown fox."` (a markdown-like query string) against a case-insensitive document
- **THEN** the query is tokenized as `["the", "quick", "brown", "fox"]`
- **AND** these tokens are matched against the document index

#### Scenario: Query case-sensitivity follows document

- **WHEN** searching for `"Quick Brown"` against a case-sensitive `IndexedDocument`
- **THEN** the query is tokenized case-sensitively as `["Quick", "Brown"]`
- **AND** a document built case-insensitively would instead tokenize the same query as `["quick", "brown"]`

#### Scenario: Empty query

- **WHEN** searching for an empty string `""`
- **THEN** an empty result list is returned with no error

#### Scenario: Repeated words in query

- **WHEN** searching for `"the quick brown fox and the lazy dog"` where "the" appears twice in the query
- **THEN** each occurrence of "the" in the query is treated as a distinct token position
- **AND** the alignment correctly maps each query position to a matching document position (or a gap) independently

#### Scenario: No query words found in document

- **WHEN** searching for `"xyzzy plugh"` where neither word exists in the document index
- **THEN** an empty result list is returned (zero anchors → zero candidate regions → zero results)

### Requirement: Score reflects gap penalties

The system SHALL assign higher scores to spans where matched words appear in tight clusters versus spans where matched words are scattered with large gaps, even when both spans contain the same number of matched words.

#### Scenario: Tight cluster scores higher than scattered

- **WHEN** two candidate spans both match 5 query words
- **AND** Span A has matched words with gaps of 0-1 words between them (tight)
- **AND** Span B has matched words with gaps of 5-10 words between them (scattered)
- **THEN** Span A receives a higher score than Span B

### Requirement: Preserve word order in matching

The system SHALL only consider matches where query word order is preserved in the document span. Matches where query words appear in a different order SHALL NOT contribute to the alignment score.

#### Scenario: Out-of-order words not matched

- **WHEN** searching for `"brown quick"` in a document containing `"quick brown fox"`
- **THEN** the alignment score reflects that "brown" appears after "quick" in the document (order violation)
- **AND** the score is lower than if the words appeared in the correct order

### Requirement: Reuse index across multiple queries

The system SHALL allow searching the same IndexedDocument with multiple different queries without rebuilding the index. The IndexedDocument SHALL be immutable after construction and thread-safe for concurrent read access.

#### Scenario: Multiple queries against one document

- **WHEN** an IndexedDocument is built once from a markdown string
- **AND** three different queries are executed against it
- **THEN** each query returns results independently
- **AND** the index is never rebuilt

### Requirement: Threshold filtering

The system SHALL filter results by a minimum `NormalizedScore` threshold. Only spans with `NormalizedScore >= threshold` are included in results. A threshold of 0.0 returns all candidate spans (up to `topN`); a threshold of 1.0 returns only exact matches (contiguous, all query words). Results are still capped at `topN` regardless of threshold.

#### Scenario: Threshold filters low-scoring matches

- **WHEN** searching with threshold = 0.8
- **AND** there are candidate spans with scores [0.95, 0.75, 0.60]
- **THEN** only the span with score 0.95 is returned

### Requirement: Argument validation

`SpanMatcher.Search` SHALL validate its arguments and throw on invalid input rather than returning misleading results:
- `null` document → `ArgumentNullException` (parameter name `doc`)
- `null` query → `ArgumentNullException` (parameter name `query`)
- `topN <= 0` → `ArgumentOutOfRangeException` (parameter name `topN`)
- `threshold` outside `[0.0, 1.0]` → `ArgumentOutOfRangeException` (parameter name `threshold`)

An empty (`""`) document or query is valid and returns an empty result list (no throw).

#### Scenario: Null arguments throw

- **WHEN** `Search` is called with a `null` document or a `null` query
- **THEN** an `ArgumentNullException` is thrown

#### Scenario: Invalid topN or threshold throw

- **WHEN** `Search` is called with `topN = 0` (or negative) or `threshold = 1.5` (or negative)
- **THEN** an `ArgumentOutOfRangeException` is thrown

### Requirement: SpanMatcher concurrency and reuse

A `SpanMatcher` instance SHALL be stateless with respect to `Search` calls and safe for concurrent invocation across threads. A single instance MAY be reused against multiple `IndexedDocument` instances. The `IAlignmentStrategy` it holds is read-only during `Search`; custom strategies that carry mutable state MUST document their own thread-safety guarantees.

#### Scenario: Concurrent searches on one SpanMatcher

- **WHEN** the same `SpanMatcher` instance is used to run several `Search` calls concurrently against the same or different `IndexedDocument` instances
- **THEN** each call returns correct results without interfering with the others

### Requirement: Performance at scale

For a reference document of ~20,000 words (~50 OCR pages) and a 50-word query, returning the top-3 spans SHALL complete in under 200 ms on conventional modern hardware (single-threaded, release build). The seed-and-cluster pipeline (design D3) is what makes this feasible; a degenerate document that defeats clustering (e.g., every query word occurring hundreds of times throughout) may exceed this budget and that is acceptable, but the common case SHALL meet it. This requirement is verified by a benchmark/integration test.

#### Scenario: Typical document returns within budget

- **WHEN** searching a ~20,000-word document with a 50-word query for top-3 results
- **THEN** the search completes in under 200 ms on reference hardware in a release build
