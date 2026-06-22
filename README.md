# ApproximateSpanMatching

A .NET library for approximate passage matching in OCR-extracted documents.

## Overview

`ApproximateSpanMatching` finds the most similar contiguous spans in a document matching a query passage, using word-level token matching with Smith-Waterman gap penalties and order preservation. Documents are indexed once and reusable across multiple queries.

## Quick Start

```csharp
using ApproximateSpanMatching;
using ApproximateSpanMatching.Matching;
using ApproximateSpanMatching.Models;

// Build an index from markdown
var doc = IndexedDocument.FromMarkdown("The **quick** brown fox jumps over the lazy dog.");

// Search for an approximate match
var matcher = new SpanMatcher();
var results = matcher.Search(doc, "quick brown fox", topN: 3);

foreach (var match in results)
{
    Console.WriteLine($"Score: {match.NormalizedScore:F2}, Coverage: {match.Coverage:F2}");
    Console.WriteLine($"  Text: {match.OriginalText}");
}
```

## Features

- Word-level token matching with Smith-Waterman alignment and affine gap penalties
- Inverted index for efficient multi-query reuse
- Pluggable alignment strategies via `IAlignmentStrategy`
- Thread-safe, immutable document index
- NFC normalization and configurable case sensitivity
- Top-N ranked results with overlap deduplication

## Target

.NET 10 class library. Zero external dependencies.

## License

MIT
