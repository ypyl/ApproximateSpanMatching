## MODIFIED Requirements

### Requirement: Find top-N matching spans

The system SHALL accept a query string, an IndexedDocument, and parameters (topN, threshold, options) and return up to topN non-overlapping spans ranked by `NormalizedScore` (gap-aware), where each returned span has `NormalizedScore >= threshold`.

The `Search` method signature SHALL be:
```csharp
public List<SpanMatch> Search(
    IndexedDocument doc,
    string query,
    int topN = 3,
    double threshold = 0.0,
    SearchOptions? options = null)
```

A span SHALL include:
- `StartIndex` and `EndIndex`: absolute word positions in the document, half-open interval `[StartIndex, EndIndex)` (`EndIndex` exclusive)
- `NormalizedScore` (double, 0.0–1.0): gap-aware quality score = `rawSmithWatermanScore / queryWordCount`
- `Coverage` (double, 0.0–1.0): fraction of query words matched = `matchedQueryWordCount / queryWordCount`
- `OriginalText`: original text extracted from the document for `[StartIndex, EndIndex)`
- `MatchedPairs`: alignment trace — list of `(queryWordIndex, docWordIndex, similarity)` records showing which words matched and how well

#### Scenario: Exact match

- **WHEN** searching for `"quick brown fox"` in a document containing `"the quick brown fox jumps"`
- **THEN** exactly one span is returned starting at "quick" and ending at "fox" with `NormalizedScore` 1.0 and `Coverage` 1.0

#### Scenario: Match with gaps in document

- **WHEN** searching for `"quick brown fox jumps over lazy dog"` in a document containing `"quick brown fox jumps over the lazy dog"`
- **THEN** one span is returned covering the matched region
- **AND** `Coverage` may be < 1.0 with default gap penalties (g=-2, e=-1), because the 1-word gap for "the" costs -3 (more than a match's +1), so SW may prefer a shorter gap-free sub-alignment (e.g. "quick brown fox jumps over" = 5/7 ≈ 0.71) over the full 7-word alignment with gap (score 4)
- **AND** `NormalizedScore` is < 1.0

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

#### Scenario: Fuzzy match with OCR error in query

- **WHEN** searching for `"qu1ck brown fox"` with fuzzy anchors enabled (threshold 0.2) against a document containing `"the quick brown fox jumps"`
- **AND** the alignment strategy uses a trigram Jaccard similarity function
- **THEN** one span is returned covering "quick brown fox"
- **AND** `NormalizedScore` is less than 1.0 (penalized for the fuzzy "qu1ck"↔"quick" match)
- **AND** the `MatchedPairs` show `Similarity < 1.0` for the "qu1ck"↔"quick" pair and `Similarity = 1.0` for the exact matches

#### Scenario: Fuzzy disabled returns empty for mismatched query

- **WHEN** searching for `"qu1ck brown fox"` with fuzzy anchors disabled (the default)
- **AND** the document contains `"the quick brown fox jumps"` (exact "qu1ck" does not appear)
- **THEN** an empty result list is returned (identical to today's behavior — "qu1ck" has zero exact anchors)

## ADDED Requirements

### Requirement: Fuzzy anchor discovery

When `SearchOptions.EnableFuzzyAnchors` is `true`, `SpanMatcher` SHALL use the approximate n-gram index to discover candidate anchors for query words that have zero exact matches in the inverted index.

For each query word:
1. Attempt exact lookup via `IndexedDocument.GetPositions(word)`
2. If exact positions exist: use them as anchors (no fuzzy fallback needed)
3. If exact positions are empty AND fuzzy is enabled: call `IndexedDocument.GetApproximatePositions(word, options.FuzzyAnchorThreshold)` and create anchors from the returned positions

Fuzzy-discovered anchors SHALL be treated identically to exact anchors during clustering — the similarity quality gates at anchor creation (positions below threshold are not returned by `GetApproximatePositions`), and surviving anchors participate in clustering normally.

When `EnableFuzzyAnchors` is `false` (default), step 3 is skipped and behavior is identical to the current implementation.

#### Scenario: Fuzzy anchor found for misspelled query word

- **WHEN** searching for `"qu1ck brown"` with `EnableFuzzyAnchors = true` and `FuzzyAnchorThreshold = 0.2`
- **AND** the document contains "quick" at position 2 and "brown" at position 3
- **THEN** "qu1ck" produces fuzzy anchors at position 2 (similarity ~0.25 ≥ 0.2)
- **AND** "brown" produces exact anchors at position 3
- **AND** both anchors cluster into the same region

#### Scenario: Fuzzy anchor below threshold is discarded

- **WHEN** searching for `"xyzzy brown"` with `EnableFuzzyAnchors = true` and `FuzzyAnchorThreshold = 0.3`
- **AND** no document word has trigram Jaccard ≥ 0.3 with "xyzzy"
- **THEN** "xyzzy" produces zero anchors
- **AND** "brown" produces exact anchors at its positions
- **AND** only "brown" anchors are used for clustering

#### Scenario: Exact anchors take priority over fuzzy

- **WHEN** a query word has exact matches in the index
- **THEN** fuzzy lookup is NOT performed for that word (exact anchors are sufficient)
- **AND** all exact positions are used as anchors regardless of the `FuzzyAnchorThreshold` value

#### Scenario: All query words fuzzy-matched

- **WHEN** searching for `"qu1ck br0wn"` with fuzzy anchors enabled
- **AND** both "qu1ck" and "br0wn" have zero exact matches but produce fuzzy anchors from "quick" and "brown" respectively
- **THEN** a region is formed from the fuzzy anchors
- **AND** the alignment scores the fuzzy word matches with their computed similarities

### Requirement: SearchOptions parameter

The system SHALL define a `SearchOptions` class to control fuzzy search behavior:

```csharp
public class SearchOptions
{
    /// <summary>Enable fuzzy anchor discovery for query words with no exact matches. Default: false.</summary>
    public bool EnableFuzzyAnchors { get; set; } = false;

    /// <summary>Minimum Jaccard similarity for a fuzzy anchor candidate. Default: 0.3.</summary>
    public double FuzzyAnchorThreshold { get; set; } = 0.3;
}
```

`SpanMatcher.Search` SHALL accept an optional `SearchOptions? options = null` parameter. When `null`, behavior SHALL default to `new SearchOptions()` (fuzzy disabled).

`FuzzyAnchorThreshold` SHALL be clamped to [0.0, 1.0]; values outside this range SHALL throw `ArgumentOutOfRangeException`.

#### Scenario: Default SearchOptions disables fuzzy

- **WHEN** `Search` is called with `options: null` or `options: new SearchOptions()`
- **THEN** fuzzy anchor discovery is disabled
- **AND** behavior is identical to the pre-fuzzy implementation

#### Scenario: Invalid FuzzyAnchorThreshold throws

- **WHEN** `Search` is called with `options.FuzzyAnchorThreshold = 1.5` or `-0.5`
- **THEN** an `ArgumentOutOfRangeException` is thrown
