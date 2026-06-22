## ADDED Requirements

### Requirement: IAlignmentStrategy interface contract

The system SHALL define an `IAlignmentStrategy` interface that accepts query tokens, document region tokens, and a document start offset, and returns an alignment result containing the optimal alignment score, span boundaries, and matched token pairs.

```csharp
public interface IAlignmentStrategy
{
    AlignmentResult Align(string[] queryTokens, string[] docRegionTokens, int docStartIndex);
}
```

`AlignmentResult` SHALL include:
- `Score` (double): Raw alignment score before normalization
- `MatchedPairs` (IReadOnlyList<(int QueryIndex, int DocIndex)>): Ordered list of matched token pairs
- `SpanStart` (int): absolute start word index in the document (inclusive) = `docStartIndex` + region-relative start
- `SpanEnd` (int): absolute end word index in the document (exclusive) = `docStartIndex` + region-relative end

#### Scenario: Strategy produces alignment from region

- **WHEN** an alignment strategy aligns query `["quick", "brown", "fox"]` against doc region `["quick", "brown", "fox", "jumps"]` with docStartIndex 5
- **THEN** the result has matched pairs: [(0,5), (1,6), (2,7)]
- **AND** SpanStart = 5 and SpanEnd = 8

#### Scenario: Strategy handles gaps in document

- **WHEN** aligning query `["quick", "fox"]` against doc region `["quick", "brown", "fox"]` with docStartIndex 0
- **THEN** the result has matched pairs: [(0,0), (1,2)]
- **AND** the score reflects a gap penalty for skipping "brown"

### Requirement: Default Smith-Waterman word alignment

The system SHALL provide a default `SmithWatermanAlignment` class implementing `IAlignmentStrategy` using Smith-Waterman local alignment adapted for exact word tokens with affine gap penalties.

Parameters:
- `gapOpenPenalty` (double, default: -2.0): Penalty for opening a new gap
- `gapExtendPenalty` (double, default: -1.0): Penalty for extending an existing gap
- Match reward: +1.0 for exact word match

#### Scenario: Default alignment with no gaps

- **WHEN** aligning query `["a", "b", "c"]` against doc region `["x", "a", "b", "c", "y"]`
- **THEN** the alignment score is 3.0 (3 matches × +1.0, no gaps)
- **AND** matched pairs are [(0,1), (1,2), (2,3)]

#### Scenario: Default alignment with a gap in document

- **WHEN** aligning query `["a", "b", "c"]` against doc region `["a", "x", "b", "c"]` with default gap penalties (g=-2, e=-1)
- **THEN** the best local alignment matches `"b"` and `"c"` (score 2.0) with matched pairs [(1,2), (2,3)]
- **AND** the full a-b-c alignment (score 0.0, pairs [(0,0), (1,2), (2,3)]) is not selected because SW reports the maximum DP cell; a single-word gap costs more than the match it enables under these defaults, so the shorter gap-free alignment wins

#### Scenario: Multiple scattered matches produce lower score

- **WHEN** aligning query `["a", "b", "c"]` against:
  - Region 1: `["a", "b", "c"]` → best score 3.0 (tight, no gaps)
  - Region 2: `["a", "x", "x", "x", "x", "x", "b", "c"]` → best score 2.0 ("b" and "c" only; the 5-word gap costs -7, making the full a-b-c path worse at -4 → reset to 0)
- **THEN** Region 1's best alignment scores higher than Region 2's

#### Scenario: Query word not in document region

- **WHEN** aligning query `["a", "z", "c"]` against doc region `["a", "b", "c"]` where "z" has no match
- **THEN** "z" is skipped via gap penalty, and the alignment matches "a" and "c" with a gap between them
- **AND** the score reflects the gap penalty

### Requirement: Alignment edge cases

The default `SmithWatermanAlignment` SHALL handle degenerate inputs without throwing:
- Empty `queryTokens` or empty `docRegionTokens` → return an `AlignmentResult` with `Score` 0.0, empty `MatchedPairs`, and `SpanStart == SpanEnd == docStartIndex`.
- A best local alignment of 0.0 (no positive-scoring sub-alignment exists, because every candidate would go negative under gap penalties) → return `Score` 0.0, empty `MatchedPairs`, and `SpanStart == SpanEnd`. The score SHALL never be reported below 0.0 (Smith-Waterman local-alignment reset).
- `null` `queryTokens` or `docRegionTokens` → throw `ArgumentNullException`.

A zero-score alignment produces no span (the matcher drops it before ranking).

#### Scenario: Empty query or region

- **WHEN** aligning an empty query `[]` against doc region `["a", "b"]` with docStartIndex 3
- **THEN** the result has `Score` 0.0, empty `MatchedPairs`, and `SpanStart == SpanEnd == 3`

#### Scenario: No positive-scoring alignment

- **WHEN** aligning query `["a", "b", "c"]` against doc region `["x", "y", "z"]` (no matching words) with default penalties
- **THEN** the result has `Score` 0.0 and empty `MatchedPairs` (the local alignment resets to 0 rather than reporting a negative score)

### Requirement: Strategy pluggability

The system SHALL allow `SpanMatcher` to accept any `IAlignmentStrategy` implementation via constructor injection or property. The default strategy SHALL be `SmithWatermanAlignment` with default parameters if none is provided.

#### Scenario: Custom alignment strategy

- **WHEN** a SpanMatcher is constructed with a custom IAlignmentStrategy
- **THEN** all Search calls use that custom strategy
- **AND** the rest of the pipeline (index, cluster, rank) behaves identically

#### Scenario: Default strategy when none provided

- **WHEN** a SpanMatcher is constructed without specifying an IAlignmentStrategy
- **THEN** it uses SmithWatermanAlignment with default gap penalties
