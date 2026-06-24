## MODIFIED Requirements

### Requirement: Find top-N matching spans

The system SHALL accept a query string, an IndexedDocument, and parameters (topN, threshold) and return up to topN non-overlapping spans ranked by `NormalizedScore` (gap-aware), where each returned span has `NormalizedScore >= threshold`.

A span SHALL include:
- `StartIndex` and `EndIndex`: absolute word positions in the document, half-open interval `[StartIndex, EndIndex)` (`EndIndex` exclusive)
- `NormalizedScore` (double, 0.0–1.0): gap-aware quality score = `rawSmithWatermanScore / queryWordCount` (see design D5)
- `Coverage` (double, 0.0–1.0): fraction of query words matched = `matchedQueryWordCount / queryWordCount`
- `OriginalText`: original text extracted from the document for `[StartIndex, EndIndex)`
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

### Requirement: Tokenize query string for matching

The system SHALL tokenize the query string using exactly the same `WordTokenizer` and tokenization rules as document indexing (see document-indexing spec: "Tokenize text into word tokens" — NFC normalization, then word characters = Unicode letters, digits, apostrophes, hyphens/dashes, and digit-internal periods). This ensures token consistency between query and document for exact word matching.

The query SHALL be tokenized in the **same case-sensitivity mode** as the target `IndexedDocument`. The `IndexedDocument` SHALL expose its `CaseSensitive` mode so that `SpanMatcher.Search` can tokenize the query consistently; mismatched modes would otherwise silently break matching.

#### Scenario: Query tokenized same as document

- **WHEN** searching for `"The **quick** brown fox."` (a text query string with formatting characters) against a case-insensitive document
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

### Requirement: Reuse index across multiple queries

The system SHALL allow searching the same IndexedDocument with multiple different queries without rebuilding the index. The IndexedDocument SHALL be immutable after construction and thread-safe for concurrent read access.

#### Scenario: Multiple queries against one document

- **WHEN** an IndexedDocument is built once from text
- **AND** three different queries are executed against it
- **THEN** each query returns results independently
- **AND** the index is never rebuilt
