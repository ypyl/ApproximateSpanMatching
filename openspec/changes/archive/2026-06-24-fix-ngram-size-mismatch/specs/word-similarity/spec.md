## MODIFIED Requirements

### Requirement: Default TrigramJaccardSimilarity implementation

The system SHALL provide a default `TrigramJaccardSimilarity` class implementing `IWordSimilarity` that computes similarity as the Jaccard coefficient over character n-grams of `$word$` (word with start/end sentinels).

The similarity SHALL be computed as:

```
Jaccard(w1, w2) = |ngrams($w1$, n) ∩ ngrams($w2$, n)| / |ngrams($w1$, n) ∪ ngrams($w2$, n)|
```

where `ngrams(padded, n)` are all length-`n` substrings extracted with stride 1 from the padded string.

**N-gram size is determined consistently for the pair, not per word:**

```
n = (max(len(w1), len(w2)) <= 4) ? 2 : 3
```

Both words SHALL use the same `n`, so that length-changing edits across the 4-character boundary (e.g., `word` → `words`, `quick` → `quik`) produce comparable n-gram sets. Bigrams are used only when **both** words are ≤ 4 characters; otherwise trigrams are used for both.

#### Scenario: Exact match returns 1.0

- **WHEN** `Similarity("quick", "quick")` is called
- **THEN** the result is 1.0 (all trigrams match)

#### Scenario: Single-character substitution (same length)

- **WHEN** `Similarity("quick", "qu1ck")` is called
- **THEN** the result is approximately 0.25 (2 shared trigrams "$qu" and "ck$" out of 8 total)

#### Scenario: Short word uses bigrams when both words are short

- **WHEN** `Similarity("cat", "bat")` is called (both are ≤ 4 chars)
- **THEN** bigrams are used for both words (`n = 2`)
- **AND** the result reflects bigram overlap of "$cat$" and "$bat$"
- **AND** the similarity is higher than if trigrams were used

#### Scenario: Length-changing edit across the 4-char boundary (insertion)

- **WHEN** `Similarity("word", "words")` is called (lengths 4 and 5)
- **THEN** trigrams are used for both words (`n = 3`, because `max(4, 5) > 4`)
- **AND** the result is greater than 0.0 (the n-gram sets are comparable and share overlap such as "$wo", "wor", "ord", "rd$"/"rds")
- **AND** the result is NOT 0.0 (the pair-consistent n-gram size prevents the empty-intersection defect)

#### Scenario: Length-changing edit across the 4-char boundary (deletion)

- **WHEN** `Similarity("quick", "quik")` is called (lengths 5 and 4)
- **THEN** trigrams are used for both words (`n = 3`, because `max(5, 4) > 4`)
- **AND** the result is greater than 0.0 (shared trigrams such as "$qu", "qui", "uik", "ck$"/"ik$")
- **AND** the result is NOT 0.0

#### Scenario: Completely different short words

- **WHEN** `Similarity("cat", "dog")` is called with both ≤ 4 chars
- **THEN** bigrams are used (`n = 2`)
- **AND** the result is 0.0 (no shared bigrams)

#### Scenario: Empty strings

- **WHEN** `Similarity("", "word")` or `Similarity("", "")` is called
- **THEN** `Similarity("", "")` returns 1.0 (both empty)
- **AND** `Similarity("", "word")` returns 0.0 (no shared n-grams possible)

#### Scenario: String with different case

- **WHEN** `Similarity("Quick", "quick")` is called
- **THEN** the result treats casing as difference (n-grams are case-sensitive; similarity < 1.0)
- **AND** callers are responsible for case normalization before calling Similarity if case-insensitive comparison is desired
