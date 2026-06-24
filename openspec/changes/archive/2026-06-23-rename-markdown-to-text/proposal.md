## Why

The tokenizer is format-agnostic — it extracts word tokens by treating any non-word character as a delimiter. It does not parse markdown syntax. Yet the public API, type names, and specs all say "markdown," misleading users into thinking the library only handles markdown-formatted text. This rename removes the artificial constraint and correctly positions the library as a general approximate span matcher for any text.

## What Changes

- **BREAKING**: Rename `IndexedDocument.FromMarkdown` static factory to `IndexedDocument.FromText`
- **BREAKING**: Rename `MarkdownTokenizer` class to `WordTokenizer`
- Update all XML doc comments in models and tokenizer to say "text" instead of "markdown"
- Update `document-indexing` spec: rename requirement titles, scenarios, and references from "markdown" to "text"
- Update `span-matching` spec: rename references to `MarkdownTokenizer` → `WordTokenizer` and markdown-specific scenario descriptions
- No behavioral changes — tokenization, indexing, matching, and alignment logic is untouched

## Capabilities

### Modified Capabilities
- `document-indexing`: Replace all "markdown" terminology with "text" in requirements, scenarios, and API references (e.g., `FromMarkdown` → `FromText`, tokenizer class name, parameter names, spec language)
- `span-matching`: Replace all "markdown" terminology with "text" in requirements and scenarios (e.g., `MarkdownTokenizer` reference, `OriginalText` description, scenario descriptions mentioning markdown)

## Impact

- Affected code: `IndexedDocument.cs`, `MarkdownTokenizer.cs` (rename), `SpanMatcher.cs` (reference to tokenizer), XML doc comments
- Affected tests: All tests referencing `FromMarkdown`, `MarkdownTokenizer` by name
- Affected specs: `openspec/specs/document-indexing/spec.md`, `openspec/specs/span-matching/spec.md`
- No dependency changes, no behavioral changes
