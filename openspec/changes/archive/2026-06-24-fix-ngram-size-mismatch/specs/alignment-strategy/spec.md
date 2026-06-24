## MODIFIED Requirements

### Requirement: Default Smith-Waterman word alignment

The system SHALL provide a default `SmithWatermanAlignment` class implementing `IAlignmentStrategy` using Smith-Waterman local alignment adapted for exact word tokens with affine gap penalties.

Parameters:
- `gapOpenPenalty` (double, default: -2.0): Penalty for opening a new gap
- `gapExtendPenalty` (double, default: -1.0): Penalty for extending an existing gap
- `wordSimilarity` (`IWordSimilarity?`, default: `null`): Optional word similarity function. When `null`, exact string equality is used (binary match: +1.0 for identical words, mismatch disallowed). When non-null, match score = `wordSimilarity.Similarity(w1, w2) × 1.0` for pairs meeting the similarity threshold, `-∞` otherwise.
- `similarityThreshold` (double, default: 0.3): Minimum similarity for two words to be considered matchable. Pairs below this threshold are treated as mismatches (score -∞). Only used when `wordSimilarity` is non-null.
- Match reward: +1.0 for exact word match. When a similarity function is provided, the effective match reward scales from 0.0 to 1.0.

When `wordSimilarity` is `null` (the default), the behavior SHALL be identical to the existing exact-match algorithm. This preserves backward compatibility.

A pair is considered a match only when `similarity >= similarityThreshold` (inclusive). Scenarios SHALL use a threshold consistent with the stated similarity values: a word with similarity below the configured threshold SHALL NOT be matched.

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

#### Scenario: Fuzzy alignment with similar words

- **WHEN** aligning query `["qu1ck", "brown", "fox"]` against doc region `["quick", "brown", "fox"]` with a trigram Jaccard similarity function (threshold 0.2) and default gap penalties
- **AND** `Similarity("qu1ck", "quick")` is approximately 0.25 (≥ 0.2, so the pair is matchable)
- **THEN** "qu1ck" matches "quick" with similarity ~0.25, contributing ~0.25 to the score
- **AND** "brown" matches "brown" with similarity 1.0, contributing 1.0
- **AND** "fox" matches "fox" with similarity 1.0, contributing 1.0
- **AND** the total score is approximately 2.25 (the exact alignment path may differ depending on gap penalty interactions)

#### Scenario: Fuzzy word below similarity threshold treated as mismatch

- **WHEN** aligning query `["xyzzy", "brown"]` against doc region `["quick", "brown"]` with similarity threshold 0.3
- **AND** `Similarity("xyzzy", "quick")` is below 0.3
- **THEN** "xyzzy" and "quick" are not matched (treated as mismatch → -∞)
- **AND** "brown" matches "brown" exactly with score 1.0
- **AND** the total alignment score is 1.0

#### Scenario: Backward compatibility — null word similarity

- **WHEN** `SmithWatermanAlignment` is constructed with `wordSimilarity: null` (the default)
- **THEN** all behavior is identical to the pre-fuzzy implementation
- **AND** mismatched words are never considered for alignment (score -∞)
