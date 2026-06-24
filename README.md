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

## Target

.NET 10 class library. Zero external dependencies.

## License

MIT
