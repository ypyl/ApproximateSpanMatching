## ADDED Requirements

### Requirement: IWordSimilarity interface

The system SHALL define an `IWordSimilarity` interface that computes a similarity score between two word strings.

```csharp
public interface IWordSimilarity
{
    /// <summary>
    /// Returns similarity ∈ [0, 1] where 1 = identical, 0 = completely different.
    /// Must be symmetric: Similarity(a, b) == Similarity(b, a).
    /// Must be reflexive: Similarity(a, a) == 1.0.
    /// </summary>
    double Similarity(string a, string b);
}
```

The interface SHALL be usable independently of the alignment and matching pipeline — it is a self-contained word-level comparison abstraction.

#### Scenario: Identical words return 1.0

- **WHEN** `Similarity("quick", "quick")` is called on any `IWordSimilarity` implementation
- **THEN** the result is exactly 1.0

#### Scenario: Symmetry

- **WHEN** `Similarity(a, b)` and `Similarity(b, a)` are called for any two strings `a`, `b`
- **THEN** both calls return the same value

#### Scenario: Completely different words return low score

- **WHEN** `Similarity("elephant", "quick")` is called on a trigram-based implementation
- **THEN** the result is near 0.0 (no meaningful character overlap)

### Requirement: Default TrigramJaccardSimilarity implementation

The system SHALL provide a default `TrigramJaccardSimilarity` class implementing `IWordSimilarity` that computes similarity as the Jaccard coefficient over character trigrams of `$word$` (word with start/end sentinels).

The similarity SHALL be computed as:

```
Jaccard(w1, w2) = |trigrams($w1$) ∩ trigrams($w2$)| / |trigrams($w1$) ∪ trigrams($w2$)|
```

where trigrams of a padded string `$word$` are all length-3 substrings extracted with stride 1.

For words with ≤ 4 characters (before padding), the implementation SHALL use character bigrams of `$word$` instead of trigrams, to avoid producing zero trigrams for very short words.

#### Scenario: Exact match returns 1.0

- **WHEN** `Similarity("quick", "quick")` is called
- **THEN** the result is 1.0 (all trigrams match)

#### Scenario: Single-character substitution

- **WHEN** `Similarity("quick", "qu1ck")` is called
- **THEN** the result is approximately 0.25 (2 shared trigrams "$qu" and "ck$" out of 8 total)

#### Scenario: Short word uses bigrams

- **WHEN** `Similarity("cat", "bat")` is called (both are ≤ 4 chars)
- **THEN** the result reflects bigram overlap of "$cat$" and "$bat$" (shared bigrams: "$", "at", "t$" out of ~8 total)
- **AND** the similarity is higher than if trigrams were used (which would produce fewer or no overlapping n-grams)

#### Scenario: Completely different short words

- **WHEN** `Similarity("cat", "dog")` is called with bigram fallback
- **THEN** the result is 0.0 (no shared bigrams: "$c","ca","at","t$" vs "$d","do","og","g$")

#### Scenario: Empty strings

- **WHEN** `Similarity("", "word")` or `Similarity("", "")` is called
- **THEN** `Similarity("", "")` returns 1.0 (both empty)
- **AND** `Similarity("", "word")` returns 0.0 (no shared n-grams possible)

#### Scenario: String with different case

- **WHEN** `Similarity("Quick", "quick")` is called
- **THEN** the result treats casing as difference (trigrams are case-sensitive; similarity < 1.0 since '$Qu' ≠ '$qu', 'Qui' ≠ 'qui', etc.)
- **AND** callers are responsible for case normalization before calling Similarity if case-insensitive comparison is desired
