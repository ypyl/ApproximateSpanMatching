## Purpose

Defines how text is tokenized into word tokens and indexed into an inverted word-position data structure for efficient multi-query reuse.
## Requirements
### Requirement: Tokenize text into word tokens

The system SHALL accept a text string and produce an ordered sequence of word tokens with their character offsets in the original text.

**Normalization (applied before tokenization):** Unicode NFC normalization of the entire input string, so visually-identical tokens composed with different combining-character sequences match (this also converts compatibility forms where applicable). The NFC-normalized string is what the `IndexedDocument` stores as `OriginalText` and what all character offsets index into.

**Token definition:** A token is a maximal contiguous run of word characters, extracted by greedy leftmost match (consume the longest possible run of word characters at each starting position). Word characters are:
- Unicode letters (any category L)
- Digits (any category N that are decimal digits — Nd)
- Apostrophes: U+0027 `'` and U+2019 ' (for contractions like "don't")
- Hyphens and dashes: U+002D `-` and U+2010–U+2015 (so "state-of-the-art" is one token)
- A single period U+002E `.` is permitted **only** between digit characters (so decimals, version numbers, and section references like `3.2`, `1.0.0` form one token). Periods between letters, or adjacent to non-digits, act as delimiters.

All other characters — whitespace, punctuation, formatting syntax (`*`, `_`, `#`, `>`, `` ` ``, `[`, `]`, `(`, `)`, etc.), the soft hyphen U+00AD, and other symbols — are delimiters and are excluded from tokens. Inline code (`` `foo` ``), code fences, link syntax `[text](url)`, and URLs are not specially parsed: their syntax characters act as delimiters, so a URL like `https://example.com` tokenizes into its alphanumeric/hyphen/dot-digit fragments (e.g. `https`, `example`, `com`) — this is intentional and consistent.

Each token SHALL include:
- `Text`: the normalized word text (lowercase by default, with option for case-sensitive)
- `StartChar`: the start character offset in the stored NFC-normalized text string, measured in UTF-16 code units (the natural indexing unit for C# `string` slicing)
- `EndChar`: the end character offset, exclusive (the index just past the last character of the token), also in UTF-16 code units

#### Scenario: Basic text tokenization

- **WHEN** a text string `"The **quick** brown fox."` is tokenized
- **THEN** the system produces tokens: `["the", "quick", "brown", "fox"]`
- **AND** each token has correct character offsets into the original string

#### Scenario: Hyphenated words

- **WHEN** a text string `"state-of-the-art solution"` is tokenized
- **THEN** the system produces tokens: `["state-of-the-art", "solution"]`

#### Scenario: Contractions

- **WHEN** a text string `"don't stop"` is tokenized
- **THEN** the system produces tokens: `["don't", "stop"]`

#### Scenario: Numbers as tokens

- **WHEN** a text string `"Section 3.2 covers details"` is tokenized
- **THEN** the system produces tokens: `["section", "3.2", "covers", "details"]`

#### Scenario: Empty input

- **WHEN** an empty string `""` is tokenized
- **THEN** the system produces an empty token sequence with no error

#### Scenario: Formatting structures and URLs split on syntax

- **WHEN** a text string ``"see `code` and [link](https://example.com/path) here"`` is tokenized
- **THEN** the system produces tokens: `["see", "code", "and", "link", "https", "example", "com", "path", "here"]`
- **AND** backticks, brackets, parentheses, colons, and slashes are delimiters

#### Scenario: Soft hyphen acts as delimiter

- **WHEN** a text string containing a soft hyphen (U+00AD) between "state" and "of" ("state\u00ADof affairs") is tokenized
- **THEN** the system produces tokens: `["state", "of", "affairs"]` — the soft hyphen is a delimiter and splits the run
- **AND** a real hyphen U+002D would instead yield `["state-of", "affairs"]` (hyphen is a word character)

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

### Requirement: Provide original text span extraction

The system SHALL allow extracting the original text for any word position range `[start, end)` — a half-open interval, `end` exclusive — from an IndexedDocument, using the stored character offsets to slice the source string from `Tokens[start].StartChar` to `Tokens[end-1].EndChar`.

#### Scenario: Span extraction

- **WHEN** extracting the span for word positions `[1, 4)` from document `"The **quick** brown fox."`
- **THEN** the system returns `"quick** brown fox"` (the original text from `StartChar` of token 1 to `EndChar` of token 3, i.e. tokens 1, 2, 3 — surrounding formatting before the first token is not included)

### Requirement: Case sensitivity option

The system SHALL support both case-sensitive and case-insensitive tokenization. In case-insensitive mode (default), all tokens are lowercased. In case-sensitive mode, tokens retain their original casing.

#### Scenario: Case-insensitive matching (default)

- **WHEN** tokenizing `"The Quick Brown Fox"` in case-insensitive mode
- **THEN** tokens are `["the", "quick", "brown", "fox"]`

#### Scenario: Case-sensitive matching

- **WHEN** tokenizing `"The Quick Brown Fox"` in case-sensitive mode
- **THEN** tokens are `["The", "Quick", "Brown", "Fox"]`

### Requirement: Empty document handling

The system SHALL accept an empty text string (`""`) when building an `IndexedDocument`, producing a document with zero tokens, an empty inverted index, and an empty `OriginalText`. Searching an empty document returns an empty result list for any query (no error).

#### Scenario: Build document from empty text

- **WHEN** `IndexedDocument.FromText("")` is called
- **THEN** the resulting document has `Tokens` count 0, an empty inverted index, and `OriginalText` equal to `""`
- **AND** searching it with any query returns an empty result list without throwing

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

