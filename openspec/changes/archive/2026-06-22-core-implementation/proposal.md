## Why

When working with OCR-extracted documents (up to ~50 pages), users need to locate approximate matches of a query passage within the document. Existing .NET libraries focus on character-level edit distance or full-document similarity — neither addresses the need to find the most similar contiguous span based on word-level token matching with gap penalties and order preservation. This library fills that gap with a reusable, indexed approach optimized for multi-query scenarios.

## What Changes

- New .NET library project `ApproximateSpanMatching` shipped as a NuGet package
- `IndexedDocument` class that tokenizes markdown text and builds a word-position inverted index, reusable across multiple queries
- `SpanMatcher` class providing `Search(IndexedDocument, query, topN, threshold)` to find the best matching spans
- `IAlignmentStrategy` interface enabling pluggable alignment implementations, with a default strict-order Smith-Waterman word-level alignment
- Gap-penalized scoring that prefers tight clusters of matched words over scattered matches
- Top-N ranked results with two normalized metrics (`NormalizedScore` gap-aware quality, `Coverage` fraction of query matched), span boundaries, and matched token traces

## Capabilities

### New Capabilities

- `document-indexing`: Tokenize markdown text into word tokens with character positions, build an inverted word-position index for efficient multi-query reuse
- `span-matching`: Core search pipeline that finds the top-N approximate matching spans using seed-anchor clustering, local alignment scoring, and threshold-based filtering
- `alignment-strategy`: Pluggable alignment interface with a default implementation for strict-order word-level sequence alignment using Smith-Waterman with affine gap penalties

### Modified Capabilities

<!-- No existing capabilities to modify — this is the initial implementation. -->

## Impact

- New C# class library project: `ApproximateSpanMatching.csproj`
- NuGet package: `ApproximateSpanMatching`
- No existing code affected — greenfield library
- Dependencies: zero external NuGet dependencies for the core library (targeting .NET 10)
