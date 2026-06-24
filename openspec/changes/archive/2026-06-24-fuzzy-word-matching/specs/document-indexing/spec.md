## MODIFIED Requirements

### Requirement: Build inverted word-position index

The system SHALL build an inverted index mapping each unique token to all positions where it appears in the document. The index SHALL support O(1) lookup by token and return all positions in ascending order.

In addition, the system SHALL build a character n-gram positional index that maps each character trigram of `$token$` (token with `$` sentinels) to all positions where words containing that trigram appear. For tokens with ≤ 4 characters (before padding), character bigrams of `$token$` SHALL be used instead of trigrams. This n-gram index enables approximate word lookup for fuzzy anchor discovery.

The n-gram index SHALL be built unconditionally (always-on) alongside the exact inverted index; it does not require opt-in configuration.

#### Scenario: Index construction

- **WHEN** an IndexedDocument is built from tokens `["the", "quick", "brown", "fox", "the", "lazy", "dog"]`
- **THEN** the inverted index maps: `"the" → [0, 4]`, `"quick" → [1]`, `"brown" → [2]`, `"fox" → [3]`, `"lazy" → [5]`, `"dog" → [6]`
- **AND** the n-gram index contains entries for the trigrams of each token (e.g., `"$qu" → [1]`, `"qui" → [1]`, `"uic" → [1]`, `"ick" → [1]`, `"ck$" → [1]` for "quick")

#### Scenario: Token not in document

- **WHEN** a token `"elephant"` is looked up in an index where it never appears
- **THEN** the system returns an empty position list

## ADDED Requirements

### Requirement: Approximate position lookup via n-grams

The system SHALL provide an internal method `GetApproximatePositions(string word, double threshold)` on `IndexedDocument` that returns positions of document words whose character n-gram Jaccard similarity to the given word meets or exceeds the threshold.

The lookup SHALL:
1. Compute trigrams of `$word$` (or bigrams for words ≤ 4 chars)
2. For each n-gram, retrieve position lists from the n-gram index
3. Group positions by the count of matching n-grams (for ranking candidates)
4. For each candidate position, retrieve the document token text and compute exact Jaccard similarity against the query word
5. Return positions where Jaccard ≥ threshold, ordered by similarity descending

Results SHALL be returned as a list of `(int Position, double Similarity)` pairs. An empty list SHALL be returned if no document words meet the threshold.

#### Scenario: Approximate lookup finds similar word

- **WHEN** `GetApproximatePositions("qu1ck", 0.2)` is called on a document containing "quick" at position 5
- **AND** the trigram Jaccard similarity between "qu1ck" and "quick" is ~0.25
- **THEN** position 5 is returned with similarity ~0.25

#### Scenario: Approximate lookup respects threshold

- **WHEN** `GetApproximatePositions("qu1ck", 0.5)` is called
- **AND** the best matching word has similarity 0.25 (below 0.5)
- **THEN** an empty list is returned

#### Scenario: No matching n-grams

- **WHEN** `GetApproximatePositions("xyzzy", 0.2)` is called on a document with no trigram overlap
- **THEN** an empty list is returned (zero candidate positions to evaluate)

#### Scenario: Multiple candidates ranked by similarity

- **WHEN** `GetApproximatePositions("cat", 0.2)` is called on a document containing "cat" at position 0 (similarity 1.0), "bat" at position 5 (Jaccard ~0.5 with bigrams), and "car" at position 10 (Jaccard ~0.6 with bigrams)
- **THEN** results are returned ordered by similarity descending: position 0 (1.0), position 10 (~0.6), position 5 (~0.5)

#### Scenario: Duplicate tokens at different positions

- **WHEN** `GetApproximatePositions("qu1ck", 0.2)` is called on a document where "quick" appears at positions 3, 8, and 15
- **THEN** all three positions are returned, each with the same similarity score
