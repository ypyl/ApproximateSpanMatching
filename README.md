# ApproximateSpanMatching

A .NET library for approximate passage matching in OCR-extracted documents.

## Overview

`ApproximateSpanMatching` finds the most similar contiguous spans in a document matching a query passage, using word-level token matching with Smith-Waterman gap penalties and order preservation. Documents are indexed once and reusable across multiple queries.

## Quick Start

```csharp
using ApproximateSpanMatching;
using ApproximateSpanMatching.Matching;
using ApproximateSpanMatching.Models;
using ApproximateSpanMatching.Similarity;

// Build an index from text
var doc = IndexedDocument.FromText("The **quick** brown fox jumps over the lazy dog.");

// Search for an approximate match
var matcher = new SpanMatcher();
var results = matcher.Search(doc, "quick brown fox", topN: 3);

foreach (var match in results)
{
    Console.WriteLine($"Score: {match.NormalizedScore:F2}, Coverage: {match.Coverage:F2}");
    Console.WriteLine($"  Text: {match.OriginalText}");
}

// Fuzzy matching: tolerate typos and OCR errors
var fuzzySim = new ApproximateSpanMatching.Similarity.TrigramJaccardSimilarity();
var fuzzyMatcher = new SpanMatcher(wordSimilarity: fuzzySim, similarityThreshold: 0.2);
var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
var fuzzyResults = fuzzyMatcher.Search(doc, "qu1ck brown fox", options: options);
// → finds "quick brown fox" with slightly lower NormalizedScore
```

## Features

- Word-level token matching with Smith-Waterman alignment and affine gap penalties
- **Fuzzy word matching** via pluggable `IWordSimilarity` — tolerates typos, OCR errors, and near-misses
- Default trigram-Jaccard similarity with automatic bigram fallback for short words
- Always-on n-gram positional index for efficient approximate lookup
- Opt-in fuzzy anchor discovery via `SearchOptions` — exact matching remains the safe default
- Inverted index for efficient multi-query reuse
- Pluggable alignment strategies via `IAlignmentStrategy`
- Thread-safe, immutable document index
- NFC normalization and configurable case sensitivity
- Top-N ranked results with overlap deduplication

## Q&A

### How do I search for partial words (e.g., `"hel"` matching `"hello"`)?

You need both fuzzy anchors (to find candidate positions when a query word has no exact index hit) and fuzzy word similarity (so the alignment recognizes `"hel"` ≈ `"hello"` rather than requiring `==`):

```csharp
var fuzzySim = new TrigramJaccardSimilarity();
var matcher = new SpanMatcher(wordSimilarity: fuzzySim);
var options = new SearchOptions { EnableFuzzyAnchors = true };
var results = matcher.Search(doc, "hel wrld", options: options);
```

This uses trigram similarity (bigram for words ≤ 4 chars) on `$word$`-padded strings. It catches **prefix-like typos and near-misses**, but it's **not substring search** — mid-word fragments like `"elo"` won't match `"hello"` (no shared trigrams).

**Limitations:** Very short queries (1–2 characters) typically score below the default 0.3 threshold. Lower the threshold or accept that single-char queries won't produce matches.

### How do I tolerate OCR errors or typos?

Same mechanism: plug in an `IWordSimilarity` into the `SpanMatcher` and enable fuzzy anchors:

```csharp
var fuzzySim = new TrigramJaccardSimilarity();
var fuzzyMatcher = new SpanMatcher(wordSimilarity: fuzzySim, similarityThreshold: 0.2);
var options = new SearchOptions { EnableFuzzyAnchors = true, FuzzyAnchorThreshold = 0.2 };
var results = fuzzyMatcher.Search(doc, "recieve", options: options);
// → matches "receive"
```

For stricter matching, raise `similarityThreshold` and `FuzzyAnchorThreshold` to 0.6+. For looser, lower both to 0.1–0.2.

### How do I find only exact matches (no gaps, no missing words)?

Set `threshold` to `1.0` and configure the alignment to punish gaps heavily enough that partial matches score below 1:

```csharp
// Exact-only: every query word must match (score = queryLength, normalized = 1.0)
var results = matcher.Search(doc, "quick brown fox", threshold: 0.99);
```

Each exact word match gives `+1.0`, so for an N-word query, a perfect alignment scores N (normalized: 1.0). Any gap or missing word reduces the score. With `threshold: 0.99`, only near-perfect or perfect alignments pass.

For true exact-only with no gaps at all, you'd need a different alignment strategy — Smith-Waterman is local and will still find subsequences even when gaps are penalized.

### Why does my 2-word query match spans with only one word?

Smith-Waterman is a **local alignment** algorithm — it finds the best-scoring subsequence, even if only part of the query matches. With default `threshold = 0.0`, a single-word hit (NormalizedScore = 0.5 for a 2-word query) passes the filter. Raise the threshold to require more coverage:

```csharp
// Require at least 2 out of 3 query words to match (threshold > 1/3 ≈ 0.34)
var results = matcher.Search(doc, "one two three", threshold: 0.35);
```

### How do I get all matches, not just top-N?

Pass a large `topN` value:

```csharp
var all = matcher.Search(doc, query, topN: int.MaxValue, threshold: 0.0);
```

This returns every non-overlapping span above the threshold. Be aware that on large documents with common query terms, this can produce many candidates and the O(n²) deduplication loop may become noticeable.

## Target

.NET 10 class library. Zero external dependencies.

## License

MIT
