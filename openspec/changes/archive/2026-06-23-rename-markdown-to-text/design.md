## Context

The current codebase uses "markdown" in type names (`MarkdownTokenizer`), factory methods (`IndexedDocument.FromMarkdown`), and XML doc comments. However, the tokenizer is format-agnostic: it treats all non-word characters as delimiters and does not parse markdown syntax. This naming choice is a leftover from the library's original scope and misrepresents its capabilities — the library works identically on plain text, HTML-stripped text, or any string of words with arbitrary non-word delimiters.

The rename affects no behavior. Every tokenization path, index structure, alignment algorithm, and ranking logic stays identical.

## Goals / Non-Goals

**Goals:**
- Rename `FromMarkdown` → `FromText` on `IndexedDocument`
- Rename `MarkdownTokenizer` → `WordTokenizer`
- Update all XML doc comments to say "text" instead of "markdown"
- Update specs to reflect the new naming

**Non-Goals:**
- No tokenization behavior changes (word character set stays the same)
- No new public API surface
- No format-specific tokenization variants (plain-text vs markdown modes)
- No structural awareness (paragraphs, sentences)

## Decisions

### D1: Rename `FromMarkdown` to `FromText`

**Rationale:** `FromText` accurately describes what the method accepts — any text. It's the simplest, most discoverable name. An alternative considered was `FromString` — rejected because "string" implies arbitrary characters, while "text" suggests natural language prose (which is the intended input domain).

**Same parameters, same return type.** This is purely a rename.

### D2: Rename `MarkdownTokenizer` to `WordTokenizer`

**Rationale:** The class extracts word tokens. The "Markdown" prefix was never accurate. A natural alternative was `Tokenizer` — rejected because in a library that may later add structural tokenizers (e.g., `SentenceTokenizer`), `WordTokenizer` is more precise and leaves namespace room.

The class remains `internal static` — only `IndexedDocument.FromText` invokes it. No public API surface change aside from what's implicitly surfaced through `SpanMatcher.Search` (which also calls it internally for query tokenization).

### D3: Spec updates use MODIFIED, not RENAMED

Since nearly every requirement that mentions "markdown" also changes body text (scenario descriptions, terminology within the requirement description), using MODIFIED (full copy with edits) is simpler and less error-prone than mixing RENAMED + MODIFIED. The requirement headers will reflect the new names within the MODIFIED block.

## Risks / Trade-offs

- **Breaking change risk**: `FromMarkdown` is the primary public entry point. Anyone upgrading to a new version will get a compile error. Mitigation: this is a pre-1.0 library with no published consumers yet. The change is trivial for any user to update (find-and-replace `FromMarkdown` → `FromText`).
- **Internal rename churn**: `SpanMatcher.Search` references `MarkdownTokenizer.Tokenize` for query tokenization. Mitigation: straightforward find-and-replace within the same assembly.
- **Test churn**: All test files reference `FromMarkdown` and `MarkdownTokenizer`. Mitigation: same find-and-replace.
